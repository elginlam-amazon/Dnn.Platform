// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
//
// Licensed to the Apache Software Foundation (ASF) under one or more
// contributor license agreements. See the NOTICE file distributed with
// this work for additional information regarding copyright ownership.
// The ASF licenses this file to you under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with
// the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using log4net.Core;
using log4net.Layout;
using log4net.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsILogger = Microsoft.Extensions.Logging.ILogger;

namespace log4net.Appender
{
    /// <summary>
    /// <para>
    /// Appends log events to the ASP.NET request's logging pipeline.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The classic ASP.NET page trace (<c>HttpContext.Current.Trace</c> / <c>Trace.axd</c>) has no
    /// equivalent on modern .NET. This appender therefore writes through the
    /// <see cref="ILoggerFactory"/> registered in the current request's service provider: events
    /// at <see cref="Level.Warn"/> and above are written with
    /// <c>LogWarning</c> and all other events
    /// with <c>LogInformation</c>.
    /// </para>
    /// <para>
    /// The formatted <see cref="Category"/> layout is used as the <c>ILogger</c> category
    /// name, which is the closest equivalent of the category parameter that used to be passed to
    /// <c>TraceContext.Write</c>/<c>TraceContext.Warn</c>. The rendered logging event is the
    /// message.
    /// </para>
    /// <para>
    /// When log4net is not running inside an ASP.NET request, or the host did not configure
    /// <see cref="AspNetContextAccessor"/>, or no <see cref="ILoggerFactory"/> is registered, the
    /// event is silently dropped — exactly as the original appender dropped events when there was
    /// no <c>HttpContext</c> or tracing was disabled.
    /// </para>
    /// </remarks>
    /// <author>Nicko Cadell</author>
    /// <author>Gert Driesen</author>
    /// <author>Ron Grabowski</author>
    public class AspNetTraceAppender : AppenderSkeleton
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetTraceAppender" /> class.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Default constructor.
        /// </para>
        /// </remarks>
        public AspNetTraceAppender()
        {
        }

        /// <summary>
        /// Write the logging event to the ASP.NET request logger.
        /// </summary>
        /// <param name="loggingEvent">the event to log</param>
        /// <remarks>
        /// <para>
        /// Resolves an <c>ILogger</c> for the formatted <see cref="Category"/> from the
        /// current request's <see cref="ILoggerFactory"/> and writes the rendered event to it.
        /// </para>
        /// </remarks>
        protected override void Append(LoggingEvent loggingEvent)
        {
            // check if log4net is running in the context of an ASP.NET application
            HttpContext httpContext = AspNetContextAccessor.Current;
            if (httpContext == null)
            {
                return;
            }

            ILoggerFactory loggerFactory = GetLoggerFactory(httpContext);
            if (loggerFactory == null)
            {
                return;
            }

            string category = this.m_category != null
                ? this.m_category.Format(loggingEvent)
                : loggingEvent.LoggerName;

            if (string.IsNullOrEmpty(category))
            {
                category = typeof(AspNetTraceAppender).FullName;
            }

            MsILogger logger = loggerFactory.CreateLogger(category);
            string message = this.RenderLoggingEvent(loggingEvent);

            if (loggingEvent.Level >= Level.Warn)
            {
                logger.LogWarning("{Log4NetEvent}", message);
            }
            else
            {
                logger.LogInformation("{Log4NetEvent}", message);
            }
        }

        private static ILoggerFactory GetLoggerFactory(HttpContext httpContext)
        {
            try
            {
                IServiceProvider services = httpContext.RequestServices;
                if (services == null)
                {
                    return null;
                }

                return services.GetService<ILoggerFactory>();
            }
            catch (ObjectDisposedException)
            {
                // The request scope has already been disposed.
                return null;
            }
            catch (InvalidOperationException)
            {
                // Request services are not available on this context.
                return null;
            }
        }

        /// <summary>
        /// This appender requires a <see cref="Layout"/> to be set.
        /// </summary>
        /// <value><c>true</c></value>
        /// <remarks>
        /// <para>
        /// This appender requires a <see cref="Layout"/> to be set.
        /// </para>
        /// </remarks>
        protected override bool RequiresLayout
        {
            get { return true; }
        }

        /// <summary>
        /// The category parameter used as the <c>ILogger</c> category name.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Defaults to %logger which will use the logger name of the current
        /// <see cref="LoggingEvent"/> as the category.
        /// </para>
        /// </remarks>
        public PatternLayout Category
        {
            get { return this.m_category; }
            set { this.m_category = value; }
        }

        /// <summary>
        /// Defaults to %logger
        /// </summary>
        private PatternLayout m_category = new PatternLayout("%logger");
    }
}
