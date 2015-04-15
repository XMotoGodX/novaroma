﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Novaroma.Interface;
using Novaroma.Interface.Download;
using Novaroma.Interface.Download.Torrent;
using Novaroma.Interface.Download.Torrent.Provider;
using UTorrent.Api;

namespace Novaroma.Services.UTorrent {

    public class UTorrentDownloader : TorrentDownloaderBase, IConfigurable {
        private readonly UTorrentSettings _settings;

        public UTorrentDownloader(IExceptionHandler exceptionHandler, IEnumerable<ITorrentMovieProvider> movieProviders, IEnumerable<ITorrentTvShowProvider> tvShowProviders): base(exceptionHandler) {
            _settings = new UTorrentSettings(movieProviders, tvShowProviders);
        }

        public override async Task<string> Download(string path, ITorrentSearchResult searchResult) {
            var client = CreateClient();
            var uri = new Uri(searchResult.MagnetUri);

            AddUrlResponse result;
            try {
                result = await client.AddUrlTorrentAsync(uri, string.IsNullOrEmpty(path) ? searchResult.Name : Guid.NewGuid().ToString());
            }
            catch (ServerUnavailableException) {
                throw new ServiceUnavailableException(ServiceName);
            }

            if (result.Error != null) throw result.Error;

            return result.AddedTorrent.Hash;
        }

        public override async Task Refresh() {
            var client = CreateClient();
            var torrents = await client.GetListAsync();

            var completeds = torrents.Result.Torrents.Where(t => t.Downloaded > 0 && t.Remaining == 0);
            foreach (var completed in completeds) {
                var hash = completed.Hash;
                var files = (await client.GetFilesAsync(hash)).Result.Files.SelectMany(fc => fc.Value.Select(f => f.Name));
                var sourcePath = completed.Path;

                var args = new DownloadCompletedEventArgs(hash, sourcePath, files);
                OnDownloadCompleted(args);
                if (args.Delete) {
                    var directory = new DirectoryInfo(sourcePath);
                    Guid guid;
                    while (directory != null && !Guid.TryParse(directory.Name, out guid))
                        directory = directory.Parent;

                    await client.DeleteTorrentAsync(hash);
                    if (directory != null)
                        directory.Delete(true);
                }
            }
        }

        protected virtual UTorrentClient CreateClient() {
            if (!Process.GetProcessesByName("uTorrent").Any()) {
                string path;
                var registryExePath = Registry.GetValue(@"HKEY_CLASSES_ROOT\uTorrent\shell\open\command", "", null) as string;
                if (registryExePath != null) {
                    var idx1 = registryExePath.IndexOf('"') + 1;
                    var idx2 = registryExePath.IndexOf('"', idx1);
                    if (idx2 == -1) idx2 = registryExePath.Length;
                    path = registryExePath.Substring(idx1, idx2 - idx1);
                }
                else {
                    var currentDirectory = Directory.GetCurrentDirectory();
                    path = Path.Combine(currentDirectory, "uTorrent\\uTorrent.exe");
                }
                Process.Start(path,"/minimized");
            }

            return Settings.Port.HasValue
                ? new UTorrentClient("127.0.0.1", Settings.Port.Value, Settings.UserName, Settings.Password)
                : new UTorrentClient(Settings.UserName, Settings.Password);
        }

        public override Task<IEnumerable<ITorrentSearchResult>> SearchMovie(string name, int? year, string imdbId, VideoQuality videoQuality = VideoQuality.Any,
                                                                            string extraKeywords = null, string excludeKeywords = null) {
            if (videoQuality == VideoQuality.Any)
                videoQuality = Settings.DefaultMovieVideoQuality.SelectedItem.Item;
            if (string.IsNullOrEmpty(extraKeywords))
                extraKeywords = Settings.DefaultMovieExtraKeywords;
            if (string.IsNullOrEmpty(excludeKeywords))
                excludeKeywords = Settings.DefaultMovieExcludeKeywords;

            return base.SearchMovie(name, year, imdbId, videoQuality, extraKeywords, excludeKeywords);
        }

        public override Task<IEnumerable<ITorrentSearchResult>> SearchTvShowEpisode(string name, int season, int episode, string episodeName, string imdbId,
                                                                                    VideoQuality videoQuality = VideoQuality.Any, string extraKeywords = null, string excludeKeywords = null) {
            if (videoQuality == VideoQuality.Any)
                videoQuality = Settings.DefaultTvShowVideoQuality.SelectedItem.Item;
            if (string.IsNullOrEmpty(extraKeywords))
                extraKeywords = Settings.DefaultTvShowExtraKeywords;
            if (string.IsNullOrEmpty(excludeKeywords))
                excludeKeywords = Settings.DefaultTvShowExcludeKeywords;

            return base.SearchTvShowEpisode(name, season, episode, episodeName, imdbId, videoQuality, extraKeywords, excludeKeywords);
        }

        protected override string ServiceName {
            get { return ServiceNames.UTorrent; }
        }

        protected override IEnumerable<ITorrentMovieProvider> MovieProviders {
            get { return Settings.MovieProviderSelection.SelectedItems; }
        }

        protected override IEnumerable<ITorrentTvShowProvider> TvShowProviders {
            get { return Settings.TvShowProviderSelection.SelectedItems; }
        }

        public UTorrentSettings Settings {
            get { return _settings; }
        }

        #region IConfigurable Members

        public string SettingName {
            get { return ServiceName; }
        }

        INotifyPropertyChanged IConfigurable.Settings {
            get { return Settings; }
        }

        public string SerializeSettings() {
            var o = new {
                Settings.UserName,
                Settings.Password,
                Settings.Port,
                MovieProviders = Settings.MovieProviderSelection.SelectedItemNames,
                TvShowProviders = Settings.TvShowProviderSelection.SelectedItemNames
            };
            return JsonConvert.SerializeObject(o);
        }

        public void DeserializeSettings(string settings) {
            var o = (JObject)JsonConvert.DeserializeObject(settings);

            Settings.UserName = o["UserName"].ToString();
            Settings.Password = o["Password"].ToString();
            int port;
            if (Int32.TryParse(o["Port"].ToString(), out port))
                Settings.Port = port;
            var movieProviders = (JArray)o["MovieProviders"];
            Settings.MovieProviderSelection.SelectedItemNames = movieProviders.Select(x => x.ToString());
            var tvShowProviders = (JArray)o["TvShowProviders"];
            Settings.TvShowProviderSelection.SelectedItemNames = tvShowProviders.Select(x => x.ToString());
        }

        #endregion
    }
}
