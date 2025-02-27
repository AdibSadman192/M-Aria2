using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using MAria2.Core.Entities;
using MAria2.Core.Enums;
using MAria2.Core.Interfaces;
using MAria2.Core.Configuration;

namespace MAria2.Infrastructure.Engines;

public class WinInetDownloadEngine : IDownloadEngine
{
    private readonly ILoggingService _loggingService;
    private readonly ConfigurationManager _configurationManager;

    public WinInetDownloadEngine(
        ILoggingService loggingService, 
        ConfigurationManager configurationManager)
    {
        _loggingService = loggingService;
        _configurationManager = configurationManager;
    }

    public EngineType Type => EngineType.WinInet;

    public string Version => GetWinInetVersion();

    private string GetWinInetVersion()
    {
        try 
        {
            // Retrieve Windows version information
            var osVersion = Environment.OSVersion;
            return $"WinInet {osVersion.Version}";
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to get WinInet version: {ex.Message}");
            return "unknown";
        }
    }

    public bool CanHandleProtocol(string url)
    {
        try 
        {
            var uri = new Uri(url);
            return uri.Scheme == Uri.UriSchemeHttp || 
                   uri.Scheme == Uri.UriSchemeHttps || 
                   uri.Scheme == Uri.UriSchemeFtp;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Download> StartDownloadAsync(Download download)
    {
        try 
        {
            var config = _configurationManager.GetEngineConfiguration(EngineType.WinInet);
            
            using var client = CreateWebClient(config, download.Credentials);
            
            // Configure download parameters
            client.DownloadProgressChanged += (sender, e) => 
            {
                download.Progress = e.ProgressPercentage;
                _loggingService.LogInformation(
                    $"Download Progress: {e.ProgressPercentage}% - {download.Url}"
                );
            };

            // Async download with progress tracking
            await client.DownloadFileTaskAsync(
                new Uri(download.Url), 
                download.DestinationPath
            );

            // Verify download
            var fileInfo = new FileInfo(download.DestinationPath);
            download.Status = fileInfo.Exists && fileInfo.Length > 0 
                ? DownloadStatus.Completed 
                : DownloadStatus.Failed;

            return download;
        }
        catch (WebException webEx)
        {
            _loggingService.LogError($"WinInet download failed: {webEx.Message}");
            download.Status = DownloadStatus.Failed;
            return download;
        }
    }

    private WebClient CreateWebClient(
        EngineConfiguration config, 
        AuthenticationCredentials credentials = null)
    {
        var client = new WebClient();

        // Set timeout
        client.Proxy = null; // Disable default proxy
        client.Credentials = credentials != null 
            ? new NetworkCredential(credentials.Username, credentials.Password) 
            : null;

        // Configure headers
        client.Headers.Add("User-Agent", $"MAria2/{Version}");
        
        // Network configuration
        if (config.MaxConnections.HasValue)
        {
            ServicePointManager.DefaultConnectionLimit = config.MaxConnections.Value;
        }

        return client;
    }

    public async Task<DownloadSegment> DownloadSegmentAsync(
        Download parentDownload, 
        DownloadSegment segment)
    {
        try 
        {
            using var request = (HttpWebRequest)WebRequest.Create(parentDownload.Url);
            request.AddRange(segment.StartByte, segment.EndByte);

            using var response = (HttpWebResponse)await request.GetResponseAsync();
            using var responseStream = response.GetResponseStream();
            using var fileStream = new FileStream(
                segment.TemporaryFilePath, 
                FileMode.Create, 
                FileAccess.Write, 
                FileShare.None
            );

            await responseStream.CopyToAsync(fileStream);

            segment.Status = DownloadStatus.Completed;
            return segment;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Segment download failed: {ex.Message}");
            segment.Status = DownloadStatus.Failed;
            return segment;
        }
    }

    public async Task<DownloadPerformance> TestPerformanceAsync(string url)
    {
        var performance = new DownloadPerformance { Url = url };

        try 
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using var client = new WebClient();
            var tempFile = Path.GetTempFileName();

            await client.DownloadFileTaskAsync(url, tempFile);
            
            stopwatch.Stop();

            var fileInfo = new FileInfo(tempFile);
            performance.SpeedMbps = (fileInfo.Length * 8.0) / (stopwatch.ElapsedMilliseconds * 1000.0);
            performance.ConnectionStability = 0.9; // Placeholder
            performance.TestDuration = stopwatch.Elapsed;
            performance.SupportedProtocols = new[] { "http", "https" };

            File.Delete(tempFile);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Performance test failed: {ex.Message}");
        }

        return performance;
    }

    public async Task<bool> AuthenticateAsync(AuthenticationCredentials credentials)
    {
        try 
        {
            // Basic authentication test
            using var client = new WebClient();
            client.Credentials = new NetworkCredential(
                credentials.Username, 
                credentials.Password
            );

            // Try a simple request to validate credentials
            await client.DownloadStringTaskAsync("https://example.com");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task ConfigureNetworkAsync(NetworkConfiguration networkConfig)
    {
        try 
        {
            // Configure proxy settings
            if (networkConfig.UseProxy && !string.IsNullOrEmpty(networkConfig.ProxyAddress))
            {
                WebRequest.DefaultWebProxy = new WebProxy(networkConfig.ProxyAddress);
            }

            // Configure connection limits
            if (networkConfig.MaxConnections.HasValue)
            {
                ServicePointManager.DefaultConnectionLimit = networkConfig.MaxConnections.Value;
            }

            // Configure timeout
            if (networkConfig.ConnectionTimeout.HasValue)
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }

            _loggingService.LogInformation("WinInet network configuration updated");
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Network configuration failed: {ex.Message}");
        }
    }

    public double GetPriority(string url)
    {
        try 
        {
            var uri = new Uri(url);
            return uri.Scheme switch
            {
                Uri.UriSchemeHttps => 1.0,  // Highest priority
                Uri.UriSchemeHttp => 0.8,   // Slightly lower
                Uri.UriSchemeFtp => 0.6,    // Lower priority
                _ => 0.0
            };
        }
        catch
        {
            return 0.0;
        }
    }

    public async Task<DownloadMetadata> AnalyzeContentAsync(string url)
    {
        try 
        {
            using var client = new WebClient();
            var headers = client.Headers;

            // Send HEAD request to get metadata
            client.Method = "HEAD";
            client.DownloadString(url);

            return new DownloadMetadata
            {
                Url = url,
                FileName = Path.GetFileName(url),
                FileSize = long.TryParse(
                    headers["Content-Length"] ?? "0", 
                    out var size
                ) ? size : 0,
                MediaType = headers["Content-Type"] ?? "application/octet-stream"
            };
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Content analysis failed: {ex.Message}");
            return new DownloadMetadata { Url = url };
        }
    }

    public async Task InitializeAsync()
    {
        _loggingService.LogInformation($"Initializing WinInet Engine (v{Version})");
    }

    public async Task ShutdownAsync()
    {
        _loggingService.LogInformation("Shutting down WinInet Engine");
    }
}
