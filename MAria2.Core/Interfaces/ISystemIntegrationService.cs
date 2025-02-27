using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading.Tasks;

namespace MAria2.Core.Interfaces
{
    public interface ISystemIntegrationService
    {
        /// <summary>
        /// Manage application privileges and permissions
        /// </summary>
        Task<PrivilegeManagementResult> ManagePrivilegesAsync(
            PrivilegeRequest request);

        /// <summary>
        /// Handle elevated permission requests
        /// </summary>
        Task<ElevatedPermissionResult> RequestElevatedPermissionsAsync(
            ElevatedPermissionRequest request);

        /// <summary>
        /// Load and manage native system libraries
        /// </summary>
        Task<NativeLibraryLoadResult> LoadNativeLibraryAsync(
            NativeLibraryLoadRequest request);

        /// <summary>
        /// Get current system security context
        /// </summary>
        Task<SystemSecurityContext> GetSystemSecurityContextAsync();

        /// <summary>
        /// Perform system-level integrity checks
        /// </summary>
        Task<SystemIntegrityCheckResult> PerformIntegrityCheckAsync();
    }

    public class PrivilegeRequest
    {
        public string ApplicationName { get; set; }
        public PrivilegeLevel RequestedLevel { get; set; }
        public string[] RequiredPermissions { get; set; }
    }

    public class PrivilegeManagementResult
    {
        public bool IsGranted { get; set; }
        public PrivilegeLevel GrantedLevel { get; set; }
        public string[] GrantedPermissions { get; set; }
        public string DenialReason { get; set; }
    }

    public class ElevatedPermissionRequest
    {
        public string Operation { get; set; }
        public string Justification { get; set; }
        public bool RequireUserConsent { get; set; }
    }

    public class ElevatedPermissionResult
    {
        public bool IsApproved { get; set; }
        public DateTime ApprovalTimestamp { get; set; }
        public string ApprovalMethod { get; set; }
        public string[] GrantedElevations { get; set; }
    }

    public class NativeLibraryLoadRequest
    {
        public string LibraryPath { get; set; }
        public string[] RequiredFunctions { get; set; }
        public bool PerformSecurityScan { get; set; }
    }

    public class NativeLibraryLoadResult
    {
        public bool IsLoaded { get; set; }
        public string LibraryPath { get; set; }
        public string[] LoadedFunctions { get; set; }
        public Dictionary<string, IntPtr> FunctionPointers { get; set; }
        public LibrarySecurityStatus SecurityStatus { get; set; }
    }

    public class SystemSecurityContext
    {
        public WindowsIdentity CurrentIdentity { get; set; }
        public bool IsAdministrator { get; set; }
        public string UserName { get; set; }
        public string[] UserGroups { get; set; }
        public OperatingSystem OSVersion { get; set; }
    }

    public class SystemIntegrityCheckResult
    {
        public bool IsSystemIntact { get; set; }
        public List<IntegrityIssue> DetectedIssues { get; set; }
        public DateTime CheckTimestamp { get; set; }
    }

    public class IntegrityIssue
    {
        public IntegrityIssueType Type { get; set; }
        public string Description { get; set; }
        public string Recommendation { get; set; }
    }

    public enum PrivilegeLevel
    {
        Standard,
        Elevated,
        Administrative,
        SystemLevel
    }

    public enum LibrarySecurityStatus
    {
        Unknown,
        Verified,
        Suspicious,
        Malicious
    }

    public enum IntegrityIssueType
    {
        FileSystemModification,
        RegistryChange,
        SecurityConfigurationAltered,
        UnauthorizedSoftwareInstallation,
        SystemFileCorruption
    }
}
