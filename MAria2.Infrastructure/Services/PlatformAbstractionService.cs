using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MAria2.Infrastructure.Services
{
    public class PlatformAbstractionService : IPlatformAbstractionService
    {
        private readonly ILogger<PlatformAbstractionService> _logger;
        private static readonly Dictionary<string, string> _dependencyInstallers = new()
        {
            { "ffmpeg", new Dictionary<OperatingSystemType, string>
                {
                    { OperatingSystemType.Windows, "winget install ffmpeg" },
                    { OperatingSystemType.MacOS, "brew install ffmpeg" },
                    { OperatingSystemType.Linux, "sudo apt-get install ffmpeg" }
                }
            },
            { "wget", new Dictionary<OperatingSystemType, string>
                {
                    { OperatingSystemType.Windows, "winget install wget" },
                    { OperatingSystemType.MacOS, "brew install wget" },
                    { OperatingSystemType.Linux, "sudo apt-get install wget" }
                }
            }
        };

        public PlatformAbstractionService(ILogger<PlatformAbstractionService> logger)
        {
            _logger = logger;
        }

        public OperatingSystemType GetOperatingSystem()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return OperatingSystemType.Windows;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return OperatingSystemType.MacOS;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return OperatingSystemType.Linux;
            
            return OperatingSystemType.Unknown;
        }

        public ArchitectureType GetSystemArchitecture()
        {
            var arch = RuntimeInformation.OSArchitecture;
            return arch switch
            {
                Architecture.X86 => ArchitectureType.X86,
                Architecture.X64 => ArchitectureType.X64,
                Architecture.Arm => ArchitectureType.Arm,
                Architecture.Arm64 => ArchitectureType.Arm64,
                _ => ArchitectureType.Unknown
            };
        }

        public bool IsPlatformSupported()
        {
            var os = GetOperatingSystem();
            var arch = GetSystemArchitecture();

            return os != OperatingSystemType.Unknown && 
                   arch != ArchitectureType.Unknown &&
                   (os == OperatingSystemType.Windows || 
                    os == OperatingSystemType.MacOS || 
                    os == OperatingSystemType.Linux);
        }

        public string GetDefaultDownloadDirectory()
        {
            return GetOperatingSystem() switch
            {
                OperatingSystemType.Windows => 
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                OperatingSystemType.MacOS => 
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Downloads"),
                OperatingSystemType.Linux => 
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Downloads"),
                _ => throw new PlatformNotSupportedException("Unsupported operating system")
            };
        }

        public async Task<bool> CheckSystemDependenciesAsync(string[] requiredDependencies)
        {
            var os = GetOperatingSystem();
            var missingDependencies = new List<string>();

            foreach (var dependency in requiredDependencies)
            {
                if (!await IsDependencyInstalledAsync(dependency, os))
                {
                    missingDependencies.Add(dependency);
                }
            }

            return missingDependencies.Count == 0;
        }

        public async Task<bool> InstallSystemDependenciesAsync(string[] dependencies)
        {
            var os = GetOperatingSystem();
            bool allInstalled = true;

            foreach (var dependency in dependencies)
            {
                if (!_dependencyInstallers.ContainsKey(dependency) || 
                    !_dependencyInstallers[dependency].ContainsKey(os))
                {
                    _logger.LogWarning($"No installation method found for {dependency} on {os}");
                    allInstalled = false;
                    continue;
                }

                var installCommand = _dependencyInstallers[dependency][os];
                var result = await ExecuteCommandAsync(installCommand);
                
                if (!result.Success)
                {
                    _logger.LogError($"Failed to install {dependency}: {result.ErrorOutput}");
                    allInstalled = false;
                }
            }

            return allInstalled;
        }

        public char GetPathSeparator()
        {
            return Path.DirectorySeparatorChar;
        }

        public async Task<long> GetAvailableDiskSpaceAsync(string path)
        {
            try 
            {
                var drive = Path.GetPathRoot(path);
                var driveInfo = new DriveInfo(drive);
                return driveInfo.AvailableFreeSpace;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting disk space: {ex.Message}");
                return -1;
            }
        }

        public async Task OptimizePlatformPerformanceAsync()
        {
            var os = GetOperatingSystem();
            
            switch (os)
            {
                case OperatingSystemType.Windows:
                    await OptimizeWindowsPerformanceAsync();
                    break;
                case OperatingSystemType.MacOS:
                    await OptimizeMacOSPerformanceAsync();
                    break;
                case OperatingSystemType.Linux:
                    await OptimizeLinuxPerformanceAsync();
                    break;
            }
        }

        private async Task<bool> IsDependencyInstalledAsync(string dependency, OperatingSystemType os)
        {
            var checkCommands = new Dictionary<OperatingSystemType, string>
            {
                { OperatingSystemType.Windows, $"where {dependency}" },
                { OperatingSystemType.MacOS, $"which {dependency}" },
                { OperatingSystemType.Linux, $"which {dependency}" }
            };

            if (!checkCommands.ContainsKey(os))
                return false;

            var result = await ExecuteCommandAsync(checkCommands[os]);
            return result.Success;
        }

        private async Task OptimizeWindowsPerformanceAsync()
        {
            // Windows-specific performance optimizations
            await ExecuteCommandAsync("powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        }

        private async Task OptimizeMacOSPerformanceAsync()
        {
            // MacOS-specific performance optimizations
            await ExecuteCommandAsync("sudo purge");
        }

        private async Task OptimizeLinuxPerformanceAsync()
        {
            // Linux-specific performance optimizations
            await ExecuteCommandAsync("sudo sysctl -w vm.drop_caches=3");
        }

        private async Task<CommandResult> ExecuteCommandAsync(string command)
        {
            try 
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = GetOperatingSystem() == OperatingSystemType.Windows ? "cmd.exe" : "/bin/bash",
                        Arguments = GetOperatingSystem() == OperatingSystemType.Windows ? $"/c {command}" : $"-c \"{command}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                return new CommandResult
                {
                    Success = process.ExitCode == 0,
                    Output = output,
                    ErrorOutput = error
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Command execution error: {ex.Message}");
                return new CommandResult { Success = false, ErrorOutput = ex.Message };
            }
        }

        private record CommandResult
        {
            public bool Success { get; init; }
            public string Output { get; init; }
            public string ErrorOutput { get; init; }
        }
    }
}
