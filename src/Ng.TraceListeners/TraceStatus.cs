using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.TraceListeners
{
    public class TraceStatus : IDisposable
    {
        string _activity;
        Stopwatch _stopwatch;

        public TraceStatus(string activity, string details = null)
        {
            if (String.IsNullOrWhiteSpace(activity))
            {
                throw new ArgumentNullException("activity");
            }

            this._activity = activity;
            this._stopwatch = new Stopwatch();
            this._stopwatch.Start();

            TraceStatus.TraceInformation(activity, state: "Start", details: details);
        }

        public void Dispose()
        {
            this._stopwatch.Stop();
            TraceStatus.TraceInformation(this._activity, state: "Stop", details: $"Elapsed Seconds: {this._stopwatch.Elapsed.TotalSeconds.ToString("#,###")}");
        }

        public static void TraceInformation(string activity = null, string state = null, string result = null, string details = null)
        {
            System.Diagnostics.Trace.TraceInformation(ComposeString(activity, state, result, details));
        }

        public static void TraceWarning(string activity = null, string state = null, string result = null, string details = null)
        {
            System.Diagnostics.Trace.TraceWarning(ComposeString(activity, state, result, details));
        }

        public static void TraceError(string activity = null, string state = null, string result = null, string details = null)
        {
            System.Diagnostics.Trace.TraceError(ComposeString(activity, state, result, details));
        }

        private static string ComposeString(string activity, string state, string result, string details)
        {
            StringBuilder text = new StringBuilder();
            text.Append("#Status");

            if (!String.IsNullOrWhiteSpace(activity))
            {
                text.Append($" #Activity={ReplaceInvalidCharacters(activity)}");
            }

            if (!String.IsNullOrWhiteSpace(state))
            {
                text.Append($" #State={ReplaceInvalidCharacters(state)}");
            }

            if (!String.IsNullOrWhiteSpace(result))
            {
                text.Append($" #Result={ReplaceInvalidCharacters(result)}");
            }

            if (!String.IsNullOrWhiteSpace(details))
            {
                text.Append($" #Details={ReplaceInvalidCharacters(details)}");
            }

            return text.ToString();
        }

        private static string ReplaceInvalidCharacters(string text)
        {
            text = text.Replace('=', '_');
            text = text.Replace('#', '_');

            return text;
        }
    }
}
