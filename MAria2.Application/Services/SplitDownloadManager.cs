using System.Collections.Concurrent;
using MAria2.Core.Entities;
using MAria2.Core.Enums;
using MAria2.Core.Interfaces;

namespace MAria2.Application.Services;

public class SplitDownloadManager
{
    private readonly IEnumerable<IDownloadEngine> _downloadEngines;
    private readonly EngineSelectionService _engineSelectionService;
    private readonly ILoggingService _loggingService;

    public SplitDownloadManager(
        IEnumerable<IDownloadEngine> downloadEngines,
        EngineSelectionService engineSelectionService,
        ILoggingService loggingService)
    {
        _downloadEngines = downloadEngines;
        _engineSelectionService = engineSelectionService;
        _loggingService = loggingService;
    }

    public async Task<Download> StartSplitDownloadAsync(
        string url, 
        string destinationPath, 
        int segments = 4)
    {
        var download = new Download
        {
            Id = Guid.NewGuid(),
            Url = url,
            DestinationPath = destinationPath,
            Status = DownloadStatus.Initializing,
            SplitDownloadInfo = new SplitDownloadInfo { TotalSegments = segments }
        };

        try 
        {
            // Analyze download characteristics
            var downloadRequest = await AnalyzeDownloadRequestAsync(url);
            
            // Determine optimal segment distribution
            var segmentDistribution = await CalculateSegmentDistributionAsync(
                downloadRequest, 
                segments
            );

            // Prepare parallel download tasks
            var segmentTasks = new List<Task<DownloadSegment>>();
            for (int i = 0; i < segments; i++)
            {
                segmentTasks.Add(DownloadSegmentAsync(
                    download, 
                    segmentDistribution[i],
                    i
                ));
            }

            // Wait for all segments
            var downloadSegments = await Task.WhenAll(segmentTasks);
            
            // Merge segments
            await MergeDownloadSegmentsAsync(download, downloadSegments);

            return download;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Split download failed: {ex.Message}");
            download.Status = DownloadStatus.Failed;
            throw;
        }
    }

    private async Task<DownloadRequest> AnalyzeDownloadRequestAsync(string url)
    {
        // Use ProtocolHandlerService to analyze download request
        var protocolHandler = new ProtocolHandlerService(
            _downloadEngines, 
            _loggingService
        );

        return await protocolHandler.AnalyzeDownloadRequestAsync(url);
    }

    private async Task<List<SegmentRange>> CalculateSegmentDistributionAsync(
        DownloadRequest request, 
        int segments)
    {
        long totalFileSize = request.DownloadCharacteristics.EstimatedFileSize;
        
        // If file size is unknown, use a default strategy
        if (totalFileSize <= 0)
        {
            throw new InvalidOperationException("Cannot split download: file size unknown");
        }

        var segmentSize = totalFileSize / segments;
        var remainingBytes = totalFileSize % segments;

        var segmentRanges = new List<SegmentRange>();
        long currentOffset = 0;

        for (int i = 0; i < segments; i++)
        {
            long currentSegmentSize = segmentSize + (i < remainingBytes ? 1 : 0);
            
            segmentRanges.Add(new SegmentRange(
                Start: currentOffset,
                Length: currentSegmentSize
            ));

            currentOffset += currentSegmentSize;
        }

        return segmentRanges;
    }

    private async Task<DownloadSegment> DownloadSegmentAsync(
        Download parentDownload, 
        SegmentRange segmentRange, 
        int segmentIndex)
    {
        var segment = new DownloadSegment
        {
            Id = Guid.NewGuid(),
            ParentDownloadId = parentDownload.Id,
            SegmentIndex = segmentIndex,
            StartByte = segmentRange.Start,
            EndByte = segmentRange.Start + segmentRange.Length - 1,
            Status = DownloadStatus.Initializing
        };

        try 
        {
            // Select best engine for this segment
            var segmentEngine = await _engineSelectionService.SelectBestEngineAsync(
                new DownloadRequest(parentDownload.Url)
            );

            // Download segment
            segment = await segmentEngine.DownloadSegmentAsync(
                parentDownload, 
                segment
            );

            return segment;
        }
        catch (Exception ex)
        {
            segment.Status = DownloadStatus.Failed;
            _loggingService.LogError(
                $"Segment {segmentIndex} download failed: {ex.Message}"
            );
            throw;
        }
    }

    private async Task MergeDownloadSegmentsAsync(
        Download download, 
        DownloadSegment[] segments)
    {
        try 
        {
            // Verify all segments are successfully downloaded
            if (segments.Any(s => s.Status != DownloadStatus.Completed))
            {
                throw new InvalidOperationException("Not all segments downloaded successfully");
            }

            // Sort segments by index
            var orderedSegments = segments.OrderBy(s => s.SegmentIndex).ToArray();

            // Merge segments
            using var outputStream = File.Create(download.DestinationPath);
            foreach (var segment in orderedSegments)
            {
                using var segmentStream = File.OpenRead(segment.TemporaryFilePath);
                await segmentStream.CopyToAsync(outputStream);
            }

            // Clean up temporary segment files
            foreach (var segment in orderedSegments)
            {
                File.Delete(segment.TemporaryFilePath);
            }

            download.Status = DownloadStatus.Completed;
        }
        catch (Exception ex)
        {
            download.Status = DownloadStatus.Failed;
            _loggingService.LogError($"Segment merge failed: {ex.Message}");
            throw;
        }
    }

    // New method for advanced download segment recovery
    public async Task<Download> ResumeSplitDownloadAsync(Download previousDownload)
    {
        // Implement download recovery mechanism
        if (previousDownload.Status != DownloadStatus.Failed)
        {
            throw new InvalidOperationException("Only failed downloads can be resumed");
        }

        var remainingSegments = await IdentifyRemainingSegmentsAsync(previousDownload);
        
        try 
        {
            var segmentTasks = remainingSegments.Select(async segment => 
                await DownloadSegmentAsync(previousDownload, 
                    new SegmentRange(segment.StartByte, segment.EndByte - segment.StartByte + 1), 
                    segment.SegmentIndex)
            ).ToList();

            var recoveredSegments = await Task.WhenAll(segmentTasks);
            
            await MergeDownloadSegmentsAsync(previousDownload, recoveredSegments);
            
            return previousDownload;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Download recovery failed: {ex.Message}");
            previousDownload.Status = DownloadStatus.Failed;
            throw;
        }
    }

    // Advanced segment identification for recovery
    private async Task<List<DownloadSegment>> IdentifyRemainingSegmentsAsync(Download download)
    {
        // Implement intelligent segment recovery logic
        return download.Segments
            .Where(s => s.Status != DownloadStatus.Completed)
            .ToList();
    }

    // Bandwidth and performance tracking
    public async Task<DownloadPerformanceMetrics> GetDownloadPerformanceAsync(Download download)
    {
        var metrics = new DownloadPerformanceMetrics
        {
            TotalFileSize = download.Segments.Sum(s => s.EndByte - s.StartByte + 1),
            DownloadedSize = download.Segments
                .Where(s => s.Status == DownloadStatus.Completed)
                .Sum(s => s.EndByte - s.StartByte + 1),
            SegmentCount = download.Segments.Length,
            CompletedSegments = download.Segments.Count(s => s.Status == DownloadStatus.Completed)
        };

        metrics.OverallProgress = (double)metrics.DownloadedSize / metrics.TotalFileSize * 100;
        
        return metrics;
    }

    // Performance tracking record
    public record DownloadPerformanceMetrics
    {
        public long TotalFileSize { get; set; }
        public long DownloadedSize { get; set; }
        public double OverallProgress { get; set; }
        public int SegmentCount { get; set; }
        public int CompletedSegments { get; set; }
    }

    // Represents a range of bytes for a download segment
    public record SegmentRange(long Start, long Length);

    // Advanced split download configuration
    public class SplitDownloadConfiguration
    {
        public int MaxSegments { get; set; } = 4;
        public bool UseMultipleEngines { get; set; } = true;
        public long MinSegmentSize { get; set; } = 1_048_576; // 1 MB
    }
}

// Additional error handling and retry strategy
public class SplitDownloadException : Exception
{
    public DownloadSegment FailedSegment { get; }
    
    public SplitDownloadException(string message, DownloadSegment failedSegment) 
        : base(message)
    {
        FailedSegment = failedSegment;
    }
}
