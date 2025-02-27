using System;
using Microsoft.Extensions.DependencyInjection;
using MAria2.Core.Interfaces;
using MAria2.Infrastructure.Services;
using System.Runtime.InteropServices;

namespace MAria2.Presentation.Configuration
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPlatformAbstractionServices(this IServiceCollection services)
        {
            // Register Platform Abstraction Service
            services.AddSingleton<IPlatformAbstractionService, PlatformAbstractionService>();

            // Conditional service registration based on platform
            var platformService = new PlatformAbstractionService(null);
            var currentOS = platformService.GetOperatingSystem();

            switch (currentOS)
            {
                case OperatingSystemType.Windows:
                    RegisterWindowsSpecificServices(services);
                    break;
                case OperatingSystemType.MacOS:
                    RegisterMacOSSpecificServices(services);
                    break;
                case OperatingSystemType.Linux:
                    RegisterLinuxSpecificServices(services);
                    break;
            }

            return services;
        }

        private static void RegisterWindowsSpecificServices(IServiceCollection services)
        {
            // Windows-specific service registrations
            services.AddSingleton<IWindowsNativeInterop, WindowsNativeInterop>();
        }

        private static void RegisterMacOSSpecificServices(IServiceCollection services)
        {
            // MacOS-specific service registrations
            services.AddSingleton<IMacOSNativeInterop, MacOSNativeInterop>();
        }

        private static void RegisterLinuxSpecificServices(IServiceCollection services)
        {
            // Linux-specific service registrations
            services.AddSingleton<ILinuxNativeInterop, LinuxNativeInterop>();
        }

        // Placeholder interfaces for platform-specific interop
        public interface IWindowsNativeInterop { }
        public interface IMacOSNativeInterop { }
        public interface ILinuxNativeInterop { }

        // Minimal implementation placeholders
        public class WindowsNativeInterop : IWindowsNativeInterop { }
        public class MacOSNativeInterop : IMacOSNativeInterop { }
        public class LinuxNativeInterop : ILinuxNativeInterop { }
    }
}
