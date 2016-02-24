using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Search.Common
{
    public static class Email
    {
        public static void SendEmail(string subject, string body, string recipients)
        {
            if (String.IsNullOrWhiteSpace(subject))
            {
                throw new ArgumentNullException("subject");
            }

            if (String.IsNullOrWhiteSpace(body))
            {
                throw new ArgumentNullException("body");
            }

            if (String.IsNullOrWhiteSpace(recipients))
            {
                throw new ArgumentNullException("recipients");
            }

            using (SmtpClient client = new SmtpClient("smtphost"))
            {
                client.UseDefaultCredentials = true;
                MailAddress from = new MailAddress(Environment.UserName);

                using (MailMessage message = new MailMessage())
                {
                    message.From = from;

                    foreach (String to in recipients.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        message.To.Add(to);
                    }

                    message.Subject = subject;
                    message.SubjectEncoding = System.Text.Encoding.UTF8;

                    message.Body = body;
                    message.BodyEncoding = System.Text.Encoding.UTF8;
                    message.IsBodyHtml = true;

                    client.Send(message);
                }
            }
        }

        public static void SendSuccessEmail(string recipients, string optionsHtml, String[] args)
        {
            string subject = "Success: " + System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
            string body = "Parameters: " + string.Join(" ", args) + "<br />";
            body += optionsHtml;
            body += "Computer: " + Environment.MachineName + "<br />";
            body += "User: " + Environment.UserName + "<br />";

            Email.SendEmail(subject, body, recipients);
        }

        public static void SendFailureEmail(string recipients, string optionsHtml, String[] args, Exception e)
        {
            string subject = "Failure: " + System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
            string body = "Parameters: " + string.Join(" ", args) + "<br />";
            body += optionsHtml;
            body += "Computer: " + Environment.MachineName + "<br />";
            body += "User: " + Environment.UserName + "<br />";
            body += "Exception Message:<br />";
            body += e.ToString();
            body = body.Replace("\n", "<br />");

            Email.SendEmail(subject, body, recipients);
        }
    }
}
