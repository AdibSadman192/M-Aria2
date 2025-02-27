using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace MAria2.Infrastructure.Services
{
    public class SystemIntegrityMonitorService : ISystemIntegrityMonitorService, IDisposable
    {
        private readonly ILogger<SystemIntegrityMonitorService> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ConcurrentDictionary<string, FileIntegrityState> _fileIntegrityStates;
        private readonly List<string> _criticalSystemPaths;
        private readonly List<string> _criticalRegistryKeys;

        public event EventHandler<SystemIntegrityIssueDetectedEventArgs> IntegrityIssueDetected;

        public SystemIntegrityMonitorService(
            ILogger<SystemIntegrityMonitorService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();
            _fileIntegrityStates = new ConcurrentDictionary<string, FileIntegrityState>();

            // Configure critical paths from configuration
            _criticalSystemPaths = configuration
                .GetSection("SystemIntegrity:CriticalPaths")
                .Get<List<string>>() ?? new List<string>
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MAria2")
                };

            _criticalRegistryKeys = configuration
                .GetSection("SystemIntegrity:CriticalRegistryKeys")
                .Get<List<string>>() ?? new List<string>
                {
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\MAria2",
                    @"HKEY_CURRENT_USER\Software\MAria2"
                };
        }

        public async Task StartMonitoringAsync()
        {
            _logger.LogInformation("Starting system integrity monitoring");

            // Initial baseline scan
            await PerformBaselineIntegrityScanAsync();

            // Start continuous monitoring tasks
            var fileMonitorTask = MonitorFileSystemIntegrityAsync(_cancellationTokenSource.Token);
            var registryMonitorTask = MonitorRegistryIntegrityAsync(_cancellationTokenSource.Token);
            var processMonitorTask = MonitorProcessIntegrityAsync(_cancellationTokenSource.Token);

            await Task.WhenAll(fileMonitorTask, registryMonitorTask, processMonitorTask);
        }

        private async Task PerformBaselineIntegrityScanAsync()
        {
            _logger.LogInformation("Performing baseline system integrity scan");

            var tasks = _criticalSystemPaths.Select(async path =>
            {
                try
                {
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var hash = await ComputeFileHashAsync(file);
                        _fileIntegrityStates[file] = new FileIntegrityState
                        {
                            Path = file,
                            BaselineHash = hash,
                            LastChecked = DateTime.UtcNow
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error scanning path {path}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task MonitorFileSystemIntegrityAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var fileState in _fileIntegrityStates)
                {
                    try
                    {
                        var currentHash = await ComputeFileHashAsync(fileState.Key);
                        if (currentHash != fileState.Value.BaselineHash)
                        {
                            RaiseIntegrityIssue(new SystemIntegrityIssue
                            {
                                Type = IntegrityIssueType.FileSystemModification,
                                Description = $"File modified: {fileState.Key}",
                                Severity = IntegritySeverity.High
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error checking file integrity: {ex.Message}");
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            }
        }

        private async Task MonitorRegistryIntegrityAsync(CancellationToken cancellationToken)
        {
            var baselineRegistryState = new ConcurrentDictionary<string, string>();

            // Capture baseline registry state
            foreach (var keyPath in _criticalRegistryKeys)
            {
                try
                {
                    var keyHash = ComputeRegistryKeyHash(keyPath);
                    baselineRegistryState[keyPath] = keyHash;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error capturing registry baseline: {ex.Message}");
                }
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var keyPath in _criticalRegistryKeys)
                {
                    try
                    {
                        var currentHash = ComputeRegistryKeyHash(keyPath);
                        if (currentHash != baselineRegistryState[keyPath])
                        {
                            RaiseIntegrityIssue(new SystemIntegrityIssue
                            {
                                Type = IntegrityIssueType.RegistryChange,
                                Description = $"Registry key modified: {keyPath}",
                                Severity = IntegritySeverity.High
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error checking registry integrity: {ex.Message}");
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
            }
        }

        private async Task MonitorProcessIntegrityAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var suspiciousProcesses = Process.GetProcesses()
                    .Where(IsSuspiciousProcess)
                    .ToList();

                foreach (var process in suspiciousProcesses)
                {
                    RaiseIntegrityIssue(new SystemIntegrityIssue
                    {
                        Type = IntegrityIssueType.UnauthorizedSoftwareInstallation,
                        Description = $"Suspicious process detected: {process.ProcessName}",
                        Severity = IntegritySeverity.Medium
                    });
                }

                await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken);
            }
        }

        private bool IsSuspiciousProcess(Process process)
        {
            // Implement complex process integrity checks
            return false; // Placeholder
        }

        private async Task<string> ComputeFileHashAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToBase64String(hash);
        }

        private string ComputeRegistryKeyHash(string keyPath)
        {
            using var sha256 = SHA256.Create();
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(
                Registry.GetValue(keyPath, "", null)?.ToString() ?? string.Empty
            );
            var hash = sha256.ComputeHash(keyBytes);
            return Convert.ToBase64String(hash);
        }

        private void RaiseIntegrityIssue(SystemIntegrityIssue issue)
        {
            _logger.LogWarning($"Integrity Issue: {issue.Description}");
            IntegrityIssueDetected?.Invoke(this, new SystemIntegrityIssueDetectedEventArgs(issue));
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }
    }

    public class FileIntegrityState
    {
        public string Path { get; set; }
        public string BaselineHash { get; set; }
        public DateTime LastChecked { get; set; }
    }

    public class SystemIntegrityIssue
    {
        public IntegrityIssueType Type { get; set; }
        public string Description { get; set; }
        public IntegritySeverity Severity { get; set; }
    }

    public class SystemIntegrityIssueDetectedEventArgs : EventArgs
    {
        public SystemIntegrityIssue Issue { get; }

        public SystemIntegrityIssueDetectedEventArgs(SystemIntegrityIssue issue)
        {
            Issue = issue;
        }
    }

    public enum IntegritySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public interface ISystemIntegrityMonitorService
    {
        Task StartMonitoringAsync();
        event EventHandler<SystemIntegrityIssueDetectedEventArgs> IntegrityIssueDetected;
    }
}
