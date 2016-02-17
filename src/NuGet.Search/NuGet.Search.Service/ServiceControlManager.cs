using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace NuGet.Search.Service
{
    /// <summary>
    /// .NET Wrapper for Service Control Manager functions.
    /// </summary>
    public class ServiceControlManager
    {
        public ServiceControlManager()
        {
        }

        public static void InstallService(String serviceName, String displayName, String fileName, ServiceType serviceType, ServiceStartType serviceStartType)
        {
            IntPtr scman = OpenSCManager(ServiceManagerRights.Connect | ServiceManagerRights.CreateService);
            try
            {
                IntPtr service = OpenService(scman, serviceName, ServiceRights.QueryStatus);
                if (service == IntPtr.Zero)
                {
                    service = CreateService(scman, serviceName, displayName, ServiceRights.QueryStatus, serviceType, serviceStartType, ServiceError.Normal, fileName, null, IntPtr.Zero, null, null, null);
                    if (service == IntPtr.Zero)
                    {
                        throw new Win32Exception();
                    }
                }
                CloseServiceHandle(service);
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        public static void InstallService(String serviceName, String displayName, String fileName)
        {
            InstallService(serviceName, displayName, fileName, ServiceType.SERVICE_WIN32_OWN_PROCESS, ServiceStartType.AutoStart);
        }

        public static void InstallDriver(String serviceName, String displayName, String fileName)
        {
            InstallService(serviceName, displayName, fileName, ServiceType.SERVICE_KERNEL_DRIVER, ServiceStartType.DemandStart);
        }

        /// <summary>
        /// Takes a service name and tries to stop and then uninstall the windows serviceError
        /// </summary>
        /// <param name="serviceName">The windows service name to uninstall</param>
        public static void UninstallService(String serviceName)
        {
            IntPtr scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                IntPtr service = OpenService(scman, serviceName, ServiceRights.StandardRightsRequired | ServiceRights.Stop | ServiceRights.QueryStatus);
                if (service == IntPtr.Zero)
                {
                    throw new ApplicationException("Service not installed.");
                }

                try
                {
                    StopService(service);

                    if (DeleteService(service) == 0)
                    {
                        throw new Win32Exception();
                    }
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Accepts a service name and returns true if the service with that service name exists
        /// </summary>
        /// <param name="serviceName">The service name that we will check for existence</param>
        /// <returns>True if that service exists false otherwise</returns>
        public static Boolean IsServiceInstalled(String serviceName)
        {
            return (GetServiceStatus(serviceName) != ServiceState.NotFound);
        }

        /// <summary>
        /// Starts the specified service (or waits for it to finish starting) if necessary.
        /// </summary>
        /// <param name="serviceName">The service name</param>
        public static void StartService(String serviceName)
        {
            IntPtr scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                IntPtr service = OpenService(scman, serviceName, ServiceRights.QueryStatus | ServiceRights.Start);
                if (service == IntPtr.Zero)
                {
                    throw new ApplicationException("Service not installed.");
                }

                try
                {
                    StartService(service);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Stops the specified service (or waits for it to finish stopping) if necessary.
        /// </summary>
        /// <param name="serviceName">The service name</param>
        public static void StopService(String serviceName)
        {
            IntPtr scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                IntPtr service = OpenService(scman, serviceName, ServiceRights.QueryStatus | ServiceRights.Stop);
                if (service == IntPtr.Zero)
                {
                    throw new Win32Exception("Could not open service.");
                }

                try
                {
                    StopService(service);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Starts the provided windows service if necessary.
        /// </summary>
        /// <param name="service">The handle to the windows service</param>
        private static void StartService(IntPtr service)
        {
            ServiceState serviceStatus = GetServiceStatus(service);
            switch (serviceStatus)
            {
                case ServiceState.Running:
                    return;

                case ServiceState.Stopped:
                    if (StartService(service, 0, 0) == 0)
                    {
                        throw new Win32Exception();
                    }
                    break;

                case ServiceState.Starting:
                    break;

                default:
                    throw new ApplicationException(String.Format("Failed to start service because it is in the {0} state.", serviceStatus));
            }

            if (!WaitForServiceStatus(service, ServiceState.Running))
            {
                throw new ApplicationException("Failed to start service.");
            }
        }

        /// <summary>
        /// Stops the provided windows service
        /// </summary>
        /// <param name="service">The handle to the windows service</param>
        private static void StopService(IntPtr service)
        {
            ServiceState serviceStatus = GetServiceStatus(service);
            switch (serviceStatus)
            {
                case ServiceState.Stopped:
                    return;

                case ServiceState.Running:
                    SERVICE_STATUS status = new SERVICE_STATUS();
                    if (ControlService(service, ServiceControl.Stop, status) == 0)
                    {
                        throw new Win32Exception();
                    }
                    break;

                case ServiceState.Stopping:
                    break;

                default:
                    throw new ApplicationException(String.Format("Failed to stop service because it is in the {0} state.", serviceStatus));
            }

            if (!WaitForServiceStatus(service, ServiceState.Stopped))
            {
                throw new ApplicationException("Service failed to stop.");
            }
        }

        /// <summary>
        /// Takes a service name and returns the <code>ServiceState</code> of the corresponding service
        /// </summary>
        /// <param name="serviceName">The service name that we will check for his <code>ServiceState</code></param>
        /// <returns>The ServiceState of the service we wanted to check</returns>
        public static ServiceState GetServiceStatus(String serviceName)
        {
            IntPtr scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                IntPtr service = OpenService(scman, serviceName, ServiceRights.QueryStatus);
                if (service == IntPtr.Zero)
                {
                    return ServiceState.NotFound;
                }

                try
                {
                    return GetServiceStatus(service);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        private static ServiceState GetServiceStatus(IntPtr service)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();
            if (QueryServiceStatus(service, status) == 0)
            {
                throw new Win32Exception();
            }
            return status.dwCurrentState;
        }

        /// <summary>
        /// Returns true when the service status has been changed to desired status, or false if it fails to do so (or make forward progress) within the specified amount of time.
        /// </summary>
        /// <param name="service">The handle to the service</param>
        /// <param name="desiredStatus">The desired state of the service</param>
        /// <param name="timeoutInMillis">The amount of time to wait without receiving any progress update from service before timing out (default: 10s)</param>
        /// <returns>bool if the service has successfully changed states within the allowed timeline</returns>
        private static Boolean WaitForServiceStatus(IntPtr service, ServiceState desiredStatus, Int32 timeoutInMillis = 10000)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();

            Int32 lastCheckpointValue = status.dwCheckPoint;
            Int32 lastCheckpointTickCount = Environment.TickCount;

            while (true)
            {
                if (QueryServiceStatus(service, status) == 0) break;

                if (status.dwCurrentState == desiredStatus)
                {
                    break;
                }

                // Do not wait longer than the wait hint. A good interval is
                // one tenth the wait hint, but no less than 1 second and no
                // more than 10 seconds.

                if (status.dwCheckPoint > lastCheckpointValue)
                {
                    // The service is making progress.
                    lastCheckpointValue = status.dwCheckPoint;
                    lastCheckpointTickCount = Environment.TickCount;

                    System.Threading.Thread.Sleep(100);
                    continue;
                }
                else
                {
                    if ((Environment.TickCount - lastCheckpointTickCount) > timeoutInMillis)
                    {
                        // No progress made within the specified timeout.
                        break;
                    }
                }
            }

            return (status.dwCurrentState == desiredStatus);
        }

        /// <summary>
        /// Opens the service manager
        /// </summary>
        /// <param name="rights">The service manager rights</param>
        /// <returns>the handle to the service manager</returns>
        private static IntPtr OpenSCManager(ServiceManagerRights rights)
        {
            IntPtr scman = OpenSCManager(null, null, rights);
            if (scman == IntPtr.Zero)
            {
                throw new Win32Exception("Could not connect to service control manager.");
            }
            return scman;
        }

        #region P/Invoke Service Control Manager

        [StructLayout(LayoutKind.Sequential)]
        private class SERVICE_STATUS
        {
            public int dwServiceType = 0;
            public ServiceState dwCurrentState = 0;
            public int dwControlsAccepted = 0;
            public int dwWin32ExitCode = 0;
            public int dwServiceSpecificExitCode = 0;
            public int dwCheckPoint = 0;
            public int dwWaitHint = 0;
        }

        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerA")]
        private static extern IntPtr OpenSCManager(string lpMachineName, string lpDatabaseName, ServiceManagerRights dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "OpenServiceA", CharSet = CharSet.Ansi)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, ServiceRights dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "CreateServiceA")]
        private static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName, ServiceRights dwDesiredAccess, ServiceType dwServiceType, ServiceStartType dwStartType, ServiceError dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId, string lpDependencies, string lp, string lpPassword);

        [DllImport("advapi32.dll")]
        private static extern int CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll")]
        private static extern int QueryServiceStatus(IntPtr hService, SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int DeleteService(IntPtr hService);

        [DllImport("advapi32.dll")]
        private static extern int ControlService(IntPtr hService, ServiceControl dwControl, SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32.dll", EntryPoint = "StartServiceA")]
        private static extern int StartService(IntPtr hService, int dwNumServiceArgs, int lpServiceArgVectors);

        #endregion
    }

    [Flags]
    internal enum ServiceManagerRights
    {
        Connect = 0x0001,
        CreateService = 0x0002,
        EnumerateService = 0x0004,
        Lock = 0x0008,
        QueryLockStatus = 0x0010,
        ModifyBootConfig = 0x0020,
        StandardRightsRequired = 0xF0000,
        AllAccess = (StandardRightsRequired | Connect | CreateService | EnumerateService | Lock | QueryLockStatus | ModifyBootConfig)
    }

    [Flags]
    internal enum ServiceRights
    {
        QueryConfig = 0x1,
        ChangeConfig = 0x2,
        QueryStatus = 0x4,
        EnumerateDependants = 0x8,
        Start = 0x10,
        Stop = 0x20,
        PauseContinue = 0x40,
        Interrogate = 0x80,
        UserDefinedControl = 0x100,
        Delete = 0x00010000,
        StandardRightsRequired = 0xF0000,
        AllAccess = (StandardRightsRequired | QueryConfig | ChangeConfig | QueryStatus | EnumerateDependants | Start | Stop | PauseContinue | Interrogate | UserDefinedControl)
    }

    public enum ServiceStartType
    {
        BootStart = 0x00000000,
        SystemStart = 0x00000001,
        AutoStart = 0x00000002,
        DemandStart = 0x00000003,
        Disabled = 0x00000004
    }

    public enum ServiceState
    {
        Unknown = -1, // The state cannot be (has not been) retrieved.
        NotFound = 0, // The service is not known on the host server.
        Stopped = 1, // The service is NET stopped.
        Starting = 2, // The service is NET started.
        Stopping = 3,
        Running = 4,
    }

    internal enum ServiceControl
    {
        Stop = 0x00000001,
        Pause = 0x00000002,
        Continue = 0x00000003,
        Interrogate = 0x00000004,
        Shutdown = 0x00000005,
        ParamChange = 0x00000006,
        NetBindAdd = 0x00000007,
        NetBindRemove = 0x00000008,
        NetBindEnable = 0x00000009,
        NetBindDisable = 0x0000000A
    }

    internal enum ServiceError
    {
        Ignore = 0x00000000,
        Normal = 0x00000001,
        Severe = 0x00000002,
        Critical = 0x00000003
    }

    [Flags]
    public enum ServiceType : int
    {
        SERVICE_KERNEL_DRIVER = 0x00000001,
        SERVICE_FILE_SYSTEM_DRIVER = 0x00000002,
        SERVICE_WIN32_OWN_PROCESS = 0x00000010,
        SERVICE_WIN32_SHARE_PROCESS = 0x00000020,
        SERVICE_INTERACTIVE_PROCESS = 0x00000100
    }
}
