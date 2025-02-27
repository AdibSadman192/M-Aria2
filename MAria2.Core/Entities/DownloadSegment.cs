using MAria2.Core.Enums;

namespace MAria2.Core.Entities;

public class DownloadSegment
{
    public Guid Id { get; set; }
    public Guid ParentDownloadId { get; set; }
    
    // Segment Identification
    public int SegmentIndex { get; set; }
    public long StartByte { get; set; }
    public long EndByte { get; set; }
    
    // Download Status
    public DownloadStatus Status { get; set; }
    public string UsedEngine { get; set; }
    
    // File Management
    public string TemporaryFilePath { get; set; }
    public long DownloadedBytes { get; set; }
    
    // Performance Metrics
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double AverageSpeed { get; set; }
    
    // Error Handling
    public string ErrorMessage { get; set; }
    
    // Retry Mechanism
    public int RetryCount { get; set; }
    public bool IsRetryAllowed { get; set; } = true;
    
    // Advanced Segment Properties
    public SegmentPriority Priority { get; set; } = SegmentPriority.Normal;
    public DownloadSegmentType SegmentType { get; set; }
}

// Segment priority for intelligent download management
public enum SegmentPriority
{
    Low,
    Normal,
    High,
    Critical
}

// Different types of download segments
public enum DownloadSegmentType
{
    Standard,
    InitialChunk,
    FinalChunk,
    Metadata,
    Verification
}
