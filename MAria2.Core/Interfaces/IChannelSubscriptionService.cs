using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MAria2.Core.Entities;

namespace MAria2.Core.Interfaces;

public interface IChannelSubscriptionService
{
    /// <summary>
    /// Create a new channel subscription
    /// </summary>
    Task<ChannelSubscription> CreateSubscriptionAsync(
        string channelUrl, 
        SubscriptionType type, 
        ChannelDownloadConfig config = null);

    /// <summary>
    /// Get all active channel subscriptions
    /// </summary>
    Task<List<ChannelSubscription>> GetActiveSubscriptionsAsync();

    /// <summary>
    /// Update an existing channel subscription
    /// </summary>
    Task<ChannelSubscription> UpdateSubscriptionAsync(
        Guid subscriptionId, 
        Action<ChannelSubscription> updateAction);

    /// <summary>
    /// Synchronize a specific channel subscription
    /// </summary>
    Task<SyncResult> SynchronizeChannelAsync(Guid subscriptionId);

    /// <summary>
    /// Synchronize all active channel subscriptions
    /// </summary>
    Task<List<SyncResult>> SynchronizeAllChannelsAsync();

    /// <summary>
    /// Remove a channel subscription
    /// </summary>
    Task RemoveSubscriptionAsync(Guid subscriptionId);

    /// <summary>
    /// Pause a channel subscription
    /// </summary>
    Task PauseSubscriptionAsync(Guid subscriptionId);

    /// <summary>
    /// Resume a paused channel subscription
    /// </summary>
    Task ResumeSubscriptionAsync(Guid subscriptionId);

    /// <summary>
    /// Get recent content for a specific channel subscription
    /// </summary>
    Task<List<ChannelContentItem>> GetRecentContentAsync(
        Guid subscriptionId, 
        int maxItems = 10);

    /// <summary>
    /// Download specific content items from a channel
    /// </summary>
    Task<List<Download>> DownloadContentItemsAsync(
        Guid subscriptionId, 
        List<Guid> contentItemIds);
}

/// <summary>
/// Represents the result of a channel synchronization
/// </summary>
public class SyncResult
{
    public Guid SubscriptionId { get; set; }
    public string ChannelName { get; set; }
    public int NewContentItemsFound { get; set; }
    public int ContentItemsDownloaded { get; set; }
    public DateTime SyncTime { get; set; }
    public bool IsSuccessful { get; set; }
    public string ErrorMessage { get; set; }
}
