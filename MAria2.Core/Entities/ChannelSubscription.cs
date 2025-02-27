using System;
using System.Collections.Generic;
using MAria2.Core.Enums;

namespace MAria2.Core.Entities;

/// <summary>
/// Represents a channel subscription for automatic content tracking and downloading
/// </summary>
public class ChannelSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Unique identifier for the channel on its platform
    /// </summary>
    public string ChannelIdentifier { get; set; }
    
    /// <summary>
    /// Display name of the channel
    /// </summary>
    public string ChannelName { get; set; }
    
    /// <summary>
    /// Platform where the channel exists
    /// </summary>
    public MediaPlatform Platform { get; set; }
    
    /// <summary>
    /// URL of the channel
    /// </summary>
    public string ChannelUrl { get; set; }
    
    /// <summary>
    /// Subscription type (e.g., YouTube, Podcast, Twitch)
    /// </summary>
    public SubscriptionType Type { get; set; }
    
    /// <summary>
    /// Current synchronization status
    /// </summary>
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    
    /// <summary>
    /// Frequency of checking for new content
    /// </summary>
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromHours(24);
    
    /// <summary>
    /// Last time the channel was synchronized
    /// </summary>
    public DateTime LastSyncTime { get; set; } = DateTime.MinValue;
    
    /// <summary>
    /// Next scheduled synchronization time
    /// </summary>
    public DateTime NextSyncTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Download configuration for this subscription
    /// </summary>
    public ChannelDownloadConfig DownloadConfig { get; set; } = new();
    
    /// <summary>
    /// List of recent content items from this channel
    /// </summary>
    public List<ChannelContentItem> RecentContent { get; set; } = new();
}

/// <summary>
/// Represents a single content item from a subscribed channel
/// </summary>
public class ChannelContentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; }
    public string Url { get; set; }
    public DateTime PublishedDate { get; set; }
    public long Duration { get; set; } // in seconds
    public ContentStatus Status { get; set; }
    public string ThumbnailUrl { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Configuration for downloading content from a channel
/// </summary>
public class ChannelDownloadConfig
{
    /// <summary>
    /// Maximum number of items to download per sync
    /// </summary>
    public int MaxItemsPerSync { get; set; } = 10;
    
    /// <summary>
    /// Preferred download quality
    /// </summary>
    public MediaQuality PreferredQuality { get; set; } = MediaQuality.Best;
    
    /// <summary>
    /// Types of content to download
    /// </summary>
    public List<ContentType> AllowedContentTypes { get; set; } = new()
    {
        ContentType.Video,
        ContentType.Audio
    };
    
    /// <summary>
    /// Download destination directory
    /// </summary>
    public string DownloadPath { get; set; }
    
    /// <summary>
    /// Naming template for downloaded files
    /// </summary>
    public string FileNamingTemplate { get; set; } = 
        "{ChannelName}/{PublishDate:yyyy-MM-dd} - {Title}{Extension}";
}

/// <summary>
/// Enumeration of media platforms
/// </summary>
public enum MediaPlatform
{
    YouTube,
    Twitch,
    Vimeo,
    Podcast,
    SoundCloud,
    Custom
}

/// <summary>
/// Enumeration of subscription types
/// </summary>
public enum SubscriptionType
{
    VideoChannel,
    AudioChannel,
    Podcast,
    LiveStream,
    Custom
}

/// <summary>
/// Subscription status
/// </summary>
public enum SubscriptionStatus
{
    Active,
    Paused,
    Error,
    Disabled
}

/// <summary>
/// Content status within a subscription
/// </summary>
public enum ContentStatus
{
    New,
    Downloaded,
    Skipped,
    Failed,
    Ignored
}

/// <summary>
/// Media quality preferences
/// </summary>
public enum MediaQuality
{
    Lowest,
    Low,
    Medium,
    High,
    Best
}

/// <summary>
/// Types of content to download
/// </summary>
public enum ContentType
{
    Video,
    Audio,
    Image,
    Document,
    Other
}
