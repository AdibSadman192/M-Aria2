using System.Net;

namespace MAria2.Infrastructure.Engines.Aria2;

public class Aria2Configuration
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6800;
    public string Secret { get; set; } = string.Empty;
    public int ConnectionCount { get; set; } = 16;
    public int MaxConcurrentDownloads { get; set; } = 5;
    public long MaxDownloadSpeed { get; set; } = 0; // Unlimited
    public long MaxUploadSpeed { get; set; } = 0; // Unlimited
    public bool EnableDHT { get; set; } = true;
    public bool EnablePeerExchange { get; set; } = true;
    public bool EnableSeeding { get; set; } = false;
    public int SeedRatio { get; set; } = 1;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    public Uri GetRpcUri() => new($"http://{Host}:{Port}/jsonrpc");
}
