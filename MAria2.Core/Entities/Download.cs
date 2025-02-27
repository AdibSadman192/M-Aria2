using MAria2.Core.Enums;

namespace MAria2.Core.Entities;

public class Download
{
    public Guid Id { get; set; }
    public string Url { get; set; }
    public string DestinationPath { get; set; }
    public long FileSize { get; set; }
    public DownloadStatus Status { get; set; }
    public DownloadPriority Priority { get; set; } = DownloadPriority.Normal
    public string PreferredEngine { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? QueuedAt { get; set; }

    // Split Download Information
    public SplitDownloadInfo SplitDownloadInfo { get; set; }

    // Metadata and Additional Properties
    public Dictionary<string, string> Metadata { get; set; } = new();
}

// Information about split downloads
public class SplitDownloadInfo
{
    public int TotalSegments { get; set; }
    public int CompletedSegments { get; set; }
    public bool IsSplitDownload { get; set; }
    public List<Guid> SegmentIds { get; set; } = new();
    public DownloadSegmentStrategy SegmentStrategy { get; set; }
}

// Enum for split download strategies
public enum DownloadSegmentStrategy
{
    EqualSize,
    AdaptiveSizing,
    EngineOptimized,
    RoundRobin
}
