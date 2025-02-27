using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System.IO;

namespace MAria2.Presentation.WinUI.Configuration
{
    public class CrossPlatformConfigurationProvider
    {
        private readonly IConfiguration _configuration;

        public CrossPlatformConfigurationProvider()
        {
            _configuration = BuildConfiguration();
        }

        private IConfiguration BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(GetConfigurationBasePath())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{GetPlatformIdentifier()}.json", optional: true)
                .AddEnvironmentVariables();

            return builder.Build();
        }

        private string GetConfigurationBasePath()
        {
            return AppContext.BaseDirectory;
        }

        private string GetPlatformIdentifier()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "windows";
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macos";
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "linux";

            return "unknown";
        }

        public T GetSection<T>(string key) where T : new()
        {
            var section = _configuration.GetSection(key);
            var config = new T();
            section.Bind(config);
            return config;
        }

        public string GetConnectionString(string name)
        {
            return _configuration.GetConnectionString(name);
        }

        public bool TryGetValue(string key, out string value)
        {
            value = _configuration[key];
            return !string.IsNullOrEmpty(value);
        }
    }

    public static class ConfigurationExtensions
    {
        public static IConfigurationBuilder AddCrossPlatformJsonFile(
            this IConfigurationBuilder builder, 
            string fileName, 
            bool optional = false, 
            bool reloadOnChange = false)
        {
            var platformIdentifier = GetPlatformSpecificFileName(fileName);
            
            builder.AddJsonFile(fileName, optional, reloadOnChange);
            
            if (!string.IsNullOrEmpty(platformIdentifier))
            {
                builder.AddJsonFile(platformIdentifier, true, reloadOnChange);
            }

            return builder;
        }

        private static string GetPlatformSpecificFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            string platformSuffix = GetPlatformSuffix();
            
            return platformSuffix != null 
                ? $"{fileNameWithoutExtension}.{platformSuffix}{extension}" 
                : null;
        }

        private static string GetPlatformSuffix()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "windows";
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macos";
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "linux";

            return null;
        }
    }
}
