using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MAria2.Core.Interfaces;

public interface IMediaInformationService
{
    /// <summary>
    /// Extracts comprehensive metadata for a given media file
    /// </summary>
    /// <param name="filePath">Path to the media file</param>
    /// <returns>Detailed media metadata</returns>
    Task<MediaMetadata> ExtractMetadataAsync(string filePath);

    /// <summary>
    /// Generates preview thumbnails for media files
    /// </summary>
    /// <param name="filePath">Path to the media file</param>
    /// <param name="thumbnailCount">Number of thumbnails to generate</param>
    /// <returns>List of thumbnail file paths</returns>
    Task<List<string>> GeneratePreviewThumbnailsAsync(string filePath, int thumbnailCount = 5);

    /// <summary>
    /// Analyzes media file codec and stream information
    /// </summary>
    /// <param name="filePath">Path to the media file</param>
    /// <returns>Detailed codec and stream information</returns>
    Task<MediaStreamInfo> AnalyzeStreamInfoAsync(string filePath);

    /// <summary>
    /// Checks if the file is a supported media type
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>True if media is supported, false otherwise</returns>
    bool IsSupportedMediaType(string filePath);

    /// <summary>
    /// Extracts detailed copyright and licensing information
    /// </summary>
    Task<CopyrightInfo> ExtractCopyrightInformationAsync(string filePath);

    /// <summary>
    /// Generates comprehensive media report
    /// </summary>
    Task<MediaReport> GenerateMediaReportAsync(string filePath);
}

/// <summary>
/// Represents comprehensive metadata for a media file
/// </summary>
public class MediaMetadata
{
    public string FilePath { get; set; }
    public string FileName { get; set; }
    public long FileSize { get; set; }
    public MediaType Type { get; set; }
    
    // Enhanced Metadata
    public string Title { get; set; }
    public string Artist { get; set; }
    public string Album { get; set; }
    public string Genre { get; set; }
    public DateTime ReleaseDate { get; set; }
    
    public TimeSpan Duration { get; set; }
    
    // Media Quality Indicators
    public int? BitRate { get; set; }
    public int? SampleRate { get; set; }
    public string Quality { get; set; }
    
    // Copyright and Licensing
    public string CopyrightInfo { get; set; }
    public string LicenseType { get; set; }
    
    // Additional Metadata
    public Dictionary<string, string> ExtendedMetadata { get; set; } = new();
    
    // Media Source Information
    public string SourceUrl { get; set; }
    public DateTime DownloadTimestamp { get; set; }
}

/// <summary>
/// Represents media stream technical details
/// </summary>
public class MediaStreamInfo
{
    public int VideoStreamCount { get; set; }
    public int AudioStreamCount { get; set; }
    public int SubtitleStreamCount { get; set; }

    public List<StreamDetails> VideoStreams { get; set; } = new();
    public List<StreamDetails> AudioStreams { get; set; } = new();
    public List<StreamDetails> SubtitleStreams { get; set; } = new();

    public class StreamDetails
    {
        public string Codec { get; set; }
        public string Language { get; set; }
        public string Resolution { get; set; }
        public string FrameRate { get; set; }
        public long Bitrate { get; set; }
        public bool IsDefault { get; set; }
    }
}

/// <summary>
/// Enumeration of supported media types
/// </summary>
public enum MediaType
{
    Unknown = 0,
    Video = 1,
    Audio = 2,
    Image = 3,
    Document = 4,
    Subtitle = 5,
    Playlist = 6
}

public class CopyrightInfo
{
    public string Owner { get; set; }
    public string LicenseType { get; set; }
    public DateTime? LicenseExpirationDate { get; set; }
    public bool IsPublicDomain { get; set; }
    public string UsageRestrictions { get; set; }
}

public class MediaReport
{
    public MediaMetadata Metadata { get; set; }
    public MediaStreamInfo StreamInfo { get; set; }
    public CopyrightInfo CopyrightDetails { get; set; }
    public List<string> PreviewThumbnails { get; set; }
    public Dictionary<string, object> AdditionalAnalysis { get; set; }
}
