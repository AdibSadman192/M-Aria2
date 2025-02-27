using System;
using System.Text.Json.Serialization;

namespace MAria2.Core.Entities;

public class Playlist
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public PlaylistType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<PlaylistItem> Items { get; set; } = new();
}

public class PlaylistItem
{
    public Guid Id { get; set; }
    public string Url { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string ThumbnailPath { get; set; }
    public DateTime AddedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public long? FileSize { get; set; }
    public string[] Tags { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlaylistType
{
    Generic,
    Music,
    Video,
    Podcast,
    Audiobook,
    Educational,
    Entertainment
}
