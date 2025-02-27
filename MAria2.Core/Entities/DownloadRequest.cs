using MAria2.Core.Enums;
using MAria2.Application.Services;

namespace MAria2.Core.Entities;

public record DownloadRequest(
    string Url,
    ProtocolType Protocol = ProtocolType.HTTP,
    ContentType ContentType = ContentType.Unknown,
    ProtocolHandlerService.DownloadCharacteristics DownloadCharacteristics = null,
    string DestinationPath = null,
    DownloadPriority Priority = DownloadPriority.Normal
)
{
    public DownloadCharacteristics Characteristics { get; init; } = 
        DownloadCharacteristics ?? new ProtocolHandlerService.DownloadCharacteristics();

    // Optional metadata for advanced download handling
    public Dictionary<string, string> Metadata { get; init; } = new();

    // Convenience method for creating download from URL
    public static DownloadRequest Create(string url) => new(url);

    // Method to set optional parameters with fluent interface
    public DownloadRequest WithDestination(string path)
        => this with { DestinationPath = path };

    public DownloadRequest WithPriority(DownloadPriority priority)
        => this with { Priority = priority };

    public DownloadRequest AddMetadata(string key, string value)
    {
        var metadata = new Dictionary<string, string>(Metadata);
        metadata[key] = value;
        return this with { Metadata = metadata };
    }
}
