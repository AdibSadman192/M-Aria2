using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace MAria2.Presentation.WinUI.Security
{
    public interface ICrossPlatformSecurityManager
    {
        bool IsAdministrator();
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword);
        string GenerateSecureToken(int length = 32);
        void SetFilePermissions(string filePath, FilePermissionLevel level);
        bool IsProcessElevated();
    }

    public enum FilePermissionLevel
    {
        ReadOnly,
        ReadWrite,
        ExecuteOnly,
        Full
    }

    public abstract class BaseCrossPlatformSecurityManager : ICrossPlatformSecurityManager
    {
        protected readonly ILogger<BaseCrossPlatformSecurityManager> _logger;

        protected BaseCrossPlatformSecurityManager(ILogger<BaseCrossPlatformSecurityManager> logger)
        {
            _logger = logger;
        }

        public virtual string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        public virtual bool VerifyPassword(string password, string hashedPassword)
        {
            return HashPassword(password) == hashedPassword;
        }

        public virtual string GenerateSecureToken(int length = 32)
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] tokenData = new byte[length];
                rng.GetBytes(tokenData);
                return Convert.ToBase64String(tokenData);
            }
        }

        public abstract bool IsAdministrator();
        public abstract bool IsProcessElevated();
        public abstract void SetFilePermissions(string filePath, FilePermissionLevel level);
    }

    public class WindowsSecurityManager : BaseCrossPlatformSecurityManager
    {
        public WindowsSecurityManager(ILogger<WindowsSecurityManager> logger) : base(logger) { }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        public override bool IsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking administrator status: {ex.Message}");
                return false;
            }
        }

        public override bool IsProcessElevated()
        {
            try
            {
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    IntPtr tokenHandle;
                    const uint TOKEN_QUERY = 0x0008;
                    
                    if (!OpenProcessToken(process.Handle, TOKEN_QUERY, out tokenHandle))
                    {
                        return false;
                    }

                    const int TokenElevation = 20;
                    uint elevationResult = 0;
                    uint returnLength;

                    IntPtr elevationPtr = Marshal.AllocHGlobal(sizeof(uint));
                    try
                    {
                        if (GetTokenInformation(tokenHandle, TokenElevation, elevationPtr, sizeof(uint), out returnLength))
                        {
                            elevationResult = (uint)Marshal.ReadInt32(elevationPtr);
                            return elevationResult > 0;
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(elevationPtr);
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking process elevation: {ex.Message}");
                return false;
            }
        }

        public override void SetFilePermissions(string filePath, FilePermissionLevel level)
        {
            try
            {
                var fileInfo = new System.IO.FileInfo(filePath);
                var security = fileInfo.GetAccessControl();
                var identity = WindowsIdentity.GetCurrent().User;

                switch (level)
                {
                    case FilePermissionLevel.ReadOnly:
                        security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                            identity, 
                            System.Security.AccessControl.FileSystemRights.Read, 
                            System.Security.AccessControl.AccessControlType.Allow));
                        break;
                    case FilePermissionLevel.ReadWrite:
                        security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                            identity, 
                            System.Security.AccessControl.FileSystemRights.ReadAndExecute, 
                            System.Security.AccessControl.AccessControlType.Allow));
                        break;
                    case FilePermissionLevel.ExecuteOnly:
                        security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                            identity, 
                            System.Security.AccessControl.FileSystemRights.ExecuteFile, 
                            System.Security.AccessControl.AccessControlType.Allow));
                        break;
                    case FilePermissionLevel.Full:
                        security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                            identity, 
                            System.Security.AccessControl.FileSystemRights.FullControl, 
                            System.Security.AccessControl.AccessControlType.Allow));
                        break;
                }

                fileInfo.SetAccessControl(security);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting file permissions: {ex.Message}");
            }
        }
    }

    public class MacOSSecurityManager : BaseCrossPlatformSecurityManager
    {
        public MacOSSecurityManager(ILogger<MacOSSecurityManager> logger) : base(logger) { }

        public override bool IsAdministrator()
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/usr/bin/id",
                        Arguments = "-u",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                return output == "0"; // Root user has ID 0
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking administrator status: {ex.Message}");
                return false;
            }
        }

        public override bool IsProcessElevated()
        {
            return IsAdministrator();
        }

        public override void SetFilePermissions(string filePath, FilePermissionLevel level)
        {
            try
            {
                string permissionMode = level switch
                {
                    FilePermissionLevel.ReadOnly => "444",
                    FilePermissionLevel.ReadWrite => "666",
                    FilePermissionLevel.ExecuteOnly => "111",
                    FilePermissionLevel.Full => "777",
                    _ => "644"
                };

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/chmod",
                        Arguments = $"{permissionMode} \"{filePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting file permissions: {ex.Message}");
            }
        }
    }

    public class LinuxSecurityManager : BaseCrossPlatformSecurityManager
    {
        public LinuxSecurityManager(ILogger<LinuxSecurityManager> logger) : base(logger) { }

        public override bool IsAdministrator()
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/usr/bin/id",
                        Arguments = "-u",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                return output == "0"; // Root user has ID 0
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking administrator status: {ex.Message}");
                return false;
            }
        }

        public override bool IsProcessElevated()
        {
            return IsAdministrator();
        }

        public override void SetFilePermissions(string filePath, FilePermissionLevel level)
        {
            try
            {
                string permissionMode = level switch
                {
                    FilePermissionLevel.ReadOnly => "444",
                    FilePermissionLevel.ReadWrite => "666",
                    FilePermissionLevel.ExecuteOnly => "111",
                    FilePermissionLevel.Full => "777",
                    _ => "644"
                };

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/chmod",
                        Arguments = $"{permissionMode} \"{filePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting file permissions: {ex.Message}");
            }
        }
    }

    public static class CrossPlatformSecurityManagerFactory
    {
        public static ICrossPlatformSecurityManager Create(ILogger logger)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new WindowsSecurityManager((ILogger<WindowsSecurityManager>)logger);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new MacOSSecurityManager((ILogger<MacOSSecurityManager>)logger);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxSecurityManager((ILogger<LinuxSecurityManager>)logger);

            throw new PlatformNotSupportedException("Unsupported operating system");
        }
    }
}
