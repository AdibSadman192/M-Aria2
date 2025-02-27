using System.Diagnostics;
using System.Text.Json;
using MAria2.Core.Entities;
using MAria2.Core.Enums;
using MAria2.Core.Interfaces;
using MAria2.Core.Configuration;

namespace MAria2.Infrastructure.Engines;

public class YtDlpDownloadEngine : IDownloadEngine
{
    private readonly ILoggingService _loggingService;
    private readonly ConfigurationManager _configurationManager;
    private readonly string _ytDlpPath;

    public YtDlpDownloadEngine(
        ILoggingService loggingService, 
        ConfigurationManager configurationManager)
    {
        _loggingService = loggingService;
        _configurationManager = configurationManager;
        _ytDlpPath = FindYtDlpExecutable();
    }

    public EngineType Type => EngineType.YtDlp;

    public string Version => GetYtDlpVersion();

    private string FindYtDlpExecutable()
    {
        // Search common locations and configuration paths
        string[] possiblePaths = 
        {
            Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".yt-dlp", "yt-dlp.exe"),
            "yt-dlp.exe"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException("yt-dlp executable not found. Please install yt-dlp.");
    }

    private string GetYtDlpVersion()
    {
        try 
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string version = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return version;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to get yt-dlp version: {ex.Message}");
            return "unknown";
        }
    }

    public bool CanHandleProtocol(string url)
    {
        // Supports various media platforms
        string[] supportedPlatforms = 
        {
            "youtube.com", 
            "youtu.be", 
            "vimeo.com", 
            "dailymotion.com", 
            "soundcloud.com", 
            "twitch.tv",
            "instagram.com",
            "twitter.com"
        };

        return supportedPlatforms.Any(platform => 
            url.Contains(platform, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Download> StartDownloadAsync(Download download)
    {
        try 
        {
            var config = _configurationManager.GetEngineConfiguration(EngineType.YtDlp);
            
            var arguments = BuildDownloadArguments(download, config);
            
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var outputBuilder = new List<string>();
            process.OutputDataReceived += (sender, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.Add(e.Data);
                    _loggingService.LogInformation($"YT-DLP Output: {e.Data}");
                }
            };

            process.ErrorDataReceived += (sender, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _loggingService.LogError($"YT-DLP Error: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            // Parse download result
            download.Status = process.ExitCode == 0 
                ? DownloadStatus.Completed 
                : DownloadStatus.Failed;

            download.EngineSpecificId = Guid.NewGuid().ToString();
            download.DownloadLog = string.Join(Environment.NewLine, outputBuilder);

            return download;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"YT-DLP download failed: {ex.Message}");
            download.Status = DownloadStatus.Failed;
            return download;
        }
    }

    private string BuildDownloadArguments(Download download, EngineConfiguration config)
    {
        var args = new List<string>
        {
            // Basic download configuration
            "--no-color",
            "--progress",
            "--print", "after_move:filename",
            
            // Output template with destination path
            $"-o \"{download.DestinationPath}\"",
            
            // Quality and format selection
            config.Quality ?? "--best-quality",
            
            // Network configuration
            $"--max-downloads {config.MaxConnections ?? 1}",
            $"--retries {config.MaxRetries ?? 3}",
            
            // Additional flags
            "--no-part",  // Disable .part file usage
            "--no-mtime", // Don't set file modification time
        };

        // Add authentication if provided
        if (download.Credentials != null)
        {
            args.Add($"--username {download.Credentials.Username}");
            args.Add($"--password {download.Credentials.Password}");
        }

        // Add the URL
        args.Add(download.Url);

        return string.Join(" ", args);
    }

    public async Task<DownloadSegment> DownloadSegmentAsync(
        Download parentDownload, 
        DownloadSegment segment)
    {
        // YT-DLP doesn't support segment downloads natively
        throw new NotSupportedException(
            "YT-DLP does not support segmented downloads.");
    }

    public async Task<DownloadPerformance> TestPerformanceAsync(string url)
    {
        try 
        {
            var startTime = DateTime.UtcNow;
            
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = $"--get-filename --get-duration {url}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var endTime = DateTime.UtcNow;
            
            return new DownloadPerformance
            {
                Url = url,
                SpeedMbps = 10.0, // Placeholder, actual speed would require more complex measurement
                ConnectionStability = 0.9,
                SupportedProtocols = new[] { "https" },
                TestDuration = endTime - startTime
            };
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Performance test failed: {ex.Message}");
            return new DownloadPerformance { Url = url };
        }
    }

    public async Task<bool> AuthenticateAsync(AuthenticationCredentials credentials)
    {
        // YT-DLP authentication test
        try 
        {
            var arguments = new List<string>
            {
                "--username", credentials.Username,
                "--password", credentials.Password,
                "--test"
            };

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = string.Join(" ", arguments),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Authentication failed: {ex.Message}");
            return false;
        }
    }

    public async Task ConfigureNetworkAsync(NetworkConfiguration networkConfig)
    {
        // Configure network settings for YT-DLP
        _loggingService.LogInformation(
            $"Configuring network for YT-DLP: Proxy={networkConfig.ProxyAddress}");
    }

    public double GetPriority(string url)
    {
        // Prioritize media platforms
        if (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)) return 1.0;
        if (url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)) return 0.9;
        if (url.Contains("vimeo.com", StringComparison.OrdinalIgnoreCase)) return 0.8;
        
        return 0.5;
    }

    public async Task<DownloadMetadata> AnalyzeContentAsync(string url)
    {
        try 
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = $"--dump-json {url}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var jsonOutput = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Parse JSON metadata
            using var jsonDocument = JsonDocument.Parse(jsonOutput);
            var root = jsonDocument.RootElement;

            return new DownloadMetadata
            {
                Url = url,
                FileName = root.GetProperty("title").GetString(),
                FileSize = root.TryGetProperty("filesize", out var fileSizeElement) 
                    ? fileSizeElement.GetInt64() 
                    : 0,
                MediaType = root.GetProperty("ext").GetString(),
                AdditionalMetadata = jsonOutput
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
        // Perform any necessary initialization
        _loggingService.LogInformation($"Initializing YT-DLP Engine (v{Version})");
    }

    public async Task ShutdownAsync()
    {
        // Perform cleanup if needed
        _loggingService.LogInformation("Shutting down YT-DLP Engine");
    }
}
