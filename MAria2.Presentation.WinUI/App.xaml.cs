using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MAria2.Application;
using MAria2.Infrastructure;
using MAria2.Presentation.WinUI.Services;
using MAria2.Presentation.WinUI.Configuration;
using MAria2.Presentation.WinUI.CrossPlatform;
using Serilog;

namespace MAria2.Presentation.WinUI;

public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; }
    private readonly ErrorHandlingService _errorHandlingService;
    private readonly SettingsService _settingsService;
    private readonly NotificationService _notificationService;
    private readonly IDependencyUpdateService _dependencyUpdateService;
    private readonly ILogger<App> _logger;

    public App()
    {
        InitializeComponent();

        // Configure Logging
        ConfigureLogging();

        // Configure Dependency Injection
        ServiceProvider = ConfigureServices();

        // Validate Platform Compatibility
        ValidatePlatformCompatibility();

        // Resolve services
        _errorHandlingService = ServiceProvider.GetRequiredService<ErrorHandlingService>();
        _settingsService = ServiceProvider.GetRequiredService<SettingsService>();
        _notificationService = ServiceProvider.GetRequiredService<NotificationService>();
        _logger = ServiceProvider.GetRequiredService<ILogger<App>>();

        // Set up main window
        Title = "M-Aria2 Universal Download Manager";
        Content = new MainWindow();
        
        // Set window size and position
        Width = 1200;
        Height = 800;
        CenterOnScreen();

        // Apply theme
        ApplyTheme();

        // Set up global error handling
        SetupGlobalExceptionHandling();
    }

    private void ConfigureLogging()
    {
        // Configure Serilog for comprehensive logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/maria2_platform_log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    private IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(configure => 
        {
            configure.AddSerilog(dispose: true);
            configure.AddConsole();
        });

        // Add application and infrastructure services
        services
            .AddApplicationServices(
                // Configure download queue
                downloadQueue => 
                {
                    downloadQueue.MaxConcurrentDownloads = 5;
                }
            )
            .AddInfrastructureServices(
                // Optional Aria2 configuration
                aria2Config => 
                {
                    aria2Config.Host = "localhost";
                    aria2Config.Port = 6800;
                },
                // Optional database path
                databasePath: null
            )
            .AddPresentationServices(
                // Configure app settings
                settings =>
                {
                    settings.MaxConcurrentDownloads = 5;
                    settings.DefaultDownloadPath = 
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + 
                        "\\Downloads";
                }
            )
            .AddCrossPlatformServices(); // Add cross-platform service registration

        // Register dependency verification service
        services.AddSingleton<IDependencyVerificationService>(sp => 
            new DependencyVerificationService(
                sp.GetRequiredService<ILoggingService>()
            )
        );

        // Register dependency update service
        services.AddSingleton<IDependencyUpdateService, DependencyUpdateService>();

        // Add presentation layer services
        services.AddSingleton(this);

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();

        return serviceProvider;
    }

    private void ValidatePlatformCompatibility()
    {
        try 
        {
            // Validate platform-specific dependencies
            CrossPlatformDependencyConfig.ValidatePlatformDependencies(ServiceProvider);

            // Perform comprehensive platform compatibility check
            PlatformCompatibilityValidator.ValidateCrossPlatformManagers(
                ServiceProvider.GetRequiredService<ILogger<App>>()
            );

            _logger?.LogInformation("Platform compatibility validation successful");
        }
        catch (Exception ex)
        {
            // Log critical platform compatibility failure
            _logger?.LogCritical(
                $"Platform compatibility validation failed: {ex.Message}\n{ex.StackTrace}"
            );

            // Show critical error notification
            ShowPlatformCompatibilityErrorNotification(ex);

            // Optionally terminate the application
            Environment.Exit(1);
        }
    }

    private void ShowPlatformCompatibilityErrorNotification(Exception ex)
    {
        // Implement a cross-platform error notification mechanism
        // This could be a system dialog, logging, or other notification method
        _notificationService?.ShowCriticalErrorNotification(
            "Platform Compatibility Error", 
            $"The application cannot start due to platform compatibility issues.\n\nError: {ex.Message}"
        );
    }

    private void ApplyTheme()
    {
        var settings = _settingsService.GetAppSettings();
        
        // Apply theme based on settings
        RequestedTheme = settings.IsDarkModeEnabled 
            ? ElementTheme.Dark 
            : ElementTheme.Light;
    }

    private void SetupGlobalExceptionHandling()
    {
        // Global unhandled exception handlers
        AppDomain.CurrentDomain.UnhandledException += (s, e) => 
            HandleUnhandledException(e.ExceptionObject as Exception);

        // WinUI specific unhandled exception handler
        UnhandledException += (s, e) => 
        {
            e.Handled = true;
            HandleUnhandledException(e.Exception);
        };
    }

    private void HandleUnhandledException(Exception ex)
    {
        if (ex == null) return;

        // Log the error
        _errorHandlingService.TrackEvent("UnhandledException", new Dictionary<string, string>
        {
            { "Message", ex.Message },
            { "StackTrace", ex.StackTrace }
        });

        // Show error dialog
        _errorHandlingService.ShowErrorDialogAsync(
            "Unexpected Error", 
            "An unexpected error occurred in the application.", 
            ex
        ).Wait();
    }

    private void CenterOnScreen()
    {
        // TODO: Implement proper screen centering for WinUI 3
        // This is a placeholder and may need adjustment
        var displayInfo = DisplayArea.GetFromWindowId(
            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WindowHandle), 
            DisplayAreaFallback.Primary);

        var bounds = displayInfo.WorkArea;
        Left = (bounds.Width - Width) / 2;
        Top = (bounds.Height - Height) / 2;
    }

    private async Task VerifyDependenciesAsync()
    {
        var dependencyService = ServiceProvider.GetRequiredService<IDependencyVerificationService>();
        var verificationResult = await dependencyService.VerifyDependenciesAsync();

        switch (verificationResult.OverallStatus)
        {
            case DependencyVerificationStatus.Failed:
                var failedDependencies = string.Join(", ", 
                    verificationResult.DependencyResults
                        .Where(r => !r.Value.IsValid)
                        .Select(r => r.Key)
                );
                
                var dialogService = ServiceProvider.GetRequiredService<IDialogService>();
                await dialogService.ShowErrorAsync(
                    "Dependency Verification Failed", 
                    $"The following dependencies failed verification: {failedDependencies}. " +
                    "Please reinstall or update the application."
                );
                
                // Optionally, exit the application
                Application.Current.Exit();
                break;

            case DependencyVerificationStatus.Error:
                var loggingService = ServiceProvider.GetRequiredService<ILoggingService>();
                loggingService.LogError("Critical error during dependency verification");
                break;
        }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Perform dependency verification before full startup
        await VerifyDependenciesAsync();

        // Start periodic dependency update checks
        _dependencyUpdateService = ServiceProvider.GetRequiredService<IDependencyUpdateService>();
        
        // Check for updates every 24 hours
        _ = _dependencyUpdateService.StartPeriodicUpdateCheckAsync(
            TimeSpan.FromHours(24)
        );

        base.OnLaunched(args);
    }

    protected override void OnExit()
    {
        // Dispose of update service to cancel background tasks
        _dependencyUpdateService?.Dispose();
        base.OnExit();
    }

    public void OpenDependencyManagementPage()
    {
        var dependencyService = ServiceProvider.GetRequiredService<IDependencyVerificationService>();
        var dialogService = ServiceProvider.GetRequiredService<IDialogService>();
        var loggingService = ServiceProvider.GetRequiredService<ILoggingService>();

        var dependencyManagementPage = new DependencyManagementPage(
            dependencyService, 
            dialogService, 
            loggingService
        );

        dependencyManagementPage.Activate();
    }
}
