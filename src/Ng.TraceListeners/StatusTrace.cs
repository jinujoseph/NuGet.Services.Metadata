using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.TraceListeners
{
    public static class StatusTrace
    {
        public static void TraceInformation(string message, string data = null)
        {
            System.Diagnostics.Trace.TraceInformation(ComposeString(message, data));
        }

        public static void TraceWarning(string message, string data = null)
        {
            System.Diagnostics.Trace.TraceWarning(ComposeString(message, data));
        }

        public static void TraceError(string message, string data = null)
        {
            System.Diagnostics.Trace.TraceError(ComposeString(message, data));
        }

        private static string ComposeString(string message, string data = null)
        {
            StringBuilder text = new StringBuilder();
            text.Append("#Status");

            if (!String.IsNullOrWhiteSpace(message))
            {
                text.Append($" #Message={ReplaceInvalidCharacters(message)}");
            }

            if (!String.IsNullOrWhiteSpace(data))
            {
                text.Append($"{Environment.NewLine} #Data={ReplaceInvalidCharacters(data)}");
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
