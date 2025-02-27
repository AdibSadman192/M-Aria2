using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MAria2.Presentation.WinUI.Network;
using MAria2.Presentation.WinUI.Security;

namespace MAria2.Presentation.WinUI.CrossPlatform
{
    public static class PlatformCompatibilityValidator
    {
        public static void ValidateCrossPlatformManagers(ILogger logger)
        {
            try 
            {
                ValidateNetworkManager(logger);
                ValidateSecurityManager(logger);
                ValidatePlatformDetection(logger);
            }
            catch (Exception ex)
            {
                logger.LogError($"Platform compatibility validation failed: {ex.Message}");
                throw;
            }
        }

        private static void ValidateNetworkManager(ILogger logger)
        {
            var networkManager = CrossPlatformNetworkManagerFactory.Create(logger);
            
            logger.LogInformation($"Network Manager Type: {networkManager.GetType().Name}");
            
            var networkInfo = networkManager.GetNetworkInfo();
            if (networkInfo == null)
                throw new InvalidOperationException("Network information retrieval failed");

            logger.LogInformation($"Hostname: {networkInfo.Hostname}");
            logger.LogInformation($"Network Connected: {networkInfo.IsConnected}");
            logger.LogInformation($"Network Type: {networkInfo.NetworkType}");
        }

        private static void ValidateSecurityManager(ILogger logger)
        {
            var securityManager = CrossPlatformSecurityManagerFactory.Create(logger);
            
            logger.LogInformation($"Security Manager Type: {securityManager.GetType().Name}");
            
            bool isAdmin = securityManager.IsAdministrator();
            logger.LogInformation($"Administrator Status: {isAdmin}");

            // Test secure token generation
            string token = securityManager.GenerateSecureToken();
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Secure token generation failed");
        }

        private static void ValidatePlatformDetection(ILogger logger)
        {
            logger.LogInformation("Performing Platform Detection Validation");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logger.LogInformation("Platform Detected: Windows");
                ValidateWindowsPlatform(logger);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                logger.LogInformation("Platform Detected: macOS");
                ValidateMacOSPlatform(logger);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                logger.LogInformation("Platform Detected: Linux");
                ValidateLinuxPlatform(logger);
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported operating system");
            }
        }

        private static void ValidateWindowsPlatform(ILogger logger)
        {
            try 
            {
                var windowsNetworkManager = new WindowsNetworkManager(logger as ILogger<WindowsNetworkManager>);
                var windowsSecurityManager = new WindowsSecurityManager(logger as ILogger<WindowsSecurityManager>);

                var networkInfo = windowsNetworkManager.GetNetworkInfo();
                logger.LogInformation($"Windows Network Info: {networkInfo?.NetworkType ?? "Unknown"}");

                bool isAdmin = windowsSecurityManager.IsAdministrator();
                logger.LogInformation($"Windows Administrator Status: {isAdmin}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Windows platform validation failed: {ex.Message}");
                throw;
            }
        }

        private static void ValidateMacOSPlatform(ILogger logger)
        {
            try 
            {
                var macOSNetworkManager = new MacOSNetworkManager(logger as ILogger<MacOSNetworkManager>);
                var macOSSecurityManager = new MacOSSecurityManager(logger as ILogger<MacOSSecurityManager>);

                var networkInfo = macOSNetworkManager.GetNetworkInfo();
                logger.LogInformation($"macOS Network Info: {networkInfo?.NetworkType ?? "Unknown"}");

                bool isAdmin = macOSSecurityManager.IsAdministrator();
                logger.LogInformation($"macOS Administrator Status: {isAdmin}");
            }
            catch (Exception ex)
            {
                logger.LogError($"macOS platform validation failed: {ex.Message}");
                throw;
            }
        }

        private static void ValidateLinuxPlatform(ILogger logger)
        {
            try 
            {
                var linuxNetworkManager = new LinuxNetworkManager(logger as ILogger<LinuxNetworkManager>);
                var linuxSecurityManager = new LinuxSecurityManager(logger as ILogger<LinuxSecurityManager>);

                var networkInfo = linuxNetworkManager.GetNetworkInfo();
                logger.LogInformation($"Linux Network Info: {networkInfo?.NetworkType ?? "Unknown"}");

                bool isAdmin = linuxSecurityManager.IsAdministrator();
                logger.LogInformation($"Linux Administrator Status: {isAdmin}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Linux platform validation failed: {ex.Message}");
                throw;
            }
        }
    }
}
