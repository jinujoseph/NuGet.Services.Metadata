using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ng.TraceListeners
{
    public class TextFileTraceListener : TraceListenerBase
    {
        private String m_logDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        private String m_logFilePrefix = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.ModuleName);

        private Int64 m_maxFileSize = (Int64)64 * 1024 * 1024;
        private Int64 m_maxSize = (Int64)4 * 1024 * 1024 * 1024;
        private Int32 m_maxAge = 14;

        private ConcurrentQueue<LogEntry> m_logEntries;
        private Int64 m_logEntriesBacklog;
        private Int64 m_maxBacklog = (Int64)64 * 1024;
        private Int64 m_flushInterval = 1;

        private Format m_format = Format.Normal;
        private String m_computerName = Environment.MachineName;
        private String m_serviceName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.ModuleName);
        private String m_traceSessionId = Guid.NewGuid().ToString().ToUpper();
        private Int64 m_traceSessionLineCount = 0;

        // Constructor used when initializeData is specified.
        // Note: The constructor only gets called when trace source is first used or someone accesses Trace.Listeners (not on application startup).
        public TextFileTraceListener(String initializeData) : base(initializeData)
        {
            Func<String, String> TranslateSpecialVariables = (s) =>
            {
                s = s.Replace("#COMPUTERNAME#", Environment.MachineName);
                s = s.Replace("#PROGRAMNAME#", Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.ModuleName));
                s = s.Replace("#SERVICENAME#", ConfigurationManager.AppSettings["ServiceName"] ?? Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.ModuleName));
                s = s.Replace("#USERNAME#", Environment.UserName.ToUpper());
                s = s.Replace("#PID#", Process.GetCurrentProcess().Id.ToString());

                if (s.StartsWith("#VOLUME:"))
                {
                    String volumeLabel = new String(s.Substring(8).TakeWhile((c) => c != '#').ToArray());
                    foreach (DriveInfo driveInfo in DriveInfo.GetDrives())
                    {
                        try
                        {
                            if (driveInfo.VolumeLabel.Equals(volumeLabel, StringComparison.InvariantCultureIgnoreCase))
                            {
                                String driveLetter = driveInfo.RootDirectory.FullName.TrimEnd('\\');
                                s = driveLetter + s.Substring(s.IndexOf('#', 1) + 1);

                                break;
                            }
                        }
                        catch
                        {
                            // driveInfo.VolumeLabel can throw an exception if the drive is bitlockered. Just ignore and try the next drive.
                        }
                    }
                }

                return s;
            };

            try
            {
                // Parse initializeData as "key1=value1,key2=value2,..."
                if (initializeData != null)
                {
                    foreach (String keyValuePair in initializeData.Split(','))
                    {
                        String[] parts = keyValuePair.Split('=');
                        if (parts.Length == 2)
                        {
                            if (parts[0].Equals("logdir", StringComparison.InvariantCultureIgnoreCase))
                            {
                                String logDir = TranslateSpecialVariables(parts[1]);
                                if (Path.IsPathRooted(logDir)) this.m_logDir = logDir;
                                else this.m_logDir = Path.Combine(this.m_logDir, logDir);
                            }
                            else if (parts[0].Equals("logfileprefix", StringComparison.InvariantCultureIgnoreCase))
                            {
                                this.m_logFilePrefix = TranslateSpecialVariables(parts[1]);
                            }
                            else if (parts[0].Equals("maxage", StringComparison.InvariantCultureIgnoreCase)) this.m_maxAge = Int32.Parse(parts[1]);
                            else if (parts[0].Equals("maxsize", StringComparison.InvariantCultureIgnoreCase)) this.m_maxSize = Int64.Parse(parts[1]) * 1024 * 1024;
                            else if (parts[0].Equals("maxfilesize", StringComparison.InvariantCultureIgnoreCase)) this.m_maxFileSize = Int64.Parse(parts[1]) * 1024 * 1024;
                            else if (parts[0].Equals("maxbacklog", StringComparison.InvariantCultureIgnoreCase)) this.m_maxBacklog = Int64.Parse(parts[1]);
                            else if (parts[0].Equals("flushinterval", StringComparison.InvariantCultureIgnoreCase)) this.m_flushInterval = Int64.Parse(parts[1]);
                            else if (parts[0].Equals("format", StringComparison.InvariantCultureIgnoreCase)) this.m_format = (Format)Enum.Parse(typeof(Format), parts[1], true);
                        }
                    }
                }

                if (!Directory.Exists(m_logDir))
                {
                    Directory.CreateDirectory(m_logDir);
                }

                // Max file sizes can't be more than max available free space on log drive (minus 1mb margin).
                // Note: GetDiskFreeSpace works even if the logdir is a network share.
                Int64 freespace = (Int64)GetDiskFreeSpace(m_logDir) - (1024 * 1024);
                if (m_maxFileSize > freespace) m_maxFileSize = freespace;
                if (m_maxSize > freespace) m_maxSize = freespace;

                // Sanity check settings.
                if (m_maxFileSize < (1024 * 1024)) throw new Exception("MaxFileSize cannot be less than 1MB.");
                if (m_maxSize < (1024 * 1024)) throw new Exception("MaxSize cannot be less than 1MB.");
                if (m_maxFileSize > m_maxSize) throw new Exception("MaxSize cannot be less than MaxFileSize.");

                m_logEntries = new ConcurrentQueue<LogEntry>();
                m_logEntriesBacklog = 0;

                // Spawn background writer thread.
                Thread writerThread = new Thread(new ThreadStart(BackgroundWriterThread));
                writerThread.IsBackground = true;
                writerThread.Priority = ThreadPriority.AboveNormal;
                writerThread.Start();
            }
            catch (Exception ex)
            {
                if (ex is ThreadAbortException)
                {
                    // Thread is being externally aborted (eg. because appdomain is being unloaded). Don't fastfail - as that will kill the entire process.
                    throw;
                }
                else
                {
                    Environment.FailFast("Exception in TextFileTraceListener.TextFileTraceListener().", ex);
                }
            }
        }

        public TextFileTraceListener()
            : this(null)
        {
        }

        private void BackgroundWriterThread()
        {
            try
            {
                // Open new log file.
                DateTime logDay = DateTime.Now.ToLocalTime().Date;
                FileStream fileStream = CreateNewLogFile();
                Int64 fileSize = 0;
                DateTime lastFlushTime = DateTime.Now.ToUniversalTime();
                Int64 unflushedEntryCount = 0;

                while (true)
                {
                    LogEntry logEntry;

                    if (m_logEntries.TryDequeue(out logEntry))
                    {
                        Interlocked.Decrement(ref m_logEntriesBacklog);

                        if (((fileSize + logEntry.Content.Length) > m_maxFileSize) ||
                            (logEntry.EventTime.Date > logDay))
                        {
                            // Writing this entry would take us past the max file size or this entry is past the date of the current log file
                            // so close log file and start a new one.
                            // Note: It is possible that an entry generated at 00:00:01 today was inserted into the logging queue before another entry
                            // that was generated on 11:59:59 yesterday on another thread and so the 11:59:59 entry from yesterday will end up in today's log instead.
                            // If someone is investigating events that happened so close to the turn of the day then it is reasonable that they might have to look at the
                            // next day's logs too so that's an acceptable edge-case to ignore for the sake of much simpler log file management logic.
                            fileStream.Flush(true);
                            fileStream.Close();
                            fileStream.Dispose();
                            logDay = DateTime.Now.ToLocalTime().Date;
                            fileStream = CreateNewLogFile();
                            fileSize = 0;
                            lastFlushTime = DateTime.Now.ToUniversalTime();
                            unflushedEntryCount = 0;
                        }

                        fileStream.Write(logEntry.Content, 0, logEntry.Content.Length);
                        fileSize += logEntry.Content.Length;
                        unflushedEntryCount++;

                        // Flush log if required for this event.
                        if (logEntry.FlushContentToDisk)
                        {
                            // This event needs to be flushed to disk before the tracing thread is notified.
                            fileStream.Flush(true);
                            lastFlushTime = DateTime.Now.ToUniversalTime();
                            unflushedEntryCount = 0;
                        }

                        // If log entry has an associated completion event then signal it.
                        if (logEntry.WaitEvent != null)
                        {
                            logEntry.WaitEvent.Set();
                        }

                        // Allow log entry to get garbage collected.
                        logEntry = null;
                    }
                    else
                    {
                        // No log entries queued up to be written.
                        // Yield to avoid background writer thread continuously spinning.
                        Thread.Sleep(10);
                    }

                    // Flush log if there are any unflushed entries and its been longer than the max flush interval since last flush.
                    if ((unflushedEntryCount > 0) && (DateTime.Now.ToUniversalTime().Subtract(lastFlushTime).TotalSeconds >= (Double)this.m_flushInterval))
                    {
                        fileStream.Flush(true);
                        lastFlushTime = DateTime.Now.ToUniversalTime();
                        unflushedEntryCount = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is ThreadAbortException)
                {
                    // Thread is being externally aborted (eg. because appdomain is being unloaded). Don't fastfail - as that will kill the entire process.
                    throw;
                }
                else
                {
                    // Exceptions here will lead to incorrect tracing, making the application hard to debug, and so are treated as fatal.
                    Environment.FailFast("Exception in TextFiletraceListener.BackgroundWriterThread().", ex);
                }
            }
        }

        /// <summary>
        /// Cleans up any old log files (according to maxage, maxsize settigns) and creates new log file [logdir]\[app]_[yyyymmdd]_[n].log.
        /// </summary>
        private FileStream CreateNewLogFile()
        {
            // Note: Log files are named [logdir]\[logfileprefix]_[yyyymmdd]_[nnnn].log
            // This makes it easy to find and sort log files.

            // Use a named mutex to ensure that when multiple instances of the same app are run at the same time they don't both try to cleanup logfiles at the same time or create a new logfile with the same name.
            Mutex logFileManipulationMutex = new Mutex(false, String.Format("Microsoft.Sonar.TextFileTraceListener.LogFileManipulationMutex.{0}", m_logFilePrefix));
            try
            {
                logFileManipulationMutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                // This exception indicates that we successfully acquired the mutex, but it was not released normally - the previous owner died holding the mutex - and so anything
                // protected by the mutex could be in an inconsistent state. We re-enumerate the state of logdir after acquiring the mutex so that should not cause any problems.
            }

            try
            {
                // First cleanup any old logs according to maxage, maxsize, etc policies.
                // Use LastWriteTime instead of CreationTime because a log file could get created and then used for several days if the application doesn't restart and it doesn't get filled up.
                SortedSet<String> logFiles = new SortedSet<String>();
                Int64 logFilesTotalSize = 0;
                foreach (String logFile in Directory.EnumerateFiles(m_logDir, String.Format("{0}_*.log", m_logFilePrefix), SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        FileInfo logFileInfo = new FileInfo(logFile);
                        Int64 logFileSize = logFileInfo.Length;
                        if ((logFileInfo.LastWriteTime.ToUniversalTime() > DateTime.Now.ToUniversalTime()) || (DateTime.Now.ToUniversalTime().Subtract(logFileInfo.LastWriteTime.ToUniversalTime()).TotalDays > (Double)m_maxAge))
                        {
                            try
                            {
                                File.Delete(logFile);
                                continue;
                            }
                            catch
                            {
                                // Unable to delete old log file so account for its size.
                                // Maybe there is enough space anyway or other log files can be deleted to make space.
                            }
                        }

                        // Log file names are already in chronologically sorted order so no need for separate sort key.
                        logFiles.Add(logFile);
                        logFilesTotalSize += logFileSize;
                    }
                    catch (FileNotFoundException)
                    {
                        // If there are multiple app domains then another instance of TextFileTraceListener may have deleted the file between when it was enumerated and now.
                        continue;
                    }
                }

                // If the total log files size is greater than the max allowed then delete old log files until there is enough room.
                Int64 logFilesMaxTotalSize = m_maxSize - m_maxFileSize;
                if (logFilesTotalSize > logFilesMaxTotalSize)
                {
                    foreach (String logFile in logFiles)
                    {
                        try
                        {
                            FileInfo logFileInfo = new FileInfo(logFile);
                            Int64 logFileSize = logFileInfo.Length;
                            File.Delete(logFile);
                            logFilesTotalSize -= logFileSize;
                            if (logFilesTotalSize < logFilesMaxTotalSize)
                            {
                                // There is enough room to create a new log file now so we're done.
                                break;
                            }
                        }
                        catch
                        {
                            // Unable to delete old log file so account for its size.
                            // Maybe there is enough space anyway or other log files can be deleted to make space.
                        }
                    }
                }
                if (logFilesTotalSize > logFilesMaxTotalSize)
                {
                    // After deleting all the old log files we could there still isn't enough room for a new log file.
                    // This means we can't do tracing and will make it hard to debug application failures so making this fatal.
                    Environment.FailFast(String.Format("Unable to free up enough space to create a new log file. Space required = {0}MB, Max space allowed = {1}MB, Space taken up by existing un-deleteable log files = {1}MB.", m_maxFileSize / (1024 * 1024), m_maxSize / (1024 * 1024), logFilesTotalSize / (1024 * 1024)));
                    throw new Exception();
                }

                // Generate new log file name.
                // If there are already log files with today's date then take the last one and increment n.
                String logFilePrefixAndDate = String.Format("{0}_{1:yyyyMMdd}_", m_logFilePrefix, DateTime.Now.ToLocalTime());
                Int64 logFileIndex = 1;
                IEnumerable<string> totalLogFilesToday = logFiles.Where(logFile => Path.GetFileNameWithoutExtension(logFile).StartsWith(logFilePrefixAndDate, StringComparison.InvariantCultureIgnoreCase));

                if (totalLogFilesToday.Count() >= 9999)
                {
                    // There are more than 9999 log files in a single day, generated by a service. 
                    // It is an indicator that recurring restarts did not solve the issue thus we terminate the service immediately
                    Environment.FailFast(String.Format("Too many log files generated in a single day. Count: {0}.", totalLogFilesToday.Count()));
                }

                String lastLogFile = totalLogFilesToday.LastOrDefault();
                if (lastLogFile != null)
                {
                    // The logfileprefix may contain underscores so skip past the prefix before splitting on '_' to find the index part.
                    String lastLogFileName = Path.GetFileNameWithoutExtension(lastLogFile);
                    String[] lastLogFileParts = lastLogFileName.Substring(logFilePrefixAndDate.Length).Split('_');
                    String lastLogFileIndexPart = lastLogFileParts[0];
                    Int64 lastLogFileIndex = Int64.Parse(lastLogFileIndexPart);
                    logFileIndex = lastLogFileIndex + 1;
                }
                String appDomainSuffix = "";
                if (AppDomain.CurrentDomain.Id > 1) appDomainSuffix = String.Format("_{0}", AppDomain.CurrentDomain.Id - 1);
                String newLogFile = Path.Combine(m_logDir, String.Format("{0}{1:0000}{2}.log", logFilePrefixAndDate, logFileIndex, appDomainSuffix));
                try
                {
                    return new FileStream(newLogFile, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                }
                catch (Exception ex)
                {
                    if (ex is ThreadAbortException)
                    {
                        // Thread is being externally aborted (eg. because appdomain is being unloaded). Don't fastfail - as that will kill the entire process.
                        throw;
                    }
                    else
                    {
                        // Failed to create file.
                        // Could be because of access denied or because someone else created a log file with this name in the meanwhile.
                        // We could try incrementing index until we get a free name but this could cause other nasty race-conditions later
                        // because it invalidates our assumption that there is only one instance of an application with the same (logdir, logfileprefix)
                        // running at a time.
                        // So safest to bail and let user handle deconfliction.
                        Environment.FailFast(String.Format("Failed to create new log file {0}.", newLogFile), ex);
                        throw new Exception();
                    }
                }
            }
            finally
            {
                logFileManipulationMutex.ReleaseMutex();
                logFileManipulationMutex.Dispose();
            }
        }

        protected override void TraceSimpleEvent(DateTime eventTime, Int32 threadId, TraceEventType eventType, String message)
        {
            // Create LogEntry, enqueue it to be written to disk by the writer thread and, if this is an error/warning/notification trace, wait for it to get written to disk.

            LogEntry logEntry = new LogEntry();
            logEntry.EventTime = eventTime.ToLocalTime();

            // PERF TODO: This String.Format() operation is the major bottleneck in the TextFileTraceListener().
            // Half the time is evaluating eventType.ToString() and the rest is the time it takes to
            // convert the other parameters to strings.
            String text = String.Empty;
            String[] lines = String.IsNullOrEmpty(message) ? new String[] { String.Empty } : message.Split(new Char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (String line in lines)
            {
                switch (m_format)
                {
                    case Format.Normal:
                        text += String.Format("##\t{0}\t{1}\t#{2} {3}\r\n", logEntry.EventTime.ToString("yyyy-MM-dd HH:mm:ss"), threadId, eventType, line);
                        break;

                    case Format.Cosmos:
                        // CosmosDataLoader can sometimes upload the same log line multiple times. It could theoretically also upload 
                        // rows of a file in a different order than they appear on disk.
                        // So we add a line number to each row to (a) dedupe rows, and (b) allow rows to be reassembled in chronological order if they get out of sync.
                        // CosmosDataLoader can also be configured to prepend a line number automatically to each log line but it prepends it in CSV format, even if the log is in TSV format.
                        // The line number might not match the line number of the file on disk if multiple threads are tracing at the same time (because the line number generation and log entry enqueing are not atomic),
                        // but within each thread the line numbers will be in chronological order so that's ok.
                        Int64 traceSessionLineNumber = Interlocked.Increment(ref m_traceSessionLineCount);
                        text += String.Format("{0}\r\n", String.Join("\t", m_traceSessionId, traceSessionLineNumber.ToString(), m_computerName, m_serviceName, logEntry.EventTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"), threadId.ToString(), Trace.CorrelationManager.ActivityId.ToString().ToUpper(), eventType.ToString(), line));
                        break;
                }
            }
            logEntry.Content = Encoding.UTF8.GetBytes(text);

            if ((eventType & (TraceEventType.Critical | TraceEventType.Error | TraceEventType.Warning)) == 0)
            {
                logEntry.FlushContentToDisk = false;
                logEntry.WaitEvent = null;
            }
            else
            {
                logEntry.FlushContentToDisk = true;
                logEntry.WaitEvent = new ManualResetEventSlim();
            }

            // Interlocked.Increment an outstanding log entry count before enqueuing and have writer thread Interlocked.Decrement so can quickly
            // tell if backlog is very large (eg. > 100,000 entries outstanding). If so then add wait event and block till trace has been processed
            // byte background writer thread. 
            // This prevents backlog continually growing faster than writer thread can write, leading to unbounded memory usage.
            // It also helps prevents the backgroundwriterthread from getting starved if cpu is at 100%.
            // Note: We only need to wait for the event to be processed, to avoid backlog buildup, not necessarily flushed to disk.
            Int64 logEntriesBacklog = Interlocked.Increment(ref m_logEntriesBacklog);
            if (logEntriesBacklog > m_maxBacklog)
            {
                logEntry.WaitEvent = logEntry.WaitEvent ?? new ManualResetEventSlim();
            }

            m_logEntries.Enqueue(logEntry);

            if (logEntry.WaitEvent != null)
            {
                logEntry.WaitEvent.Wait();
            }
        }

        /// <summary>
        /// Returns the amount of free disk space (in bytes) on the volume containing the specified directory.
        /// Note: GetDiskFreeSpace works even if the logdir is a network share (eg. \\server\foo\bar).
        /// </summary>
        private static UInt64 GetDiskFreeSpace(String directoryPath)
        {
            UInt64 freeBytesAvailable;
            UInt64 totalNumberOfBytes;
            UInt64 totalNumberOfFreeBytes;

            Boolean success = GetDiskFreeSpaceEx(Path.GetFullPath(directoryPath), out freeBytesAvailable, out totalNumberOfBytes, out totalNumberOfFreeBytes);
            if (!success) throw new System.ComponentModel.Win32Exception();

            return freeBytesAvailable;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern Boolean GetDiskFreeSpaceEx(String lpDirectoryName, out UInt64 lpFreeBytesAvailable, out UInt64 lpTotalNumberOfBytes, out UInt64 lpTotalNumberOfFreeBytes);

        enum Format
        {
            Normal,
            Cosmos
        }

        class LogEntry
        {
            // The date & time of the log entry.
            public DateTime EventTime { get; set; }

            // Pre-formatted text (including eventtime, threadid, eventtype, message) encoded in UTF8, ready for writer thread to write directly to disk.
            public Byte[] Content { get; set; }

            public Boolean FlushContentToDisk { get; set; }

            // Wait event to notify if this is an error/warning/notification event that needs to be flushed to disk and the tracing thread notified.
            public ManualResetEventSlim WaitEvent { get; set; }
        }
    }
}
