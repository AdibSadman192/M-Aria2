using System.Text.Json;
using System.Diagnostics;
using MAria2.Core.Interfaces;
using MAria2.Core.Exceptions;

namespace MAria2.Infrastructure.Logging;

public class DependencyUpdateLogger
{
    private readonly ILoggingService _baseLogger;
    private readonly string _logDirectoryPath;

    public DependencyUpdateLogger(
        ILoggingService baseLogger, 
        string logDirectoryPath = null)
    {
        _baseLogger = baseLogger;
        _logDirectoryPath = logDirectoryPath ?? 
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "MAria2", 
                "Logs", 
                "DependencyUpdates"
            );

        // Ensure log directory exists
        Directory.CreateDirectory(_logDirectoryPath);
    }

    public void LogUpdateAttempt(DependencyUpdateInfo updateInfo)
    {
        var logEntry = new DependencyUpdateLogEntry
        {
            Timestamp = DateTime.UtcNow,
            DependencyName = updateInfo.Name,
            CurrentVersion = updateInfo.Version,
            EventType = DependencyUpdateEventType.UpdateAttempted
        };

        WriteLogEntry(logEntry);
        _baseLogger.LogInfo($"Attempting to update {updateInfo.Name} to version {updateInfo.Version}");
    }

    public void LogUpdateSuccess(DependencyUpdateInfo updateInfo)
    {
        var logEntry = new DependencyUpdateLogEntry
        {
            Timestamp = DateTime.UtcNow,
            DependencyName = updateInfo.Name,
            CurrentVersion = updateInfo.Version,
            EventType = DependencyUpdateEventType.UpdateSucceeded
        };

        WriteLogEntry(logEntry);
        _baseLogger.LogInfo($"Successfully updated {updateInfo.Name} to version {updateInfo.Version}");
    }

    public void LogUpdateFailure(
        DependencyUpdateInfo updateInfo, 
        DependencyUpdateException exception)
    {
        var logEntry = new DependencyUpdateLogEntry
        {
            Timestamp = DateTime.UtcNow,
            DependencyName = updateInfo.Name,
            CurrentVersion = updateInfo.Version,
            EventType = DependencyUpdateEventType.UpdateFailed,
            ErrorType = exception.ErrorType,
            ErrorMessage = exception.Message,
            ErrorSeverity = exception.GetErrorSeverity()
        };

        WriteLogEntry(logEntry);
        _baseLogger.LogError(
            $"Update failed for {updateInfo.Name}: {exception.GetUserFriendlyMessage()}"
        );
    }

    public void LogUpdateCheck(List<DependencyUpdateInfo> availableUpdates)
    {
        var logEntry = new DependencyUpdateLogEntry
        {
            Timestamp = DateTime.UtcNow,
            EventType = DependencyUpdateEventType.UpdateCheckPerformed,
            UpdatesAvailable = availableUpdates.Select(u => new UpdateInfo
            {
                Name = u.Name,
                Version = u.Version
            }).ToList()
        };

        WriteLogEntry(logEntry);
        _baseLogger.LogInfo(
            $"Update check completed. {availableUpdates.Count} updates available."
        );
    }

    private void WriteLogEntry(DependencyUpdateLogEntry logEntry)
    {
        try 
        {
            // Create daily log file
            var logFileName = $"dependency_updates_{DateTime.UtcNow:yyyyMMdd}.jsonl";
            var logFilePath = Path.Combine(_logDirectoryPath, logFileName);

            // Append log entry as a JSON line
            var jsonLine = JsonSerializer.Serialize(logEntry) + Environment.NewLine;
            File.AppendAllText(logFilePath, jsonLine);
        }
        catch (Exception ex)
        {
            // Fallback logging if file writing fails
            _baseLogger.LogError($"Failed to write dependency update log: {ex.Message}");
        }
    }

    public async Task<List<DependencyUpdateLogEntry>> GetRecentLogEntriesAsync(
        int count = 100, 
        DependencyUpdateEventType? filterEventType = null)
    {
        var logFiles = Directory.GetFiles(_logDirectoryPath, "dependency_updates_*.jsonl")
            .OrderByDescending(f => f)
            .Take(3); // Look in last 3 log files

        var logEntries = new List<DependencyUpdateLogEntry>();

        foreach (var logFile in logFiles)
        {
            var lines = await File.ReadAllLinesAsync(logFile);
            
            var fileEntries = lines
                .Select(line => JsonSerializer.Deserialize<DependencyUpdateLogEntry>(line))
                .Where(entry => entry != null)
                .Where(entry => 
                    !filterEventType.HasValue || 
                    entry.EventType == filterEventType.Value
                )
                .OrderByDescending(entry => entry.Timestamp)
                .Take(count);

            logEntries.AddRange(fileEntries);

            if (logEntries.Count >= count)
            {
                logEntries = logEntries.Take(count).ToList();
                break;
            }
        }

        return logEntries;
    }
}

// Log entry model for dependency updates
public class DependencyUpdateLogEntry
{
    public DateTime Timestamp { get; set; }
    public string DependencyName { get; set; }
    public string CurrentVersion { get; set; }
    public DependencyUpdateEventType EventType { get; set; }
    public DependencyErrorType? ErrorType { get; set; }
    public string ErrorMessage { get; set; }
    public ErrorSeverity? ErrorSeverity { get; set; }
    public List<UpdateInfo> UpdatesAvailable { get; set; }
}

public class UpdateInfo
{
    public string Name { get; set; }
    public string Version { get; set; }
}

public enum DependencyUpdateEventType
{
    UpdateCheckPerformed,
    UpdateAttempted,
    UpdateSucceeded,
    UpdateFailed
}
