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

using System.Collections;
using System.Collections.Generic;
using System.IO;
using log4net.Core;
using log4net.Util;
using Microsoft.AspNetCore.Http;

namespace log4net.Layout.Pattern
{
    /// <summary>
    /// Converter for items in the <see cref="HttpContext" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Outputs an item from the <see cref="HttpContext" />.
    /// </para>
    /// </remarks>
    /// <author>Ron Grabowski</author>
    internal sealed class AspNetContextPatternConverter : AspNetPatternLayoutConverter
    {
        /// <summary>
        /// Write the ASP.NET HttpContext item to the output.
        /// </summary>
        /// <param name="writer"><see cref="TextWriter" /> that will receive the formatted result.</param>
        /// <param name="loggingEvent">The <see cref="LoggingEvent" /> on which the pattern converter should be executed.</param>
        /// <param name="httpContext">The <see cref="HttpContext" /> under which the ASP.NET request is running.</param>
        /// <remarks>
        /// <para>
        /// Writes out the value of a named property. The property name
        /// should be set in the <see cref="log4net.Util.PatternConverter.Option"/>
        /// property. If no property has been set, all key value pairs from
        /// <see cref="HttpContext.Items"/> are written to the output.
        /// </para>
        /// </remarks>
        protected override void Convert(TextWriter writer, LoggingEvent loggingEvent, HttpContext httpContext)
        {
            IDictionary<object, object> items = httpContext.Items;
            if (items == null)
            {
                writer.Write(SystemInfo.NotAvailableText);
                return;
            }

            if (this.Option != null)
            {
                object value;
                if (!items.TryGetValue(this.Option, out value))
                {
                    value = null;
                }

                WriteObject(writer, loggingEvent.Repository, value);
            }
            else
            {
                // HttpContext.Items is IDictionary<object, object> on modern .NET; copy into a
                // non-generic IDictionary so the rendered output keeps the original
                // {key1=value1, key2=value2} shape.
                Hashtable all = new Hashtable(items.Count);
                foreach (KeyValuePair<object, object> entry in items)
                {
                    if (entry.Key == null)
                    {
                        continue;
                    }

                    all[entry.Key] = entry.Value;
                }

                WriteObject(writer, loggingEvent.Repository, all);
            }
        }
    }
}
