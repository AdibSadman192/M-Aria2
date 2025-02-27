using MAria2.Core.Interfaces;
using MAria2.Core.Enums;

namespace MAria2.Infrastructure.Engines.Aria2;

public class Aria2CapabilityProvider : IEngineCapabilityProvider
{
    public EngineType EngineType => EngineType.Aria2;

    private static readonly string[] SupportedProtocols = new[]
    {
        "http", "https", "ftp", "sftp", "magnet", "torrent", "metalink"
    };

    private static readonly string[] SupportedContentTypes = new[]
    {
        "application/octet-stream", 
        "video/", 
        "audio/", 
        "application/zip", 
        "application/x-rar-compressed"
    };

    public bool SupportsProtocol(string protocol)
    {
        return SupportedProtocols.Any(p => 
            protocol.Equals(p, StringComparison.OrdinalIgnoreCase));
    }

    public bool SupportsContentType(string contentType)
    {
        return SupportedContentTypes.Any(supportedType => 
            contentType.StartsWith(supportedType, StringComparison.OrdinalIgnoreCase));
    }

    public int GetPriority(string url)
    {
        // Higher priority for torrents and metalinks
        if (url.Contains(".torrent", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            return 90;
        }

        // Medium priority for HTTP/HTTPS
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return 50;
        }

        // Lower priority for other protocols
        return 20;
    }
}
