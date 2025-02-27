using MAria2.Core.Enums;

namespace MAria2.Core.Interfaces;

public interface IEngineCapabilityProvider
{
    EngineType EngineType { get; }
    bool SupportsProtocol(string protocol);
    bool SupportsContentType(string contentType);
    int GetPriority(string url);
}
