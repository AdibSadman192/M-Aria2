using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MAria2.Presentation.WinUI.Processes
{
    public interface ICrossPlatformProcessManager
    {
        int StartProcess(string command, string arguments);
        bool StopProcess(int processId);
        bool KillProcess(int processId);
        ProcessInfo GetProcessInfo(int processId);
        IEnumerable<ProcessInfo> GetRunningProcesses();
        void MonitorProcesses(Action<ProcessInfo> onProcessChanged);
    }

    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string Name { get; set; }
        public long MemoryUsage { get; set; }
        public double CpuUsage { get; set; }
        public ProcessState State { get; set; }
        public DateTime StartTime { get; set; }
    }

    public enum ProcessState
    {
        Running,
        Suspended,
        Stopped,
        Zombie
    }

    public abstract class BaseCrossPlatformProcessManager : ICrossPlatformProcessManager
    {
        protected readonly ILogger<BaseCrossPlatformProcessManager> _logger;
        protected ConcurrentDictionary<int, ProcessInfo> _runningProcesses;
        private CancellationTokenSource _monitorCancellationSource;

        protected BaseCrossPlatformProcessManager(ILogger<BaseCrossPlatformProcessManager> logger)
        {
            _logger = logger;
            _runningProcesses = new ConcurrentDictionary<int, ProcessInfo>();
            _monitorCancellationSource = new CancellationTokenSource();
        }

        public abstract int StartProcess(string command, string arguments);

        public virtual bool StopProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.Stop();
                UpdateProcessState(processId, ProcessState.Stopped);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping process {processId}: {ex.Message}");
                return false;
            }
        }

        public virtual bool KillProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.Kill(true);
                _runningProcesses.TryRemove(processId, out _);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error killing process {processId}: {ex.Message}");
                return false;
            }
        }

        public virtual ProcessInfo GetProcessInfo(int processId)
        {
            if (_runningProcesses.TryGetValue(processId, out ProcessInfo processInfo))
            {
                UpdateProcessDetails(processInfo);
                return processInfo;
            }
            return null;
        }

        public virtual IEnumerable<ProcessInfo> GetRunningProcesses()
        {
            UpdateAllProcessDetails();
            return _runningProcesses.Values;
        }

        public virtual void MonitorProcesses(Action<ProcessInfo> onProcessChanged)
        {
            Task.Run(() =>
            {
                while (!_monitorCancellationSource.Token.IsCancellationRequested)
                {
                    UpdateAllProcessDetails();
                    foreach (var process in _runningProcesses.Values)
                    {
                        onProcessChanged?.Invoke(process);
                    }
                    Thread.Sleep(5000); // Monitor every 5 seconds
                }
            }, _monitorCancellationSource.Token);
        }

        protected virtual void UpdateProcessState(int processId, ProcessState state)
        {
            if (_runningProcesses.TryGetValue(processId, out ProcessInfo processInfo))
            {
                processInfo.State = state;
            }
        }

        protected abstract void UpdateProcessDetails(ProcessInfo processInfo);
        protected abstract void UpdateAllProcessDetails();

        public void Dispose()
        {
            _monitorCancellationSource.Cancel();
        }
    }

    public class WindowsProcessManager : BaseCrossPlatformProcessManager
    {
        public WindowsProcessManager(ILogger<WindowsProcessManager> logger) : base(logger) { }

        public override int StartProcess(string command, string arguments)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();

                var processInfo = new ProcessInfo
                {
                    ProcessId = process.Id,
                    Name = process.ProcessName,
                    StartTime = process.StartTime,
                    State = ProcessState.Running
                };

                _runningProcesses[process.Id] = processInfo;
                return process.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting process: {ex.Message}");
                return -1;
            }
        }

        protected override void UpdateProcessDetails(ProcessInfo processInfo)
        {
            try
            {
                var process = Process.GetProcessById(processInfo.ProcessId);
                using (var performanceCounter = new PerformanceCounter("Process", "% Processor Time", process.ProcessName))
                {
                    processInfo.CpuUsage = performanceCounter.NextValue() / Environment.ProcessorCount;
                    processInfo.MemoryUsage = process.WorkingSet64;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating process details: {ex.Message}");
            }
        }

        protected override void UpdateAllProcessDetails()
        {
            foreach (var processInfo in _runningProcesses.Values)
            {
                UpdateProcessDetails(processInfo);
            }
        }
    }

    public class MacOSProcessManager : BaseCrossPlatformProcessManager
    {
        public MacOSProcessManager(ILogger<MacOSProcessManager> logger) : base(logger) { }

        public override int StartProcess(string command, string arguments)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{command} {arguments}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();

                var processInfo = new ProcessInfo
                {
                    ProcessId = process.Id,
                    Name = process.ProcessName,
                    StartTime = process.StartTime,
                    State = ProcessState.Running
                };

                _runningProcesses[process.Id] = processInfo;
                return process.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting process: {ex.Message}");
                return -1;
            }
        }

        protected override void UpdateProcessDetails(ProcessInfo processInfo)
        {
            try
            {
                var psOutput = ExecuteBashCommand($"ps -p {processInfo.ProcessId} -o %cpu,rss");
                var lines = psOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                
                if (lines.Length > 1)
                {
                    var values = lines[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    processInfo.CpuUsage = double.Parse(values[0]);
                    processInfo.MemoryUsage = long.Parse(values[1]) * 1024; // Convert KB to bytes
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating process details: {ex.Message}");
            }
        }

        protected override void UpdateAllProcessDetails()
        {
            foreach (var processInfo in _runningProcesses.Values)
            {
                UpdateProcessDetails(processInfo);
            }
        }

        private string ExecuteBashCommand(string command)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }
    }

    public class LinuxProcessManager : BaseCrossPlatformProcessManager
    {
        public LinuxProcessManager(ILogger<LinuxProcessManager> logger) : base(logger) { }

        public override int StartProcess(string command, string arguments)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{command} {arguments}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();

                var processInfo = new ProcessInfo
                {
                    ProcessId = process.Id,
                    Name = process.ProcessName,
                    StartTime = process.StartTime,
                    State = ProcessState.Running
                };

                _runningProcesses[process.Id] = processInfo;
                return process.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting process: {ex.Message}");
                return -1;
            }
        }

        protected override void UpdateProcessDetails(ProcessInfo processInfo)
        {
            try
            {
                var psOutput = ExecuteBashCommand($"ps -p {processInfo.ProcessId} -o %cpu,rss");
                var lines = psOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                
                if (lines.Length > 1)
                {
                    var values = lines[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    processInfo.CpuUsage = double.Parse(values[0]);
                    processInfo.MemoryUsage = long.Parse(values[1]) * 1024; // Convert KB to bytes
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating process details: {ex.Message}");
            }
        }

        protected override void UpdateAllProcessDetails()
        {
            foreach (var processInfo in _runningProcesses.Values)
            {
                UpdateProcessDetails(processInfo);
            }
        }

        private string ExecuteBashCommand(string command)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }
    }

    public static class CrossPlatformProcessManagerFactory
    {
        public static ICrossPlatformProcessManager Create(ILogger logger)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new WindowsProcessManager((ILogger<WindowsProcessManager>)logger);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new MacOSProcessManager((ILogger<MacOSProcessManager>)logger);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxProcessManager((ILogger<LinuxProcessManager>)logger);

            throw new PlatformNotSupportedException("Unsupported operating system");
        }
    }
}
