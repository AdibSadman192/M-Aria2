using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MAria2.Presentation.WinUI.CrossPlatform;
using MAria2.Presentation.WinUI.Network;
using MAria2.Presentation.WinUI.Security;

namespace MAria2.Tests
{
    [TestClass]
    [TestCategory("PlatformCompatibility")]
    public class PlatformCompatibilityTests
    {
        private IServiceProvider _serviceProvider;
        private ILogger<PlatformCompatibilityTests> _logger;

        [TestInitialize]
        public void Initialize()
        {
            var services = new ServiceCollection();
            
            // Configure logging
            services.AddLogging(configure => 
            {
                configure.AddConsole();
                configure.SetMinimumLevel(LogLevel.Debug);
            });

            // Add cross-platform services
            services.AddSingleton(sp => 
                CrossPlatformNetworkManagerFactory.Create(
                    sp.GetRequiredService<ILogger<ICrossPlatformNetworkManager>>()
                )
            );

            services.AddSingleton(sp => 
                CrossPlatformSecurityManagerFactory.Create(
                    sp.GetRequiredService<ILogger<ICrossPlatformSecurityManager>>()
                )
            );

            _serviceProvider = services.BuildServiceProvider();
            _logger = _serviceProvider.GetRequiredService<ILogger<PlatformCompatibilityTests>>();
        }

        [TestMethod]
        public void TestNetworkManagerCompatibility()
        {
            var networkManager = _serviceProvider.GetRequiredService<ICrossPlatformNetworkManager>();
            
            Assert.IsNotNull(networkManager, "Network manager should be instantiated");
            
            var networkInfo = networkManager.GetNetworkInfo();
            
            Assert.IsNotNull(networkInfo, "Network information should be retrievable");
            Assert.IsFalse(string.IsNullOrEmpty(networkInfo.Hostname), "Hostname should not be empty");
        }

        [TestMethod]
        public void TestSecurityManagerCompatibility()
        {
            var securityManager = _serviceProvider.GetRequiredService<ICrossPlatformSecurityManager>();
            
            Assert.IsNotNull(securityManager, "Security manager should be instantiated");
            
            // Test administrator check
            bool isAdmin = securityManager.IsAdministrator();
            _logger.LogInformation($"Administrator Status: {isAdmin}");

            // Test secure token generation
            string token = securityManager.GenerateSecureToken();
            Assert.IsFalse(string.IsNullOrEmpty(token), "Secure token generation should work");
        }

        [TestMethod]
        public void TestPlatformSpecificImplementation()
        {
            var platformService = CrossPlatformServiceManager.Current;
            
            Assert.IsNotNull(platformService, "Platform service should be detected");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.IsTrue(platformService is WindowsPlatformService, 
                    "Windows platform service should be used on Windows");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Assert.IsTrue(platformService is MacOSPlatformService, 
                    "macOS platform service should be used on macOS");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.IsTrue(platformService is LinuxPlatformService, 
                    "Linux platform service should be used on Linux");
            }
        }

        [TestMethod]
        public void TestCrossPlatformManagerCreation()
        {
            // Verify factory methods work across different platforms
            var networkManager = CrossPlatformNetworkManagerFactory.Create(_logger);
            var securityManager = CrossPlatformSecurityManagerFactory.Create(_logger);

            Assert.IsNotNull(networkManager, "Network manager factory should work");
            Assert.IsNotNull(securityManager, "Security manager factory should work");
        }

        [TestMethod]
        public void TestPlatformSpecificNetworkManagerCreation()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var windowsNetworkManager = new WindowsNetworkManager(_logger as ILogger<WindowsNetworkManager>);
                Assert.IsNotNull(windowsNetworkManager, "Windows network manager should be creatable");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var macOSNetworkManager = new MacOSNetworkManager(_logger as ILogger<MacOSNetworkManager>);
                Assert.IsNotNull(macOSNetworkManager, "macOS network manager should be creatable");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var linuxNetworkManager = new LinuxNetworkManager(_logger as ILogger<LinuxNetworkManager>);
                Assert.IsNotNull(linuxNetworkManager, "Linux network manager should be creatable");
            }
        }
    }
}
