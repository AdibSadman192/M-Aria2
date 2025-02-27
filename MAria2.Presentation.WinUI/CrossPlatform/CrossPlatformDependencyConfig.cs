using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MAria2.Presentation.WinUI.CrossPlatform;
using MAria2.Presentation.WinUI.Network;
using MAria2.Presentation.WinUI.Security;

namespace MAria2.Presentation.WinUI.Configuration
{
    public static class CrossPlatformDependencyConfig
    {
        public static IServiceCollection AddCrossPlatformServices(this IServiceCollection services)
        {
            // Platform-specific service registration
            services.AddSingleton(CrossPlatformServiceManager.Current);

            // Add cross-platform network and security managers
            services.AddSingleton(sp => 
            {
                var logger = sp.GetRequiredService<ILogger<ICrossPlatformNetworkManager>>();
                return CrossPlatformNetworkManagerFactory.Create(logger);
            });

            services.AddSingleton(sp => 
            {
                var logger = sp.GetRequiredService<ILogger<ICrossPlatformSecurityManager>>();
                return CrossPlatformSecurityManagerFactory.Create(logger);
            });

            // Conditional service registrations based on platform
            var platformService = CrossPlatformServiceManager.Current;

            if (platformService is WindowsPlatformService)
            {
                AddWindowsSpecificServices(services);
            }
            else if (platformService is MacOSPlatformService)
            {
                AddMacOSSpecificServices(services);
            }
            else if (platformService is LinuxPlatformService)
            {
                AddLinuxSpecificServices(services);
            }

            // Common cross-platform services
            AddCommonServices(services);

            return services;
        }

        private static void AddCommonServices(IServiceCollection services)
        {
            // Services common to all platforms
            services.AddLogging();
            services.AddHttpClient();
        }

        private static void AddWindowsSpecificServices(IServiceCollection services)
        {
            // Windows-specific service registrations
            services.AddSingleton<WindowsNetworkManager>();
            services.AddSingleton<WindowsSecurityManager>();
        }

        private static void AddMacOSSpecificServices(IServiceCollection services)
        {
            // macOS-specific service registrations
            services.AddSingleton<MacOSNetworkManager>();
            services.AddSingleton<MacOSSecurityManager>();
        }

        private static void AddLinuxSpecificServices(IServiceCollection services)
        {
            // Linux-specific service registrations
            services.AddSingleton<LinuxNetworkManager>();
            services.AddSingleton<LinuxSecurityManager>();
        }

        // Platform-specific dependency validation
        public static void ValidatePlatformDependencies(IServiceProvider serviceProvider)
        {
            try 
            {
                // Attempt to resolve cross-platform managers
                var networkManager = serviceProvider.GetRequiredService<ICrossPlatformNetworkManager>();
                var securityManager = serviceProvider.GetRequiredService<ICrossPlatformSecurityManager>();

                // Perform basic platform validation
                ValidatePlatformSpecificServices(networkManager, securityManager);
            }
            catch (Exception ex)
            {
                // Log detailed dependency resolution errors
                var logger = serviceProvider.GetRequiredService<ILogger<object>>();
                logger.LogError($"Platform dependency validation failed: {ex.Message}");
                throw;
            }
        }

        private static void ValidatePlatformSpecificServices(
            ICrossPlatformNetworkManager networkManager, 
            ICrossPlatformSecurityManager securityManager)
        {
            // Platform-specific validation checks
            var networkInfo = networkManager.GetNetworkInfo();
            var isAdmin = securityManager.IsAdministrator();

            if (networkInfo == null)
                throw new InvalidOperationException("Network manager failed to retrieve network information");

            if (networkManager.GetType().Name.Contains("Windows") && !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                throw new PlatformNotSupportedException("Windows-specific network manager used on non-Windows platform");

            if (networkManager.GetType().Name.Contains("MacOS") && !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                throw new PlatformNotSupportedException("macOS-specific network manager used on non-macOS platform");

            if (networkManager.GetType().Name.Contains("Linux") && !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                throw new PlatformNotSupportedException("Linux-specific network manager used on non-Linux platform");
        }
    }
}
