using System.Data.SQLite;
using MAria2.Core.Entities;
using MAria2.Core.Enums;
using MAria2.Core.Interfaces;
using Newtonsoft.Json;

namespace MAria2.Infrastructure.Repositories;

public class SqliteDownloadRepository : IDownloadRepository, IDisposable
{
    private readonly SQLiteConnection _connection;
    private readonly string _connectionString;

    public SqliteDownloadRepository(string databasePath = null)
    {
        // Use a default path if not provided
        _connectionString = databasePath ?? 
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "MAria2", 
                "downloads.sqlite"
            );

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(_connectionString));

        // Create connection
        _connection = new SQLiteConnection($"Data Source={_connectionString};");
        _connection.Open();

        // Create table if not exists
        CreateTable();
    }

    private void CreateTable()
    {
        using var command = new SQLiteCommand(_connection)
        {
            CommandText = @"
                CREATE TABLE IF NOT EXISTS Downloads (
                    Id TEXT PRIMARY KEY,
                    Url TEXT NOT NULL,
                    DestinationPath TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    Priority INTEGER NOT NULL,
                    SelectedEngine INTEGER NOT NULL,
                    DownloadData TEXT
                )"
        };
        command.ExecuteNonQuery();
    }

    public async Task<Download> GetByIdAsync(Guid id)
    {
        using var command = new SQLiteCommand(_connection)
        {
            CommandText = "SELECT * FROM Downloads WHERE Id = @Id"
        };
        command.Parameters.AddWithValue("@Id", id.ToString());

        using var reader = await command.ExecuteReaderAsync();
        return reader.Read() ? MapDownloadFromReader(reader) : null;
    }

    public async Task<IReadOnlyList<Download>> GetAllAsync()
    {
        using var command = new SQLiteCommand(_connection)
        {
            CommandText = "SELECT * FROM Downloads"
        };

        var downloads = new List<Download>();
        using var reader = await command.ExecuteReaderAsync();
        while (reader.Read())
        {
            downloads.Add(MapDownloadFromReader(reader));
        }
        return downloads;
    }

    public async Task<IReadOnlyList<Download>> GetByStatusAsync(DownloadStatus status)
    {
        using var command = new SQLiteCommand(_connection)
        {
            CommandText = "SELECT * FROM Downloads WHERE Status = @Status"
        };
        command.Parameters.AddWithValue("@Status", (int)status);

        var downloads = new List<Download>();
        using var reader = await command.ExecuteReaderAsync();
        while (reader.Read())
        {
            downloads.Add(MapDownloadFromReader(reader));
        }
        return downloads;
    }

    public async Task AddAsync(Download download)
    {
        using var command = new SQLiteCommand(_connection)
        {
            CommandText = @"
                INSERT INTO Downloads 
                (Id, Url, DestinationPath, Status, Priority, SelectedEngine, DownloadData) 
                VALUES (@Id, @Url, @DestinationPath, @Status, @Priority, @SelectedEngine, @DownloadData)"
        };

        command.Parameters.AddWithValue("@Id", download.Id.ToString());
        command.Parameters.AddWithValue("@Url", download.Url);
        command.Parameters.AddWithValue("@DestinationPath", download.DestinationPath);
        command.Parameters.AddWithValue("@Status", (int)download.Status);
        command.Parameters.AddWithValue("@Priority", (int)download.Priority);
        command.Parameters.AddWithValue("@SelectedEngine", (int)download.SelectedEngine);
        command.Parameters.AddWithValue("@DownloadData", 
            JsonConvert.SerializeObject(download, Formatting.Indented));

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(Download download)
    {
        using var command = new SQLiteCommand(_connection)
        {
            CommandText = @"
                UPDATE Downloads 
                SET Url = @Url, 
                    DestinationPath = @DestinationPath, 
                    Status = @Status, 
                    Priority = @Priority, 
                    SelectedEngine = @SelectedEngine, 
                    DownloadData = @DownloadData 
                WHERE Id = @Id"
        };

        command.Parameters.AddWithValue("@Id", download.Id.ToString());
        command.Parameters.AddWithValue("@Url", download.Url);
        command.Parameters.AddWithValue("@DestinationPath", download.DestinationPath);
        command.Parameters.AddWithValue("@Status", (int)download.Status);
        command.Parameters.AddWithValue("@Priority", (int)download.Priority);
        command.Parameters.AddWithValue("@SelectedEngine", (int)download.SelectedEngine);
        command.Parameters.AddWithValue("@DownloadData", 
            JsonConvert.SerializeObject(download, Formatting.Indented));

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        using var command = new SQLiteCommand(_connection)
        {
            CommandText = "DELETE FROM Downloads WHERE Id = @Id"
        };
        command.Parameters.AddWithValue("@Id", id.ToString());

        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<Download>> GetCompletedDownloadsAsync()
    {
        using var command = new SQLiteCommand(_connection)
        {
            CommandText = "SELECT * FROM Downloads WHERE Status = @Status"
        };
        command.Parameters.AddWithValue("@Status", (int)DownloadStatus.Completed);

        var downloads = new List<Download>();
        using var reader = await command.ExecuteReaderAsync();
        while (reader.Read())
        {
            downloads.Add(MapDownloadFromReader(reader));
        }
        return downloads;
    }

    public async Task<IReadOnlyList<Download>> GetActiveDownloadsAsync()
    {
        using var command = new SQLiteCommand(_connection)
        {
            CommandText = "SELECT * FROM Downloads WHERE Status IN (@Queued, @Downloading, @Paused)"
        };
        command.Parameters.AddWithValue("@Queued", (int)DownloadStatus.Queued);
        command.Parameters.AddWithValue("@Downloading", (int)DownloadStatus.Downloading);
        command.Parameters.AddWithValue("@Paused", (int)DownloadStatus.Paused);

        var downloads = new List<Download>();
        using var reader = await command.ExecuteReaderAsync();
        while (reader.Read())
        {
            downloads.Add(MapDownloadFromReader(reader));
        }
        return downloads;
    }

    private Download MapDownloadFromReader(SQLiteDataReader reader)
    {
        // Deserialize full download object from JSON if available
        var downloadData = reader["DownloadData"].ToString();
        if (!string.IsNullOrEmpty(downloadData))
        {
            return JsonConvert.DeserializeObject<Download>(downloadData);
        }

        // Fallback to manual mapping
        return new Download
        {
            Id = Guid.Parse(reader["Id"].ToString()),
            Url = reader["Url"].ToString(),
            DestinationPath = reader["DestinationPath"].ToString(),
            Status = (DownloadStatus)Convert.ToInt32(reader["Status"]),
            Priority = (DownloadPriority)Convert.ToInt32(reader["Priority"]),
            SelectedEngine = (EngineType)Convert.ToInt32(reader["SelectedEngine"])
        };
    }

    public void Dispose()
    {
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}
