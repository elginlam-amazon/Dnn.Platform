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

using System.IO;
using log4net.Core;
using log4net.Util;
using Microsoft.AspNetCore.Http;

namespace log4net.Layout.Pattern
{
    /// <summary>
    /// Abstract class that provides access to the current <see cref="HttpContext"/> that
    /// derived classes need.
    /// </summary>
    /// <remarks>
    /// The context is resolved through <see cref="AspNetContextAccessor"/> (the modern .NET
    /// replacement for <c>HttpContext.Current</c>). This class handles the case where there is
    /// no active request by writing <see cref="SystemInfo.NotAvailableText" /> to the writer.
    /// </remarks>
    /// <author>Ron Grabowski</author>
    internal abstract class AspNetPatternLayoutConverter : PatternLayoutConverter
    {
        protected override void Convert(TextWriter writer, LoggingEvent loggingEvent)
        {
            HttpContext httpContext = AspNetContextAccessor.Current;
            if (httpContext == null)
            {
                writer.Write(SystemInfo.NotAvailableText);
            }
            else
            {
                this.Convert(writer, loggingEvent, httpContext);
            }
        }

        /// <summary>
        /// Derived pattern converters must override this method in order to
        /// convert conversion specifiers in the correct way.
        /// </summary>
        /// <param name="writer"><see cref="TextWriter" /> that will receive the formatted result.</param>
        /// <param name="loggingEvent">The <see cref="LoggingEvent" /> on which the pattern converter should be executed.</param>
        /// <param name="httpContext">The <see cref="HttpContext" /> under which the ASP.NET request is running.</param>
        protected abstract void Convert(TextWriter writer, LoggingEvent loggingEvent, HttpContext httpContext);
    }
}
