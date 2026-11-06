// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Text;

    using DotNetNuke.Instrumentation;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.Extensions.Logging;

    /// <summary>An <see cref="IDnnTraceWriter"/> implementation writing to the DNN <see cref="ILogger"/>.</summary>
    /// <remarks>
    /// Replaces the Web API 2 <c>System.Web.Http.Tracing.ITraceWriter</c> implementation which was installed
    /// into <c>GlobalConfiguration.Configuration.Services</c>; the trace levels map one-for-one onto
    /// <see cref="LogLevel"/> as they did before.
    /// </remarks>
    internal sealed partial class TraceWriter : IDnnTraceWriter
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<TraceWriter>();
        private readonly bool enabled;

        /// <summary>Initializes a new instance of the <see cref="TraceWriter"/> class.</summary>
        /// <param name="isTracingEnabled">Whether tracing is enabled.</param>
        public TraceWriter(bool isTracingEnabled)
        {
            this.enabled = isTracingEnabled;
        }

        /// <inheritdoc />
        public void Trace(HttpRequest request, string category, DnnTraceLevel level, Action<DnnTraceRecord> traceAction)
        {
            if (!this.enabled || level == DnnTraceLevel.Off)
            {
                return;
            }

            var rec = new DnnTraceRecord(request, category, level);
            traceAction(rec);
            Log(rec);
        }

        private static void Log(DnnTraceRecord rec)
        {
            var message = new StringBuilder();
            if (rec.Request != null)
            {
                if (!string.IsNullOrEmpty(rec.Request.Method))
                {
                    message.Append(' ').Append(rec.Request.Method);
                }

                message.Append(' ').Append(rec.Request.GetDisplayUrl());
            }

            if (!string.IsNullOrEmpty(rec.Category))
            {
                message.Append(' ').Append(rec.Category);
            }

            if (!string.IsNullOrEmpty(rec.Message))
            {
                message.Append(' ').Append(rec.Message);
            }

            var output = message.ToString();
            if (string.IsNullOrEmpty(output))
            {
                return;
            }

            var logLevel = rec.Level switch
            {
                DnnTraceLevel.Debug => LogLevel.Debug,
                DnnTraceLevel.Info => LogLevel.Information,
                DnnTraceLevel.Warn => LogLevel.Warning,
                DnnTraceLevel.Error => LogLevel.Error,
                DnnTraceLevel.Fatal => LogLevel.Critical,
                _ => LogLevel.None,
            };

            Logger.TraceWriterLogMessage(logLevel, output);
        }
    }
}
