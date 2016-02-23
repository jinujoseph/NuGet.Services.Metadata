using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.Sarif
{
    /// <summary>
    /// Logs the start and stop time of an activity.
    /// </summary>
    public class ActivityTimer : StatusTimer
    {
        public ActivityTimer(string activity, string details = null) : base(activity, details, "NG906", "NG907")
        {
        }
    }
}
