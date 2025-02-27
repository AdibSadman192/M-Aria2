namespace MAria2.Core.Enums;

public enum EngineHealthStatus
{
    // Engine is fully operational and performing optimally
    Healthy,

    // Engine is experiencing some performance issues
    Degraded,

    // Engine is not functioning correctly
    Unhealthy,

    // Engine is currently being tested or initialized
    Initializing,

    // Engine is temporarily unavailable
    Unavailable,

    // Engine has been disabled by user or system
    Disabled
}
