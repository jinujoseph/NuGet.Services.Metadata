using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Ng.SendEventLogEmail
{
    class Program
    {
        static int Main(string[] args)
        {
            Options options = new Options();

            try
            {
                options.ReadAppConfigValues();

                if (!CommandLine.Parser.Default.ParseArguments(args, options))
                {
                    return 1;
                }

                if (!options.Validate())
                {
                    Trace.TraceInformation(options.GetUsage());
                    return 1;
                }

                Trace.TraceInformation(options.GetParameterValueText());

                Program program = new Program();
                program.Run(options);
            }
            catch (Exception e)
            {
                Trace.TraceError("Exception in main thread!\n{0}\n", e.ToString());
                return 2;
            }

            return 0;
        }

        void Run(Options options)
        {
            string level = "Message";
            string provider = String.Empty;
            string eventHtml = String.Empty;

            //Query the event log for the event record.
            Trace.WriteLine($"Fetching event log record {options.EventSource} {options.EventId}.");
            EventLogQuery query = new EventLogQuery($"{options.EventSource}", PathType.LogName, $"*[System/EventRecordID = {options.EventId}]");
            using (EventLogReader reader = new EventLogReader(query))
            {
                EventRecord record = reader.ReadEvent();

                // If we found the record, add the event message to the email body.
                // If we didn't find the record, we'll still send the email, but it won't include any of the event data.
                if (record != null)
                {
                    if (record.LevelDisplayName.Equals("Information", StringComparison.OrdinalIgnoreCase) && options.IgnoreInformation)
                    {
                        Trace.TraceInformation("Ignoring information message");
                        return;
                    }
                    else if (record.LevelDisplayName.Equals("Warning", StringComparison.OrdinalIgnoreCase) && options.IgnoreWarning)
                    {
                        Trace.TraceInformation("Ignoring warning message");
                        return;
                    }
                    else if (record.LevelDisplayName.Equals("Error", StringComparison.OrdinalIgnoreCase) && options.IgnoreError)
                    {
                        Trace.TraceInformation("Ignoring error message");
                        return;
                    }

                    Trace.WriteLine($"Received event data.");

                    level = record.LevelDisplayName;
                    provider = record.ProviderName;

                    eventHtml += $"<div>MESSAGE: <br/>{HttpUtility.HtmlEncode(record.FormatDescription()).Replace("\n", "<br/>")}</div>";
                }
            }

            // This will be the email body text.
            string line1 = $"{level} message from {Environment.MachineName}";
            string line2 = $"    Source: {options.EventSource}/{provider}";
            string line3 = $"    EventRecordID: {options.EventId}";

            string messageText = $"<div>{HttpUtility.HtmlEncode(line1)}</div>";
            messageText += $"<div>&nbsp;&nbsp;&nbsp;&nbsp;{HttpUtility.HtmlEncode(line2)}</div>";
            messageText += $"<div>&nbsp;&nbsp;&nbsp;&nbsp;{HttpUtility.HtmlEncode(line3)}</div>";
            messageText += $"<div>&nbsp;</div>";
            messageText += eventHtml;

            // Send the email
            MailMessage message = new MailMessage();
            message.From = new MailAddress(options.EmailFrom.Trim());
            foreach (string emailTo in options.EmailTo.Split(',', ';'))
            {
                message.To.Add(new MailAddress(emailTo.Trim()));
            }
            message.Subject = $"{options.EventSource} Service {level} message #{options.EventId}";
            message.Body = messageText;
            message.IsBodyHtml = true;

            SmtpClient client = new SmtpClient(options.EmailSmtp);
            client.UseDefaultCredentials = false;
            client.Credentials = new System.Net.NetworkCredential(options.EmailFrom, options.EmailFromPassword);
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.EnableSsl = true;
            client.Send(message);

            Trace.WriteLine($"Sent email to {options.EmailTo}.");
        }
    }
}
