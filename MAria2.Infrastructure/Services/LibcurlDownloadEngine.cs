using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using MAria2.Core.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;
using System.Diagnostics;
using System.Text;

namespace MAria2.Infrastructure.Services
{
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public class LibcurlDownloadEngine : ILibcurlDownloadEngine
    {
        private readonly ILogger<LibcurlDownloadEngine> _logger;
        private readonly IPlatformAbstractionService _platformService;
        private readonly IErrorRecoveryService _errorRecoveryService;

        public LibcurlDownloadOptions LibcurlOptions { get; set; } = new LibcurlDownloadOptions();
        public DownloadEngineMetadata EngineMetadata { get; } = new DownloadEngineMetadata
        {
            EngineName = "LibCurl",
            Version = "1.0.0",
            Description = "High-performance libcurl-based download engine"
        };

        public LibcurlDownloadEngine(
            ILogger<LibcurlDownloadEngine> logger,
            IPlatformAbstractionService platformService,
            IErrorRecoveryService errorRecoveryService)
        {
            _logger = logger;
            _platformService = platformService;
            _errorRecoveryService = errorRecoveryService;
        }

        public async Task<bool> IsLibcurlInstalledAsync()
        {
            try 
            {
                var result = await ExecuteCurlCommandAsync("--version");
                return result.Success;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetLibcurlVersionAsync()
        {
            try 
            {
                var result = await ExecuteCurlCommandAsync("--version");
                return result.Success ? ExtractLibcurlVersion(result.Output) : "Unknown";
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
                if (!await IsLibcurlInstalledAsync())
                {
                    throw new InvalidOperationException("Libcurl is not installed.");
                }

                var commandArgs = BuildCurlDownloadCommand(url, destinationPath);
                var result = await ExecuteCurlCommandAsync(commandArgs, cancellationToken);

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

        private string BuildCurlDownloadCommand(string url, string destinationPath)
        {
            var commandBuilder = new StringBuilder($"-L -o \"{destinationPath}\" \"{url}\"");

            // Connection Timeout
            commandBuilder.Append($" --connect-timeout {LibcurlOptions.ConnectTimeout}");

            // Redirects
            if (LibcurlOptions.FollowRedirects)
            {
                commandBuilder.Append($" -L --max-redirs {LibcurlOptions.MaxRedirects}");
            }

            // Authentication
            if (!string.IsNullOrEmpty(LibcurlOptions.Username))
            {
                commandBuilder.Append($" -u {LibcurlOptions.Username}:{LibcurlOptions.Password}");
                
                if (LibcurlOptions.UseDigestAuth)
                {
                    commandBuilder.Append(" --digest");
                }
            }

            // SSL/TLS Options
            if (!LibcurlOptions.VerifySSL)
            {
                commandBuilder.Append(" -k"); // Insecure mode
            }

            if (!string.IsNullOrEmpty(LibcurlOptions.ClientCertPath))
            {
                commandBuilder.Append($" --cert \"{LibcurlOptions.ClientCertPath}\"");
                
                if (!string.IsNullOrEmpty(LibcurlOptions.ClientCertPassword))
                {
                    commandBuilder.Append($":'{LibcurlOptions.ClientCertPassword}'");
                }
            }

            // Proxy Configuration
            if (!string.IsNullOrEmpty(LibcurlOptions.ProxyServer))
            {
                var proxyUrl = $"{LibcurlOptions.ProxyServer}:{LibcurlOptions.ProxyPort}";
                var proxyTypeMap = new Dictionary<ProxyType, string>
                {
                    { ProxyType.HTTP, "http" },
                    { ProxyType.HTTPS, "https" },
                    { ProxyType.SOCKS4, "socks4" },
                    { ProxyType.SOCKS5, "socks5" }
                };

                commandBuilder.Append($" --proxy {proxyTypeMap[LibcurlOptions.ProxyType]}://{proxyUrl}");

                if (!string.IsNullOrEmpty(LibcurlOptions.ProxyUsername))
                {
                    commandBuilder.Append($" --proxy-user {LibcurlOptions.ProxyUsername}:{LibcurlOptions.ProxyPassword}");
                }
            }

            // User Agent
            if (!string.IsNullOrEmpty(LibcurlOptions.UserAgent))
            {
                commandBuilder.Append($" -A \"{LibcurlOptions.UserAgent}\"");
            }

            // Compression
            if (LibcurlOptions.AcceptCompression)
            {
                var encodings = string.Join(",", LibcurlOptions.AcceptedEncodings);
                commandBuilder.Append($" --compressed --accept-encoding \"{encodings}\"");
            }

            // Resume Support
            if (LibcurlOptions.EnableResumeSupport)
            {
                commandBuilder.Append(" -C -"); // Continue partial downloads
            }

            // Speed Limit
            if (LibcurlOptions.MaxDownloadSpeed > 0)
            {
                commandBuilder.Append($" --limit-rate {LibcurlOptions.MaxDownloadSpeed}");
            }

            return commandBuilder.ToString();
        }

        private async Task<(bool Success, string Output, string Error)> ExecuteCurlCommandAsync(
            string arguments, 
            CancellationToken cancellationToken = default)
        {
            try 
            {
                var curlPath = await GetCurlExecutablePath();
                var startInfo = new ProcessStartInfo
                {
                    FileName = curlPath,
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
                _logger.LogError($"Curl command execution failed: {ex.Message}");
                return (false, null, ex.Message);
            }
        }

        private async Task<string> GetCurlExecutablePath()
        {
            // Platform-specific curl path detection
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "curl";
            }

            throw new PlatformNotSupportedException("Libcurl is only supported on Linux and MacOS");
        }

        private string ExtractLibcurlVersion(string versionOutput)
        {
            // Extract version from typical curl version output
            var versionLines = versionOutput.Split('\n');
            foreach (var line in versionLines)
            {
                if (line.Contains("curl") && line.Contains("libcurl"))
                {
                    return line.Split("libcurl")[1].Trim().Split(' ')[0];
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
                ErrorMessage = $"Libcurl download failed for {url}: {ex.Message}",
                SourceComponent = nameof(LibcurlDownloadEngine),
                Severity = ErrorSeverity.Error,
                StackTrace = ex.StackTrace
            });
        }
    }
}
