using MAria2.Core.Entities;
using MAria2.Core.Enums;
using MAria2.Core.Interfaces;
using Newtonsoft.Json.Linq;

namespace MAria2.Infrastructure.Engines.Aria2;

public class Aria2Engine : IDownloadEngine
{
    private readonly Aria2ConnectionManager _connectionManager;
    private readonly Aria2Configuration _configuration;

    public EngineType Type => EngineType.Aria2;

    public Aria2Engine(Aria2Configuration configuration)
    {
        _configuration = configuration;
        _connectionManager = new Aria2ConnectionManager(configuration);
    }

    public async Task<Download> StartDownloadAsync(Download download)
    {
        var options = new Dictionary<string, string>
        {
            { "max-connection-per-server", _configuration.ConnectionCount.ToString() },
            { "max-concurrent-downloads", _configuration.MaxConcurrentDownloads.ToString() },
            { "max-download-limit", _configuration.MaxDownloadSpeed.ToString() },
            { "max-upload-limit", _configuration.MaxUploadSpeed.ToString() },
            { "dir", Path.GetDirectoryName(download.DestinationPath) },
            { "out", Path.GetFileName(download.DestinationPath) }
        };

        // Add BitTorrent specific options if applicable
        if (download.Url.Contains(".torrent", StringComparison.OrdinalIgnoreCase) ||
            download.Url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            options["bt-enable-dht"] = _configuration.EnableDHT.ToString().ToLower();
            options["bt-enable-peer-exchange"] = _configuration.EnablePeerExchange.ToString().ToLower();
            options["seed-ratio"] = _configuration.SeedRatio.ToString();
            options["seed-time"] = _configuration.EnableSeeding ? "0" : "";
        }

        var gid = await _connectionManager.SendRpcRequestAsync(
            "addUri", 
            new[] { download.Url }, 
            options
        );

        download.Id = Guid.Parse(gid.ToString());
        download.Status = DownloadStatus.Downloading;
        download.SelectedEngine = EngineType.Aria2;

        return download;
    }

    public async Task PauseDownloadAsync(Download download)
    {
        await _connectionManager.SendRpcRequestAsync("pause", download.Id.ToString());
        download.Status = DownloadStatus.Paused;
    }

    public async Task ResumeDownloadAsync(Download download)
    {
        await _connectionManager.SendRpcRequestAsync("unpause", download.Id.ToString());
        download.Status = DownloadStatus.Downloading;
    }

    public async Task CancelDownloadAsync(Download download)
    {
        await _connectionManager.SendRpcRequestAsync("remove", download.Id.ToString());
        download.Status = DownloadStatus.Canceled;
    }

    public async Task<DownloadProgress> GetProgressAsync(Download download)
    {
        var status = await _connectionManager.SendRpcRequestAsync("tellStatus", download.Id.ToString());

        return new DownloadProgress
        {
            TotalBytes = long.Parse(status["totalLength"].ToString()),
            DownloadedBytes = long.Parse(status["completedLength"].ToString()),
            SpeedBps = long.Parse(status["downloadSpeed"].ToString()),
            ElapsedTime = TimeSpan.FromSeconds(long.Parse(status["downloadSpeed"].ToString()) > 0 
                ? long.Parse(status["completedLength"].ToString()) / long.Parse(status["downloadSpeed"].ToString()) 
                : 0)
        };
    }
}
