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

        public ElfieCmd(string version)
        {
            if (!DoesVersionExist(version))
            {
                throw new ArgumentOutOfRangeException($"The binaries for the version {version} do not exist.");
            }

            this.Version = version;
        }

        public string Version
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
            // The resource URI for the idx file we're about to create.
            Uri idxResourceUri = null;

            // Loby.Indexer.exe -p "C:\Code\OSSDep\ossdep-playground\src\Loby\ArribaPaths.txt" -o ..\Index --dl 19000 --pn Arriba --rn 1.0.0.stable --url http://github.com/Arriba --full

            // Get the list of files to index.
            IEnumerable<string> assemblyFiles = this.GetFilesToIndex(targetDirectory);
            if (assemblyFiles.Count() == 0)
            {
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

            string applicationDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string indexerApplicationPath = Path.Combine(applicationDirectory, "Dependencies", "Elfie", Version, "Elfie.Indexer.exe");
            Trace.TraceInformation($"Running {indexerApplicationPath} {arguments}");

            Cmd cmd = Cmd.Echo(indexerApplicationPath, arguments, TimeSpan.FromMinutes(2));

            if (!cmd.HasExited)
            {
                cmd.Kill();
                throw new Exception("The indexer did not complete within the alloted time period.");
            }
            else if (cmd.ExitCode != 0)
            {
                throw new Exception($"The indexer exited with code {cmd.ExitCode}.");
            }

            string idxFile = Directory.GetFiles(idxDirectory, "*.idx").FirstOrDefault();

            if (String.IsNullOrWhiteSpace(idxFile))
            {
                throw new Exception("The indexer did not produce an idx file.");
            }

            return idxFile;
        }

        private IEnumerable<string> GetFilesToIndex(string targetDirectory)
        {
            string libDirectory = Path.Combine(targetDirectory, "lib");

            // If the lib directory doesn't exist, there's no files to process.
            if (!Directory.Exists(libDirectory))
            {
                return new string[0];
            }

            // We need to process a few different assembly file types.
            string[] assemblyExtensions = new[] { ".exe", ".dll", ".winmd" };
            var assemblyFiles = Directory.GetFiles(libDirectory).Where(file => assemblyExtensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));

            return assemblyFiles;
        }

        static string GetDependencyRootPath()
        {
            string appPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string dependencyPath = Path.Combine(appPath, DEPENDENCYRELATIVEPATH);
            dependencyPath = Path.GetFullPath(dependencyPath);

            return dependencyPath;
        }

        public static bool DoesVersionExist(string version)
        {
            if (String.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            string versionPath = Path.Combine(GetDependencyRootPath(), version, INDEXEREXE);

            return File.Exists(versionPath);
        }
    }
}
