using System;
using System.Collections.Generic;

namespace MediaDownloader.Models
{
    public class MediaMetadata
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Uploader { get; set; } = string.Empty;
        public string DurationString { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string WebpageUrl { get; set; } = string.Empty;
        public bool IsPlaylist { get; set; }
        public int PlaylistCount { get; set; }
        public List<string> AvailableResolutions { get; set; } = new();
    }

    public class DownloadRequest
    {
        public string Url { get; set; } = string.Empty;
        public string DestinationFolder { get; set; } = string.Empty;
        public bool IsAudioOnly { get; set; }
        public string VideoQuality { get; set; } = "Melhor Qualidade (Best)";
        public string VideoFormat { get; set; } = "mp4";
        public string AudioQuality { get; set; } = "320 kbps (Melhor)";
        public string AudioFormat { get; set; } = "mp3";
        public bool DownloadPlaylist { get; set; }
        public bool EmbedMetadata { get; set; } = true;
        public bool EmbedSubtitles { get; set; } = false;
    }
}
