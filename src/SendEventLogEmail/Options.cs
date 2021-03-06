﻿using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.SendEventLogEmail
{
    public class Options
    {
        public Options()
        {
        }

        private const string eventIdText = "eventid";
        private const string eventSourceText = "eventsource";
        private const string emailFromText = "emailfrom";
        private const string emailFromPasswordText = "emailfrompassword";
        private const string emailToText = "emailto";
        private const string emailSmtpText = "emailsmtp";
        private const string ignoreInformationText = "ignoreinformation";
        private const string ignoreWarningText = "ignorewarning";
        private const string ignoreErrorText = "ignoreerror";
        private const string tableStorageText = "tablestorage";
        private const string knownLogsTableText = "knownlogstable";

        [Option('i', Options.eventIdText, Required = true, HelpText = @"The event id of the event to send email for.")]
        public int EventId { get; set; }

        [Option('s', Options.eventSourceText, Required = true, HelpText = @"The event source of the event to send email for.")]
        public string EventSource { get; set; }

        [Option('f', Options.emailFromText, Required = false, HelpText = @"The email address to send the email from.")]
        public string EmailFrom { get; set; }

        [Option('p', Options.emailFromPasswordText, Required = false, HelpText = @"The password for the email address to send the email from.")]
        public string EmailFromPassword { get; set; }

        [Option('t', Options.emailToText, Required = false, HelpText = @"The list of email addresses to send the email to. For multiple addresses separate with a ';'.")]
        public string EmailTo { get; set; }

        [Option('m', Options.emailSmtpText, Required = false, HelpText = @"The smtp server to send the email from.")]
        public string EmailSmtp { get; set; }

        [Option('n', Options.ignoreInformationText, Required = false, HelpText = @"True to ignore information logs.")]
        public bool IgnoreInformation { get; set; }

        [Option('w', Options.ignoreWarningText, Required = false, HelpText = @"True to ignore warning logs.")]
        public bool IgnoreWarning { get; set; }

        [Option('e', Options.ignoreErrorText, Required = false, HelpText = @"True to ignore error logs.")]
        public bool IgnoreError { get; set; }

        [Option('a', Options.tableStorageText, Required = false, HelpText = @"The Azure table storage connection string.")]
        public string TableStorageConnectionString { get; set; }

        [Option('l', Options.knownLogsTableText, Required = false, HelpText = @"The table name which contains the known logs.")]
        public string KnownLogsTableName { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }

        public string GetParameterValueText()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Input Parameters" + Environment.NewLine);
            text.Append($"    {eventIdText}: {this.EventId}{ Environment.NewLine}");
            text.Append($"    {eventSourceText}: {this.EventSource}{ Environment.NewLine}");
            text.Append($"    {emailFromText}: {this.EmailFrom}{ Environment.NewLine}");
            text.Append($"    {emailFromPasswordText}: {new String('X', this.EmailFromPassword.Length)}{ Environment.NewLine}");
            text.Append($"    {emailToText}: {this.EmailTo}{ Environment.NewLine}");
            text.Append($"    {emailSmtpText}: {this.EmailSmtp}{ Environment.NewLine}");
            text.Append($"    {ignoreInformationText}: {this.IgnoreInformation}{ Environment.NewLine}");
            text.Append($"    {ignoreWarningText}: {this.IgnoreWarning}{ Environment.NewLine}");
            text.Append($"    {ignoreErrorText}: {this.IgnoreError}{ Environment.NewLine}");
            text.Append($"    {tableStorageText}: {this.TableStorageConnectionString}{ Environment.NewLine}");
            text.Append($"    {knownLogsTableText}: {this.KnownLogsTableName}{ Environment.NewLine}");

            return text.ToString();
        }

        public void ReadAppConfigValues()
        {
            this.EmailFrom = GetConfigValue(emailFromText, this.EmailFrom);
            this.EmailFromPassword = GetConfigValue(emailFromPasswordText, this.EmailFromPassword);
            this.EmailTo = GetConfigValue(emailToText, this.EmailTo);
            this.EmailSmtp = GetConfigValue(emailSmtpText, this.EmailSmtp);
            this.IgnoreInformation = GetConfigValue(ignoreInformationText, this.IgnoreInformation);
            this.IgnoreWarning = GetConfigValue(ignoreWarningText, this.IgnoreWarning);
            this.IgnoreError = GetConfigValue(ignoreErrorText, this.IgnoreError);
            this.TableStorageConnectionString = GetConfigValue(tableStorageText, String.Empty);
            this.KnownLogsTableName = GetConfigValue(knownLogsTableText, String.Empty);
        }

        public bool Validate()
        {
            List<string> validationErrors = new List<string>();

            if (this.EventId < 0)
            {
                validationErrors.Add("eventid must be specified.");
            }

            if (String.IsNullOrWhiteSpace(this.EventSource))
            {
                validationErrors.Add("eventsource must be specified.");
            }

            if (String.IsNullOrWhiteSpace(this.EmailFrom))
            {
                validationErrors.Add("emailfrom must be specified.");
            }

            if (String.IsNullOrWhiteSpace(this.EmailFromPassword))
            {
                validationErrors.Add("emailfrompassword must be specified.");
            }

            if (String.IsNullOrWhiteSpace(this.EmailTo))
            {
                validationErrors.Add("emailto must be specified.");
            }

            if (String.IsNullOrWhiteSpace(this.EmailSmtp))
            {
                validationErrors.Add("emailsmtp must be specified.");
            }

            if (String.IsNullOrWhiteSpace(this.TableStorageConnectionString))
            {
                validationErrors.Add("tablestorage must be specified.");
            }

            if (String.IsNullOrWhiteSpace(this.KnownLogsTableName))
            {
                validationErrors.Add("knownLogsTableText must be specified.");
            }

            if (validationErrors.Count > 0)
            {
                Trace.TraceError(String.Join(Environment.NewLine, validationErrors.ToArray()));
                return false;
            }
            else
            {
                return true;
            }
        }

        private String GetConfigValue(String keyName, String defaultValue)
        {
            String value = System.Configuration.ConfigurationManager.AppSettings[keyName];

            if (value == null)
            {
                value = defaultValue;
            }

            return value;
        }

        private bool GetConfigValue(String keyName, bool defaultValue)
        {
            bool value = defaultValue;
            String valueText = System.Configuration.ConfigurationManager.AppSettings[keyName];

            if (String.IsNullOrWhiteSpace(valueText) || !bool.TryParse(valueText, out value))
            {
                value = defaultValue;
            }

            return value;
        }
    }
}
