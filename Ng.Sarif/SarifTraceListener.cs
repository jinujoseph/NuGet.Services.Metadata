using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Sdk;
using Ng.Sarif.Sdk;
using Ng.TraceListeners;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ng.Sarif
{
    /// <summary>
    /// Helper class to log to the Azure tables.
    /// </summary>
    public class SarifTraceListener : AzureTableStorageStatusTraceListener
    {
        static SarifTraceListener()
        {
            AggregateCatalog catalog = new AggregateCatalog();
            catalog.Catalogs.Add(new AssemblyCatalog(System.Reflection.Assembly.GetExecutingAssembly()));
            CompositionContainer container = new CompositionContainer(catalog);
            SarifTraceListener.Rules = container.GetExportedValues<IRuleDescriptor>();
        }

        public static IEnumerable<IRuleDescriptor> Rules
        {
            get;
            private set;
        }

        public SarifTraceListener(String initializeData) : base(initializeData)
        {
        }

        public static void TraceInformation(string message)
        {
            TraceInformation("NG001", message);
        }

        public static void TraceInformation(string ruleId, string message)
        {
            TraceResult(ruleId, message, Level.Information);
        }

        public static void TraceInformation(string ruleId, string message, Exception exception)
        {
            TraceResult(ruleId, message, exception, Level.Information);
        }

        public static void TraceWarning(string message)
        {
            TraceWarning("NG002", message);
        }

        public static void TraceWarning(string ruleId, string message)
        {
            TraceResult(ruleId, message, Level.Warning);
        }

        public static void TraceWarning(string ruleId, string message, Exception exception)
        {
            TraceResult(ruleId, message, exception, Level.Warning);
        }

        public static void TraceError(string message)
        {
            TraceError("NG003", message);
        }

        public static void TraceError(string ruleId, string message)
        {
            TraceResult(ruleId, message, Level.Error);
        }

        public static void TraceError(string ruleId, string message, Exception exception)
        {
            TraceResult(ruleId, message, exception, Level.Error);
        }

        private static void TraceResult(string ruleId, string message, Level messageLevel)
        {
            TraceResult(ruleId, message, null, messageLevel);
        }

        private static void TraceResult(string ruleId, string message, Exception exception, Level messageLevel)
        {
            IRuleDescriptor rule = SarifTraceListener.Rules.Where(r => r.Id.Equals(ruleId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            // Create a result for the rule.
            Result result = new Result();
            result.RuleId = rule == null ? ruleId : rule.Id;
            result.ShortMessage = message;
            result.FullMessage = message;

            switch (messageLevel)
            {
                case Level.Information:
                    result.Kind = ResultKind.Note;
                    break;
                case Level.Warning:
                    result.Kind = ResultKind.Warning;
                    break;
                case Level.Error:
                default:
                    result.Kind = ResultKind.Error;
                    break;
            }

            if (exception != null)
            {
                result.FullMessage += Environment.NewLine + Environment.NewLine + exception.ToString();
                result.Stacks = exception.ToCodeLocations();
            }

            // Add some properties about the machine.
            result.Properties = new Dictionary<string, string>();
            result.Properties["Machine"] = Environment.MachineName;
            result.Properties["ThreadId"] = System.Threading.Thread.CurrentThread.ManagedThreadId.ToString();
            result.Properties["EventTime"] = DateTime.Now.ToUniversalTime().ToString();
            result.Properties["Application"] = System.Reflection.Assembly.GetEntryAssembly() != null ? Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location) : null;
            result.Properties["ProcessId"] = System.Diagnostics.Process.GetCurrentProcess().Id.ToString();

            string json = result.ToJson();

            // Write to the log.
            switch (messageLevel)
            {
                case Level.Information:
                    StatusTrace.TraceInformation(result.FullMessage, json);
                    break;
                case Level.Warning:
                    StatusTrace.TraceWarning(result.FullMessage, json);
                    break;
                case Level.Error:
                default:
                    StatusTrace.TraceError(result.FullMessage, json);
                    break;
            }
        }

        /// <summary>
        /// Write the sarif ToolInfo to the log.
        /// </summary>
        public static void TraceToolInfo()
        {
            Assembly entryAssembly = Assembly.GetEntryAssembly();

            ToolInfo toolInfo = new ToolInfo();
            toolInfo.FullName = Path.GetFileName(entryAssembly.Location);
            toolInfo.Name = Path.GetFileNameWithoutExtension(entryAssembly.Location);
            toolInfo.FileVersion = entryAssembly.GetName().Version.ToString();
            toolInfo.Version = toolInfo.FileVersion;

            string json = toolInfo.ToJson();

            StatusTrace.TraceInformation("NG908", json);
        }

        /// <summary>
        /// Write the sarif RunInfo to the log.
        /// </summary>
        /// <param name="invocationInfo"></param>
        public static void TraceRunInfo(string invocationInfo)
        {
            Assembly entryAssembly = Assembly.GetEntryAssembly();

            RunInfo runInfo = new RunInfo();
            runInfo.InvocationInfo = invocationInfo;

            string json = runInfo.ToJson();

            StatusTrace.TraceInformation("NG909", json);
        }

        /// <summary>
        /// The trace level for a log.
        /// </summary>
        private enum Level
        {
            Information,
            Warning,
            Error,
        }
    }
}
