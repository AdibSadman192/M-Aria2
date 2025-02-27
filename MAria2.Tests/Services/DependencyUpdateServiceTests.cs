using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using MAria2.Infrastructure.Services;
using MAria2.Core.Interfaces;
using MAria2.Core.Exceptions;

namespace MAria2.Tests.Services;

public class DependencyUpdateServiceTests
{
    private readonly Mock<ILoggingService> _mockLoggingService;
    private readonly Mock<IDialogService> _mockDialogService;

    public DependencyUpdateServiceTests()
    {
        _mockLoggingService = new Mock<ILoggingService>();
        _mockDialogService = new Mock<IDialogService>();
    }

    [Fact]
    public async Task CheckForUpdates_WhenUpdatesAvailable_ReturnsUpdateList()
    {
        // Arrange
        var tempLibPath = Path.Combine(Path.GetTempPath(), "MAria2_Test_Lib");
        Directory.CreateDirectory(tempLibPath);

        // Create a test dependencies.json
        var dependenciesPath = Path.Combine(tempLibPath, "dependencies.json");
        await File.WriteAllTextAsync(dependenciesPath, JsonSerializer.Serialize(new List<DependencyConfig>
        {
            new DependencyConfig 
            { 
                Name = "Aria2", 
                RequiredRuntimeVersion = "1.35.0" 
            }
        }));

        // Mock HttpClient to return test update config
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(), 
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new List<DependencyUpdateInfo>
                {
                    new DependencyUpdateInfo
                    {
                        Name = "Aria2",
                        Version = "1.36.0",
                        DownloadUrl = "https://example.com/aria2-1.36.0.zip",
                        SupportedArchitectures = new[] { "X64" }
                    }
                }))
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var updateService = new DependencyUpdateService(
            _mockLoggingService.Object, 
            _mockDialogService.Object, 
            tempLibPath
        );

        // Act
        var updates = await updateService.CheckForUpdatesAsync();

        // Assert
        Assert.Single(updates);
        Assert.Equal("Aria2", updates[0].Name);
        Assert.Equal("1.36.0", updates[0].Version);
    }

    [Fact]
    public async Task UpdateDependency_WhenCompatible_SuccessfullyUpdates()
    {
        // Arrange
        var tempLibPath = Path.Combine(Path.GetTempPath(), "MAria2_Test_Lib");
        Directory.CreateDirectory(tempLibPath);

        // Mock HttpClient for download
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(), 
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(new byte[1024]) // Simulate download
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);

        var updateService = new DependencyUpdateService(
            _mockLoggingService.Object, 
            _mockDialogService.Object, 
            tempLibPath
        );

        var updateInfo = new DependencyUpdateInfo
        {
            Name = "TestDependency",
            Version = "1.0.0",
            DownloadUrl = "https://example.com/test.zip",
            ExpectedHash = ComputeTestHash(new byte[1024]),
            SupportedArchitectures = new[] { "X64" }
        };

        // Act
        var result = await updateService.UpdateDependencyAsync(updateInfo);

        // Assert
        Assert.True(result);
        _mockLoggingService.Verify(
            l => l.LogInfo(It.Is<string>(s => s.Contains("Successfully updated"))), 
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateDependency_WhenIncompatible_ThrowsException()
    {
        // Arrange
        var tempLibPath = Path.Combine(Path.GetTempPath(), "MAria2_Test_Lib");
        Directory.CreateDirectory(tempLibPath);

        var updateService = new DependencyUpdateService(
            _mockLoggingService.Object, 
            _mockDialogService.Object, 
            tempLibPath
        );

        var updateInfo = new DependencyUpdateInfo
        {
            Name = "IncompatibleDependency",
            Version = "1.0.0",
            DownloadUrl = "https://example.com/incompatible.zip",
            SupportedArchitectures = new[] { "ARM" } // Incompatible with X64
        };

        // Act & Assert
        await Assert.ThrowsAsync<DependencyCompatibilityException>(
            () => updateService.UpdateDependencyAsync(updateInfo)
        );
    }

    private string ComputeTestHash(byte[] data)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
