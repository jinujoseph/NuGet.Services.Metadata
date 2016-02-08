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
    class ElfieCmd
    {
        const string DEPENDENCYRELATIVEPATH = "Dependencies\\Elfie";
        const string INDEXEREXE = "Elfie.Indexer.exe";

        public ElfieCmd(Version version)
        {
            if (!DoesToolVersionExist(version))
            {
                throw new ArgumentOutOfRangeException($"The binaries for the version {version} do not exist.");
            }

            this.Version = version;
        }

        public Version Version
        {
            get;
            private set;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetDirectory"></param>
        /// <returns>If successful, returns the path to the idx file.
        /// If there were no files to index, returns null.
        /// If there was an error generating the idx file, an exception is thrown.
        /// </returns>
        public string RunIndexer(string targetDirectory, string packageId, string packageVersion, int downloadCount = 0)
        {
            // Loby.Indexer.exe -p "C:\Code\OSSDep\ossdep-playground\src\Loby\ArribaPaths.txt" -o ..\Index --dl 19000 --pn Arriba --rn 1.0.0.stable --url http://github.com/Arriba --full

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

            string indexerApplicationPath = GetElfieIndexerPath(this.Version);
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

        static string GetDependencyRootPath()
        {
            string appPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string dependencyPath = Path.Combine(appPath, DEPENDENCYRELATIVEPATH);
            dependencyPath = Path.GetFullPath(dependencyPath);

            return dependencyPath;
        }

        static string GetElfieIndexerPath(Version version)
        {
            if (version == null)
            {
                throw new ArgumentNullException("version");
            }

            string versionPath = Path.Combine(GetDependencyRootPath(), version.ToString(), INDEXEREXE);

            return versionPath;
        }

        public static bool DoesToolVersionExist(Version version)
        {
            if (version == null)
            {
                throw new ArgumentNullException("version");
            }

            string versionPath = GetElfieIndexerPath(version);

            return File.Exists(versionPath);
        }

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
