using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ng.TraceListeners
{
    /// <summary>
    /// Replacement for System.Diagnostics.EventLogTraceListener.
    /// Redirects warning/error/notification traces to the 'NuGet' custom event log (overrideable).
    /// Ignores information/verbose traces
    /// 
    /// NOTE: The app using this must be run atleast once as LocalSystem/Administrator to create the event source. Subsequent runs can be under any user account.
    /// </summary>
    public class EventLogTraceListener : TraceListenerBase
    {
        // Note: Only the first 8 characters of a custom event log name are significant.
        // (eg. 'Microsoft Detections' and 'Microsoft NuGet' would be treated as the same log.)
        private String _eventLogName = "NuGet";

        private String _eventSourceName;

        // Constructor used when initializeData is specified.
        // Note: The constructor only gets called when trace source is first used or someone accesses Trace.Listeners (not on application startup).
        public EventLogTraceListener(String initializeData) : base(initializeData)
        {
            try
            {
                // Parse initializeData as "key1=value1,key2=value2,..."
                if (initializeData != null)
                {
                    foreach (String keyValuePair in initializeData.Split(','))
                    {
                        String[] parts = keyValuePair.Split('=');
                        if (parts.Length == 2)
                        {
                            if (parts[0].Equals("source", StringComparison.InvariantCultureIgnoreCase)) this._eventSourceName = parts[1];
                            else if (parts[0].Equals("log", StringComparison.InvariantCultureIgnoreCase)) this._eventLogName = parts[1];
                        }
                    }
                }

                if (this._eventSourceName == null)
                {
                    this._eventSourceName = ConfigurationManager.AppSettings["ServiceName"] ?? System.IO.Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.ModuleName);
                }

                CreateEventSourceIfNecessary();
            }
            catch (Exception ex)
            {
                if (ex is ThreadAbortException)
                {
                    // Thread is being externally aborted (eg. because appdomain is being unloaded). Don't fastfail - as that will kill the entire process.
                    throw;
                }
                else
                {
                    Environment.FailFast("Exception in EventLogTraceListener.EventLogTraceListener().", ex);
                }
            }
        }

        public EventLogTraceListener()
            : this(null)
        {
        }

        private void CreateEventSourceIfNecessary()
        {
            // Create event source if necessary.
            try
            {
                // Check that the event source exists and is registered under the correct event log.
                if (EventLog.SourceExists(this._eventSourceName))
                {
                    String logName = EventLog.LogNameFromSourceName(this._eventSourceName, ".");
                    if (logName.Equals(this._eventLogName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return;
                    }

                    // Event source already exists but is registered to a different event log. Delete it so it can be 
                    // re-registered to the desired event log.
                    EventLog.DeleteEventSource(this._eventSourceName);
                }

                EventLog.CreateEventSource(this._eventSourceName, this._eventLogName);
            }
            catch (System.Security.SecurityException ex)
            {
                // EventSource does not exist and we do not have permissions to create it.
                // Log error to Application event log and bail.
                Environment.FailFast(String.Format("Unable to create event source {0}. Re-run application as Administrator.", this._eventSourceName), ex);
            }
        }

        protected override void TraceSimpleEvent(DateTime eventTime, Int32 threadId, TraceEventType eventType, String message, Int32 eventId)
        {
            EventLogEntryType entryType;
            switch (eventType)
            {
                case TraceEventType.Critical:
                case TraceEventType.Error:
                    entryType = EventLogEntryType.Error;
                    break;

                case TraceEventType.Warning:
                    entryType = EventLogEntryType.Warning;
                    break;

                default:
                    // Don't log any other events to the event log.
                    return;
            }

            String eventText = message;

            try
            {
                StackTrace stackTrace = GetStackTraceForCurrentThread();

                // Generate event id based on hash of callstack so its easy to distinguish distinct events in Event Viewer.
                // Event id has to be between 0..65535
                if (eventId == 0)
                {
                    eventId = 0;

                    foreach (StackFrame stackFrame in stackTrace.GetFrames())
                    {
                        System.Reflection.MethodBase stackFrameMethod = stackFrame.GetMethod();
                        eventId ^= stackFrameMethod.Module.Name.GetHashCode();
                        eventId ^= stackFrameMethod.Name.GetHashCode();
                    }

                    // Restrict generated event ids to the range 32768..65535 so that explicit event ids can be kept separate.
                    eventId = Math.Abs((eventId ^ (eventId >> 16)) & 0x00007FFF) + 0x00008000;
                }

                eventText = String.Format("{0}\n\nTHREAD: {1}\n\nCALLSTACK:\n{2}\n", message, threadId, stackTrace.ToString());
            }
            catch
            {
                // Not being able to capture a callstack or generate an eventId is not fatal.
            }

            // Attempting to write events where message string is longer than 31,839 bytes (32,766 bytes on Windows operating systems before Windows Vista) will throw an ArgumentException.
            // So truncating eventText to 15,919 chars (31,838 unicode bytes).
            // http://msdn.microsoft.com/en-us/library/xzwc042w(v=vs.100).aspx
            const Int32 EVENTLOG_MAX_MESSAGESTRING_CHARS = 15919;
            if (eventText.Length > EVENTLOG_MAX_MESSAGESTRING_CHARS)
            {
                eventText = eventText.Substring(0, EVENTLOG_MAX_MESSAGESTRING_CHARS);
            }

            EventLog.WriteEntry(this._eventSourceName, eventText, entryType, eventId);
        }

        /// <summary>
        /// Retrieves a stack trace for the current thread, omitting tracing-related frames.
        /// </summary>
        protected StackTrace GetStackTraceForCurrentThread()
        {
            StackTrace stackTrace = new StackTrace();

            // Skip tracing-related frames by searching back up the callstack for TraceSimpleEvent() then the first frame not in this assembly or System.dll.
            Boolean baseFrameFound = false;
            Int32 framesToSkip = 0;
            foreach (StackFrame stackFrame in stackTrace.GetFrames())
            {
                System.Reflection.MethodBase method = stackFrame.GetMethod();
                if (baseFrameFound)
                {
                    if (!(method.Module.Assembly.Equals(System.Reflection.Assembly.GetExecutingAssembly()) || method.Module.Assembly.ManifestModule.Name.Equals("System.dll")))
                    {
                        return new StackTrace(framesToSkip);
                    }
                }
                else
                {
                    if (method.Module.Assembly.Equals(this.GetType().Assembly) && method.Name.Equals("TraceSimpleEvent"))
                    {
                        baseFrameFound = true;
                    }
                }
                framesToSkip++;
            }

            // Unable to find the expected frames on the callstack. Just return a full stacktrace.
            return stackTrace;
        }
    }
}
