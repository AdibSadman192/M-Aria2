using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using MAria2.Core.Entities;
using MAria2.Core.Interfaces;
using MAria2.IntegrationTests.TestConfiguration;

namespace MAria2.IntegrationTests.IntegrationTests;

public class PerformanceStressTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDownloadQueueService _downloadQueueService;
    private readonly ILoggingService _loggingService;

    public PerformanceStressTests()
    {
        _serviceProvider = TestDependencyInjection.CreateTestServiceProvider();
        _downloadQueueService = _serviceProvider.GetRequiredService<IDownloadQueueService>();
        _loggingService = _serviceProvider.GetRequiredService<ILoggingService>();
    }

    [Fact]
    public async Task ConcurrentDownloads_ShouldHandleHighLoad()
    {
        // Arrange
        const int concurrentDownloads = 20;
        var tempPath = Path.Combine(Path.GetTempPath(), "stress_test");
        Directory.CreateDirectory(tempPath);

        var downloads = Enumerable.Range(1, concurrentDownloads)
            .Select(i => new Download
            {
                Url = $"https://example.com/testfile{i}.zip",
                DestinationPath = Path.Combine(tempPath, $"download_{i}.zip")
            })
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        // Act
        var downloadTasks = downloads.Select(d => _downloadQueueService.QueueDownloadAsync(d));
        var results = await Task.WhenAll(downloadTasks);

        stopwatch.Stop();

        // Assert
        Assert.Equal(concurrentDownloads, results.Length);
        Assert.All(results, download => 
        {
            Assert.NotNull(download);
            Assert.True(download.Status != Core.Enums.DownloadStatus.Failed, 
                $"Download failed: {download.Url}");
        });

        // Performance logging
        _loggingService.LogInformation(
            $"Stress Test: {concurrentDownloads} concurrent downloads " +
            $"completed in {stopwatch.ElapsedMilliseconds} ms"
        );

        // Performance threshold (adjust as needed)
        Assert.True(stopwatch.ElapsedMilliseconds < 60000, 
            "Concurrent downloads took too long");
    }

    [Fact]
    public async Task LongRunningDownload_ShouldMaintainStability()
    {
        // Arrange
        var largeFileUrl = "https://example.com/largefile.iso";
        var destinationPath = Path.Combine(
            Path.GetTempPath(), 
            $"long_running_test_{Guid.NewGuid():N}.iso"
        );

        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        // Act
        var download = new Download
        {
            Url = largeFileUrl,
            DestinationPath = destinationPath
        };

        var downloadTask = _downloadQueueService.QueueDownloadAsync(
            download, 
            cancellationTokenSource.Token
        );

        try 
        {
            await downloadTask;
        }
        catch (OperationCanceledException)
        {
            // Expected if download takes too long
            _loggingService.LogWarning("Long-running download test cancelled");
        }

        // Assert
        Assert.False(cancellationTokenSource.IsCancellationRequested, 
            "Download was unexpectedly cancelled");
        
        if (File.Exists(destinationPath))
        {
            var fileInfo = new FileInfo(destinationPath);
            Assert.True(fileInfo.Length > 0, "Downloaded file is empty");
        }
    }

    [Fact]
    public async Task MemoryUsage_ShouldRemainStable()
    {
        // Arrange
        const int downloadBatches = 5;
        const int downloadsPerBatch = 10;
        var tempPath = Path.Combine(Path.GetTempPath(), "memory_test");
        Directory.CreateDirectory(tempPath);

        var initialMemory = Process.GetCurrentProcess().WorkingSet64;

        // Act
        for (int batch = 0; batch < downloadBatches; batch++)
        {
            var downloads = Enumerable.Range(1, downloadsPerBatch)
                .Select(i => new Download
                {
                    Url = $"https://example.com/memtest_{batch}_{i}.zip",
                    DestinationPath = Path.Combine(tempPath, $"memtest_{batch}_{i}.zip")
                })
                .ToList();

            var downloadTasks = downloads.Select(d => _downloadQueueService.QueueDownloadAsync(d));
            await Task.WhenAll(downloadTasks);

            // Optional: Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        var finalMemory = Process.GetCurrentProcess().WorkingSet64;

        // Assert
        _loggingService.LogInformation(
            $"Memory Usage: Initial={initialMemory}, Final={finalMemory}"
        );

        // Allow up to 100% increase in memory usage
        Assert.True(
            finalMemory < initialMemory * 2, 
            "Memory usage increased beyond acceptable threshold"
        );
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
