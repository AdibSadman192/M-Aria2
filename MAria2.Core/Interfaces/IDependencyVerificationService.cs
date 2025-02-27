using System.Threading.Tasks;

namespace MAria2.Core.Interfaces;

public interface IDependencyVerificationService
{
    /// <summary>
    /// Verifies all registered dependencies for compatibility and integrity
    /// </summary>
    /// <returns>Comprehensive dependency verification result</returns>
    Task<DependencyVerificationResult> VerifyDependenciesAsync();

    /// <summary>
    /// Saves the current dependency configuration
    /// </summary>
    /// <param name="dependencies">List of dependencies to save</param>
    Task SaveDependencyConfigAsync(List<DependencyConfig> dependencies);
}

/// <summary>
/// Configuration for a single dependency
/// </summary>
public class DependencyConfig
{
    public string Name { get; set; }
    public string RelativePath { get; set; }
    public string ExpectedHash { get; set; }
    public string[] SupportedArchitectures { get; set; }
    public string RequiredRuntimeVersion { get; set; }
}

/// <summary>
/// Represents the result of a dependency verification process
/// </summary>
public class DependencyVerificationResult
{
    public DependencyVerificationStatus OverallStatus { get; set; }
    public Dictionary<string, DependencyVerificationInfo> DependencyResults { get; set; } 
        = new Dictionary<string, DependencyVerificationInfo>();
}

/// <summary>
/// Detailed information about a single dependency's verification
/// </summary>
public class DependencyVerificationInfo
{
    public string Name { get; set; }
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }
}

/// <summary>
/// Represents the overall status of dependency verification
/// </summary>
public enum DependencyVerificationStatus
{
    Passed,
    Failed,
    Error
}
