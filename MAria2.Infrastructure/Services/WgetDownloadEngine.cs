using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using MAria2.Core.Models;
using Microsoft.Extensions.Logging;

namespace MAria2.Infrastructure.Services
{
    public class WgetDownloadEngine : IWgetDownloadEngine
    {
        private readonly ILogger<WgetDownloadEngine> _logger;
        private readonly IPlatformAbstractionService _platformService;
        private readonly IErrorRecoveryService _errorRecoveryService;

        public WgetDownloadOptions WgetOptions { get; set; } = new WgetDownloadOptions();
        public DownloadEngineMetadata EngineMetadata { get; } = new DownloadEngineMetadata
        {
            EngineName = "Wget",
            Version = "1.0.0",
            Description = "Robust wget-based download engine"
        };

        public WgetDownloadEngine(
            ILogger<WgetDownloadEngine> logger,
            IPlatformAbstractionService platformService,
            IErrorRecoveryService errorRecoveryService)
        {
            _logger = logger;
            _platformService = platformService;
            _errorRecoveryService = errorRecoveryService;
        }

        public async Task<bool> IsWgetInstalledAsync()
        {
            try 
            {
                var result = await ExecuteWgetCommandAsync("--version");
                return result.Success;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetWgetVersionAsync()
        {
            try 
            {
                var result = await ExecuteWgetCommandAsync("--version");
                return result.Success ? ExtractWgetVersion(result.Output) : "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        public async Task<DownloadResult> DownloadFileAsync(
            string url, 
            string destinationPath, 
            CancellationToken cancellationToken = default)
        {
            try 
            {
                if (!await IsWgetInstalledAsync())
                {
                    throw new InvalidOperationException("Wget is not installed.");
                }

                var commandArgs = BuildWgetDownloadCommand(url, destinationPath);
                var result = await ExecuteWgetCommandAsync(commandArgs, cancellationToken);

                return new DownloadResult
                {
                    Success = result.Success,
                    FilePath = destinationPath,
                    ErrorMessage = result.Success ? null : result.Error,
                    BytesDownloaded = result.Success ? GetFileSize(destinationPath) : 0
                };
            }
            catch (Exception ex)
            {
                await LogDownloadErrorAsync(ex, url);
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private string BuildWgetDownloadCommand(string url, string destinationPath)
        {
            var commandBuilder = new StringBuilder($"-O \"{destinationPath}\" \"{url}\"");

            // Recursive download
            if (WgetOptions.Recursive)
            {
                commandBuilder.Append($" -r -l {WgetOptions.MaxDepth}");
            }

            // Mirror site
            if (WgetOptions.MirrorSite)
            {
                commandBuilder.Append(" -m");
            }

            // Convert links
            if (WgetOptions.ConvertLinks)
            {
                commandBuilder.Append(" -k");
            }

            // Timeout and retry
            commandBuilder.Append($" -T {WgetOptions.Timeout} -t {WgetOptions.Tries}");

            // Continue download
            if (WgetOptions.ContinueDownload)
            {
                commandBuilder.Append(" -c");
            }

            // Authentication
            if (!string.IsNullOrEmpty(WgetOptions.Username))
            {
                commandBuilder.Append($" --user={WgetOptions.Username}");
            }
            if (!string.IsNullOrEmpty(WgetOptions.Password))
            {
                commandBuilder.Append($" --password={WgetOptions.Password}");
            }

            // Bandwidth limiting
            if (WgetOptions.Throttle && WgetOptions.MaxDownloadSpeed > 0)
            {
                commandBuilder.Append($" --limit-rate={WgetOptions.MaxDownloadSpeed}");
            }

            // Robots.txt
            if (WgetOptions.IgnoreRobotsTxt)
            {
                commandBuilder.Append(" --execute robots=off");
            }

            // User Agent
            if (!string.IsNullOrEmpty(WgetOptions.UserAgent))
            {
                commandBuilder.Append($" -U \"{WgetOptions.UserAgent}\"");
            }

            // Proxy Configuration
            if (!string.IsNullOrEmpty(WgetOptions.ProxyServer))
            {
                var proxyUrl = $"{WgetOptions.ProxyServer}:{WgetOptions.ProxyPort}";
                commandBuilder.Append($" --proxy={proxyUrl}");

                if (!string.IsNullOrEmpty(WgetOptions.ProxyUsername))
                {
                    commandBuilder.Append($" --proxy-user={WgetOptions.ProxyUsername}");
                }
                if (!string.IsNullOrEmpty(WgetOptions.ProxyPassword))
                {
                    commandBuilder.Append($" --proxy-password={WgetOptions.ProxyPassword}");
                }
            }

            return commandBuilder.ToString();
        }

        private async Task<(bool Success, string Output, string Error)> ExecuteWgetCommandAsync(
            string arguments, 
            CancellationToken cancellationToken = default)
        {
            try 
            {
                var wgetPath = await GetWgetExecutablePath();
                var startInfo = new ProcessStartInfo
                {
                    FileName = wgetPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(
                    outputTask, 
                    errorTask, 
                    process.WaitForExitAsync(cancellationToken)
                );

                return (
                    process.ExitCode == 0, 
                    await outputTask, 
                    await errorTask
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Wget command execution failed: {ex.Message}");
                return (false, null, ex.Message);
            }
        }

        private async Task<string> GetWgetExecutablePath()
        {
            // Platform-specific wget path detection
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Check common Windows locations or use system PATH
                var possiblePaths = new[]
                {
                    @"C:\Program Files\Wget\wget.exe",
                    @"C:\Program Files (x86)\Wget\wget.exe"
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path)) return path;
                }

                // Fallback to system PATH
                return "wget.exe";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "wget";
            }

            throw new PlatformNotSupportedException("Wget is not supported on this platform");
        }

        private string ExtractWgetVersion(string versionOutput)
        {
            // Extract version from typical wget version output
            var versionLines = versionOutput.Split('\n');
            foreach (var line in versionLines)
            {
                if (line.Contains("GNU Wget") && line.Contains("version"))
                {
                    return line.Split("version")[1].Trim().Split(' ')[0];
                }
            }
            return "Unknown";
        }

        private long GetFileSize(string filePath)
        {
            try 
            {
                return new FileInfo(filePath).Length;
            }
            catch
            {
                return 0;
            }
        }

        private async Task LogDownloadErrorAsync(Exception ex, string url)
        {
            await _errorRecoveryService.LogErrorAsync(new ErrorDetails
            {
                ErrorCategory = nameof(ErrorCategory.DownloadFailure),
                ErrorMessage = $"Wget download failed for {url}: {ex.Message}",
                SourceComponent = nameof(WgetDownloadEngine),
                Severity = ErrorSeverity.Error,
                StackTrace = ex.StackTrace
            });
        }
    }
}
