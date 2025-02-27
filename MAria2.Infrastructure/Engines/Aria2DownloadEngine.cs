using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using MAria2.Core.Entities;
using MAria2.Core.Enums;
using MAria2.Core.Interfaces;
using MAria2.Core.Configuration;

namespace MAria2.Infrastructure.Engines;

public class Aria2DownloadEngine : IDownloadEngine, IEngineCapabilityProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILoggingService _loggingService;
    private readonly ConfigurationManager _configManager;

    // Aria2 RPC configuration
    private string _rpcUrl = "http://localhost:6800/jsonrpc";
    private string _secretToken;

    public EngineType Type => EngineType.Aria2;
    public string Version => "1.36.0";

    public Aria2DownloadEngine(
        ILoggingService loggingService, 
        ConfigurationManager configManager)
    {
        _httpClient = new HttpClient();
        _loggingService = loggingService;
        _configManager = configManager;

        // Load Aria2-specific configuration
        var engineConfig = _configManager.GetEngineConfiguration(EngineType.Aria2);
        ConfigureEngineAsync(engineConfig).Wait();
    }

    public async Task InitializeAsync()
    {
        try 
        {
            // Start Aria2 daemon if not running
            await StartAria2DaemonAsync();
            _loggingService.LogInformation("Aria2 engine initialized successfully");
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Aria2 initialization failed: {ex.Message}");
            throw;
        }
    }

    private async Task StartAria2DaemonAsync()
    {
        // Implement Aria2 daemon startup logic
        var startInfo = new ProcessStartInfo
        {
            FileName = "aria2c",
            Arguments = $"--enable-rpc --rpc-listen-all=true --rpc-secret={_secretToken}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        
        // Wait for RPC to be available
        await Task.Delay(2000);
    }

    public async Task<Download> StartDownloadAsync(Download download)
    {
        try 
        {
            var rpcRequest = new Aria2RpcRequest
            {
                Method = "aria2.addUri",
                Params = new object[] 
                { 
                    _secretToken, 
                    new[] { download.Url }, 
                    new Dictionary<string, object>
                    {
                        { "dir", download.DestinationPath },
                        { "out", download.FileName }
                    }
                }
            };

            var response = await SendRpcRequestAsync<string>(rpcRequest);
            download.EngineSpecificId = response;
            download.Status = DownloadStatus.Downloading;

            return download;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Aria2 download start failed: {ex.Message}");
            download.Status = DownloadStatus.Failed;
            throw;
        }
    }

    public async Task PauseDownloadAsync(Download download)
    {
        var rpcRequest = new Aria2RpcRequest
        {
            Method = "aria2.pause",
            Params = new object[] { _secretToken, download.EngineSpecificId }
        };

        await SendRpcRequestAsync<object>(rpcRequest);
        download.Status = DownloadStatus.Paused;
    }

    public async Task ResumeDownloadAsync(Download download)
    {
        var rpcRequest = new Aria2RpcRequest
        {
            Method = "aria2.unpause",
            Params = new object[] { _secretToken, download.EngineSpecificId }
        };

        await SendRpcRequestAsync<object>(rpcRequest);
        download.Status = DownloadStatus.Downloading;
    }

    public async Task CancelDownloadAsync(Download download)
    {
        var rpcRequest = new Aria2RpcRequest
        {
            Method = "aria2.remove",
            Params = new object[] { _secretToken, download.EngineSpecificId }
        };

        await SendRpcRequestAsync<object>(rpcRequest);
        download.Status = DownloadStatus.Cancelled;
    }

    public async Task<DownloadProgress> GetProgressAsync(Download download)
    {
        var rpcRequest = new Aria2RpcRequest
        {
            Method = "aria2.tellStatus",
            Params = new object[] { _secretToken, download.EngineSpecificId }
        };

        var status = await SendRpcRequestAsync<Aria2DownloadStatus>(rpcRequest);

        return new DownloadProgress
        {
            BytesDownloaded = long.Parse(status.DownloadedBytes),
            TotalBytes = long.Parse(status.TotalBytes),
            Progress = CalculateProgress(status),
            DownloadSpeed = ParseSpeed(status.DownloadSpeed),
            EstimatedTimeRemaining = CalculateTimeRemaining(status)
        };
    }

    public bool CanHandleProtocol(string url)
    {
        var uri = new Uri(url);
        return uri.Scheme switch
        {
            "http" or "https" or "ftp" or "sftp" => true,
            _ => false
        };
    }

    public bool SupportsContentType(string contentType)
    {
        // Aria2 supports most content types
        return true;
    }

    public async Task<PerformanceTestResult> TestPerformanceAsync(string url)
    {
        var stopwatch = Stopwatch.StartNew();

        try 
        {
            var rpcRequest = new Aria2RpcRequest
            {
                Method = "aria2.getVersion",
                Params = new object[] { _secretToken }
            };

            await SendRpcRequestAsync<object>(rpcRequest);
            stopwatch.Stop();

            return new PerformanceTestResult(
                SpeedMbps: 100.0, // Placeholder
                ConnectionStability: 0.95,
                SupportedProtocols: new[] { "http", "https", "ftp", "sftp" },
                AverageLatency: stopwatch.Elapsed,
                SuccessRate: 100
            );
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Performance test failed: {ex.Message}");
            throw;
        }
    }

    public async Task<Download> SplitDownloadAsync(Download download, int segments)
    {
        var rpcRequest = new Aria2RpcRequest
        {
            Method = "aria2.addUri",
            Params = new object[] 
            { 
                _secretToken, 
                new[] { download.Url }, 
                new Dictionary<string, object>
                {
                    { "split", segments },
                    { "max-concurrent-downloads", segments }
                }
            }
        };

        var response = await SendRpcRequestAsync<string>(rpcRequest);
        download.EngineSpecificId = response;
        return download;
    }

    public async Task<Download> MergeDownloadSegmentsAsync(Download download)
    {
        // Aria2 handles segment merging automatically
        return download;
    }

    public async Task ConfigureNetworkAsync(NetworkConfiguration networkConfig)
    {
        var rpcRequest = new Aria2RpcRequest
        {
            Method = "aria2.changeGlobalOption",
            Params = new object[] 
            { 
                _secretToken,
                new Dictionary<string, object>
                {
                    { "max-concurrent-downloads", networkConfig.MaxConnections },
                    { "connect-timeout", networkConfig.ConnectionTimeout },
                    { "proxy", networkConfig.UseProxy ? networkConfig.ProxyAddress : "" }
                }
            }
        };

        await SendRpcRequestAsync<object>(rpcRequest);
    }

    public async Task<bool> AuthenticateAsync(AuthenticationCredentials credentials)
    {
        // Aria2 supports various authentication methods
        if (!string.IsNullOrEmpty(credentials.Token))
        {
            _secretToken = credentials.Token;
            return true;
        }

        return false;
    }

    public async Task<ContentMetadata> AnalyzeContentAsync(string url)
    {
        var rpcRequest = new Aria2RpcRequest
        {
            Method = "aria2.getOption",
            Params = new object[] { _secretToken, url }
        };

        // Placeholder implementation
        return new ContentMetadata(
            Url: url,
            FileSize: 0,
            ContentType: "application/octet-stream",
            FileName: Path.GetFileName(url),
            LastModified: DateTime.UtcNow
        );
    }

    public async Task ConfigureEngineAsync(EngineConfiguration config)
    {
        _secretToken = config.EngineSpecificSettings.TryGetValue("SecretToken", out var token) 
            ? token 
            : Guid.NewGuid().ToString();

        // Additional Aria2-specific configuration
        var rpcRequest = new Aria2RpcRequest
        {
            Method = "aria2.changeGlobalOption",
            Params = new object[] 
            { 
                _secretToken,
                new Dictionary<string, object>
                {
                    { "max-concurrent-downloads", config.MaxConnections },
                    { "connect-timeout", config.Timeout }
                }
            }
        };

        await SendRpcRequestAsync<object>(rpcRequest);
    }

    public async Task ShutdownAsync()
    {
        var rpcRequest = new Aria2RpcRequest
        {
            Method = "aria2.shutdown",
            Params = new object[] { _secretToken }
        };

        await SendRpcRequestAsync<object>(rpcRequest);
    }

    private async Task<T> SendRpcRequestAsync<T>(Aria2RpcRequest request)
    {
        var jsonRequest = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_rpcUrl, content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var rpcResponse = JsonSerializer.Deserialize<Aria2RpcResponse<T>>(responseContent);

        if (rpcResponse.Error != null)
        {
            throw new Exception($"RPC Error: {rpcResponse.Error.Message}");
        }

        return rpcResponse.Result;
    }

    // Utility methods
    private double CalculateProgress(Aria2DownloadStatus status)
    {
        long total = long.Parse(status.TotalBytes);
        long downloaded = long.Parse(status.DownloadedBytes);
        return total > 0 ? (double)downloaded / total * 100 : 0;
    }

    private double ParseSpeed(string speedString)
    {
        // Convert Aria2 speed string to Mbps
        return double.TryParse(speedString, out var speed) 
            ? speed / 1_000_000 
            : 0;
    }

    private TimeSpan CalculateTimeRemaining(Aria2DownloadStatus status)
    {
        long total = long.Parse(status.TotalBytes);
        long downloaded = long.Parse(status.DownloadedBytes);
        double speed = ParseSpeed(status.DownloadSpeed);

        return speed > 0 
            ? TimeSpan.FromSeconds((total - downloaded) / speed) 
            : TimeSpan.Zero;
    }

    // Engine capability provider implementation
    public int GetPriority(string url)
    {
        // Higher priority for HTTP/HTTPS downloads
        var uri = new Uri(url);
        return uri.Scheme switch
        {
            "https" => 100,
            "http" => 90,
            "ftp" => 70,
            "sftp" => 60,
            _ => 0
        };
    }

    public bool CanPartiallyResume(Download download)
    {
        // Aria2 supports partial resume for most downloads
        return true;
    }
}

// Aria2 RPC request model
internal class Aria2RpcRequest
{
    public string JsonRpc { get; set; } = "2.0";
    public string Method { get; set; }
    public object[] Params { get; set; }
    public string Id { get; set; } = Guid.NewGuid().ToString();
}

// Aria2 RPC response model
internal class Aria2RpcResponse<T>
{
    public T Result { get; set; }
    public Aria2RpcError Error { get; set; }
    public string JsonRpc { get; set; }
    public string Id { get; set; }
}

// Aria2 RPC error model
internal class Aria2RpcError
{
    public int Code { get; set; }
    public string Message { get; set; }
}

// Aria2 download status model
internal class Aria2DownloadStatus
{
    public string Gid { get; set; }
    public string Status { get; set; }
    public string TotalLength { get; set; }
    public string CompletedLength { get; set; }
    public string DownloadSpeed { get; set; }
    public string TotalBytes { get; set; }
    public string DownloadedBytes { get; set; }
}
