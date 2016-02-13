using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.TraceListeners
{
    public class AzureTraceListener : TraceListenerBase
    {
        private DiagnosticMonitorTraceListener _traceListener = null;

        static AzureTraceListener()
        {
        }

        private static Boolean IsRunningInAzureRoleQuickCheck()
        {
            return (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("__WaRuntimeAgent__")));
        }

        public AzureTraceListener(String initializeData) : base(initializeData)
        {
            if (IsRunningInAzureRoleQuickCheck() && RoleEnvironment.IsAvailable)
            {
                this._traceListener = new DiagnosticMonitorTraceListener();
            }
        }

        protected override void TraceSimpleEvent(DateTime eventTime, Int32 threadId, TraceEventType eventType, String message, Int32 eventId, TraceEventCache eventCache, String source)
        {
            if (this._traceListener != null)
            {
                this._traceListener.TraceEvent(eventCache, source, eventType, eventId, message);
            }
        }
    }
}
