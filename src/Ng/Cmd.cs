using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ng
{
    /// <summary>
    ///  Cmd is a small utility class which will run a requested command, wait for it to complete,
    ///  and capture output and the exit code from it. Cmd is used to incorporate batch script
    ///  segments easily into managed code (but with debuggability).
    /// </summary>
    public class Cmd : IDisposable
    {
        private object _locker;

        /// <summary>
        ///  Create a new command given an executable, arguments, and whether to echo output.
        /// </summary>
        /// <param name="executable">Full Path of executable to run</param>
        /// <param name="arguments">Arguments to pass to executable, or String.Empty</param>
        /// <param name="shouldEcho">True to write command output to Trace log, false not to</param>
        /// <param name="outputFilePath">File to redirect output to, or String.Empty</param>
        private Cmd(string executable, string arguments, bool shouldEcho, string outputFilePath)
        {
            this._locker = new object();

            this.ShouldEcho = shouldEcho;
            this.Command = executable + " " + (arguments ?? String.Empty);

            this.Process = new Process();
            this.Process.StartInfo.FileName = executable;
            this.Process.StartInfo.Arguments = arguments;
            this.Process.StartInfo.WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            this.Process.StartInfo.CreateNoWindow = true;
            this.Process.StartInfo.UseShellExecute = false;
            this.Process.StartInfo.RedirectStandardError = true;
            this.Process.StartInfo.RedirectStandardOutput = true;

            this.Process.OutputDataReceived += Process_OutputDataReceived;
            this.Process.ErrorDataReceived += Process_ErrorDataReceived;

            if (!String.IsNullOrEmpty(outputFilePath))
            {
                this.OutputWriter = new StreamWriter(outputFilePath, false);
            }
        }

        private Process Process
        {
            get; set;
        }

        private bool ShouldEcho
        {
            get; set;
        }

        private StreamWriter OutputWriter
        {
            get; set;
        }

        public string Command
        {
            get; private set;
        }

        public bool WasKilled
        {
            get;
            private set;
        }

        /// <summary>
        ///  Run a given executable with the given arguments, allowing up to the timeout for it to exit.
        ///  Will wait until timeout for command to complete, but returns afterward either way. You need
        ///  to check Cmd.HasExited to verify the command completed.
        /// </summary>        
        /// <param name="executable">Full Path of executable to run</param>
        /// <param name="arguments">Arguments to pass to executable</param>
        /// <param name="timeout">Timeout to wait for command to complete</param>
        /// <param name="memoryLimitBytes">Limit of memory use to allow, in bytes, -1 for no limit</param>
        /// <param name="outputFilePath">File to redirect output to, or String.Empty</param>
        /// <returns>Cmd instance to check final state of launched executable.</returns>
        public static Cmd Echo(string executable, string arguments, TimeSpan timeout, long memoryLimitBytes = -1, string outputFilePath = null)
        {
            Cmd cmd = new Cmd(executable, arguments, true, outputFilePath);
            cmd.Wait(timeout, memoryLimitBytes);
            return cmd;
        }

        /// <summary>
        ///  Run a given executable with the given arguments, allowing up to the timeout for it to exit.
        ///  Will wait until timeout for command to complete, but returns afterward either way. You need
        ///  to check Cmd.HasExited to verify the command completed.
        /// </summary>        
        /// <param name="executable">Full Path of executable to run</param>
        /// <param name="arguments">Arguments to pass to executable</param>
        /// <param name="timeout">Timeout to wait for command to complete</param>
        /// <param name="memoryLimitBytes">Limit of memory use to allow, in bytes, -1 for no limit</param>
        /// <param name="outputFilePath">File to redirect output to, or String.Empty</param>
        /// <returns>Cmd instance to check final state of launched executable.</returns>
        public static Cmd Quiet(string executable, string arguments, TimeSpan timeout, long memoryLimitBytes = -1, string outputFilePath = null)
        {
            Cmd cmd = new Cmd(executable, arguments, false, outputFilePath);
            cmd.Wait(timeout, memoryLimitBytes);
            return cmd;
        }

        /// <summary>
        ///  Wait up to timeout for this command instance to exit. Check return value
        ///  to determine if command HasExited. If the memory use exceeds the limit,
        ///  the process will be killed.
        /// </summary>
        /// <param name="timeout">Timeout to wait for command to exit.</param>
        /// <param name="memoryLimitBytes">Memory use limit for process, -1 for no limit.</param>
        /// <returns>True if it exited, False otherwise</returns>
        public bool Wait(TimeSpan timeout, long memoryLimitBytes = -1)
        {
            if (this.ShouldEcho) Trace.WriteLine(this.Command);

            this.Process.Start();
            this.Process.BeginOutputReadLine();
            this.Process.BeginErrorReadLine();

            bool exited = false;

            long totalBytes = 0;
            Stopwatch runtime = Stopwatch.StartNew();

            // Wait for the process to exit or exceed timeout or memory limit
            while (runtime.Elapsed < timeout)
            {
                exited = this.Process.WaitForExit(1000);
                if (exited)
                {
                    break;
                }

                if (memoryLimitBytes != -1)
                {
                    try
                    {
                        totalBytes = this.Process.WorkingSet64;
                        if (totalBytes > memoryLimitBytes)
                        {
                            break;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // [happens when the process exited between the WaitForExit and the WorkingSet64 get]
                    }
                }
            }

            // If it didn't exit, kill it
            if (!exited)
            {
                Trace.TraceError("Cmd: Killing command {0}, running {2} seconds, using {3} bytes. (limit {4} seconds, {5} bytes)\r\n{1}", this.Process.Id, this.Command, runtime.Elapsed.TotalSeconds, totalBytes, timeout.TotalSeconds, memoryLimitBytes);
                this.WasKilled = true;
                this.Kill();
            }

            // Warn if exit code was bad
            if (exited && this.ExitCode != 0)
            {
                Trace.TraceWarning("Cmd: Exit Code {0} returned by: {1}", this.ExitCode, this.Command);
            }

            // Close output file, if there was one
            if (this.OutputWriter != null)
            {
                lock (_locker)
                {
                    if (this.OutputWriter != null)
                    {
                        this.OutputWriter.Dispose();
                        this.OutputWriter = null;
                    }
                }
            }

            return exited;
        }

        /// <summary>
        ///  Kill the command process.
        /// </summary>
        public void Kill()
        {
            this.Process.Kill();

            // wait up to 2 minutes for the process to exit.
            for (int i = 0; i < 120; i++)
            {
                if (this.Process.HasExited)
                {
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            if (this.OutputWriter != null)
            {
                lock (_locker)
                {
                    if (this.OutputWriter != null)
                    {
                        this.OutputWriter.Dispose();
                        this.OutputWriter = null;
                    }
                }
            }
        }

        /// <summary>
        ///  Returns whether the command exited.
        /// </summary>
        public bool HasExited
        {
            get
            {
                return this.WasKilled || this.Process.HasExited;
            }
        }

        /// <summary>
        ///  Returns the exit code from the command. You must check HasExited before attempting to read this.
        /// </summary>
        public int ExitCode
        {
            get
            {
                return this.Process.ExitCode;
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }

            if (this.ShouldEcho)
            {
                Trace.WriteLine(e.Data);
            }

            lock (this._locker)
            {
                if (this.OutputWriter != null)
                {
                    this.OutputWriter.WriteLine(e.Data);
                }
            }
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }

            if (this.ShouldEcho)
            {
                Trace.WriteLine(e.Data);
            }

            lock (this._locker)
            {
                if (this.OutputWriter != null)
                {
                    this.OutputWriter.WriteLine(e.Data);
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (this.OutputWriter != null)
                    {
                        lock (_locker)
                        {
                            if (this.OutputWriter != null)
                            {
                                this.OutputWriter.Dispose();
                                this.OutputWriter = null;
                            }
                        }
                    }

                    if (this.Process != null)
                    {
                        if (!this.Process.HasExited)
                        {
                            this.Process.Kill();
                        }

                        this.Process.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
