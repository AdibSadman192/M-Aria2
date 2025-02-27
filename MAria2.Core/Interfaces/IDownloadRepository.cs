using MAria2.Core.Entities;

namespace MAria2.Core.Interfaces;

public interface IDownloadRepository
{
    Task<Download> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Download>> GetAllAsync();
    Task<IReadOnlyList<Download>> GetByStatusAsync(DownloadStatus status);
    Task AddAsync(Download download);
    Task UpdateAsync(Download download);
    Task DeleteAsync(Guid id);
    Task<IReadOnlyList<Download>> GetCompletedDownloadsAsync();
    Task<IReadOnlyList<Download>> GetActiveDownloadsAsync();
}
