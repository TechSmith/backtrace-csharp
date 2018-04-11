﻿using Backtrace.Base;
using Backtrace.Interfaces;
using Backtrace.Model;
using Backtrace.Services;
using System;
using System.Collections.Generic;
using System.Reflection;
#if !NET35
using System.Threading.Tasks;
#endif

namespace Backtrace
{
    /// <summary>
    /// Backtrace .NET Client 
    /// </summary>
    public class BacktraceClient : BacktraceBase<object>, IBacktraceClient<object>
    {
        /// <summary>
        /// Set an event executed before sending data to Backtrace API
        /// </summary>
        public Action<BacktraceReport> OnReportStart;

        /// <summary>
        /// Set an event executed after sending data to Backtrace API
        /// </summary>
        public Action<BacktraceResult> AfterSend;

        #region Constructor
#if !NETSTANDARD2_0
        /// <summary>
        /// Initializing Backtrace client instance
        /// </summary>
        /// <param name="sectionName">Backtrace configuration section in App.config or Web.config file. Default section is BacktraceCredentials</param>
        /// <param name="attributes">Client's attributes</param>
        /// <param name="databaseSettings">Backtrace database settings</param>
        /// <param name="reportPerMin">Numbers of records sending per one min</param>
        /// <param name="tlsLegacySupport">Set SSL and TLS flags for https request to Backtrace API</param>
        public BacktraceClient(
            BacktraceDatabaseSettings databaseSettings,
            string sectionName = "BacktraceCredentials",
            Dictionary<string, object> attributes = null,
            uint reportPerMin = 3,
            bool tlsLegacySupport = false)
            : base(BacktraceCredentials.ReadConfigurationSection(sectionName),
                attributes, new BacktraceDatabase<object>(databaseSettings), reportPerMin, tlsLegacySupport)
        { }

        /// <summary>
        /// Initializing Backtrace client instance
        /// </summary>
        /// <param name="sectionName">Backtrace configuration section in App.config or Web.config file. Default section is BacktraceCredentials</param>
        /// <param name="attributes">Client's attributes</param>
        /// <param name="databaseSettings">Backtrace database settings</param>
        /// <param name="reportPerMin">Numbers of records sending per one min</param>
        /// <param name="tlsLegacySupport">Set SSL and TLS flags for https request to Backtrace API</param>
        public BacktraceClient(
            string sectionName = "BacktraceCredentials",
            Dictionary<string, object> attributes = null,
            IBacktraceDatabase<object> database = null,
            uint reportPerMin = 3,
            bool tlsLegacySupport = false)
            : base(BacktraceCredentials.ReadConfigurationSection(sectionName),
                attributes, database, reportPerMin, tlsLegacySupport)
        { }
#endif
        /// <summary>
        /// Initializing Backtrace client instance with BacktraceCredentials
        /// </summary>
        /// <param name="backtraceCredentials">Backtrace credentials</param>
        /// <param name="attributes">Client's attributes</param>
        /// <param name="databaseSettings">Backtrace database settings</param>
        /// <param name="reportPerMin">Numbers of records sending per one minute</param>
        /// <param name="tlsLegacySupport">Set SSL and TLS flags for https request to Backtrace API</param>
        public BacktraceClient(
            BacktraceCredentials backtraceCredentials,
            BacktraceDatabaseSettings databaseSettings,
            Dictionary<string, object> attributes = null,            
            uint reportPerMin = 3,
            bool tlsLegacySupport = false)
            : base(backtraceCredentials, attributes,
                  databaseSettings, reportPerMin, tlsLegacySupport)
        { }

        /// <summary>
        /// Initializing Backtrace client instance with BacktraceCredentials
        /// </summary>
        /// <param name="backtraceCredentials">Backtrace credentials</param>
        /// <param name="attributes">Client's attributes</param>
        /// <param name="databaseSettings">Backtrace database settings</param>
        /// <param name="reportPerMin">Numbers of records sending per one minute</param>
        /// <param name="tlsLegacySupport">Set SSL and TLS flags for https request to Backtrace API</param>
        public BacktraceClient(
            BacktraceCredentials backtraceCredentials,
            Dictionary<string, object> attributes = null,
            IBacktraceDatabase<object> backtraceDatabase = null,
            uint reportPerMin = 3,
            bool tlsLegacySupport = false)
            : base(backtraceCredentials, attributes,
                  backtraceDatabase, reportPerMin, tlsLegacySupport)
        { }
        #endregion

        #region Send synchronous
        /// <summary>
        /// Sending an exception to Backtrace API
        /// </summary>
        /// <param name="exception">Current exception</param>
        /// <param name="attributes">Additional information about application state</param>
        /// <param name="attachmentPaths">Path to all report attachments</param>
        public virtual BacktraceResult Send(
            Exception exception,
            Dictionary<string, object> attributes = null,
            List<string> attachmentPaths = null)
        {
            return Send(new BacktraceReport(exception, attributes, attachmentPaths));
        }

        /// <summary>
        /// Sending a message to Backtrace API
        /// </summary>
        /// <param name="message">Custom client message</param>
        /// <param name="attributes">Additional information about application state</param>
        /// <param name="attachmentPaths">Path to all report attachments</param>
        public virtual BacktraceResult Send(
            string message,
            Dictionary<string, object> attributes = null,
            List<string> attachmentPaths = null)
        {
            return Send(new BacktraceReport(message, attributes, attachmentPaths));
        }

        /// <summary>
        /// Sending a backtrace report to Backtrace API
        /// </summary>
        /// <param name="backtraceReport">Current report</param>
        public BacktraceResult Send(BacktraceReport backtraceReport)
        {
            OnReportStart?.Invoke(backtraceReport);
            var result = base.Send(backtraceReport);
            AfterSend?.Invoke(result);

            //check if there is more errors to send
            //handle inner exception
            result.InnerExceptionResult = HandleInnerException(backtraceReport);
            return result;
        }
        #endregion

#if !NET35
        #region Send asynchronous
        /// <summary>
        /// Sending asynchronous Backtrace report to Backtrace API
        /// </summary>
        /// <param name="backtraceReport">Current report</param>
        /// <returns>Server response</returns>
        public async Task<BacktraceResult> SendAsync(BacktraceReport backtraceReport)
        {
            OnReportStart?.Invoke(backtraceReport);
            var result = await base.SendAsync(backtraceReport);
            AfterSend?.Invoke(result);

            //check if there is more errors to send
            //handle inner exception
            result.InnerExceptionResult = await HandleInnerExceptionAsync(backtraceReport);
            return result;
        }

        /// <summary>
        /// Sending a message to Backtrace API
        /// </summary>
        /// <param name="message">Custom client message</param>
        /// <param name="attributes">Additional information about application state</param>
        /// <param name="attachmentPaths">Path to all report attachments</param>
        public virtual async Task<BacktraceResult> SendAsync(
            string message,
            Dictionary<string, object> attributes = null,
            List<string> attachmentPaths = null)
        {
            return await SendAsync(new BacktraceReport(message, attributes, attachmentPaths));
        }

        /// <summary>
        /// Sending asynchronous exception to Backtrace API
        /// </summary>
        /// <param name="exception">Current exception</param>
        /// <param name="attributes">Additional information about application state</param>
        /// <param name="attachmentPaths">Path to all report attachments</param>
        public virtual async Task<BacktraceResult> SendAsync(
            Exception exception,
            Dictionary<string, object> attributes = null,
            List<string> attachmentPaths = null)
        {
            return await SendAsync(new BacktraceReport(exception, attributes, attachmentPaths));
        }
        #endregion

        /// <summary>
        /// Handle inner exception in current backtrace report
        /// if inner exception exists, client should send report twice - one with current exception, one with inner exception
        /// </summary>
        /// <param name="report">current report</param>
        private async Task<BacktraceResult> HandleInnerExceptionAsync(BacktraceReport report)
        {
            var innerExceptionReport = CreateInnerReport(report);
            if (innerExceptionReport == null)
            {
                return null;
            }
            return await SendAsync(innerExceptionReport);
        }
#endif

        /// <summary>
        /// Handle inner exception in current backtrace report
        /// if inner exception exists, client should send report twice - one with current exception, one with inner exception
        /// </summary>
        /// <param name="report">current report</param>
        private BacktraceResult HandleInnerException(BacktraceReport report)
        {
            var innerExceptionReport = CreateInnerReport(report);
            if (innerExceptionReport == null)
            {
                return null;
            }
            return Send(innerExceptionReport);
        }

        private BacktraceReport CreateInnerReport(BacktraceReport report)
        {
            // there is no additional exception inside current exception
            // or exception does not exists
            if (!report.ExceptionTypeReport || report.Exception.InnerException == null)
            {
                return null;
            }
            //we have to create a copy of an inner exception report
            //to have the same calling assembly property
            return report.CreateInnerReport();
        }
    }
}
