using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace NuGet.Search.IndexerService
{
    /// <summary>
    /// This is a utility class to help access config setting.
    /// </summary>
    public class Config
    {
        public String PackageVersionElasticSearchServerUrl { get; set; }
        public String ElasticSearchServerUrl { get; set; }
        public String OutputDirectory { get; set; }
        public String QueueDirectory { get; set; }
        public String IndexName { get; set; }
        public String FailedDirectory { get; set; }
        public Int32 MaxIndexerThreads { get; set; }
        public String PackageSource { get; set; }
        public bool IndexLatestVersion { get; set; }
        public bool IndexAbsoluteLatestVersion { get; set; }
        public bool IndexHistoricalVersions { get; set; }

        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        /// <summary>
        /// Read the application's .config file and populates property values.
        /// </summary>
        internal bool Initialize()
        {
            this.ElasticSearchServerUrl = this.GetConfigValue("ElasticSearchServerUrl", false) ?? @"http://mikefan-server0:9200/";
            this.OutputDirectory = this.GetConfigValue("OutputDirectory", false) ?? @".\OUTPUT";
            this.QueueDirectory = this.GetConfigValue("QueueDirectory", false) ?? @".\QUEUE";
            this.IndexName = this.GetConfigValue("IndexName", false) ?? "nuget-" + Environment.UserName;
            this.FailedDirectory = this.GetConfigValue("FailedDirectory", false) ?? @".\FAILED";
            this.MaxIndexerThreads = Int32.Parse(this.GetConfigValue("MaxIndexerThreads", false) ?? "1");
            this.PackageSource = this.GetConfigValue("PackageSource", false);
            this.IndexLatestVersion = Boolean.Parse(this.GetConfigValue("IndexLatestVersion", false) ?? "true");
            this.IndexAbsoluteLatestVersion = Boolean.Parse(this.GetConfigValue("IndexAbsoluteLatestVersion", false) ?? "true");
            this.IndexHistoricalVersions = Boolean.Parse(this.GetConfigValue("IndexHistoricalVersions", false) ?? "true");


            // Config values can also be specified/overriden on the commandline.
            String[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                if (!ParseCommandLineArguments(args))
                {
                    return false;
                }

                Trace.TraceInformation("Parsed commandline arguments.");
            }

            return true;
        }

        internal void ShowUsage()
        {
            Console.Write(
                "{0} [options] <package source>\n" +
                "\n" +
                "Options:\n" +
                "  -e <ElasticSearch server>         # ElasticSearch server URL\n" +
                "  -i <index name>                   # ElasticSearch index name\n" +
                "  -od <output directory>                   # Output directory\n" +
                "  -xml <xml configuration file>                   # XML configuration file\n" +
                "  -pid <package name>                   # Nuget package Id to index\n" +
                "  -pv <package version>                   # Nuget package version to index\n" +
                "",
                System.Diagnostics.Process.GetCurrentProcess().MainModule.ModuleName
                );
        }

        internal Boolean ParseCommandLineArguments(String[] args)
        {
            // NOTE: We cannot use -i or -u commandline arguments because the ServiceHelper library looks for those to install/uninstall service.

            for (Int32 index = 1; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "-e":
                        index++;
                        this.ElasticSearchServerUrl = args[index];
                        break;

                    case "-i":
                        index++;
                        this.IndexName = args[index];
                        break;

                    case "-od":
                        index++;
                        this.OutputDirectory = args[index];
                        break;

                    case "-pid":
                        index++;
                        this.PackageId = args[index];
                        break;

                    case "-pv":
                        index++;
                        this.PackageVersion = args[index];
                        break;

                    case "-xml":
                        index++;
                        string xmlConfigFile = args[index];
                        Config config = Config.Deserialize(xmlConfigFile);
                        if (!String.IsNullOrWhiteSpace(config.ElasticSearchServerUrl))
                        {
                            this.ElasticSearchServerUrl = config.ElasticSearchServerUrl;
                        }
                        if (!String.IsNullOrWhiteSpace(config.FailedDirectory))
                        {
                            this.FailedDirectory = config.FailedDirectory;
                        }
                            this.IndexAbsoluteLatestVersion = config.IndexAbsoluteLatestVersion;
                            this.IndexHistoricalVersions = config.IndexHistoricalVersions;
                            this.IndexLatestVersion = config.IndexLatestVersion;
                        if (!String.IsNullOrWhiteSpace(config.IndexName))
                        {
                            this.IndexName = config.IndexName;
                        }
                            this.MaxIndexerThreads = config.MaxIndexerThreads;
                        if (!String.IsNullOrWhiteSpace(config.OutputDirectory))
                        {
                            this.OutputDirectory = config.OutputDirectory;
                        }
                        if (!String.IsNullOrWhiteSpace(config.PackageId))
                        {
                            this.PackageId = config.PackageId;
                        }
                        if (!String.IsNullOrWhiteSpace(config.PackageSource))
                        {
                            this.PackageSource = config.PackageSource;
                        }
                        if (!String.IsNullOrWhiteSpace(config.PackageVersion))
                        {
                            this.PackageVersion = config.PackageVersion;
                        }
                        if (!String.IsNullOrWhiteSpace(config.QueueDirectory))
                        {
                            this.QueueDirectory = config.QueueDirectory;
                        }
                        break;

                    case "-?":
                    case "-h":
                    case "-H":
                    case "--help":
                    case "/?":
                    case "/h":
                    case "/H":
                        ShowUsage();
                        return false;

                    default:
                        if (!args[index].StartsWith("-"))
                        {
                            this.PackageSource = args[index];
                            break;
                        }
                        Trace.TraceError("Invalid argument: {0}\n", args[index]);
                        ShowUsage();
                        return false;
                }
            }

            if (args != null && args.Length != 0)
            {
                if (String.IsNullOrWhiteSpace(this.PackageSource))
                {
                    Trace.TraceError("An package source url (e.g. https://packages.nuget.org/api/v2) must be specified.");
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Looks up the specified key in the application's .config file.
        /// Throws KeyNotFoundException if the key is not present in the config file.
        /// </summary>
        internal String GetConfigValue(String keyName, Boolean throwIfMissing = true)
        {
            String value = System.Configuration.ConfigurationManager.AppSettings[keyName];
            if (value == null)
            {
                if (throwIfMissing) throw new System.Collections.Generic.KeyNotFoundException(String.Format("Config file does not contain a value for '{0}'.", keyName));
            }
            return value;
        }

        public void Serialize(string path)
        {
            XmlSerializer serializer = new XmlSerializer(this.GetType());

            using (FileStream fs = File.OpenWrite(path))
            {
                serializer.Serialize(fs, this);
            }
        }

        public static Config Deserialize(string path)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Config));

            using (FileStream fs = File.OpenRead(path))
            {
                return (Config)serializer.Deserialize(fs);
            }
        }
    }
}