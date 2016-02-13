using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ng.TraceListeners
{
    public class ConsoleTraceListener : TraceListenerBase
    {
        public ConsoleTraceListener(String initializeData) : base(initializeData)
        {
            // Ignore trace messages if running as a service, as a scheduled task, or in Azure (where console output won't be seen), unless stdout has been redirected.
            if (!Environment.UserInteractive)
            {
                if (!IsStdoutRedirected())
                {
                    this.IsEnabled = false;
                }
            }
        }

        public ConsoleTraceListener()
            : this(null)
        {
        }

        protected override void TraceSimpleEvent(DateTime eventTime, Int32 threadId, TraceEventType eventType, String message)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            String text = this.GetEventString(eventTime, threadId, eventType, message);

            // Lock to prevent interleaving of messages from multiple threads.
            lock (this)
            {
                Console.WriteLine(text);
            }
        }

        public static Boolean IsStdoutRedirected()
        {
            IntPtr stdout = GetStdHandle(StdHandle.Stdout);
            if (stdout == IntPtr.Zero)
            {
                // No standard output handle exists.
                return false;
            }

            switch (GetFileType(stdout))
            {
                case FileType.Char:
                    return false;

                case FileType.Disk:
                case FileType.Pipe:
                    return true;

                default:
                    throw Marshal.GetExceptionForHR(Marshal.GetLastWin32Error());
            }
        }

        private enum FileType { Unknown, Disk, Char, Pipe };

        private enum StdHandle { Stdin = -10, Stdout = -11, Stderr = -12 };

        [DllImport("kernel32.dll", EntryPoint = "GetStdHandle", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr GetStdHandle(StdHandle nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern FileType GetFileType(IntPtr handle);
    }
}
