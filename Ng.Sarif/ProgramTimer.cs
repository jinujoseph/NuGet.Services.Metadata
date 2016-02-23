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
    /// Logs the start and stop time of an application.
    /// </summary>
    public class ProgramTimer : StatusTimer
    {
        public ProgramTimer(string details = null) : base(Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location), details, "NG904", "NG905")
        {
        }
    }
}
