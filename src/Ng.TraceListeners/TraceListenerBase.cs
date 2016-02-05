using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ng.TraceListeners
{
    abstract public class TraceListenerBase : TraceListener
    {
        public TraceListenerBase(String initializeData)
        {
            if (!String.IsNullOrWhiteSpace(initializeData))
            {
                foreach (String token in initializeData.ToLower().Split(',').Select(x => x.Trim()))
                {
                    switch (token)
                    {
                        case "datetime":
                            this.IncludeDate = true;
                            this.IncludeTime = true;
                            break;

                        case "date":
                            this.IncludeDate = true;
                            break;

                        case "time":
                            this.IncludeTime = true;
                            break;

                        case "threadid":
                            this.IncludeThreadId = true;
                            break;

                        case "computer":
                            this.IncludeComputerName = true;
                            break;
                    }
                }
            }

            this.IsEnabled = true;
        }

        public Boolean IsEnabled
        {
            get;
            protected set;
        }

        public Boolean IncludeDate
        {
            get;
            protected set;
        }

        public Boolean IncludeTime
        {
            get;
            protected set;
        }

        public Boolean IncludeThreadId
        {
            get;
            protected set;
        }

        public Boolean IncludeComputerName
        {
            get;
            protected set;
        }

        public override void Write(String message)
        {
            this.WriteLine(message);
        }

        public override void WriteLine(String message)
        {
            try
            {
                this.TraceSimpleEvent(DateTime.Now, Thread.CurrentThread.ManagedThreadId, TraceEventType.Information, message);
            }
            catch (Exception ex)
            {
                if (ex is NotImplementedException)
                {
                    string dateTimeString = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                    int managedThreadId = Thread.CurrentThread.ManagedThreadId;
                    Debug.WriteLine($"[.] {dateTimeString} #{managedThreadId, -2} {message}");
                }
                else
                {
                    // Tracing needs to be reliable otherwise it will be a pain to track down application errors, so make tracing errors fatal.
                    Environment.FailFast("Exception in trace listener.", ex);
                }
            }
        }

        // Gets called when someone does TraceSource.TraceEvent().
        public override void TraceEvent(TraceEventCache eventCache, String source, TraceEventType eventType, Int32 id)
        {
            TraceEvent(eventCache, source, eventType, id, null);
        }

        // Gets called when someone does Trace.TraceXxx() or TraceSource.TraceEvent().
        public override void TraceEvent(TraceEventCache eventCache, String source, TraceEventType eventType, Int32 id, String format, params object[] args)
        {
            if (args == null)
            {
                this.TraceEvent(eventCache, source, eventType, id, format);
            }
            else
            {
                this.TraceEvent(eventCache, source, eventType, id, String.Format(format, args));
            }
        }

        // Gets called when someone does Trace.TraceXxx() or TraceSource.TraceEvent().
        public override void TraceEvent(TraceEventCache eventCache, String source, TraceEventType eventType, Int32 id, String message)
        {
            // Extract #EventId:123 prefix.
            if ((message != null) && (message.Length > 0) && (message[0] == '#'))
            {
                if (message.StartsWith("#EventId:"))
                {
                    message = message.Substring(9).TrimStart();
                    String idAsString = new String(message.TakeWhile(c => Char.IsDigit(c)).ToArray());
                    if (idAsString != String.Empty)
                    {
                        id = Int32.Parse(idAsString);
                        message = message.Substring(idAsString.Length).TrimStart();
                    }
                }
            }

            try
            {
                // Note: By default, the id is 0.
                TraceSimpleEvent(eventCache.DateTime.ToLocalTime(), Thread.CurrentThread.ManagedThreadId, eventType, message, id, eventCache, source);
            }
            catch (Exception ex)
            {
                if (ex is NotImplementedException)
                {
                    string dateTimeString = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                    int managedThreadId = Thread.CurrentThread.ManagedThreadId;
                    Debug.WriteLine($"[{eventType}] {dateTimeString} #{managedThreadId,-2} {message}");
                }
                else if (ex is ThreadAbortException)
                {
                    // Thread is being externally aborted (e.g. because appdomain is being unloaded). Don't fastfail - as that will kill the entire process.
                    throw;
                }
                else
                {
                    // Tracing needs to be reliable otherwise it will be a pain to track down application errors, so make tracing errors fatal.
                    Environment.FailFast("Exception in trace listener.", ex);
                }
            }
        }

        protected virtual void TraceSimpleEvent(DateTime eventTime, Int32 threadId, TraceEventType eventType, String message, Int32 eventId, TraceEventCache eventCache, String source)
        {
            TraceSimpleEvent(eventTime, threadId, eventType, message, eventId);
        }

        protected virtual void TraceSimpleEvent(DateTime eventTime, Int32 threadId, TraceEventType eventType, String message, Int32 eventId)
        {
            TraceSimpleEvent(eventTime, threadId, eventType, message);
        }

        protected virtual void TraceSimpleEvent(DateTime eventTime, Int32 threadId, TraceEventType eventType, String message)
        {
            throw new NotImplementedException();
        }

        protected string GetEventString(DateTime eventTime, Int32 threadId, TraceEventType eventType, String message, bool includeDate = true, bool includeTime = true, bool includeComputerName = true, bool includeThreadId = true)
        {
            StringBuilder text = new StringBuilder();
            switch (eventType)
            {
                case TraceEventType.Error:
                case TraceEventType.Critical:
                    text.Append("[!]");
                    break;

                case TraceEventType.Warning:
                    text.Append("[~]");
                    break;

                case TraceEventType.Information:
                    text.Append("[.]");
                    break;

                default:
                    text.Append("[$]");

                    // If the message is not critical/error/warning/informational, include the event type in the message.
                    message = $"#{eventType} {message}";
                    break;
            }

            DateTime now = DateTime.Now;
            if (includeDate)
            {
                text.Append($" {now.ToString("yyyy/MM/dd")}");
            }

            if (includeTime)
            {
                text.Append($" {now.ToString("HH:mm:ss")}");
            }

            if (includeComputerName)
            {
                text.Append($" {Environment.MachineName}");
            }

            if (includeThreadId)
            {
                text.Append($" {threadId, -2}");
            }

            text.Append($" {message}");

            return text.ToString();
        }
    }
}
