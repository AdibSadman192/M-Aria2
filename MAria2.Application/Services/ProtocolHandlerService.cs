using System.Text.RegularExpressions;
using MAria2.Core.Entities;
using MAria2.Core.Enums;
using MAria2.Core.Interfaces;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;

namespace MAria2.Application.Services;

public class ProtocolHandlerService
{
    private readonly IEnumerable<IDownloadEngine> _downloadEngines;
    private readonly ILoggingService _loggingService;

    // Regex patterns for different content types and protocols
    private static readonly Dictionary<string, ProtocolType> ProtocolPatterns = new()
    {
        { @"^https?://.*\.(torrent)$", ProtocolType.Torrent },
        { @"^https?://.*\.(magnet):.*$", ProtocolType.Magnet },
        { @"^(ftp|sftp)://.*$", ProtocolType.FTP },
        { @"^https?://.*\.(m3u8|playlist)$", ProtocolType.StreamPlaylist },
        { @"^https?://.*youtube\.com.*$", ProtocolType.VideoStreaming },
        { @"^https?://.*vimeo\.com.*$", ProtocolType.VideoStreaming },
        { @"^https?://.*\.(zip|rar|7z|tar\.gz)$", ProtocolType.Compressed },
        { @"^https?://.*\.(iso|img)$", ProtocolType.DiskImage }
    };

    // Content type detection patterns
    private static readonly Dictionary<string, ContentType> ContentPatterns = new()
    {
        { @"^video/", ContentType.Video },
        { @"^audio/", ContentType.Audio },
        { @"^image/", ContentType.Image },
        { @"^application/pdf$", ContentType.Document },
        { @"^text/", ContentType.Text },
        { @"^application/x-bittorrent$", ContentType.Torrent }
    };

    // Performance tracking for engines
    private readonly ConcurrentDictionary<Type, EnginePerformanceMetrics> _enginePerformanceMetrics 
        = new ConcurrentDictionary<Type, EnginePerformanceMetrics>();

    // Advanced protocol patterns with more specific matching
    private static readonly Dictionary<string, (ProtocolType Type, double Complexity)> AdvancedProtocolPatterns = new()
    {
        { @"^https?://.*\.(torrent)$", (ProtocolType.Torrent, 0.9) },
        { @"^magnet:\?xt=urn:btih:.*$", (ProtocolType.Magnet, 0.8) },
        { @"^(ftp|sftp)://.*$", (ProtocolType.FTP, 0.7) },
        { @"^https?://.*\.(m3u8|playlist)$", (ProtocolType.StreamPlaylist, 0.6) },
        { @"^https?://.*youtube\.com.*$", (ProtocolType.VideoStreaming, 0.5) },
        { @"^https?://.*vimeo\.com.*$", (ProtocolType.VideoStreaming, 0.5) },
        { @"^https?://.*\.(zip|rar|7z|tar\.gz)$", (ProtocolType.Compressed, 0.4) },
        { @"^https?://.*\.(iso|img)$", (ProtocolType.DiskImage, 0.4) }
    };

    // Enhanced content type detection with more granular types
    private static readonly Dictionary<string, (ContentType Type, double Specificity)> AdvancedContentPatterns = new()
    {
        { @"^video/mp4$", (ContentType.Video, 0.9) },
        { @"^video/x-matroska$", (ContentType.Video, 0.8) },
        { @"^audio/mpeg$", (ContentType.Audio, 0.9) },
        { @"^audio/x-wav$", (ContentType.Audio, 0.8) },
        { @"^image/jpeg$", (ContentType.Image, 0.9) },
        { @"^image/png$", (ContentType.Image, 0.9) },
        { @"^application/pdf$", (ContentType.Document, 0.9) },
        { @"^text/plain$", (ContentType.Text, 0.8) },
        { @"^application/x-bittorrent$", (ContentType.Torrent, 0.9) }
    };

    public ProtocolHandlerService(
        IEnumerable<IDownloadEngine> downloadEngines,
        ILoggingService loggingService)
    {
        _downloadEngines = downloadEngines;
        _loggingService = loggingService;
    }

    public async Task<DownloadRequest> AnalyzeDownloadRequestAsync(string url)
    {
        try 
        {
            var uri = new Uri(url);
            
            return new DownloadRequest(
                Url: url,
                Protocol: DetectAdvancedProtocolType(url).Type,
                ContentType: (await DetectAdvancedContentTypeAsync(url)).Type,
                DownloadCharacteristics: await AnalyzeDownloadCharacteristicsAsync(url)
            );
        }
        catch (UriFormatException ex)
        {
            _loggingService.LogError($"Invalid URL format: {url}");
            throw new InvalidOperationException("Invalid URL format", ex);
        }
    }

    private ProtocolType DetectProtocolType(string url)
    {
        foreach (var pattern in ProtocolPatterns)
        {
            if (Regex.IsMatch(url, pattern.Key, RegexOptions.IgnoreCase))
            {
                return pattern.Value;
            }
        }
        return ProtocolType.HTTP; // Default
    }

    private async Task<ContentType> DetectContentTypeAsync(string url)
    {
        try 
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request);
            
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            
            foreach (var pattern in ContentPatterns)
            {
                if (Regex.IsMatch(contentType, pattern.Key, RegexOptions.IgnoreCase))
                {
                    return pattern.Value;
                }
            }

            return ContentType.Unknown;
        }
        catch 
        {
            // Fallback to generic detection if HEAD request fails
            return ContentType.Unknown;
        }
    }

    private async Task<DownloadCharacteristics> AnalyzeDownloadCharacteristicsAsync(string url)
    {
        var characteristics = new DownloadCharacteristics
        {
            FileExtension = Path.GetExtension(url),
            EstimatedFileSize = await EstimateFileSizeAsync(url)
        };

        return characteristics;
    }

    private async Task<long> EstimateFileSizeAsync(string url)
    {
        try 
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request);
            
            return response.Content.Headers.ContentLength ?? -1;
        }
        catch 
        {
            return -1; // Unable to estimate
        }
    }

    public void TrackEnginePerformance(IDownloadEngine engine, DownloadMetrics metrics)
    {
        var engineType = engine.GetType();
        _enginePerformanceMetrics.AddOrUpdate(
            engineType,
            new EnginePerformanceMetrics(metrics),
            (type, existingMetrics) => existingMetrics.Update(metrics)
        );
    }

    private (ProtocolType Type, double Complexity) DetectAdvancedProtocolType(string url)
    {
        foreach (var pattern in AdvancedProtocolPatterns)
        {
            if (Regex.IsMatch(url, pattern.Key, RegexOptions.IgnoreCase))
            {
                return pattern.Value;
            }
        }
        return (ProtocolType.HTTP, 0.1); // Default with low complexity
    }

    private async Task<(ContentType Type, double Specificity)> DetectAdvancedContentTypeAsync(string url)
    {
        try 
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request);
            
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            
            foreach (var pattern in AdvancedContentPatterns)
            {
                if (Regex.IsMatch(contentType, pattern.Key, RegexOptions.IgnoreCase))
                {
                    return pattern.Value;
                }
            }

            return (ContentType.Unknown, 0.1);
        }
        catch 
        {
            return (ContentType.Unknown, 0.1);
        }
    }

    public async Task<IDownloadEngine> RecommendDownloadEngineAsync(DownloadRequest request)
    {
        var compatibleEngines = _downloadEngines
            .Where(engine => engine.CanHandleProtocol(request.Url))
            .ToList();

        if (!compatibleEngines.Any())
        {
            throw new NotSupportedException($"No engine supports the URL: {request.Url}");
        }

        // Enhanced scoring with historical performance and current request characteristics
        var rankedEngines = compatibleEngines
            .Select(engine => new 
            {
                Engine = engine,
                Score = ScoreEngineForRequest(engine, request),
                HistoricalPerformance = GetHistoricalEnginePerformance(engine)
            })
            .OrderByDescending(x => x.Score * x.HistoricalPerformance)
            .ToList();

        var topEngine = rankedEngines.First().Engine;
        
        _loggingService.LogInformation(
            $"Recommended engine {topEngine.GetType().Name} " +
            $"for URL: {request.Url} " +
            $"(Protocol: {request.Protocol}, Content: {request.ContentType})"
        );

        return topEngine;
    }

    private double ScoreEngineForRequest(IDownloadEngine engine, DownloadRequest request)
    {
        double score = 0;

        // Protocol support
        score += engine.CanHandleProtocol(request.Url) ? 0.4 : 0;

        // Content type considerations
        score += request.ContentType switch
        {
            ContentType.Video => 0.3,
            ContentType.Audio => 0.2,
            ContentType.Torrent => 0.3,
            ContentType.StreamPlaylist => 0.3,
            _ => 0.1
        };

        // Size considerations
        score += request.DownloadCharacteristics.EstimatedFileSize switch
        {
            long size when size > 1_000_000_000 => 0.2, // Large files
            long size when size > 100_000_000 => 0.1,   // Medium files
            _ => 0.05
        };

        return score;
    }

    private double GetHistoricalEnginePerformance(IDownloadEngine engine)
    {
        var engineType = engine.GetType();
        if (_enginePerformanceMetrics.TryGetValue(engineType, out var metrics))
        {
            return metrics.CalculateOverallPerformanceScore();
        }
        return 1.0; // Default neutral score
    }

    private class EnginePerformanceMetrics
    {
        public int TotalDownloads { get; private set; }
        public int SuccessfulDownloads { get; private set; }
        public double AverageDownloadSpeed { get; private set; }
        public double AverageDownloadTime { get; private set; }

        public EnginePerformanceMetrics(DownloadMetrics initialMetrics)
        {
            Update(initialMetrics);
        }

        public EnginePerformanceMetrics Update(DownloadMetrics metrics)
        {
            TotalDownloads++;
            SuccessfulDownloads += metrics.IsSuccessful ? 1 : 0;
            
            // Weighted moving average
            AverageDownloadSpeed = (AverageDownloadSpeed * (TotalDownloads - 1) + metrics.DownloadSpeed) / TotalDownloads;
            AverageDownloadTime = (AverageDownloadTime * (TotalDownloads - 1) + metrics.DownloadTime) / TotalDownloads;

            return this;
        }

        public double CalculateOverallPerformanceScore()
        {
            // Complex scoring considering success rate, speed, and time
            double successRate = (double)SuccessfulDownloads / TotalDownloads;
            double speedScore = Math.Min(AverageDownloadSpeed / 1_000_000, 1.0); // Normalize speed
            double timeScore = 1 - Math.Min(AverageDownloadTime / 3600, 1.0); // Normalize time

            return (successRate * 0.5) + (speedScore * 0.3) + (timeScore * 0.2);
        }
    }

    public class DownloadMetrics
    {
        public bool IsSuccessful { get; set; }
        public double DownloadSpeed { get; set; } // Bytes per second
        public double DownloadTime { get; set; } // Seconds
        public long FileSize { get; set; }
    }

    public class DownloadCharacteristics
    {
        public string FileExtension { get; set; }
        public long EstimatedFileSize { get; set; }
    }
}
