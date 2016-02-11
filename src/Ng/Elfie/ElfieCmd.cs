// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ng.Elfie
{
    /// <summary>
    /// Provides access to the Elfie command line tools.
    /// </summary>
    class ElfieCmd
    {
        const string DEPENDENCYRELATIVEPATH = "Dependencies\\Elfie";
        const string INDEXEREXE = "Elfie.Indexer.exe";
        const string MERGEREXE = "Elfie.Merger.exe";

        /// <summary>
        /// Creates a new instance of ElfieCmd which will run the Elfie command line tools
        /// for the given toolset version.
        /// </summary>
        /// <param name="toolsetVersion">The toolset version of the Elfie command line tools</param>
        public ElfieCmd(Version toolsetVersion)
        {
            if (!DoesToolVersionExist(toolsetVersion))
            {
                throw new ArgumentOutOfRangeException($"The binaries for the version {toolsetVersion} do not exist.");
            }

            this.ToolsetVersion = toolsetVersion;
        }

        /// <summary>
        /// The toolset version of Elfie command line tools.
        /// </summary>
        public Version ToolsetVersion
        {
            get;
            private set;
        }

        /// <summary>
        /// Runs the Elfie.Indexer.exe command line tool to create an Idx file.
        /// </summary>
        /// <param name="targetDirectory"></param>
        /// <returns>If successful, returns the path to the idx file.
        /// If there were no files to index, returns null.
        /// If there was an error generating the idx file, an exception is thrown.
        /// </returns>
        public string RunIndexer(string targetDirectory, string packageId, string packageVersion, int downloadCount = 0)
        {
            // Elfie.Indexer.exe -p "C:\Temp\ElfiePaths.txt" -o ..\Index --dl 19000 --pn Arriba --rn 1.0.0.stable --url http://github.com/ElfieIndexer --full

            // Get the list of files to index.
            IEnumerable<string> assemblyFiles = this.GetFilesToIndex(targetDirectory);
            if (assemblyFiles.Count() == 0)
            {
                Trace.TraceInformation("The target directory didn't contain any files to index. Skipping.");
                return null;
            }

            // Create assembly list file
            string assemblyListFile = Path.Combine(targetDirectory, "assemblyList.txt");
            File.WriteAllLines(assemblyListFile, assemblyFiles);

            // Create output directory
            string idxDirectory = Path.Combine(targetDirectory, "Idx");
            Directory.CreateDirectory(idxDirectory);

            // Create log directory
            string logsDirectory = Path.Combine(targetDirectory, "Logs");
            Directory.CreateDirectory(logsDirectory);

            string arguments = String.Format("-p \"{0}\" -o \"{1}\" --dl \"{2}\" --pn \"{3}\" --rn \"{4}\" --ln \"{5}\" ", assemblyListFile, idxDirectory, downloadCount, packageId, packageVersion, logsDirectory);

            string indexerApplicationPath = GetElfieIndexerPath(this.ToolsetVersion);
            Trace.TraceInformation($"Running {indexerApplicationPath} {arguments}");

            // Run the indexer.
            Cmd cmd = Cmd.Echo(indexerApplicationPath, arguments, TimeSpan.FromMinutes(2));

            if (!cmd.HasExited)
            {
                cmd.Kill();
                throw new ElfieException("The indexer did not complete within the alloted time period.");
            }
            else if (cmd.ExitCode != 0)
            {
                throw new ElfieException($"The indexer exited with code {cmd.ExitCode}.");
            }

            string idxFile = Directory.GetFiles(idxDirectory, "*.idx").FirstOrDefault();

            if (String.IsNullOrWhiteSpace(idxFile))
            {
                throw new ElfieException("The indexer did not produce an idx file.");
            }

            return idxFile;
        }

        /// <summary>
        /// Runs the Elfie.Merger.exe command line tool to create an ardb/txt file.
        /// </summary>
        /// <param name="idxListFile"></param>
        /// <returns>If successful, returns the path to the ardb file.
        /// If there were no files to index, returns null.
        /// If there was an error generating the ardb file, an exception is thrown.
        /// </returns>
        public string RunMerger(string idxListFile, string outputDirectory)
        {
            // Elfie.Merger.exe -p "C:\Temp\Index.StablePackages.PublicApis" -o "C:\Temp\Index.StablePackages.PublicApis" --dl 0.95

            if (!File.Exists(idxListFile))
            {
                throw new FileNotFoundException("The idx list file does not exist.");
            }

            // Create output directory
            Directory.CreateDirectory(outputDirectory);

            // Create log directory
            string logsDirectory = Path.Combine(outputDirectory, "Logs");
            Directory.CreateDirectory(logsDirectory);

            string arguments = $"-p \"{idxListFile}\" -o \"{outputDirectory}\" --ln \"{logsDirectory}\" --dl 1.0";

            string mergerApplicationPath = GetElfieMergerPath(this.ToolsetVersion);
            Trace.TraceInformation($"Running {mergerApplicationPath} {arguments}");

            // Run the merger.
            Cmd cmd = Cmd.Echo(mergerApplicationPath, arguments, TimeSpan.FromMinutes(60));

            if (!cmd.HasExited)
            {
                cmd.Kill();
                throw new ElfieException("The merger did not complete within the alloted time period.");
            }
            else if (cmd.ExitCode != 0)
            {
                throw new ElfieException($"The merger exited with code {cmd.ExitCode}.");
            }

            string ardbFile = Directory.GetFiles(outputDirectory, "*.ardb.txt").FirstOrDefault();

            if (String.IsNullOrWhiteSpace(ardbFile))
            {
                throw new ElfieException("The merger did not produce an ardb.txt file.");
            }

            return ardbFile;
        }

        /// <summary>
        /// Collects the files to index. This include all of the assembly files
        /// in the lib subdirectory.
        /// </summary>
        /// <param name="targetDirectory">The directory which contains the files to index.</param>
        /// <returns>Returns a list of assembly files in the lib subdirectory.</returns>
        private IEnumerable<string> GetFilesToIndex(string targetDirectory)
        {
            string libDirectory = Path.Combine(targetDirectory, "lib");

            // If the lib directory doesn't exist, there's no files to process.
            if (!Directory.Exists(libDirectory))
            {
                Trace.TraceInformation("The target directory does not contain a lib subdirectory.");
                return new string[0];
            }

            // We need to process a few different assembly file types.
            string[] assemblyExtensions = new[] { ".exe", ".dll", ".winmd" };
            var allFiles = Directory.EnumerateFiles(libDirectory, "*.*", SearchOption.AllDirectories);
            var assemblyFiles = allFiles.Where(file => assemblyExtensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));

            return assemblyFiles;
        }

        /// <summary>
        /// Gets the path to the folder which contains the tool dependencies.
        /// </summary>
        /// <remarks>The dependencies are located in the Dependencies subdirectory of the 
        /// folder which contains NG.exe.</remarks>
        static string GetDependencyRootPath()
        {
            string appPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string dependencyPath = Path.Combine(appPath, DEPENDENCYRELATIVEPATH);
            dependencyPath = Path.GetFullPath(dependencyPath);

            return dependencyPath;
        }

        /// <summary>
        /// Gets the path to Elfie.Indexer.exe for the given toolset version.
        /// </summary>
        static string GetElfieIndexerPath(Version toolsetVersion)
        {
            if (toolsetVersion == null)
            {
                throw new ArgumentNullException("toolsetVersion");
            }

            string versionPath = Path.Combine(GetDependencyRootPath(), toolsetVersion.ToString(), INDEXEREXE);

            return versionPath;
        }

        /// <summary>
        /// Gets the path to Elfie.Merger.exe for the given toolset version.
        /// </summary>
        string GetElfieMergerPath(Version toolsetVersion)
        {
            if (toolsetVersion == null)
            {
                throw new ArgumentNullException("toolsetVersion");
            }

            string versionPath = Path.Combine(GetDependencyRootPath(), toolsetVersion.ToString(), MERGEREXE);

            return versionPath;
        }
        
        /// <summary>
                 /// Determines if the toolset with the given version number is available.
                 /// </summary>
        public static bool DoesToolVersionExist(Version toolsetVersion)
        {
            if (toolsetVersion == null)
            {
                throw new ArgumentNullException("version");
            }

            string versionPath = GetElfieIndexerPath(toolsetVersion);

            return File.Exists(versionPath);
        }

        /// <summary>
        /// Returns a string which is the major.minor value of the given version.
        /// </summary>
        public static string GetShortVersion(Version version)
        {
            if (version == null)
            {
                throw new ArgumentNullException("version");
            }

            return $"{version.Major}.{version.Minor}";
        }
    }
}
