using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using MAria2.Core.Interfaces;
using MAria2.Core.Entities;

namespace MAria2.Infrastructure.Services;

public class DependencyVerificationService
{
    private readonly ILoggingService _loggingService;
    private readonly string _dependencyConfigPath;
    private readonly string _libraryBasePath;

    public DependencyVerificationService(
        ILoggingService loggingService,
        string libraryBasePath = null)
    {
        _loggingService = loggingService;
        _libraryBasePath = libraryBasePath ?? 
            Path.Combine(AppContext.BaseDirectory, "lib");
        _dependencyConfigPath = Path.Combine(_libraryBasePath, "dependencies.json");
    }

    public async Task<DependencyVerificationResult> VerifyDependenciesAsync()
    {
        var result = new DependencyVerificationResult();

        try 
        {
            // Load dependency configuration
            var dependencies = await LoadDependencyConfigAsync();

            // Verify each dependency
            foreach (var dependency in dependencies)
            {
                var verificationTask = VerifySingleDependencyAsync(dependency);
                result.DependencyResults.Add(
                    dependency.Name, 
                    await verificationTask
                );
            }

            // Calculate overall verification status
            result.OverallStatus = result.DependencyResults.Values.All(r => r.IsValid)
                ? DependencyVerificationStatus.Passed
                : DependencyVerificationStatus.Failed;

            return result;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Dependency verification failed: {ex.Message}");
            result.OverallStatus = DependencyVerificationStatus.Error;
            return result;
        }
    }

    private async Task<List<DependencyConfig>> LoadDependencyConfigAsync()
    {
        if (!File.Exists(_dependencyConfigPath))
        {
            return CreateDefaultDependencyConfig();
        }

        try 
        {
            var jsonContent = await File.ReadAllTextAsync(_dependencyConfigPath);
            return JsonSerializer.Deserialize<List<DependencyConfig>>(jsonContent) 
                   ?? CreateDefaultDependencyConfig();
        }
        catch 
        {
            return CreateDefaultDependencyConfig();
        }
    }

    private async Task<DependencyVerificationInfo> VerifySingleDependencyAsync(DependencyConfig dependency)
    {
        var result = new DependencyVerificationInfo 
        { 
            Name = dependency.Name 
        };

        try 
        {
            // Check file existence
            var libraryPath = Path.Combine(_libraryBasePath, dependency.RelativePath);
            if (!File.Exists(libraryPath))
            {
                result.IsValid = false;
                result.ErrorMessage = "Library file not found";
                return result;
            }

            // Verify file hash
            if (!string.IsNullOrEmpty(dependency.ExpectedHash))
            {
                var fileHash = await CalculateFileHashAsync(libraryPath);
                result.IsValid = fileHash == dependency.ExpectedHash;
                
                if (!result.IsValid)
                {
                    result.ErrorMessage = "File hash mismatch";
                }
            }

            // Verify library compatibility
            result.IsValid &= VerifyLibraryCompatibility(libraryPath, dependency);

            // Additional runtime checks
            result.IsValid &= CheckRuntimeDependencies(dependency);

            return result;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ErrorMessage = $"Verification error: {ex.Message}";
            return result;
        }
    }

    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private bool VerifyLibraryCompatibility(string libraryPath, DependencyConfig dependency)
    {
        try 
        {
            // Check architecture compatibility
            var architecture = RuntimeInformation.ProcessArchitecture;
            var expectedArchitecture = dependency.SupportedArchitectures;

            if (expectedArchitecture != null && 
                !expectedArchitecture.Contains(architecture.ToString()))
            {
                _loggingService.LogWarning(
                    $"Architecture mismatch for {dependency.Name}: " +
                    $"Expected {string.Join(',', expectedArchitecture)}, " +
                    $"Got {architecture}"
                );
                return false;
            }

            // For native libraries, perform additional checks
            if (Path.GetExtension(libraryPath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return VerifyNativeLibrary(libraryPath);
            }

            return true;
        }
        catch 
        {
            return false;
        }
    }

    private bool VerifyNativeLibrary(string libraryPath)
    {
        try 
        {
            // Attempt to load the library
            var moduleHandle = NativeMethods.LoadLibrary(libraryPath);
            
            if (moduleHandle == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                _loggingService.LogWarning(
                    $"Failed to load library {libraryPath}. Error code: {errorCode}"
                );
                return false;
            }

            // Unload the library
            NativeMethods.FreeLibrary(moduleHandle);
            return true;
        }
        catch (Exception ex)
        {
            _loggingService.LogError(
                $"Native library verification failed: {ex.Message}"
            );
            return false;
        }
    }

    private bool CheckRuntimeDependencies(DependencyConfig dependency)
    {
        // Check for required runtime versions or other dependencies
        if (dependency.RequiredRuntimeVersion != null)
        {
            var currentVersion = Environment.Version;
            return currentVersion >= new Version(dependency.RequiredRuntimeVersion);
        }
        return true;
    }

    private List<DependencyConfig> CreateDefaultDependencyConfig()
    {
        return new List<DependencyConfig>
        {
            new DependencyConfig
            {
                Name = "Aria2",
                RelativePath = "aria2/aria2c.exe",
                ExpectedHash = null, // Add expected hash
                SupportedArchitectures = new[] { "X64" },
                RequiredRuntimeVersion = "6.0.0"
            },
            new DependencyConfig
            {
                Name = "YtDlp",
                RelativePath = "yt-dlp/yt-dlp.exe",
                ExpectedHash = null, // Add expected hash
                SupportedArchitectures = new[] { "X64" },
                RequiredRuntimeVersion = "6.0.0"
            }
            // Add more default dependencies
        };
    }

    // Save updated dependency configuration
    public async Task SaveDependencyConfigAsync(List<DependencyConfig> dependencies)
    {
        var jsonContent = JsonSerializer.Serialize(
            dependencies, 
            new JsonSerializerOptions { WriteIndented = true }
        );
        
        await File.WriteAllTextAsync(_dependencyConfigPath, jsonContent);
    }

    // Native method imports for library loading
    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hModule);
    }

    // Dependency configuration model
    public class DependencyConfig
    {
        public string Name { get; set; }
        public string RelativePath { get; set; }
        public string ExpectedHash { get; set; }
        public string[] SupportedArchitectures { get; set; }
        public string RequiredRuntimeVersion { get; set; }
    }

    // Verification result models
    public class DependencyVerificationResult
    {
        public DependencyVerificationStatus OverallStatus { get; set; }
        public Dictionary<string, DependencyVerificationInfo> DependencyResults { get; set; } 
            = new Dictionary<string, DependencyVerificationInfo>();
    }

    public class DependencyVerificationInfo
    {
        public string Name { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }

    public enum DependencyVerificationStatus
    {
        Passed,
        Failed,
        Error
    }
}
