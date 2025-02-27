using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MAria2.Core.Interfaces
{
    public enum OperatingSystemType
    {
        Windows,
        MacOS,
        Linux,
        Unknown
    }

    public enum ArchitectureType
    {
        X86,
        X64,
        Arm,
        Arm64,
        Unknown
    }

    public interface IPlatformAbstractionService
    {
        /// <summary>
        /// Detects the current operating system
        /// </summary>
        OperatingSystemType GetOperatingSystem();

        /// <summary>
        /// Detects the current system architecture
        /// </summary>
        ArchitectureType GetSystemArchitecture();

        /// <summary>
        /// Checks if the current platform is supported
        /// </summary>
        bool IsPlatformSupported();

        /// <summary>
        /// Gets platform-specific download directory
        /// </summary>
        string GetDefaultDownloadDirectory();

        /// <summary>
        /// Checks if specific system dependencies are installed
        /// </summary>
        Task<bool> CheckSystemDependenciesAsync(string[] requiredDependencies);

        /// <summary>
        /// Installs missing system dependencies
        /// </summary>
        Task<bool> InstallSystemDependenciesAsync(string[] dependencies);

        /// <summary>
        /// Gets platform-specific file path separators
        /// </summary>
        char GetPathSeparator();

        /// <summary>
        /// Checks available disk space
        /// </summary>
        Task<long> GetAvailableDiskSpaceAsync(string path);

        /// <summary>
        /// Performs platform-specific optimizations
        /// </summary>
        Task OptimizePlatformPerformanceAsync();
    }

    public class PlatformInfo
    {
        public OperatingSystemType OperatingSystem { get; set; }
        public ArchitectureType Architecture { get; set; }
        public string OSVersion { get; set; }
        public bool Is64BitOperatingSystem { get; set; }
        public string ProcessorName { get; set; }
    }
}
