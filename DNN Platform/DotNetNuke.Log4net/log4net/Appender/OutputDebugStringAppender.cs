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
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using log4net.Core;
using log4net.Util;

namespace log4net.Appender
{
    /// <summary>Appends log events to the OutputDebugString system.</summary>
    /// <remarks>
    /// <para>
    /// OutputDebugStringAppender appends log events to the
    /// OutputDebugString system.
    /// </para>
    /// <para>
    /// The string is passed to the native <c>OutputDebugString</c> 
    /// function.
    /// </para>
    /// <para>
    /// The native <c>OutputDebugString</c> function only exists on Windows. The appender type
    /// itself remains constructible and configurable on every platform so that log4net
    /// configuration parsing never fails, but on non-Windows platforms <see cref="Append"/>
    /// is a documented no-op that emits a single <see cref="LogLog"/> warning instead.
    /// </para>
    /// </remarks>
    /// <author>Nicko Cadell</author>
    /// <author>Gert Driesen</author>
    public class OutputDebugStringAppender : AppenderSkeleton
    {
        private bool m_unsupportedPlatformWarningIssued;

        /// <summary>Initializes a new instance of the <see cref="OutputDebugStringAppender" /> class.</summary>
        /// <remarks>
        /// <para>
        /// Default constructor.
        /// </para>
        /// </remarks>
        public OutputDebugStringAppender()
        {
        }

        /// <summary>Write the logging event to the output debug string API</summary>
        /// <param name="loggingEvent">the event to log</param>
        /// <remarks>
        /// <para>
        /// Write the logging event to the output debug string API. On platforms other than
        /// Windows this is a no-op and a warning is written to <see cref="LogLog"/> once.
        /// </para>
        /// </remarks>
        protected override void Append(LoggingEvent loggingEvent) 
        {
            if (!OperatingSystem.IsWindows())
            {
                if (!this.m_unsupportedPlatformWarningIssued)
                {
                    this.m_unsupportedPlatformWarningIssued = true;
                    LogLog.Warn(declaringType, "OutputDebugStringAppender: the native OutputDebugString API is only available on Windows; this appender is a no-op on the current platform.");
                }

                return;
            }

            OutputDebugString(this.RenderLoggingEvent(loggingEvent));
        } 

        /// <summary>This appender requires a <see cref="Layout"/> to be set.</summary>
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

        /// <summary>Stub for OutputDebugString native method</summary>
        /// <param name="message">the string to output</param>
        /// <remarks>
        /// <para>
        /// Stub for OutputDebugString native method. Windows only.
        /// </para>
        /// </remarks>
        [SupportedOSPlatform("windows")]
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        protected static extern void OutputDebugString(string message);

        /// <summary>
        /// The fully qualified type of the OutputDebugStringAppender class.
        /// </summary>
        private static readonly Type declaringType = typeof(OutputDebugStringAppender);
    }
}
