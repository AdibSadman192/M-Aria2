using MAria2.Core.Entities;
using MAria2.Core.Services;

namespace MAria2.Core.Interfaces;

public interface IDownloadVerificationService
{
    /// <summary>
    /// Verifies the integrity of a downloaded file
    /// </summary>
    /// <param name="download">The download to verify</param>
    /// <returns>Verification result with detailed status</returns>
    Task<DownloadVerificationResult> VerifyDownloadAsync(Download download);

    /// <summary>
    /// Attempts to repair a failed or incomplete download
    /// </summary>
    /// <param name="download">The download to repair</param>
    /// <returns>True if repair was successful, false otherwise</returns>
    Task<bool> RepairDownloadAsync(Download download);
}
