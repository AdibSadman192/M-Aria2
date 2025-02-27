using System;
using System.Collections.Generic;
using MAria2.Core.Enums;

namespace MAria2.Core.Entities;

/// <summary>
/// Represents a post-processing task for downloaded content
/// </summary>
public class PostProcessingTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// The original download associated with this task
    /// </summary>
    public Download SourceDownload { get; set; }
    
    /// <summary>
    /// Type of post-processing to apply
    /// </summary>
    public PostProcessingType Type { get; set; }
    
    /// <summary>
    /// Configuration for the post-processing task
    /// </summary>
    public PostProcessingConfig Configuration { get; set; } = new();
    
    /// <summary>
    /// Current status of the post-processing task
    /// </summary>
    public PostProcessingStatus Status { get; set; } = PostProcessingStatus.Pending;
    
    /// <summary>
    /// Timestamp when the task was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Timestamp when the task was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Any error messages from processing
    /// </summary>
    public string ErrorMessage { get; set; }
    
    /// <summary>
    /// Resulting file path after processing
    /// </summary>
    public string ResultFilePath { get; set; }
}

/// <summary>
/// Configuration for post-processing tasks
/// </summary>
public class PostProcessingConfig
{
    /// <summary>
    /// Target file format for conversion
    /// </summary>
    public string TargetFormat { get; set; }
    
    /// <summary>
    /// Quality settings for conversion
    /// </summary>
    public string Quality { get; set; } = "medium";
    
    /// <summary>
    /// Specific codec to use for conversion
    /// </summary>
    public string Codec { get; set; }
    
    /// <summary>
    /// Resolution for video processing
    /// </summary>
    public string Resolution { get; set; }
    
    /// <summary>
    /// Audio sample rate for audio processing
    /// </summary>
    public int? AudioSampleRate { get; set; }
    
    /// <summary>
    /// Bitrate for audio/video conversion
    /// </summary>
    public string Bitrate { get; set; }
    
    /// <summary>
    /// Additional processing options
    /// </summary>
    public Dictionary<string, string> AdditionalOptions { get; set; } = 
        new Dictionary<string, string>();
}

/// <summary>
/// Represents a metadata extraction task
/// </summary>
public class MetadataExtractionTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceFilePath { get; set; }
    public Dictionary<string, string> ExtractedMetadata { get; set; } = 
        new Dictionary<string, string>();
    public MetadataExtractionStatus Status { get; set; }
}

/// <summary>
/// Enumeration of post-processing types
/// </summary>
public enum PostProcessingType
{
    None,
    FormatConversion,
    AudioExtraction,
    VideoTrimming,
    Compression,
    Normalization,
    Thumbnail,
    Subtitles,
    Metadata
}

/// <summary>
/// Status of post-processing tasks
/// </summary>
public enum PostProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Status of metadata extraction
/// </summary>
public enum MetadataExtractionStatus
{
    Pending,
    Extracting,
    Completed,
    Failed
}
