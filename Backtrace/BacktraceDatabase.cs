﻿using Backtrace.Base;
using Backtrace.Common;
using Backtrace.Interfaces;
using Backtrace.Model;
using Backtrace.Model.Database;
using Backtrace.Services;
using Backtrace.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using System.Diagnostics;
#if !NET35
using System.Threading.Tasks;
#endif

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Backtrace.Tests")]
namespace Backtrace
{
    /// <summary>
    /// Backtrace Database 
    /// </summary>
    public class BacktraceDatabase : IBacktraceDatabase
    {
        /// <summary>
        /// Backtrace Api instance. Use BacktraceApi to send data to Backtrace server
        /// </summary>
        public IBacktraceApi BacktraceApi { get; set; }

        /// <summary>
        /// Database context - in memory cache and entry operations
        /// </summary>
        internal IBacktraceDatabaseContext BacktraceDatabaseContext { get; set; }

        /// <summary>
        /// File context - file collection operations
        /// </summary>
        internal IBacktraceDatabaseFileContext BacktraceDatabaseFileContext { get; set; }

        /// <summary>
        /// Database settings
        /// </summary>
        private BacktraceDatabaseSettings DatabaseSettings { get; set; }

        /// <summary>
        /// Database path
        /// </summary>
        private string DatabasePath => DatabaseSettings.DatabasePath;

        private bool _timerBackgroundWork = false;
        /// <summary>
        /// Determine if BacktraceDatabase is enable and library can store reports
        /// </summary>
        private bool _enable = false;

        private readonly Timer _timer = new Timer();

        /// <summary>
        /// Create disabled instance of BacktraceDatabase
        /// </summary>
        /// <param name="databaseSettings"></param>
        public BacktraceDatabase()
        { }

        /// <summary>
        /// Create new Backtrace database instance
        /// </summary>
        /// <param name="path">Path to database directory</param>
        public BacktraceDatabase(string path)
            : this(new BacktraceDatabaseSettings(path))
        { }

        /// <summary>
        /// Create Backtrace database instance
        /// </summary>
        /// <param name="databaseSettings">Backtrace database settings</param>
        public BacktraceDatabase(BacktraceDatabaseSettings databaseSettings)
        {
            if (databaseSettings == null || string.IsNullOrEmpty(databaseSettings.DatabasePath))
            {
                return;
            }
            if (!Directory.Exists(databaseSettings.DatabasePath))
            {
                throw new ArgumentException("Databse path does not exists");
            }
            DatabaseSettings = databaseSettings;
            BacktraceDatabaseContext = new BacktraceDatabaseContext(DatabasePath, DatabaseSettings.RetryLimit, DatabaseSettings.RetryOrder);
            BacktraceDatabaseFileContext = new BacktraceDatabaseFileContext(DatabasePath);
        }

        /// <summary>
        /// Start database tasks
        /// </summary>
        public void Start()
        {
            //database not exists
            if (DatabaseSettings == null)
            {
                return;
            }
            if (BacktraceDatabaseContext?.Any() == true)
            {
                _enable = true;
                return;
            }
            // load reports from hard drive
            LoadReports();
            // remove orphaned files
            RemoveOrphaned();
            // setup database timer events
            if (DatabaseSettings.RetryBehavior == RetryBehavior.ByInterval
                || !DatabaseSettings.AutoSendMode)
            {
                SetupTimer();
            }
            //Enable database
            _enable = true;
        }

        private void SetupTimer()
        {
            // timer require time in ms
            _timer.Interval = DatabaseSettings.RetryInterval * 1000;
            // don't stop timer work
            _timer.AutoReset = true;
#if NET35
            _timer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
#else
            _timer.Elapsed += new ElapsedEventHandler(OnTimedEventAsync);
#endif
            _timer.Enabled = true;

        }

        /// <summary>
        /// Set BacktraceApi instance
        /// </summary>
        /// <param name="backtraceApi">BacktraceApi instance</param>
        public void SetApi(IBacktraceApi backtraceApi)
        {
            BacktraceApi = backtraceApi;
        }

        /// <summary>
        /// Get settings 
        /// </summary>
        /// <returns>Current database settings</returns>
        public BacktraceDatabaseSettings GetSettings()
        {
            return DatabaseSettings;
        }


        /// <summary>
        /// Delete all existing files and directories in current database directory
        /// </summary>
        public void Clear()
        {
            BacktraceDatabaseContext?.Clear();
        }

        /// <summary>
        /// Add new report to BacktraceDatabase
        /// </summary>
        public BacktraceDatabaseEntry Add(BacktraceReportBase backtraceReport, Dictionary<string, object> attributes, MiniDumpType miniDumpType = MiniDumpType.Normal)
        {
            if (!_enable)
            {
                return null;
            }
            if (BacktraceDatabaseContext.Count() + 1 > DatabaseSettings.MaxRecordCount && DatabaseSettings.MaxRecordCount != 0)
            {
                throw new ArgumentException("Maximum number of entries available in BacktraceDatabase");
            }
            if (miniDumpType != MiniDumpType.None)
            {
                string minidumpPath = GenerateMiniDump(backtraceReport, miniDumpType);
                if (!string.IsNullOrEmpty(minidumpPath))
                {
                    backtraceReport.SetMinidumpPath(minidumpPath);
                }
            }

            var data = backtraceReport.ToBacktraceData(attributes);
            return BacktraceDatabaseContext.Add(data);
        }


        /// <summary>
        /// Get all stored reports in BacktraceDatabase
        /// </summary>
        /// <returns>All stored reports in BacktraceDatabase</returns>
        public IEnumerable<BacktraceDatabaseEntry> Get() => BacktraceDatabaseContext?.Get() ?? new List<BacktraceDatabaseEntry>();

        public void Delete(BacktraceDatabaseEntry entry) => BacktraceDatabaseContext?.Delete(entry);

        /// <summary>
        /// Send and delete all entries from database
        /// </summary>
        public void Flush()
        {
            if (BacktraceApi == null)
            {
                throw new ArgumentException("BacktraceApi is required if you want to use Flush method");
            }
            var entry = BacktraceDatabaseContext?.FirstOrDefault();
            while (entry != null)
            {
                var backtraceData = entry.BacktraceData;
                Delete(entry);
                entry = BacktraceDatabaseContext.FirstOrDefault();
                if (backtraceData != null)
                {
                    BacktraceApi.Send(backtraceData);
                }
            }
        }
#if !NET35
        /// <summary>
        /// Send and asynchronous delete all entries from database
        /// </summary>
        public async Task FlushAsync()
        {
            if (BacktraceApi == null)
            {
                throw new ArgumentException("BacktraceApi is required if you want to use Flush method");
            }
            var entry = BacktraceDatabaseContext?.FirstOrDefault();
            while (entry != null)
            {
                var backtraceData = entry.BacktraceData;
                Delete(entry);
                entry = BacktraceDatabaseContext.FirstOrDefault();
                if (backtraceData != null)
                {
                    await BacktraceApi.SendAsync(backtraceData);
                }
            }
        }

        private async void OnTimedEventAsync(object source, ElapsedEventArgs e)
        {
            if (!BacktraceDatabaseContext.Any() || _timerBackgroundWork) return;
            _timerBackgroundWork = true;
            _timer.Stop();
            //read first entry (keep in mind LIFO and FIFO settings) from memory database
            var entry = BacktraceDatabaseContext.FirstOrDefault();
            while (entry != null)
            {
                var backtraceData = entry.BacktraceData;
                //meanwhile someone delete data from a disk
                if (backtraceData == null || backtraceData.Report == null)
                {
                    Delete(entry);
                }
                else
                {
                    //send entry from database to API
                    var result = await BacktraceApi.SendAsync(backtraceData);
                        if (result.Status == BacktraceResultStatus.Ok)
                    {
                        Delete(entry);
                    }
                    else
                    {
                        entry.Dispose();
                        BacktraceDatabaseContext.IncrementBatchRetry();
                        break;
                    }

                }
                entry = BacktraceDatabaseContext.FirstOrDefault();
            }
            _timer.Start();
            _timerBackgroundWork = false;
        }
#endif
        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            if (!BacktraceDatabaseContext.Any() || _timerBackgroundWork) return;
            _timerBackgroundWork = true;
            _timer.Stop();
            //read first entry (keep in mind LIFO and FIFO settings) from memory database
            var entry = BacktraceDatabaseContext.FirstOrDefault();
            while (entry != null)
            {
                var backtraceData = entry.BacktraceData;
                //meanwhile someone delete data from a disk
                if (backtraceData == null || backtraceData.Report == null)
                {
                    Delete(entry);
                }
                else
                {
                    //send entry from database to API
                    var result = BacktraceApi.Send(backtraceData);
                    if (result.Status == BacktraceResultStatus.Ok)
                    {
                        Delete(entry);
                    }
                    else
                    {
                        entry.Dispose();
                        BacktraceDatabaseContext.IncrementBatchRetry();
                        break;

                    }
                }
                entry = BacktraceDatabaseContext.FirstOrDefault();
            }
            _timerBackgroundWork = false;
            _timer.Start();

        }

        /// <summary>
        /// Create new minidump file in database directory path. Minidump file name is a random Guid
        /// </summary>
        /// <param name="backtraceReport">Current report</param>
        /// <param name="miniDumpType">Generated minidump type</param>
        /// <returns>Path to minidump file</returns>
        private string GenerateMiniDump(BacktraceReportBase backtraceReport, MiniDumpType miniDumpType)
        {
            //note that every minidump file generated by app ends with .dmp extension
            //its important information if you want to clear minidump file
            string minidumpDestinationPath = Path.Combine(DatabaseSettings.DatabasePath, $"{backtraceReport.Uuid}-dump.dmp");
            MinidumpException minidumpExceptionType = backtraceReport.ExceptionTypeReport
                ? MinidumpException.Present
                : MinidumpException.None;

            bool minidumpSaved = MinidumpHelper.Write(
                filePath: minidumpDestinationPath,
                options: miniDumpType,
                exceptionType: minidumpExceptionType);

            return minidumpSaved
                ? minidumpDestinationPath
                : string.Empty;
        }

        /// <summary>
        /// Get total number of entries in database
        /// </summary>
        /// <returns>Total number of entries</returns>
        internal int Count() => BacktraceDatabaseContext.Count();

        /// <summary>
        /// Detect all orphaned minidump files
        /// </summary>
        private void RemoveOrphaned()
        {
            var entries = BacktraceDatabaseContext.Get();
            BacktraceDatabaseFileContext.RemoveOrphaned(entries);
        }

        /// <summary>
        /// Load all reports stored in path passed by user
        /// </summary>
        private void LoadReports()
        {
            var files = BacktraceDatabaseFileContext.GetEntries();
            foreach (var file in files)
            {
                var entry = BacktraceDatabaseEntry.ReadFromFile(file);
                if (!entry.Valid())
                {
                    entry.Delete();
                    continue;
                }
                BacktraceDatabaseContext.Add(entry);
                entry.Dispose();
            }
        }

        #region dispose
        private bool _disposed = false; // To detect redundant calls

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
#if !NET35
                    BacktraceApi?.Dispose();
                    _timer?.Dispose();
#endif
                }
                _disposed = true;
            }
        }

        ~BacktraceDatabase()
        {
            Dispose(false);
        }
        #endregion
    }
}