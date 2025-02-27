using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MAria2.Core.Entities;

namespace MAria2.Core.Interfaces;

public interface IPostProcessingService
{
    /// <summary>
    /// Create a post-processing task for a downloaded file
    /// </summary>
    Task<PostProcessingTask> CreateTaskAsync(
        Download sourceDownload, 
        PostProcessingType type, 
        PostProcessingConfig config = null);

    /// <summary>
    /// Execute a specific post-processing task
    /// </summary>
    Task<PostProcessingTask> ExecuteTaskAsync(Guid taskId);

    /// <summary>
    /// Execute multiple post-processing tasks
    /// </summary>
    Task<List<PostProcessingTask>> ExecuteTasksAsync(List<Guid> taskIds);

    /// <summary>
    /// Get all pending post-processing tasks
    /// </summary>
    Task<List<PostProcessingTask>> GetPendingTasksAsync();

    /// <summary>
    /// Cancel a specific post-processing task
    /// </summary>
    Task CancelTaskAsync(Guid taskId);

    /// <summary>
    /// Extract metadata from a file
    /// </summary>
    Task<MetadataExtractionTask> ExtractMetadataAsync(string filePath);

    /// <summary>
    /// Get detailed processing options for a file type
    /// </summary>
    Task<List<PostProcessingType>> GetSupportedProcessingTypesAsync(string filePath);

    /// <summary>
    /// Automatically determine best post-processing strategy
    /// </summary>
    Task<PostProcessingTask> AutoProcessAsync(Download sourceDownload);

    /// <summary>
    /// Create advanced post-processing pipeline
    /// </summary>
    Task<PostProcessingPipeline> CreateProcessingPipelineAsync(
        List<PostProcessingType> processingSteps, 
        PostProcessingConfig config);

    /// <summary>
    /// Get recommended processing strategy based on file characteristics
    /// </summary>
    Task<PostProcessingRecommendation> GetRecommendedProcessingStrategyAsync(
        Download sourceDownload);
}

public enum PostProcessingType
{
    // Existing types...
    
    // New advanced processing types
    VideoTranscode = 100,
    AudioNormalization = 101,
    SubtitleExtraction = 102,
    ImageConversion = 103,
    FileCompression = 104,
    SecuritySanitization = 105,
    MetadataCleaning = 106,
    AIEnhancement = 107
}

public class PostProcessingConfig
{
    // Existing configuration options...
    
    // Advanced configuration
    public VideoTranscodeOptions VideoTranscode { get; set; }
    public AudioNormalizationOptions AudioNormalization { get; set; }
    public SubtitleExtractionOptions SubtitleExtraction { get; set; }
    public ImageConversionOptions ImageConversion { get; set; }
    public FileCompressionOptions FileCompression { get; set; }
    public SecuritySanitizationOptions SecuritySanitization { get; set; }
    public MetadataCleaningOptions MetadataCleaning { get; set; }
    public AIEnhancementOptions AIEnhancement { get; set; }
}

// Detailed configuration classes
public class VideoTranscodeOptions
{
    public string TargetFormat { get; set; }
    public int? TargetBitrate { get; set; }
    public string Resolution { get; set; }
    public string VideoCodec { get; set; }
    public string AudioCodec { get; set; }
    public bool RemoveMetadata { get; set; }
}

public class AudioNormalizationOptions
{
    public double TargetVolume { get; set; }
    public bool ApplyCompression { get; set; }
    public bool ReduceDynamicRange { get; set; }
}

public class SubtitleExtractionOptions
{
    public string[] TargetLanguages { get; set; }
    public string OutputFormat { get; set; }
    public bool ExtractAll { get; set; }
}

public class ImageConversionOptions
{
    public string TargetFormat { get; set; }
    public int? Quality { get; set; }
    public int? MaxWidth { get; set; }
    public int? MaxHeight { get; set; }
    public bool Resize { get; set; }
}

public class FileCompressionOptions
{
    public string CompressionFormat { get; set; }
    public int? CompressionLevel { get; set; }
    public bool SplitArchive { get; set; }
    public long? MaxArchiveSize { get; set; }
}

public class SecuritySanitizationOptions
{
    public bool RemoveExecutables { get; set; }
    public bool ScanForMalware { get; set; }
    public bool AnonymizeMetadata { get; set; }
}

public class MetadataCleaningOptions
{
    public bool RemovePersonalInfo { get; set; }
    public bool StandardizeMetadata { get; set; }
    public string[] MetadataFieldsToRemove { get; set; }
}

public class AIEnhancementOptions
{
    public bool EnhanceVideoQuality { get; set; }
    public bool UpscaleResolution { get; set; }
    public bool NoiseReduction { get; set; }
    public bool ColorCorrection { get; set; }
}

public class PostProcessingPipeline
{
    public Guid PipelineId { get; set; }
    public List<PostProcessingTask> Tasks { get; set; }
    public DateTime CreatedAt { get; set; }
    public ProcessingPipelineStatus Status { get; set; }
}

public class PostProcessingRecommendation
{
    public List<PostProcessingType> RecommendedProcessingTypes { get; set; }
    public PostProcessingConfig SuggestedConfiguration { get; set; }
    public string Rationale { get; set; }
}

public enum ProcessingPipelineStatus
{
    Created,
    Queued,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Result of a batch post-processing operation
/// </summary>
public class BatchProcessingResult
{
    public int TotalTasks { get; set; }
    public int SuccessfulTasks { get; set; }
    public int FailedTasks { get; set; }
    public List<Guid> SuccessfulTaskIds { get; set; } = new();
    public List<Guid> FailedTaskIds { get; set; } = new();
    public TimeSpan TotalProcessingTime { get; set; }
}
