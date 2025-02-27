using MAria2.Core.Enums;
using MAria2.Application.Services;

namespace MAria2.Core.Interfaces;

public interface IEngineHealthMonitorService : IDisposable
{
    // Retrieve health metrics for a specific engine
    EngineHealthMonitorService.EngineHealthMetrics GetEngineHealthMetrics(string engineName);

    // Get performance history for an engine
    IReadOnlyCollection<EngineHealthMonitorService.DownloadPerformanceRecord> 
        GetEnginePerformanceHistory(string engineName);

    // Event for tracking engine health changes
    event EventHandler<EngineHealthMonitorService.EngineHealthChangedEventArgs> EngineHealthChanged;
}
