using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace MAria2.Presentation.WinUI.Logging
{
    public static class CrossPlatformLoggingConfig
    {
        public static ILoggerFactory ConfigureCrossPlatformLogging(IConfiguration configuration)
        {
            // Platform-specific log path determination
            string logPath = DetermineLogPath();

            // Configure Serilog with cross-platform settings
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    theme: GetConsoleTheme(),
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.File(
                    path: logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 10,
                    fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB
                    rollOnFileSizeLimit: true
                )
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            return new LoggerFactory()
                .AddSerilog(dispose: true);
        }

        private static string DetermineLogPath()
        {
            string baseLogPath = GetPlatformSpecificLogDirectory();
            return System.IO.Path.Combine(baseLogPath, "MAria2-.log");
        }

        private static string GetPlatformSpecificLogDirectory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return System.IO.Path.Combine(
                    Environment.GetEnvironmentVariable("HOME"), 
                    "Library", 
                    "Logs"
                );
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "/var/log/maria2";
            }

            return AppContext.BaseDirectory;
        }

        private static ConsoleTheme GetConsoleTheme()
        {
            // Adaptive console theme based on platform
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ConsoleThemes.Literate;
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return ConsoleThemes.Grayscale;
            }
            
            return ConsoleThemes.Sixteen;
        }
    }
}
