using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.Sarif
{
    /// <summary>
    /// Logs the start and stop time of an activity.
    /// </summary>
    public abstract class StatusTimer : IDisposable
    {
        string _activity;
        string _startRuleId;
        string _stopRuleId;
        Stopwatch _stopwatch;

        public StatusTimer(string activity, string details, string startRuleId, string stopRuleId)
        {
            if (String.IsNullOrWhiteSpace(activity))
            {
                throw new ArgumentNullException("activity");
            }

            if (String.IsNullOrWhiteSpace(startRuleId))
            {
                throw new ArgumentNullException("startRuleId");
            }

            if (String.IsNullOrWhiteSpace(stopRuleId))
            {
                throw new ArgumentNullException("stopRuleId");
            }

            if (String.IsNullOrWhiteSpace(details))
            {
                details = String.Empty;
            }

            this._activity = activity;
            this._startRuleId = startRuleId;
            this._stopRuleId = stopRuleId;
            this._stopwatch = new Stopwatch();
            this._stopwatch.Start();

            SarifTraceListener.TraceInformation(_startRuleId, $"Start {_activity} {details}");
        }

        public void Dispose()
        {
            this._stopwatch.Stop();
            SarifTraceListener.TraceInformation(_stopRuleId, $"Stop {_activity} Elapsed Seconds: {this._stopwatch.Elapsed.TotalSeconds.ToString("#,###")}");
        }
    }
}
