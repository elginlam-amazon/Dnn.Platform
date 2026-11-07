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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using log4net.Core;
using log4net.Util;
using Microsoft.AspNetCore.Http;

namespace log4net.Layout.Pattern
{
    /// <summary>
    /// Converter for items in the ASP.NET session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Outputs an item from the <see cref="ISession" /> of the current request.
    /// </para>
    /// </remarks>
    /// <author>Ron Grabowski</author>
    internal sealed class AspNetSessionPatternConverter : AspNetPatternLayoutConverter
    {
        /// <summary>
        /// Write the ASP.NET session item to the output.
        /// </summary>
        /// <param name="writer"><see cref="TextWriter" /> that will receive the formatted result.</param>
        /// <param name="loggingEvent">The <see cref="LoggingEvent" /> on which the pattern converter should be executed.</param>
        /// <param name="httpContext">The <see cref="HttpContext" /> under which the ASP.NET request is running.</param>
        /// <remarks>
        /// <para>
        /// Writes out the value of a named property. The property name
        /// should be set in the <see cref="log4net.Util.PatternConverter.Option"/>
        /// property. If no property has been set, all key value pairs from the session will
        /// be written to the output. When the request has no session (session state is not
        /// enabled) <see cref="SystemInfo.NotAvailableText"/> is written instead.
        /// </para>
        /// </remarks>
        protected override void Convert(TextWriter writer, LoggingEvent loggingEvent, HttpContext httpContext)
        {
            ISession session = null;
            try
            {
                session = httpContext.Session;
            }
            catch (InvalidOperationException)
            {
                // Session state is not configured, or the request already completed
                // (ObjectDisposedException derives from InvalidOperationException);
                // treat it like the classic case of HttpContext.Session returning null.
            }

            if (session == null || !session.IsAvailable)
            {
                writer.Write(SystemInfo.NotAvailableText);
                return;
            }

            if (this.Option != null)
            {
                WriteObject(writer, loggingEvent.Repository, GetValue(session, this.Option));
            }
            else
            {
                // The classic converter wrote the HttpSessionState itself, which the default
                // renderer enumerates as its KEY collection, producing "{key1, key2}".
                // ISession exposes the same key collection, so write it directly to keep the
                // rendered output byte-identical to the .NET Framework behaviour.
                List<string> keys = new List<string>();
                foreach (string key in session.Keys)
                {
                    if (key == null)
                    {
                        continue;
                    }

                    keys.Add(key);
                }

                WriteObject(writer, loggingEvent.Repository, keys);
            }
        }

        /// <summary>
        /// Reads a single session entry, decoding it as UTF-8 text when possible.
        /// </summary>
        /// <param name="session">The session to read from.</param>
        /// <param name="key">The key of the entry to read.</param>
        /// <returns>The decoded string, the raw bytes when they are not valid UTF-8 text, or <c>null</c> when the key is absent.</returns>
        private static object GetValue(ISession session, string key)
        {
            byte[] raw;
            if (!session.TryGetValue(key, out raw) || raw == null)
            {
                return null;
            }

            try
            {
                return new UTF8Encoding(false, true).GetString(raw);
            }
            catch (DecoderFallbackException)
            {
                // Not text (e.g. a serialized object); hand the raw bytes to the renderer.
                return raw;
            }
        }
    }
}
