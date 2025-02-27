using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Moq;
using MAria2.Core.Entities;
using MAria2.Core.Interfaces;
using MAria2.Core.Enums;
using MAria2.IntegrationTests.TestConfiguration;

namespace MAria2.IntegrationTests.IntegrationTests;

public class DownloadOrchestrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDownloadQueueService _downloadQueueService;
    private readonly EngineSelectionService _engineSelectionService;
    private readonly INotificationService _notificationService;
    private readonly ILoggingService _loggingService;

    public DownloadOrchestrationTests()
    {
        _serviceProvider = TestDependencyInjection.CreateTestServiceProvider();
        
        _downloadQueueService = _serviceProvider.GetRequiredService<IDownloadQueueService>();
        _engineSelectionService = _serviceProvider.GetRequiredService<EngineSelectionService>();
        _notificationService = _serviceProvider.GetRequiredService<INotificationService>();
        _loggingService = _serviceProvider.GetRequiredService<ILoggingService>();
    }

    [Fact]
    public async Task DownloadQueue_ShouldHandleMultipleDownloads()
    {
        // Arrange
        var downloads = new[]
        {
            new Download 
            { 
                Url = "https://example.com/video1.mp4", 
                DestinationPath = Path.Combine(Path.GetTempPath(), "test1.mp4") 
            },
            new Download 
            { 
                Url = "https://example.com/audio1.mp3", 
                DestinationPath = Path.Combine(Path.GetTempPath(), "test2.mp3") 
            }
        };

        // Act
        var queueTasks = downloads.Select(d => _downloadQueueService.QueueDownloadAsync(d));
        var results = await Task.WhenAll(queueTasks);

        // Assert
        Assert.All(results, download => 
        {
            Assert.NotNull(download);
            Assert.NotEqual(DownloadStatus.Failed, download.Status);
        });
    }

    [Fact]
    public async Task EngineSelection_ShouldChooseBestEngine()
    {
        // Arrange
        var testUrls = new[]
        {
            "https://www.youtube.com/watch?v=test",
            "https://example.com/largefile.zip",
            "https://podcast.example.com/episode.mp3"
        };

        // Act
        var engineSelectionTasks = testUrls.Select(async url => 
        {
            var request = new DownloadRequest(url);
            return await _engineSelectionService.SelectBestEngineAsync(request);
        });

        var selectedEngines = await Task.WhenAll(engineSelectionTasks);

        // Assert
        Assert.All(selectedEngines, engine => 
        {
            Assert.NotNull(engine);
            Assert.True(engine.CanHandleProtocol(engine.Version));
        });
    }

    [Fact]
    public async Task NotificationService_ShouldDispatchEvents()
    {
        // Arrange
        var download = new Download 
        { 
            Url = "https://example.com/test.mp4",
            DestinationPath = Path.Combine(Path.GetTempPath(), "notification_test.mp4")
        };

        var notificationReceived = false;
        _notificationService.RegisterNotificationHandler(
            NotificationType.DownloadStarted, 
            async (d) => 
            {
                notificationReceived = true;
                await Task.CompletedTask;
            }
        );

        // Act
        await _downloadQueueService.QueueDownloadAsync(download);

        // Assert
        Assert.True(notificationReceived, "Notification was not received");
    }

    [Fact]
    public async Task SplitDownload_ShouldHandleLargeFiles()
    {
        // Arrange
        var splitDownloadManager = _serviceProvider.GetRequiredService<SplitDownloadManager>();
        var largeFileUrl = "https://example.com/largefile.zip";
        var destinationPath = Path.Combine(Path.GetTempPath(), "large_split_download.zip");

        // Act
        var download = await splitDownloadManager.StartSplitDownloadAsync(
            largeFileUrl, 
            destinationPath, 
            segments: 4
        );

        // Assert
        Assert.NotNull(download);
        Assert.Equal(DownloadStatus.Completed, download.Status);
        Assert.True(File.Exists(destinationPath));
    }

    [Fact]
    public async Task PlaylistManagement_ShouldCreateAndDownloadPlaylist()
    {
        // Arrange
        var playlistService = _serviceProvider.GetRequiredService<IPlaylistManagementService>();
        var destinationPath = Path.Combine(Path.GetTempPath(), "playlist_downloads");
        Directory.CreateDirectory(destinationPath);

        // Create playlist
        var playlist = await playlistService.CreatePlaylistAsync("Test Playlist", PlaylistType.Music);
        
        // Add items
        await playlistService.AddItemToPlaylistAsync(playlist.Id, new PlaylistItem 
        { 
            Url = "https://example.com/song1.mp3", 
            Title = "Test Song 1" 
        });
        await playlistService.AddItemToPlaylistAsync(playlist.Id, new PlaylistItem 
        { 
            Url = "https://example.com/song2.mp3", 
            Title = "Test Song 2" 
        });

        // Act
        var downloadedPlaylist = await playlistService.DownloadPlaylistAsync(playlist.Id, destinationPath);

        // Assert
        Assert.NotNull(downloadedPlaylist);
        Assert.Equal(2, downloadedPlaylist.Items.Count);
        Assert.All(downloadedPlaylist.Items, item => 
        {
            var expectedPath = Path.Combine(destinationPath, $"{item.Title}.mp3");
            Assert.True(File.Exists(expectedPath), $"File {expectedPath} not found");
        });
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
