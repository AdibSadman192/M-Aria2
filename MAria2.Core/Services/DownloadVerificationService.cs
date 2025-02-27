using System.Security.Cryptography;
using MAria2.Core.Entities;
using MAria2.Core.Interfaces;

namespace MAria2.Core.Services;

public class DownloadVerificationService : IDownloadVerificationService
{
    private readonly ILoggingService _loggingService;

    public DownloadVerificationService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public async Task<DownloadVerificationResult> VerifyDownloadAsync(Download download)
    {
        var result = new DownloadVerificationResult
        {
            DownloadId = download.Id,
            StartTime = DateTime.UtcNow
        };

        try 
        {
            // Verify file integrity
            result.FileExists = File.Exists(download.DestinationPath);
            if (!result.FileExists)
            {
                result.Status = VerificationStatus.FileNotFound;
                return result;
            }

            // Calculate file hash
            result.FileHash = await CalculateFileHashAsync(download.DestinationPath);
            
            // Compare with expected hash if provided
            if (!string.IsNullOrEmpty(download.ExpectedHash))
            {
                result.HashMatches = result.FileHash == download.ExpectedHash;
                result.Status = result.HashMatches 
                    ? VerificationStatus.Verified 
                    : VerificationStatus.HashMismatch;
            }
            else 
            {
                result.Status = VerificationStatus.NoHashProvided;
            }

            // Additional verification checks
            result.FileSize = new FileInfo(download.DestinationPath).Length;
            result.IsComplete = result.FileSize == download.ExpectedFileSize;

            return result;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Download verification failed: {ex.Message}");
            result.Status = VerificationStatus.VerificationFailed;
            return result;
        }
        finally 
        {
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
        }
    }

    private async Task<string> CalculateFileHashAsync(
        string filePath, 
        HashAlgorithm hashAlgorithm = null)
    {
        hashAlgorithm ??= SHA256.Create();

        using var stream = File.OpenRead(filePath);
        var hash = await hashAlgorithm.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public async Task<bool> RepairDownloadAsync(Download download)
    {
        var verificationResult = await VerifyDownloadAsync(download);

        if (verificationResult.Status == VerificationStatus.Verified)
            return true;

        // Implement download repair strategies
        if (download.Segments != null && download.Segments.Any())
        {
            // Attempt segment-level repair
            var brokenSegments = download.Segments
                .Where(s => s.Status != DownloadStatus.Completed)
                .ToList();

            // Re-download broken segments
            foreach (var segment in brokenSegments)
            {
                // Logic to re-download specific segments
                // This would involve calling the split download manager
            }
        }

        // Final verification after repair
        var repairResult = await VerifyDownloadAsync(download);
        return repairResult.Status == VerificationStatus.Verified;
    }
}

public enum VerificationStatus
{
    Unverified,
    Verified,
    FileNotFound,
    HashMismatch,
    NoHashProvided,
    VerificationFailed
}

public class DownloadVerificationResult
{
    public Guid DownloadId { get; set; }
    public bool FileExists { get; set; }
    public string FileHash { get; set; }
    public bool HashMatches { get; set; }
    public long FileSize { get; set; }
    public bool IsComplete { get; set; }
    public VerificationStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
}
