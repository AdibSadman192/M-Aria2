using MAria2.Application.Services;
using MAria2.Core.Entities;
using MAria2.Core.Enums;

namespace MAria2.Application.Commands;

public record StartDownloadCommand(string Url, string DestinationPath)
{
    public Download Execute(DownloadService downloadService) =>
        downloadService.StartDownloadAsync(Url, DestinationPath).Result;
}

public record PauseDownloadCommand(Guid DownloadId)
{
    public void Execute(DownloadService downloadService) =>
        downloadService.PauseDownloadAsync(DownloadId).Wait();
}

public record ResumeDownloadCommand(Guid DownloadId)
{
    public void Execute(DownloadService downloadService) =>
        downloadService.ResumeDownloadAsync(DownloadId).Wait();
}

public record CancelDownloadCommand(Guid DownloadId)
{
    public void Execute(DownloadService downloadService) =>
        downloadService.CancelDownloadAsync(DownloadId).Wait();
}

public record SwitchEngineCommand(Guid DownloadId, EngineType TargetEngine)
{
    public Download Execute(EngineManagerService engineManagerService, DownloadService downloadService)
    {
        // First, get the download
        var download = downloadService.GetDownloadProgressAsync(DownloadId).Result;
        
        // Then switch the engine
        return engineManagerService
            .SwitchDownloadEngineAsync(download, TargetEngine)
            .Result;
    }
}

public record SetDownloadPriorityCommand(Guid DownloadId, DownloadPriority Priority)
{
    public void Execute(DownloadQueueService downloadQueueService) =>
        downloadQueueService.SetDownloadPriority(DownloadId, Priority);
}
