using System;
using System.Collections.Concurrent;
using System.Text.Json;
using MAria2.Core.Entities;
using MAria2.Core.Interfaces;
using MAria2.Core.Enums;

namespace MAria2.Application.Services;

public class PlaylistManagementService : IPlaylistManagementService, IDisposable
{
    private readonly IDownloadQueueService _downloadQueueService;
    private readonly ILoggingService _loggingService;
    private readonly IThumbnailExtractionService _thumbnailService;
    private readonly INotificationService _notificationService;
    private readonly string _playlistStoragePath;

    // Thread-safe playlist storage
    private ConcurrentDictionary<Guid, Playlist> _playlists;
    private readonly object _playlistLock = new();

    public PlaylistManagementService(
        IDownloadQueueService downloadQueueService,
        ILoggingService loggingService,
        IThumbnailExtractionService thumbnailService,
        INotificationService notificationService)
    {
        _downloadQueueService = downloadQueueService;
        _loggingService = loggingService;
        _thumbnailService = thumbnailService;
        _notificationService = notificationService;

        // Set up playlist storage directory
        _playlistStoragePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MAria2",
            "Playlists"
        );
        Directory.CreateDirectory(_playlistStoragePath);

        // Initialize playlists
        _playlists = new ConcurrentDictionary<Guid, Playlist>();
        LoadSavedPlaylists();
    }

    private void LoadSavedPlaylists()
    {
        try 
        {
            var playlistFiles = Directory.GetFiles(_playlistStoragePath, "*.json");
            foreach (var file in playlistFiles)
            {
                try 
                {
                    var playlistJson = File.ReadAllText(file);
                    var playlist = JsonSerializer.Deserialize<Playlist>(playlistJson);
                    
                    if (playlist != null)
                    {
                        _playlists[playlist.Id] = playlist;
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Failed to load playlist {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Playlist loading failed: {ex.Message}");
        }
    }

    public async Task<Playlist> CreatePlaylistAsync(
        string name, 
        PlaylistType type = PlaylistType.Generic)
    {
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _playlists[playlist.Id] = playlist;
        await SavePlaylistAsync(playlist);

        _loggingService.LogInformation($"Created playlist: {name}");
        return playlist;
    }

    public async Task<Playlist> AddItemToPlaylistAsync(
        Guid playlistId, 
        PlaylistItem item)
    {
        if (!_playlists.TryGetValue(playlistId, out var playlist))
        {
            throw new InvalidOperationException("Playlist not found");
        }

        // Validate and enrich playlist item
        item.Id = Guid.NewGuid();
        item.AddedAt = DateTime.UtcNow;

        // Try to extract thumbnail
        try 
        {
            var download = new Download { Url = item.Url };
            item.ThumbnailPath = await _thumbnailService.ExtractThumbnailAsync(download);
        }
        catch 
        {
            // Thumbnail extraction is optional
        }

        lock (_playlistLock)
        {
            playlist.Items.Add(item);
            playlist.UpdatedAt = DateTime.UtcNow;
        }

        await SavePlaylistAsync(playlist);
        _loggingService.LogInformation($"Added item to playlist {playlist.Name}");

        return playlist;
    }

    public async Task<Playlist> RemoveItemFromPlaylistAsync(
        Guid playlistId, 
        Guid itemId)
    {
        if (!_playlists.TryGetValue(playlistId, out var playlist))
        {
            throw new InvalidOperationException("Playlist not found");
        }

        lock (_playlistLock)
        {
            var itemToRemove = playlist.Items.FirstOrDefault(i => i.Id == itemId);
            if (itemToRemove != null)
            {
                playlist.Items.Remove(itemToRemove);
                playlist.UpdatedAt = DateTime.UtcNow;
            }
        }

        await SavePlaylistAsync(playlist);
        _loggingService.LogInformation($"Removed item from playlist {playlist.Name}");

        return playlist;
    }

    public async Task<Playlist> DownloadPlaylistAsync(
        Guid playlistId, 
        string destinationDirectory)
    {
        if (!_playlists.TryGetValue(playlistId, out var playlist))
        {
            throw new InvalidOperationException("Playlist not found");
        }

        // Ensure destination directory exists
        Directory.CreateDirectory(destinationDirectory);

        var downloadTasks = playlist.Items.Select(async item =>
        {
            try 
            {
                var download = new Download
                {
                    Url = item.Url,
                    DestinationPath = Path.Combine(
                        destinationDirectory, 
                        $"{item.Title ?? "download"}_{Guid.NewGuid():N}{Path.GetExtension(item.Url)}"
                    )
                };

                // Queue download
                await _downloadQueueService.QueueDownloadAsync(download);

                // Notify about download
                await _notificationService.SendDownloadNotificationAsync(
                    download, 
                    NotificationType.DownloadStarted
                );

                return download;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Playlist item download failed: {ex.Message}");
                return null;
            }
        });

        await Task.WhenAll(downloadTasks);

        return playlist;
    }

    private async Task SavePlaylistAsync(Playlist playlist)
    {
        try 
        {
            var filePath = Path.Combine(
                _playlistStoragePath, 
                $"{playlist.Id}.json"
            );

            var options = new JsonSerializerOptions { WriteIndented = true };
            var playlistJson = JsonSerializer.Serialize(playlist, options);

            await File.WriteAllTextAsync(filePath, playlistJson);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Playlist save failed: {ex.Message}");
        }
    }

    public IEnumerable<Playlist> GetAllPlaylists() => 
        _playlists.Values.AsEnumerable();

    public Playlist GetPlaylistById(Guid playlistId) => 
        _playlists.TryGetValue(playlistId, out var playlist) 
            ? playlist 
            : null;

    public async Task DeletePlaylistAsync(Guid playlistId)
    {
        if (_playlists.TryRemove(playlistId, out var playlist))
        {
            try 
            {
                var filePath = Path.Combine(
                    _playlistStoragePath, 
                    $"{playlistId}.json"
                );

                File.Delete(filePath);
                _loggingService.LogInformation($"Deleted playlist: {playlist.Name}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Playlist deletion failed: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        // Perform any necessary cleanup
        _playlists.Clear();
    }
}
