using MAria2.Core.Entities;
using MAria2.Core.Interfaces;
using MAria2.Core.Enums;

namespace MAria2.Application.Services;

public class DownloadService
{
    private readonly IEnumerable<IDownloadEngine> _downloadEngines;
    private readonly IDownloadRepository _downloadRepository;
    private readonly ILoggingService _loggingService;
    private readonly EngineSelectionService _engineSelectionService;

    // Track active downloads and their current engines
    private readonly Dictionary<Guid, (Download Download, IDownloadEngine Engine)> 
        _activeDownloads = new();

    public DownloadService(
        IEnumerable<IDownloadEngine> downloadEngines,
        IDownloadRepository downloadRepository,
        ILoggingService loggingService,
        EngineSelectionService engineSelectionService)
    {
        _downloadEngines = downloadEngines;
        _downloadRepository = downloadRepository;
        _loggingService = loggingService;
        _engineSelectionService = engineSelectionService;
    }

    public async Task StartDownloadAsync(
        string url, 
        string destinationPath, 
        string preferredEngineName = null)
    {
        // Create download entity
        var download = new Download
        {
            Id = Guid.NewGuid(),
            Url = url,
            DestinationPath = destinationPath,
            StartedAt = DateTime.UtcNow,
            Status = DownloadStatus.Initializing
        };

        try 
        {
            // Select download engine
            var selectedEngine = await SelectDownloadEngineAsync(url, preferredEngineName);
            
            // Start download
            var startedDownload = await selectedEngine.StartDownloadAsync(download);
            
            // Track active download
            _activeDownloads[download.Id] = (startedDownload, selectedEngine);
            
            // Persist download record
            await _downloadRepository.AddDownloadAsync(startedDownload);

            return startedDownload;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Download start failed: {ex.Message}");
            download.Status = DownloadStatus.Failed;
            await _downloadRepository.AddDownloadAsync(download);
            throw;
        }
    }

    private async Task<IDownloadEngine> SelectDownloadEngineAsync(
        string url, 
        string preferredEngineName = null)
    {
        // If preferred engine is specified, try to use it
        if (!string.IsNullOrEmpty(preferredEngineName))
        {
            var preferredEngine = _downloadEngines
                .FirstOrDefault(e => e.GetType().Name == preferredEngineName);
            
            if (preferredEngine != null && preferredEngine.CanHandleProtocol(url))
            {
                return preferredEngine;
            }
        }

        // Use engine selection service to choose best engine
        return await _engineSelectionService.SelectBestEngineAsync(
            new DownloadRequest(url)
        );
    }

    public async Task PauseDownloadAsync(Guid downloadId)
    {
        if (_activeDownloads.TryGetValue(downloadId, out var downloadInfo))
        {
            await downloadInfo.Engine.PauseDownloadAsync(downloadInfo.Download);
            downloadInfo.Download.Status = DownloadStatus.Paused;
            await _downloadRepository.UpdateDownloadAsync(downloadInfo.Download);
        }
    }

    public async Task ResumeDownloadAsync(Guid downloadId)
    {
        var download = await _downloadRepository.GetDownloadByIdAsync(downloadId);
        
        if (download != null)
        {
            var engine = await SelectDownloadEngineAsync(download.Url);
            await engine.ResumeDownloadAsync(download);
            
            _activeDownloads[downloadId] = (download, engine);
        }
    }

    public void CancelDownload(Download download)
    {
        if (_activeDownloads.TryGetValue(download.Id, out var downloadInfo))
        {
            downloadInfo.Engine.CancelDownloadAsync(download);
            _activeDownloads.Remove(download.Id);
        }
    }

    public IDownloadEngine GetCurrentEngine(Download download)
    {
        return _activeDownloads.TryGetValue(download.Id, out var downloadInfo)
            ? downloadInfo.Engine
            : null;
    }

    public async Task<DownloadProgress> GetDownloadProgressAsync(Guid downloadId)
    {
        if (_activeDownloads.TryGetValue(downloadId, out var downloadInfo))
        {
            return await downloadInfo.Engine.GetProgressAsync(downloadInfo.Download);
        }

        // Fallback to repository if not in active downloads
        var download = await _downloadRepository.GetDownloadByIdAsync(downloadId);
        
        if (download != null)
        {
            var engine = await SelectDownloadEngineAsync(download.Url);
            return await engine.GetProgressAsync(download);
        }

        throw new InvalidOperationException("Download not found");
    }
}
