﻿using System;
using System.Collections.Generic;
using System.Linq;
using Novaroma.Interface.Model;
using Novaroma.Interface.Track;

namespace Novaroma.Model {

    public class TvShow : Media {
        private bool _autoDownload = true;
        private bool _isActive;
        private string _status;
        private ICollection<TvShowSeason> _seasons;
        private DateTime _lastUpdateDate;

        public TvShow() {
            Seasons = new List<TvShowSeason>();
            AutoDownload = true;
        }

        public bool AutoDownload {
            get { return _autoDownload; }
            set {
                if (_autoDownload == value) return;

                _autoDownload = value;
                RaisePropertyChanged("AutoDownload");
            }
        }

        public bool IsActive {
            get { return _isActive; }
            set {
                if (_isActive == value) return;

                _isActive = value;
                RaisePropertyChanged("IsActive");
            }
        }

        public string Status {
            get { return _status; }
            set {
                if (_status == value) return;

                _status = value;
                RaisePropertyChanged("Status");
            }
        }

        public ICollection<TvShowSeason> Seasons {
            get { return _seasons; }
            set {
                if (Equals(_seasons, value)) return;

                _seasons = value;
                RaisePropertyChanged("Seasons");
            }
        }

        public DateTime LastUpdateDate {
            get { return _lastUpdateDate; }
            set {
                if (_lastUpdateDate == value) return;

                _lastUpdateDate = value;
                RaisePropertyChanged("LastUpdateDate");
            }
        }

        public bool? AllBackgroundDownload {
            get {
                int nc = 0, tc = 0;
                var seasons = Seasons.ToList();
                seasons.ForEach(s => {
                    var d = s.AllBackgroundDownload;
                    if (!d.HasValue) nc++;
                    else if (d.Value) tc++;
                });
                if (nc > 0) return null;
                return tc == seasons.Count;
            }
            set {
                Seasons.ToList().ForEach(e => e.AllBackgroundDownload = value.HasValue && value.Value);
                RaisePropertyChanged("AllBackgroundDownload");
            }
        }

        public bool? AllBackgroundSubtitleDownload {
            get {
                int nc = 0, tc = 0;
                var seasons = Seasons.ToList();
                seasons.ForEach(s => {
                    var d = s.AllBackgroundSubtitleDownload;
                    if (!d.HasValue) nc++;
                    else if (d.Value) tc++;
                });
                if (nc > 0) return null;
                return tc == seasons.Count;
            }
            set {
                Seasons.ToList().ForEach(e => e.AllBackgroundSubtitleDownload = value.HasValue && value.Value);
                RaisePropertyChanged("AllBackgroundSubtitleDownload");
            }
        }

        public bool? AllWatched {
            get {
                int nc = 0, tc = 0;
                var seasons = Seasons.ToList();
                seasons.ForEach(s => {
                    var d = s.AllWatched;
                    if (!d.HasValue) nc++;
                    else if (d.Value) tc++;
                });
                if (nc > 0) return null;
                return tc == seasons.Count;
            }
            set {
                Seasons.ToList().ForEach(e => e.AllWatched = value.HasValue && value.Value);
                RaisePropertyChanged("AllWatched");
            }
        }

        public TvShowEpisode UnseenEpisode {
            get {
                return Seasons.SelectMany(s => s.Episodes).FirstOrDefault(e => !e.IsWatched);
            }
        }

        protected override void RaisePropertyChanged(string propertyName = null) {
            base.RaisePropertyChanged(propertyName);
            base.RaisePropertyChanged("UnseenEpisode");
        }

        protected override void CopyFrom(IEntity entity) {
            var external = Helper.ConvertTo<TvShow>(entity);

            CopyFrom(external);

            AutoDownload = external.AutoDownload;
            IsActive = external.IsActive;
            Status = external.Status;
            LastUpdateDate = external.LastUpdateDate;

            var c = Seasons.Count;
            var ec = external.Seasons.Count;
            while (c > ec) Seasons.Remove(Seasons.ElementAt(--c));

            for (var i = 0; i < ec; i++) {
                TvShowSeason season;
                if (c < i + 1) {
                    season = new TvShowSeason {TvShowId = Id, TvShow = this};
                    Seasons.Add(season);
                }
                else
                    season = Seasons.ElementAt(i);

                season.CopyFrom(external.Seasons.ElementAt(i));
            }
        }

        public void MergeEpisodes(IEnumerable<ITvShowEpisodeInfo> episodes, bool subtitlesNeeded) {
            episodes = episodes.OrderBy(e => e.Season, new SeasonComparer()).ThenBy(e => e.Episode);

            ITvShowEpisodeInfo lastEpisode = null;
            foreach (var episodeInfo in episodes) {
                var season = Seasons.FirstOrDefault(s => s.Season == episodeInfo.Season);
                if (season == null) {
                    season = new TvShowSeason { TvShowId = Id, TvShow = this, Season = episodeInfo.Season };
                    Seasons.Add(season);
                }

                var episode = season.Episodes.FirstOrDefault(e => e.Episode == episodeInfo.Episode);
                if (episode == null) {
                    episode = new TvShowEpisode { TvShowSeason = season };
                    season.Episodes.Add(episode);
                    episode.Episode = episodeInfo.Episode;
                    episode.BackgroundDownload = AutoDownload;
                    episode.BackgroundSubtitleDownload = AutoDownload && subtitlesNeeded;
                }

                episode.AirDate = episodeInfo.AirDate;
                episode.Name = episodeInfo.Name;
                episode.Overview = episodeInfo.Overview;

                lastEpisode = episodeInfo;
            }

            if (lastEpisode != null) {
                for (var i = Seasons.Count - 1; i >= 0; i--) {
                    var season = Seasons.ElementAt(i);
                    if (season.Season > lastEpisode.Season)
                        Seasons.Remove(season);
                    else if (season.Season == lastEpisode.Season) {
                        for (var j = season.Episodes.Count - 1; j >= 0; j--) {
                            var episode = season.Episodes.ElementAt(j);
                            if (episode.Episode > lastEpisode.Episode)
                                season.Episodes.Remove(episode);
                        }
                    }
                }
            }
        }

        public void Update(ITvShowUpdate tvShowUpdate, bool subtitlesNeeded) {
            if (tvShowUpdate == null) return;

            IsActive = tvShowUpdate.IsActive;
            Status = tvShowUpdate.Status;
            LastUpdateDate = tvShowUpdate.UpdateDate;

            MergeEpisodes(tvShowUpdate.UpdateEpisodes, subtitlesNeeded);
        }

        internal void OnSeasonBackgroundDownloadChange() {
            RaisePropertyChanged("AllBackgroundDownload");
        }

        internal void OnSeasonBackgroundSubtitleDownloadChange() {
            RaisePropertyChanged("AllBackgroundSubtitleDownload");
        }

        internal void OnSeasonIsWatchedChange() {
            RaisePropertyChanged("AllWatched");
        }
    }
}
