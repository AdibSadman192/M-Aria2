using System;
using System.Threading.Tasks;
using Moq;
using Xunit;
using MAria2.Core.Entities;
using MAria2.Core.Enums;
using MAria2.Core.Interfaces;
using MAria2.Core.Configuration;
using MAria2.Infrastructure.Engines;

namespace MAria2.Tests.Infrastructure;

public class Aria2DownloadEngineTests : IAsyncLifetime
{
    private readonly Mock<ILoggingService> _mockLoggingService;
    private readonly Mock<ConfigurationManager> _mockConfigManager;
    private readonly Aria2DownloadEngine _aria2Engine;

    public Aria2DownloadEngineTests()
    {
        // Setup mock dependencies
        _mockLoggingService = new Mock<ILoggingService>();
        _mockConfigManager = new Mock<ConfigurationManager>(_mockLoggingService.Object);

        // Configure default engine configuration
        _mockConfigManager
            .Setup(c => c.GetEngineConfiguration(EngineType.Aria2))
            .Returns(new EngineConfiguration 
            { 
                Type = EngineType.Aria2,
                Enabled = true,
                MaxConnections = 5,
                Timeout = 30
            });

        // Create Aria2 engine instance
        _aria2Engine = new Aria2DownloadEngine(
            _mockLoggingService.Object, 
            _mockConfigManager.Object
        );
    }

    public async Task InitializeAsync()
    {
        // Initialize the engine before tests
        await _aria2Engine.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        // Shutdown the engine after tests
        await _aria2Engine.ShutdownAsync();
    }

    [Fact]
    public void Engine_ShouldHaveCorrectType()
    {
        Assert.Equal(EngineType.Aria2, _aria2Engine.Type);
        Assert.NotNull(_aria2Engine.Version);
    }

    [Theory]
    [InlineData("http://example.com/file.zip", true)]
    [InlineData("https://example.com/file.zip", true)]
    [InlineData("ftp://example.com/file.zip", true)]
    [InlineData("sftp://example.com/file.zip", true)]
    [InlineData("torrent://example.com/file.torrent", false)]
    public void CanHandleProtocol_ShouldWorkCorrectly(string url, bool expectedResult)
    {
        Assert.Equal(expectedResult, _aria2Engine.CanHandleProtocol(url));
    }

    [Fact]
    public async Task TestPerformanceAsync_ShouldReturnValidResult()
    {
        var testUrl = "https://example.com/testfile";
        var performanceResult = await _aria2Engine.TestPerformanceAsync(testUrl);

        Assert.NotNull(performanceResult);
        Assert.True(performanceResult.SpeedMbps > 0);
        Assert.True(performanceResult.ConnectionStability > 0 && performanceResult.ConnectionStability <= 1);
        Assert.NotEmpty(performanceResult.SupportedProtocols);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidToken_ShouldSucceed()
    {
        var credentials = new AuthenticationCredentials(Token: "test-token");
        var authResult = await _aria2Engine.AuthenticateAsync(credentials);

        Assert.True(authResult);
    }

    [Fact]
    public async Task ConfigureNetworkAsync_ShouldApplyConfiguration()
    {
        var networkConfig = new NetworkConfiguration
        {
            UseProxy = true,
            ProxyAddress = "http://proxy.example.com:8080",
            MaxConnections = 10,
            ConnectionTimeout = 45
        };

        await _aria2Engine.ConfigureNetworkAsync(networkConfig);

        // Verify logging of configuration
        _mockLoggingService.Verify(
            l => l.LogInformation(It.IsAny<string>()),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task SplitDownloadAsync_ShouldSupportMultiSegmentDownload()
    {
        var download = new Download
        {
            Url = "https://example.com/largefile.zip",
            DestinationPath = @"C:\Downloads",
            FileName = "largefile.zip"
        };

        var splitDownload = await _aria2Engine.SplitDownloadAsync(download, 4);

        Assert.NotNull(splitDownload.EngineSpecificId);
        Assert.Equal(download.Url, splitDownload.Url);
    }

    [Fact]
    public void GetPriority_ShouldRankProtocolsProperly()
    {
        var protocols = new[]
        {
            "https://example.com",
            "http://example.com",
            "ftp://example.com",
            "sftp://example.com",
            "unknown://example.com"
        };

        var priorities = protocols.Select(p => _aria2Engine.GetPriority(p)).ToArray();

        Assert.True(priorities[0] > priorities[1]); // https > http
        Assert.True(priorities[1] > priorities[2]); // http > ftp
        Assert.True(priorities[2] > priorities[3]); // ftp > sftp
        Assert.True(priorities[4] == 0); // unknown protocol
    }

    [Fact]
    public async Task AnalyzeContentAsync_ShouldProvideMetadata()
    {
        var testUrl = "https://example.com/sample.zip";
        var metadata = await _aria2Engine.AnalyzeContentAsync(testUrl);

        Assert.NotNull(metadata);
        Assert.Equal(testUrl, metadata.Url);
        Assert.NotNull(metadata.FileName);
    }

    // Error handling tests
    [Fact]
    public async Task StartDownloadAsync_WithInvalidUrl_ShouldThrowException()
    {
        var invalidDownload = new Download { Url = "invalid-url" };

        await Assert.ThrowsAsync<Exception>(() => 
            _aria2Engine.StartDownloadAsync(invalidDownload)
        );
    }
}

// Additional mock setup for complex scenarios
public class Aria2TestFixture : IDisposable
{
    public Mock<ILoggingService> LoggingService { get; }
    public Mock<ConfigurationManager> ConfigManager { get; }

    public Aria2TestFixture()
    {
        LoggingService = new Mock<ILoggingService>();
        ConfigManager = new Mock<ConfigurationManager>(LoggingService.Object);
    }

    public void Dispose()
    {
        // Cleanup resources if needed
    }
}
