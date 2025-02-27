namespace MAria2.Infrastructure.Services;

public class DatabaseConfigurationService
{
    public string DatabasePath { get; }
    public long MaxDatabaseSizeMB { get; }
    public int MaxDownloadHistoryEntries { get; }

    public DatabaseConfigurationService(
        string databasePath = null, 
        long maxDatabaseSizeMB = 100, 
        int maxDownloadHistoryEntries = 1000)
    {
        // Default database path in user's local app data
        DatabasePath = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "MAria2", 
            "downloads.sqlite"
        );

        MaxDatabaseSizeMB = maxDatabaseSizeMB;
        MaxDownloadHistoryEntries = maxDownloadHistoryEntries;

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
    }

    public void ManageDatabaseSize(IDownloadRepository repository)
    {
        // TODO: Implement database size and history management
        // 1. Check current database file size
        // 2. If exceeds MaxDatabaseSizeMB, remove oldest completed downloads
        // 3. Maintain only MaxDownloadHistoryEntries
    }
}
