using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace MAria2.Presentation.WinUI.Network
{
    public interface ICrossPlatformNetworkManager
    {
        NetworkInfo GetNetworkInfo();
        bool TestInternetConnection(string testUrl = "https://www.google.com");
        IEnumerable<NetworkInterface> GetActiveNetworkInterfaces();
        IPAddress GetPublicIPAddress();
        void MonitorNetworkChanges(Action<NetworkChangeEvent> onNetworkChanged);
    }

    public class NetworkInfo
    {
        public string Hostname { get; set; }
        public IPAddress LocalIPAddress { get; set; }
        public IPAddress PublicIPAddress { get; set; }
        public string NetworkType { get; set; }
        public bool IsConnected { get; set; }
        public long DownloadSpeed { get; set; }
        public long UploadSpeed { get; set; }
    }

    public class NetworkChangeEvent
    {
        public bool ConnectionStatusChanged { get; set; }
        public string PreviousNetworkType { get; set; }
        public string CurrentNetworkType { get; set; }
    }

    public abstract class BaseCrossPlatformNetworkManager : ICrossPlatformNetworkManager
    {
        protected readonly ILogger<BaseCrossPlatformNetworkManager> _logger;
        private CancellationTokenSource _monitorCancellationSource;

        protected BaseCrossPlatformNetworkManager(ILogger<BaseCrossPlatformNetworkManager> logger)
        {
            _logger = logger;
            _monitorCancellationSource = new CancellationTokenSource();
        }

        public virtual bool TestInternetConnection(string testUrl = "https://www.google.com")
        {
            try
            {
                using (var client = new WebClient())
                {
                    using (client.OpenRead(testUrl))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public virtual IEnumerable<NetworkInterface> GetActiveNetworkInterfaces()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up);
        }

        public abstract NetworkInfo GetNetworkInfo();
        public abstract IPAddress GetPublicIPAddress();

        public virtual void MonitorNetworkChanges(Action<NetworkChangeEvent> onNetworkChanged)
        {
            Task.Run(() =>
            {
                var previousNetworkType = GetNetworkInfo().NetworkType;

                while (!_monitorCancellationSource.Token.IsCancellationRequested)
                {
                    var currentNetworkInfo = GetNetworkInfo();
                    var currentNetworkType = currentNetworkInfo.NetworkType;

                    if (currentNetworkType != previousNetworkType)
                    {
                        onNetworkChanged?.Invoke(new NetworkChangeEvent
                        {
                            ConnectionStatusChanged = true,
                            PreviousNetworkType = previousNetworkType,
                            CurrentNetworkType = currentNetworkType
                        });

                        previousNetworkType = currentNetworkType;
                    }

                    Thread.Sleep(5000); // Check every 5 seconds
                }
            }, _monitorCancellationSource.Token);
        }

        public void Dispose()
        {
            _monitorCancellationSource.Cancel();
        }
    }

    public class WindowsNetworkManager : BaseCrossPlatformNetworkManager
    {
        public WindowsNetworkManager(ILogger<WindowsNetworkManager> logger) : base(logger) { }

        public override NetworkInfo GetNetworkInfo()
        {
            try
            {
                var networkInterfaces = GetActiveNetworkInterfaces();
                var primaryInterface = networkInterfaces.FirstOrDefault();

                return new NetworkInfo
                {
                    Hostname = Dns.GetHostName(),
                    LocalIPAddress = primaryInterface?.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address,
                    PublicIPAddress = GetPublicIPAddress(),
                    NetworkType = GetNetworkType(),
                    IsConnected = networkInterfaces.Any(),
                    DownloadSpeed = GetNetworkSpeed("download"),
                    UploadSpeed = GetNetworkSpeed("upload")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting network info: {ex.Message}");
                return new NetworkInfo();
            }
        }

        public override IPAddress GetPublicIPAddress()
        {
            try
            {
                using (var client = new WebClient())
                {
                    string publicIpResponse = client.DownloadString("https://api.ipify.org");
                    return IPAddress.Parse(publicIpResponse);
                }
            }
            catch
            {
                return null;
            }
        }

        private string GetNetworkType()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Contains("Connected") 
                ? (output.Contains("802.11") ? "WiFi" : "Ethernet") 
                : "Disconnected";
        }

        private long GetNetworkSpeed(string direction)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netstat",
                        Arguments = "-e",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var match = Regex.Match(output, direction == "download" 
                    ? @"Bytes Received\s*:\s*(\d+)" 
                    : @"Bytes Sent\s*:\s*(\d+)");

                return match.Success ? long.Parse(match.Groups[1].Value) : 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    public class MacOSNetworkManager : BaseCrossPlatformNetworkManager
    {
        public MacOSNetworkManager(ILogger<MacOSNetworkManager> logger) : base(logger) { }

        public override NetworkInfo GetNetworkInfo()
        {
            try
            {
                var networkInterfaces = GetActiveNetworkInterfaces();
                var primaryInterface = networkInterfaces.FirstOrDefault();

                return new NetworkInfo
                {
                    Hostname = Dns.GetHostName(),
                    LocalIPAddress = primaryInterface?.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address,
                    PublicIPAddress = GetPublicIPAddress(),
                    NetworkType = GetNetworkType(),
                    IsConnected = networkInterfaces.Any(),
                    DownloadSpeed = GetNetworkSpeed("download"),
                    UploadSpeed = GetNetworkSpeed("upload")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting network info: {ex.Message}");
                return new NetworkInfo();
            }
        }

        public override IPAddress GetPublicIPAddress()
        {
            try
            {
                using (var client = new WebClient())
                {
                    string publicIpResponse = client.DownloadString("https://api.ipify.org");
                    return IPAddress.Parse(publicIpResponse);
                }
            }
            catch
            {
                return null;
            }
        }

        private string GetNetworkType()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/sbin/networksetup",
                    Arguments = "-listallhardwareports",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Contains("Wi-Fi") 
                ? "WiFi" 
                : (output.Contains("Ethernet") ? "Ethernet" : "Disconnected");
        }

        private long GetNetworkSpeed(string direction)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/netstat",
                        Arguments = "-b -n -I en0",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var match = Regex.Match(output, direction == "download" 
                    ? @"Bytes In\s*(\d+)" 
                    : @"Bytes Out\s*(\d+)");

                return match.Success ? long.Parse(match.Groups[1].Value) : 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    public class LinuxNetworkManager : BaseCrossPlatformNetworkManager
    {
        public LinuxNetworkManager(ILogger<LinuxNetworkManager> logger) : base(logger) { }

        public override NetworkInfo GetNetworkInfo()
        {
            try
            {
                var networkInterfaces = GetActiveNetworkInterfaces();
                var primaryInterface = networkInterfaces.FirstOrDefault();

                return new NetworkInfo
                {
                    Hostname = Dns.GetHostName(),
                    LocalIPAddress = primaryInterface?.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address,
                    PublicIPAddress = GetPublicIPAddress(),
                    NetworkType = GetNetworkType(),
                    IsConnected = networkInterfaces.Any(),
                    DownloadSpeed = GetNetworkSpeed("download"),
                    UploadSpeed = GetNetworkSpeed("upload")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting network info: {ex.Message}");
                return new NetworkInfo();
            }
        }

        public override IPAddress GetPublicIPAddress()
        {
            try
            {
                using (var client = new WebClient())
                {
                    string publicIpResponse = client.DownloadString("https://api.ipify.org");
                    return IPAddress.Parse(publicIpResponse);
                }
            }
            catch
            {
                return null;
            }
        }

        private string GetNetworkType()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"ip link show\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Contains("wlan") 
                ? "WiFi" 
                : (output.Contains("eth") ? "Ethernet" : "Disconnected");
        }

        private long GetNetworkSpeed(string direction)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = "-c \"cat /sys/class/net/eth0/statistics/" + 
                            (direction == "download" ? "rx_bytes" : "tx_bytes") + "\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return long.TryParse(output.Trim(), out long bytes) ? bytes : 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    public static class CrossPlatformNetworkManagerFactory
    {
        public static ICrossPlatformNetworkManager Create(ILogger logger)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new WindowsNetworkManager((ILogger<WindowsNetworkManager>)logger);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new MacOSNetworkManager((ILogger<MacOSNetworkManager>)logger);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxNetworkManager((ILogger<LinuxNetworkManager>)logger);

            throw new PlatformNotSupportedException("Unsupported operating system");
        }
    }
}
