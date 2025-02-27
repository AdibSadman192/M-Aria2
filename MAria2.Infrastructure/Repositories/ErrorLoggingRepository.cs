using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MAria2.Infrastructure.Repositories
{
    public class ErrorLoggingRepository
    {
        private readonly string _errorLogPath;
        private readonly ILogger<ErrorLoggingRepository> _logger;
        private const string ERROR_LOG_FILENAME = "error_logs.json";
        private const string ARCHIVE_DIRECTORY = "error_archives";

        public ErrorLoggingRepository(
            IPlatformAbstractionService platformService,
            ILogger<ErrorLoggingRepository> logger)
        {
            _logger = logger;
            var downloadDir = platformService.GetDefaultDownloadDirectory();
            
            // Create dedicated error logging directory
            _errorLogPath = Path.Combine(
                downloadDir, 
                ".maria2", 
                "error_logs"
            );
            
            Directory.CreateDirectory(_errorLogPath);
            Directory.CreateDirectory(Path.Combine(_errorLogPath, ARCHIVE_DIRECTORY));
        }

        public async Task SaveErrorLogsAsync(IEnumerable<ErrorDetails> errorLogs)
        {
            try 
            {
                var filePath = Path.Combine(_errorLogPath, ERROR_LOG_FILENAME);
                var jsonContent = JsonSerializer.Serialize(errorLogs, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });

                await File.WriteAllTextAsync(filePath, jsonContent);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving error logs: {ex.Message}");
            }
        }

        public async Task<IEnumerable<ErrorDetails>> LoadErrorLogsAsync(
            DateTime? startTime = null, 
            DateTime? endTime = null,
            ErrorSeverity? minSeverity = null)
        {
            try 
            {
                var filePath = Path.Combine(_errorLogPath, ERROR_LOG_FILENAME);
                
                if (!File.Exists(filePath))
                    return Enumerable.Empty<ErrorDetails>();

                var jsonContent = await File.ReadAllTextAsync(filePath);
                var allErrorLogs = JsonSerializer.Deserialize<List<ErrorDetails>>(jsonContent) 
                    ?? Enumerable.Empty<ErrorDetails>();

                // Apply filtering
                return allErrorLogs.Where(log => 
                    (!startTime.HasValue || log.Timestamp >= startTime.Value) &&
                    (!endTime.HasValue || log.Timestamp <= endTime.Value) &&
                    (!minSeverity.HasValue || log.Severity >= minSeverity.Value)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading error logs: {ex.Message}");
                return Enumerable.Empty<ErrorDetails>();
            }
        }

        public async Task ArchiveOldErrorLogsAsync(TimeSpan retentionPeriod)
        {
            try 
            {
                var cutoffTime = DateTime.UtcNow.Subtract(retentionPeriod);
                var errorLogs = await LoadErrorLogsAsync();

                // Separate logs to archive and keep
                var logsToArchive = errorLogs
                    .Where(log => log.Timestamp < cutoffTime)
                    .ToList();
                var logsToKeep = errorLogs
                    .Where(log => log.Timestamp >= cutoffTime)
                    .ToList();

                // Archive old logs
                if (logsToArchive.Any())
                {
                    var archiveFileName = $"error_log_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                    var archiveFilePath = Path.Combine(
                        _errorLogPath, 
                        ARCHIVE_DIRECTORY, 
                        archiveFileName
                    );

                    var archiveJsonContent = JsonSerializer.Serialize(
                        logsToArchive, 
                        new JsonSerializerOptions { WriteIndented = true }
                    );

                    await File.WriteAllTextAsync(archiveFilePath, archiveJsonContent);
                }

                // Update current error log file
                await SaveErrorLogsAsync(logsToKeep);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error archiving error logs: {ex.Message}");
            }
        }

        public async Task<IEnumerable<string>> GetArchivedErrorLogFilesAsync()
        {
            try 
            {
                var archiveDirectory = Path.Combine(_errorLogPath, ARCHIVE_DIRECTORY);
                return Directory.GetFiles(archiveDirectory, "*.json");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving archived error logs: {ex.Message}");
                return Enumerable.Empty<string>();
            }
        }
    }
}
