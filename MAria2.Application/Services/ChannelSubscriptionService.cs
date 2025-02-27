using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MAria2.Core.Entities;
using MAria2.Core.Interfaces;
using MAria2.Core.Configuration;
using YoutubeDL.Net;

namespace MAria2.Application.Services;

public class ChannelSubscriptionService : IChannelSubscriptionService
{
    private readonly ILoggingService _loggingService;
    private readonly IDownloadQueueService _downloadQueueService;
    private readonly ConfigurationManager _configurationManager;
    private readonly YoutubeDLClient _youtubeDlClient;

    // In-memory storage for subscriptions (replace with persistent storage in production)
    private readonly ConcurrentDictionary<Guid, ChannelSubscription> _subscriptions = 
        new ConcurrentDictionary<Guid, ChannelSubscription>();

    public ChannelSubscriptionService(
        ILoggingService loggingService,
        IDownloadQueueService downloadQueueService,
        ConfigurationManager configurationManager)
    {
        _loggingService = loggingService;
        _downloadQueueService = downloadQueueService;
        _configurationManager = configurationManager;
        
        // Initialize YouTube-DL client for content extraction
        _youtubeDlClient = new YoutubeDLClient(new YoutubeDLOptions
        {
            DownloadPath = _configurationManager.GetApplicationDataPath()
        });
    }

    public async Task<ChannelSubscription> CreateSubscriptionAsync(
        string channelUrl, 
        SubscriptionType type, 
        ChannelDownloadConfig config = null)
    {
        try 
        {
            // Validate channel URL
            if (string.IsNullOrEmpty(channelUrl))
                throw new ArgumentException("Channel URL cannot be empty");

            // Detect platform
            var platform = DetectPlatform(channelUrl);

            // Extract channel metadata
            var channelInfo = await ExtractChannelMetadataAsync(channelUrl, platform);

            var subscription = new ChannelSubscription
            {
                ChannelUrl = channelUrl,
                ChannelName = channelInfo.Name,
                ChannelIdentifier = channelInfo.Id,
                Platform = platform,
                Type = type,
                DownloadConfig = config ?? new ChannelDownloadConfig
                {
                    DownloadPath = Path.Combine(
                        _configurationManager.GetDownloadPath(), 
                        channelInfo.Name
                    )
                }
            };

            _subscriptions[subscription.Id] = subscription;
            _loggingService.LogInformation($"Created subscription for {channelInfo.Name}");

            return subscription;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Channel subscription creation failed: {ex.Message}");
            throw;
        }
    }

    public async Task<List<ChannelSubscription>> GetActiveSubscriptionsAsync()
    {
        return _subscriptions.Values
            .Where(s => s.Status == SubscriptionStatus.Active)
            .ToList();
    }

    public async Task<ChannelSubscription> UpdateSubscriptionAsync(
        Guid subscriptionId, 
        Action<ChannelSubscription> updateAction)
    {
        if (_subscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            updateAction(subscription);
            _loggingService.LogInformation($"Updated subscription: {subscription.ChannelName}");
            return subscription;
        }

        throw new KeyNotFoundException($"Subscription {subscriptionId} not found");
    }

    public async Task<SyncResult> SynchronizeChannelAsync(Guid subscriptionId)
    {
        var syncResult = new SyncResult { SubscriptionId = subscriptionId };

        try 
        {
            var subscription = _subscriptions[subscriptionId];
            
            // Fetch recent content
            var recentContent = await FetchRecentContentAsync(
                subscription.ChannelUrl, 
                subscription.DownloadConfig.MaxItemsPerSync
            );

            // Filter and process new content
            var newContent = FilterNewContent(subscription, recentContent);
            var downloadedItems = await DownloadNewContentAsync(subscription, newContent);

            // Update subscription
            subscription.RecentContent.AddRange(newContent);
            subscription.LastSyncTime = DateTime.UtcNow;
            subscription.NextSyncTime = DateTime.UtcNow.Add(subscription.SyncInterval);

            // Prepare sync result
            syncResult.ChannelName = subscription.ChannelName;
            syncResult.NewContentItemsFound = newContent.Count;
            syncResult.ContentItemsDownloaded = downloadedItems.Count;
            syncResult.IsSuccessful = true;
            syncResult.SyncTime = DateTime.UtcNow;

            return syncResult;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Channel sync failed: {ex.Message}");
            
            syncResult.IsSuccessful = false;
            syncResult.ErrorMessage = ex.Message;
            return syncResult;
        }
    }

    public async Task<List<SyncResult>> SynchronizeAllChannelsAsync()
    {
        var activeSubscriptions = await GetActiveSubscriptionsAsync();
        
        var syncTasks = activeSubscriptions
            .Select(sub => SynchronizeChannelAsync(sub.Id))
            .ToList();

        return await Task.WhenAll(syncTasks);
    }

    public async Task RemoveSubscriptionAsync(Guid subscriptionId)
    {
        if (_subscriptions.TryRemove(subscriptionId, out _))
        {
            _loggingService.LogInformation($"Removed subscription: {subscriptionId}");
        }
        else
        {
            throw new KeyNotFoundException($"Subscription {subscriptionId} not found");
        }
    }

    public async Task PauseSubscriptionAsync(Guid subscriptionId)
    {
        await UpdateSubscriptionAsync(subscriptionId, sub => 
            sub.Status = SubscriptionStatus.Paused);
    }

    public async Task ResumeSubscriptionAsync(Guid subscriptionId)
    {
        await UpdateSubscriptionAsync(subscriptionId, sub => 
            sub.Status = SubscriptionStatus.Active);
    }

    public async Task<List<ChannelContentItem>> GetRecentContentAsync(
        Guid subscriptionId, 
        int maxItems = 10)
    {
        var subscription = _subscriptions[subscriptionId];
        return subscription.RecentContent
            .OrderByDescending(c => c.PublishedDate)
            .Take(maxItems)
            .ToList();
    }

    public async Task<List<Download>> DownloadContentItemsAsync(
        Guid subscriptionId, 
        List<Guid> contentItemIds)
    {
        var subscription = _subscriptions[subscriptionId];
        var contentItems = subscription.RecentContent
            .Where(c => contentItemIds.Contains(c.Id))
            .ToList();

        var downloads = new List<Download>();

        foreach (var item in contentItems)
        {
            try 
            {
                var download = new Download
                {
                    Url = item.Url,
                    DestinationPath = GenerateDestinationPath(subscription, item)
                };

                var queuedDownload = await _downloadQueueService.QueueDownloadAsync(download);
                downloads.Add(queuedDownload);

                item.Status = queuedDownload.Status == DownloadStatus.Completed 
                    ? ContentStatus.Downloaded 
                    : ContentStatus.Failed;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(
                    $"Failed to download content item {item.Title}: {ex.Message}"
                );
                item.Status = ContentStatus.Failed;
            }
        }

        return downloads;
    }

    private MediaPlatform DetectPlatform(string channelUrl)
    {
        var uri = new Uri(channelUrl);
        return uri.Host switch
        {
            var h when h.Contains("youtube.com") => MediaPlatform.YouTube,
            var h when h.Contains("twitch.tv") => MediaPlatform.Twitch,
            var h when h.Contains("vimeo.com") => MediaPlatform.Vimeo,
            _ => MediaPlatform.Custom
        };
    }

    private async Task<(string Id, string Name)> ExtractChannelMetadataAsync(
        string channelUrl, 
        MediaPlatform platform)
    {
        try 
        {
            // Use YouTube-DL to extract channel metadata
            var channelInfo = await _youtubeDlClient.GetChannelInfoAsync(channelUrl);
            
            return (
                Id: channelInfo.ChannelId, 
                Name: channelInfo.ChannelName
            );
        }
        catch
        {
            // Fallback to URL-based naming
            return (
                Id: Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                Name: new Uri(channelUrl).Host
            );
        }
    }

    private async Task<List<ChannelContentItem>> FetchRecentContentAsync(
        string channelUrl, 
        int maxItems)
    {
        try 
        {
            var recentContent = await _youtubeDlClient.GetRecentContentAsync(
                channelUrl, 
                maxItems
            );

            return recentContent.Select(item => new ChannelContentItem
            {
                Title = item.Title,
                Url = item.Url,
                PublishedDate = item.PublishDate,
                Duration = (long)item.Duration.TotalSeconds,
                ThumbnailUrl = item.ThumbnailUrl,
                Status = ContentStatus.New
            }).ToList();
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to fetch recent content: {ex.Message}");
            return new List<ChannelContentItem>();
        }
    }

    private List<ChannelContentItem> FilterNewContent(
        ChannelSubscription subscription, 
        List<ChannelContentItem> fetchedContent)
    {
        return fetchedContent
            .Where(item => 
                !subscription.RecentContent.Any(existing => 
                    existing.Url == item.Url) &&
                item.PublishedDate > subscription.LastSyncTime)
            .ToList();
    }

    private async Task<List<ChannelContentItem>> DownloadNewContentAsync(
        ChannelSubscription subscription, 
        List<ChannelContentItem> newContent)
    {
        var downloadedItems = new List<ChannelContentItem>();

        foreach (var item in newContent)
        {
            try 
            {
                var download = new Download
                {
                    Url = item.Url,
                    DestinationPath = GenerateDestinationPath(subscription, item)
                };

                var queuedDownload = await _downloadQueueService.QueueDownloadAsync(download);

                if (queuedDownload.Status == DownloadStatus.Completed)
                {
                    item.Status = ContentStatus.Downloaded;
                    downloadedItems.Add(item);
                }
                else
                {
                    item.Status = ContentStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(
                    $"Failed to download content item {item.Title}: {ex.Message}"
                );
                item.Status = ContentStatus.Failed;
            }
        }

        return downloadedItems;
    }

    private string GenerateDestinationPath(
        ChannelSubscription subscription, 
        ChannelContentItem item)
    {
        var config = subscription.DownloadConfig;
        var template = config.FileNamingTemplate;

        var fileName = template
            .Replace("{ChannelName}", subscription.ChannelName)
            .Replace("{PublishDate:yyyy-MM-dd}", item.PublishedDate.ToString("yyyy-MM-dd"))
            .Replace("{Title}", SanitizeFileName(item.Title))
            .Replace("{Extension}", Path.GetExtension(item.Url));

        return Path.Combine(config.DownloadPath, fileName);
    }

    private string SanitizeFileName(string fileName)
    {
        return string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
    }
}
