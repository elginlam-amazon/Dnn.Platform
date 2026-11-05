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
using System.IO;
using log4net.Core;
using log4net.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace log4net.Layout.Pattern
{
    /// <summary>
    /// Converter for items in the ASP.NET cache.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Outputs an item from the application's <see cref="IMemoryCache"/>, which replaces the
    /// classic <c>HttpRuntime.Cache</c>. The cache is resolved from
    /// <see cref="HttpContext.RequestServices"/>. Because <see cref="IMemoryCache"/> cannot be
    /// enumerated, the "all cache items" output is built from the keys the application has
    /// registered with <see cref="AspNetCacheKeyRegistry"/>; keys that cannot be read render as
    /// the <see cref="SystemInfo.NullText"/> marker.
    /// </para>
    /// </remarks>
    /// <author>Ron Grabowski</author>
    internal sealed class AspNetCachePatternConverter : AspNetPatternLayoutConverter
    {
        /// <summary>
        /// Write the ASP.NET cache item to the output.
        /// </summary>
        /// <param name="writer"><see cref="TextWriter" /> that will receive the formatted result.</param>
        /// <param name="loggingEvent">The <see cref="LoggingEvent" /> on which the pattern converter should be executed.</param>
        /// <param name="httpContext">The <see cref="HttpContext" /> under which the ASP.NET request is running.</param>
        /// <remarks>
        /// <para>
        /// Writes out the value of a named property. The property name
        /// should be set in the <see cref="log4net.Util.PatternConverter.Option"/>
        /// property. If no property has been set, all known key value pairs from the cache will
        /// be written to the output. When no cache is available
        /// <see cref="SystemInfo.NotAvailableText"/> is written.
        /// </para>
        /// </remarks>
        protected override void Convert(TextWriter writer, LoggingEvent loggingEvent, HttpContext httpContext)
        {
            IMemoryCache cache = GetCache(httpContext);
            if (cache == null)
            {
                writer.Write(SystemInfo.NotAvailableText);
                return;
            }

            if (this.Option != null)
            {
                WriteObject(writer, loggingEvent.Repository, GetValue(cache, this.Option));
            }
            else
            {
                // Non-generic IDictionary so the rendered output keeps the original
                // {key1=value1, key2=value2} shape.
                Hashtable all = new Hashtable();
                foreach (object key in AspNetCacheKeyRegistry.GetKeys())
                {
                    all[key] = GetValue(cache, key);
                }

                WriteObject(writer, loggingEvent.Repository, all);
            }
        }

        private static IMemoryCache GetCache(HttpContext httpContext)
        {
            try
            {
                IServiceProvider services = httpContext.RequestServices;
                if (services == null)
                {
                    return null;
                }

                return services.GetService<IMemoryCache>();
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

        private static object GetValue(IMemoryCache cache, object key)
        {
            object value;
            if (cache.TryGetValue(key, out value))
            {
                return value;
            }

            // The entry has expired, was never added, or the key is not readable:
            // render the documented NULL marker.
            return null;
        }
    }
}
