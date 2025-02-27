using MAria2.Core.Entities;

namespace MAria2.Core.Interfaces;

public interface IPlaylistManagementService
{
    /// <summary>
    /// Create a new playlist
    /// </summary>
    Task<Playlist> CreatePlaylistAsync(string name, PlaylistType type = PlaylistType.Generic);

    /// <summary>
    /// Add an item to an existing playlist
    /// </summary>
    Task<Playlist> AddItemToPlaylistAsync(Guid playlistId, PlaylistItem item);

    /// <summary>
    /// Remove an item from a playlist
    /// </summary>
    Task<Playlist> RemoveItemFromPlaylistAsync(Guid playlistId, Guid itemId);

    /// <summary>
    /// Download all items in a playlist
    /// </summary>
    Task<Playlist> DownloadPlaylistAsync(Guid playlistId, string destinationDirectory);

    /// <summary>
    /// Retrieve all playlists
    /// </summary>
    IEnumerable<Playlist> GetAllPlaylists();

    /// <summary>
    /// Get a specific playlist by ID
    /// </summary>
    Playlist GetPlaylistById(Guid playlistId);

    /// <summary>
    /// Delete a playlist
    /// </summary>
    Task DeletePlaylistAsync(Guid playlistId);
}
