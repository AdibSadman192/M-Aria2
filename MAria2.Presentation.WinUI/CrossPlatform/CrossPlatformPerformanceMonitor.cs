using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management;
using Microsoft.Extensions.Logging;

namespace MAria2.Presentation.WinUI.Performance
{
    public interface ICrossPlatformPerformanceMonitor
    {
        double GetCpuUsage();
        long GetTotalMemory();
        long GetAvailableMemory();
        double GetDiskUsage(string path);
        void LogSystemPerformance(ILogger logger);
    }

    public class WindowsPerformanceMonitor : ICrossPlatformPerformanceMonitor
    {
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;

        public WindowsPerformanceMonitor()
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
        }

        public double GetCpuUsage()
        {
            return _cpuCounter.NextValue();
        }

        public long GetTotalMemory()
        {
            using (var mc = new ManagementClass("Win32_ComputerSystem"))
            {
                var moc = mc.GetInstances();
                foreach (ManagementObject item in moc)
                {
                    return Convert.ToInt64(item["TotalPhysicalMemory"]);
                }
            }
            return 0;
        }

        public long GetAvailableMemory()
        {
            return (long)(_ramCounter.NextValue() * 1024 * 1024);
        }

        public double GetDiskUsage(string path)
        {
            var drive = new DriveInfo(path);
            return (drive.TotalSize - drive.AvailableFreeSpace) / (double)drive.TotalSize * 100;
        }

        public void LogSystemPerformance(ILogger logger)
        {
            logger.LogInformation("Windows System Performance: " +
                $"CPU: {GetCpuUsage():F2}%, " +
                $"Total Memory: {GetTotalMemory() / (1024 * 1024):F2} MB, " +
                $"Available Memory: {GetAvailableMemory() / (1024 * 1024):F2} MB");
        }
    }

    public class MacOSPerformanceMonitor : ICrossPlatformPerformanceMonitor
    {
        public double GetCpuUsage()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"top -l 1 | grep 'CPU usage'\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Parse CPU usage from output
            var match = System.Text.RegularExpressions.Regex.Match(output, @"CPU usage: (\d+\.\d+)%");
            return match.Success ? double.Parse(match.Groups[1].Value) : 0;
        }

        public long GetTotalMemory()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/sbin/sysctl",
                    Arguments = "-n hw.memsize",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return long.TryParse(output.Trim(), out long memoryBytes) ? memoryBytes : 0;
        }

        public long GetAvailableMemory()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/vm_stat",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var freeMatch = System.Text.RegularExpressions.Regex.Match(output, @"free:\s+(\d+)");
            return freeMatch.Success 
                ? long.Parse(freeMatch.Groups[1].Value) * 4096 // Pages are 4096 bytes
                : 0;
        }

        public double GetDiskUsage(string path)
        {
            var drive = new DriveInfo(path);
            return (drive.TotalSize - drive.AvailableFreeSpace) / (double)drive.TotalSize * 100;
        }

        public void LogSystemPerformance(ILogger logger)
        {
            logger.LogInformation("macOS System Performance: " +
                $"CPU: {GetCpuUsage():F2}%, " +
                $"Total Memory: {GetTotalMemory() / (1024 * 1024):F2} MB, " +
                $"Available Memory: {GetAvailableMemory() / (1024 * 1024):F2} MB");
        }
    }

    public class LinuxPerformanceMonitor : ICrossPlatformPerformanceMonitor
    {
        public double GetCpuUsage()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"top -bn1 | grep 'Cpu(s)' | sed 's/.*, *\\([0-9.]*\\)%* id.*/\\1/' | awk '{print 100 - $1}'\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return double.TryParse(output.Trim(), out double cpuUsage) ? cpuUsage : 0;
        }

        public long GetTotalMemory()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"grep MemTotal /proc/meminfo | awk '{print $2 * 1024}'\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return long.TryParse(output.Trim(), out long memoryBytes) ? memoryBytes : 0;
        }

        public long GetAvailableMemory()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"grep MemAvailable /proc/meminfo | awk '{print $2 * 1024}'\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return long.TryParse(output.Trim(), out long memoryBytes) ? memoryBytes : 0;
        }

        public double GetDiskUsage(string path)
        {
            var drive = new DriveInfo(path);
            return (drive.TotalSize - drive.AvailableFreeSpace) / (double)drive.TotalSize * 100;
        }

        public void LogSystemPerformance(ILogger logger)
        {
            logger.LogInformation("Linux System Performance: " +
                $"CPU: {GetCpuUsage():F2}%, " +
                $"Total Memory: {GetTotalMemory() / (1024 * 1024):F2} MB, " +
                $"Available Memory: {GetAvailableMemory() / (1024 * 1024):F2} MB");
        }
    }

    public static class CrossPlatformPerformanceMonitorFactory
    {
        public static ICrossPlatformPerformanceMonitor Create()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new WindowsPerformanceMonitor();
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new MacOSPerformanceMonitor();
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxPerformanceMonitor();

            throw new PlatformNotSupportedException("Unsupported operating system");
        }
    }
}
