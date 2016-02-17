using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Win32;

namespace NuGet.Search.Service
{
    /// <summary>
    /// This singleton class manages the service lifetime.
    /// </summary>
    public class ServiceHelper
    {
        private static GenericService Service { get; set; }

        /// <summary>
        /// Returns the service name (which the 'ServiceName' app.config key value if defined, else the base name of the main assembly).
        /// </summary>
        public static String ServiceName
        {
            get
            {
                return ConfigurationManager.AppSettings["ServiceName"] ?? Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
            }
        }

        // A couple of extra events to coordinate the shutdown sequence once ShutdownEvent has been set.
        internal static ManualResetEvent ShutdownEvent2 { get; private set; }
        internal static ManualResetEvent ShutdownEvent3 { get; private set; }

        /// <summary>
        /// An event that gets signalled when the service should stop.
        /// After this event is signalled the process will exit after all threads have returned or 1 minute elapses, whichever is shorter.
        /// </summary>
        public static ManualResetEvent ShutdownEvent { get; private set; }

        /// <summary>
        /// Gets the service registry key (HKLM\SERVICES\CurrentCOntrolSet\[servicename]).
        /// The key gets created when the service is installed or the first time RegisterService() is called.
        /// The key (and all subkeys) are deleted when the service is uninstalled.
        /// </summary>
        public static RegistryKey ServiceKey { get; private set; }

        /// <summary>
        /// Call this method from the program's Main() function when the service functionality begins.
        /// </summary>
        public static void BeginService()
        {
            String serviceName = ServiceHelper.ServiceName;

            // Services normally do not have commandline arguments so using commandline arguments to control service install & uninstall.
            String[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                switch (args[1].ToLower())
                {
                    case "-i":
                        InstallService();
                        Environment.Exit(0);
                        break;

                    case "-u":
                        UninstallService();
                        Environment.Exit(0);
                        break;
                }
            }

            ServiceHelper.ShutdownEvent = new ManualResetEvent(false);
            ServiceHelper.ShutdownEvent2 = new ManualResetEvent(false);
            ServiceHelper.ShutdownEvent3 = new ManualResetEvent(false);

            try
            {
                // Create or open service registry key.
                ServiceHelper.ServiceKey = Registry.LocalMachine.CreateSubKey(String.Format(@"SOFTWARE\Microsoft\{0}", serviceName), RegistryKeyPermissionCheck.ReadWriteSubTree);
            }
            catch (UnauthorizedAccessException)
            {
                // The service key may not have been created yet, or this account may not have access to it.
                // This will tyically happen if we run this as a console app for testing.
                // So use an alternate reg key instead.
                ServiceHelper.ServiceKey = Registry.CurrentUser.CreateSubKey(String.Format(@"SYSTEM\CurrentControlSet\services\{0}", serviceName), RegistryKeyPermissionCheck.ReadWriteSubTree);
            }

            if (Environment.UserInteractive)
            {
                // Service was launched from the commandline.
                // Treat ctrl-c as the equivalent of a graceful service shutdown.
                Console.CancelKeyPress += new ConsoleCancelEventHandler((s, e) =>
                {
                    // Signal ShutdownEvent and then wait for program to call EndService().
                    // Exit process forcefully if program does not call EndService within 30s (same as the Service Control Manager would do).
                    ServiceHelper.ShutdownEvent.Set();
                    ServiceHelper.ShutdownEvent2.WaitOne(30 * 1000);
                });
            }
            else
            {
                ServiceHelper.Service = new GenericService();
                ServiceBase[] ServicesToRun = new ServiceBase[] { ServiceHelper.Service };

                // ServiceBase.Run() blocks until all the services in the process stop so do this from another thread.
                // The thread returns cleanly when the service is stopped.
                Thread thread = new Thread(new ThreadStart(() =>
                {
                    ServiceBase.Run(ServicesToRun);

                    // Once ServiceBase.Run() returns, the service is considered stopped.
                    // If the process exits before this then we get the 'Process exited unexpectedly' message when trying to
                    // stop the service so we signal an event once ServiceBase.Run() returns that EndService() can wait on before returning
                    // and potentially exiting the process.
                    ServiceHelper.ShutdownEvent3.Set();
                }));
                thread.IsBackground = true; // Set as background thread so it doesn't block the app from exiting once the main thread exits.
                thread.Start();
            }

            Trace.TraceInformation(@"#Notification Started program {0} v{1} as user {2}\{3}.", Process.GetCurrentProcess().MainModule.FileName, Process.GetCurrentProcess().MainModule.FileVersionInfo.FileVersion, Environment.UserDomainName, Environment.UserName);
        }

        /// <summary>
        /// Call this method from the program's Main() function when the service functionality ends.
        /// The service will then be marked 'stopped'.
        /// </summary>
        public static void EndService()
        {
            Trace.TraceInformation(@"#Notification Stopped program {0} v{1}.", Process.GetCurrentProcess().MainModule.FileName, Process.GetCurrentProcess().MainModule.FileVersionInfo.FileVersion);

            ServiceHelper.ShutdownEvent2.Set();

            if (!Environment.UserInteractive)
            {
                ServiceHelper.Service.Stop();
                ServiceHelper.ShutdownEvent3.WaitOne();
            }

            // Forcefully exit process in case any other non-background threads are still running.
            Environment.Exit(0);
        }

        #region Service Installation & Uninstallation

        private static void InstallService()
        {
            if (IsServiceInstalled())
            {
                Trace.TraceInformation("The {0} service is already installed.", ServiceHelper.ServiceName);
                return;
            }

            String serviceAccount;
            using (Installer installer = CreateServiceInstaller(out serviceAccount))
            {
                try
                {
                    // InstallUtil automatically creates an event source with the same name as the service in the Application log.
                    // This fails if the event source already exists, so delete it if it already exists.
                    if (EventLog.SourceExists(ServiceHelper.ServiceName))
                    {
                        EventLog.DeleteEventSource(ServiceHelper.ServiceName);
                    }

                    IDictionary state = new Hashtable();
                    installer.Install(state);

                    // Delete the event source that was created automatically by the installer so that it can be re-registered to another event log.
                    if (EventLog.SourceExists(ServiceHelper.ServiceName))
                    {
                        EventLog.DeleteEventSource(ServiceHelper.ServiceName);
                    }

                    // Issue a trace message to cause any trace listeners to get loaded.
                    // This allows eg. the EventLogTraceListsner to run elevated so it can create its event source.
                    // If we didn't want any message to get logged then we could just have done Trace.Listeners.GetEnumerator() instead.
                    Trace.TraceInformation("#Notification Successfully installed {0} service.", ServiceHelper.ServiceName);
                }
                catch (Exception e)
                {
                    Trace.TraceError("Failed to install service: {0}", e.ToString());
                }
            }
        }

        private static void UninstallService()
        {
            if (!IsServiceInstalled())
            {
                Trace.TraceInformation("The {0} service is not installed.", ServiceHelper.ServiceName);
                return;
            }

            String serviceAccount;
            using (Installer installer = CreateServiceInstaller(out serviceAccount))
            {
                try
                {
                    IDictionary state = null;
                    installer.Uninstall(state);
                    Trace.TraceInformation("#Notification Successfully uninstalled {0} service.", ServiceHelper.ServiceName);
                }
                catch (Exception e)
                {
                    Trace.TraceError("Failed to uninstall service: {0}", e.ToString());
                }
            }
        }

        private static Boolean IsServiceInstalled()
        {
            using (ServiceController controller = new ServiceController(ServiceHelper.ServiceName))
            {
                try
                {
                    ServiceControllerStatus status = controller.Status;
                }
                catch
                {
                    return false;
                }
                return true;
            }
        }

        private static Installer CreateServiceInstaller(out String serviceAccount)
        {
            Func<KeyValueConfigurationElement, String, String> coalesceValue =
                (element, defaultValue) => element == null ? defaultValue : element.Value;

            Assembly serviceAssembly = Assembly.GetEntryAssembly();
            Configuration serviceConfiguration = ConfigurationManager.OpenExeConfiguration(serviceAssembly.Location);
            KeyValueConfigurationCollection serviceAppSettings = serviceConfiguration.AppSettings.Settings;

            serviceAccount = coalesceValue(serviceAppSettings["ServiceAccount"], ServiceAccount.LocalSystem.ToString());
            ServiceAccount serviceAccountType;
            String serviceAccountUsername;
            String serviceAccountPassword;
            if (serviceAccount.Equals(ServiceAccount.LocalSystem.ToString(), StringComparison.CurrentCultureIgnoreCase) || serviceAccount.Equals(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Translate(typeof(NTAccount)).Value, StringComparison.CurrentCultureIgnoreCase))
            {
                serviceAccountType = ServiceAccount.LocalSystem;
                serviceAccountUsername = null;
                serviceAccountPassword = null;
                serviceAccount = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Translate(typeof(NTAccount)).Value;
            }
            else if (serviceAccount.Equals(ServiceAccount.LocalService.ToString(), StringComparison.CurrentCultureIgnoreCase) || serviceAccount.Equals(new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null).Translate(typeof(NTAccount)).Value, StringComparison.CurrentCultureIgnoreCase))
            {
                serviceAccountType = ServiceAccount.LocalService;
                serviceAccountUsername = null;
                serviceAccountPassword = null;
                serviceAccount = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null).Translate(typeof(NTAccount)).Value;
            }
            else if (serviceAccount.Equals(ServiceAccount.NetworkService.ToString(), StringComparison.CurrentCultureIgnoreCase) || serviceAccount.Equals(new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null).Translate(typeof(NTAccount)).Value, StringComparison.CurrentCultureIgnoreCase))
            {
                serviceAccountType = ServiceAccount.NetworkService;
                serviceAccountUsername = null;
                serviceAccountPassword = null;
                serviceAccount = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null).Translate(typeof(NTAccount)).Value;
            }
            else
            {
                serviceAccountType = ServiceAccount.User;
                serviceAccountUsername = serviceAccount;
                serviceAccountPassword = serviceAppSettings["ServiceAccountPassword"].Value;
            }

            TransactedInstaller installer = new TransactedInstaller();

            ServiceInstaller si = new ServiceInstaller();
            si.ServiceName = ServiceHelper.ServiceName;
            si.DisplayName = coalesceValue(serviceAppSettings["ServiceDisplayName"], serviceAssembly.GetName().Name);
            si.Description = coalesceValue(serviceAppSettings["ServiceDescription"], serviceAssembly.GetName().Name);
            // Add dependent services, if any
            if (serviceAppSettings["DependsOnServices"] != null)
            {
                si.ServicesDependedOn = serviceAppSettings["DependsOnServices"].Value.Split(new char[] { ',', ';', ' ' });
            }
            si.StartType = ServiceStartMode.Automatic;
            installer.Installers.Add(si);

            ServiceProcessInstaller spi = new ServiceProcessInstaller();
            spi.Account = serviceAccountType;
            spi.Username = serviceAccountUsername;
            spi.Password = serviceAccountPassword;
            installer.Installers.Add(spi);

            String serviceAssemblyPath = Path.GetFullPath(serviceAssembly.Location);
            if (installer.Context == null)
            {
                installer.Context = new InstallContext(Path.ChangeExtension(serviceAssemblyPath, ".InstallLog"), null);
            }
            installer.Context.Parameters["assemblypath"] = serviceAssemblyPath;

            return installer;
        }

        #endregion
    }

    /// <summary>
    /// This class implements the ServiceBase interface on behalf of the ServiceHelper class.
    /// </summary>
    public class GenericService : ServiceBase
    {
        Boolean m_isStopping;

        public GenericService()
        {
            m_isStopping = false;

            this.ServiceName = ConfigurationManager.AppSettings["ServiceName"] ?? Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
            this.CanShutdown = true;
        }

        protected override void OnStart(string[] args)
        {
            // The default working directory for a service is the system32 directory.
            // Change it to be the directory where the executable is located.
            Environment.CurrentDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
            //System.IO.Directory.SetCurrentDirectory(Application.StartupPath);

            // Normally we would call the service's start method here but to keep the interface simple the program's Main()
            // function does the service work.

            // The start method needs to return quickly so create a separate thread for the main program loop.
            //Thread thread = new Thread(new ThreadStart(Program.Run));
            //thread.Start();
        }

        protected override void OnStop()
        {
            if (!m_isStopping)
            {
                m_isStopping = true;
                Trace.TraceInformation(@"Stopping service {0}.", this.ServiceName);

                if ((ServiceHelper.ShutdownEvent == null) || (!ServiceHelper.ShutdownEvent.Set()))
                {
                    // Failed to signal shutdown event so there is no way to notify the program to shutdown gracefully.
                    Environment.FailFast("Failed to do graceful shutdown.");
                }

                // When this function returns, the service appears 'stopped'.
                // Idealy we don't want the service to appear stopped until it really is (ie. the program has done any cleanup necessary and
                // explicitly called EndService()) so wait upto 30s for that to happen.
                if (!ServiceHelper.ShutdownEvent2.WaitOne(30 * 1000))
                {
                    Environment.FailFast("Failed to do graceful shutdown in 1min.");
                }
            }
        }

        protected override void OnShutdown()
        {
            Trace.TraceInformation(@"Stopping service {0} because the system is shutting down.", this.ServiceName);
            this.Stop();
        }
    }
}
