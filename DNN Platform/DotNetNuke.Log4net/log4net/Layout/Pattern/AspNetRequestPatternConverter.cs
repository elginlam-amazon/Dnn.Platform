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
using log4net.Core;
using log4net.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace log4net.Layout.Pattern
{
    /// <summary>
    /// Converter for items in the ASP.NET request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Outputs an item from the current <see cref="HttpRequest" />. The classic
    /// <c>HttpRequest.Params</c> collection does not exist on modern .NET, so the value is
    /// looked up across the query string, the form (only when the request actually carries a
    /// form content type) and the request headers, in that order — which matches the classic
    /// precedence of <c>Params</c> closely enough for logging purposes.
    /// </para>
    /// </remarks>
    /// <author>Ron Grabowski</author>
    internal sealed class AspNetRequestPatternConverter : AspNetPatternLayoutConverter
    {
        /// <summary>
        /// Write the ASP.NET request item to the output.
        /// </summary>
        /// <param name="writer"><see cref="TextWriter" /> that will receive the formatted result.</param>
        /// <param name="loggingEvent">The <see cref="LoggingEvent" /> on which the pattern converter should be executed.</param>
        /// <param name="httpContext">The <see cref="HttpContext" /> under which the ASP.NET request is running.</param>
        /// <remarks>
        /// <para>
        /// Writes out the value of a named property. The property name
        /// should be set in the <see cref="log4net.Util.PatternConverter.Option"/>
        /// property. If no property has been set, every request parameter is written to the
        /// output; when there is no request <see cref="SystemInfo.NotAvailableText"/> is written.
        /// </para>
        /// </remarks>
        protected override void Convert(TextWriter writer, LoggingEvent loggingEvent, HttpContext httpContext)
        {
            HttpRequest request = null;
            try
            {
                request = httpContext.Request;
            }
            catch (ObjectDisposedException)
            {
                // The request has already completed; treat it like the classic case of the
                // Request property being unavailable (e.g. during application start).
            }
            catch (InvalidOperationException)
            {
                // Request features are not available on this context.
            }

            if (request == null)
            {
                writer.Write(SystemInfo.NotAvailableText);
                return;
            }

            if (this.Option != null)
            {
                WriteObject(writer, loggingEvent.Repository, GetParam(request, this.Option));
            }
            else
            {
                // Non-generic IDictionary so the rendered output keeps the original
                // {key1=value1, key2=value2} shape of the Params collection.
                Hashtable all = new Hashtable();
                Add(all, request.Query);
                if (HasForm(request))
                {
                    Add(all, request.Form);
                }

                Add(all, request.Headers);

                WriteObject(writer, loggingEvent.Repository, all);
            }
        }

        private static bool HasForm(HttpRequest request)
        {
            try
            {
                // Reading Request.Form on a non-form request throws at runtime.
                return request.HasFormContentType;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static object GetParam(HttpRequest request, string key)
        {
            StringValues values;

            if (request.Query != null && request.Query.TryGetValue(key, out values))
            {
                return values.ToString();
            }

            if (HasForm(request))
            {
                try
                {
                    if (request.Form != null && request.Form.TryGetValue(key, out values))
                    {
                        return values.ToString();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Form could not be read (e.g. the body has already been consumed).
                }
            }

            if (request.Headers != null && request.Headers.TryGetValue(key, out values))
            {
                return values.ToString();
            }

            return null;
        }

        private static void Add(Hashtable target, IEnumerable<KeyValuePair<string, StringValues>> source)
        {
            if (source == null)
            {
                return;
            }

            try
            {
                foreach (KeyValuePair<string, StringValues> entry in source)
                {
                    if (entry.Key == null || target.ContainsKey(entry.Key))
                    {
                        continue;
                    }

                    target[entry.Key] = entry.Value.ToString();
                }
            }
            catch (InvalidOperationException)
            {
                // The collection could not be enumerated (e.g. unreadable form body).
            }
        }
    }
}
