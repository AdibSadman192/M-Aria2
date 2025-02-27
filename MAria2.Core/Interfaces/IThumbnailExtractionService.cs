using MAria2.Core.Entities;

namespace MAria2.Core.Interfaces;

public interface IThumbnailExtractionService
{
    /// <summary>
    /// Extract a thumbnail for a given download
    /// </summary>
    /// <param name="download">The downloaded file</param>
    /// <param name="width">Optional thumbnail width</param>
    /// <param name="height">Optional thumbnail height</param>
    /// <returns>Path to the generated thumbnail</returns>
    Task<string> ExtractThumbnailAsync(Download download, int? width = null, int? height = null);

    /// <summary>
    /// Retrieve a cached thumbnail for a file
    /// </summary>
    /// <param name="originalFilePath">Path to the original file</param>
    /// <returns>Path to the cached thumbnail, or null if not found</returns>
    Task<string> GetCachedThumbnailAsync(string originalFilePath);

    /// <summary>
    /// Clear old thumbnails from the cache
    /// </summary>
    /// <param name="olderThan">Optional time threshold for cache cleanup</param>
    void ClearThumbnailCache(TimeSpan? olderThan = null);
}
