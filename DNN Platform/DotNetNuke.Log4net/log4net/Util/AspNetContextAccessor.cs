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

using Microsoft.AspNetCore.Http;

namespace log4net.Util
{
    /// <summary>
    /// Provides log4net with access to the ASP.NET Core request context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>HttpContext.Current</c> does not exist on modern .NET. The hosting application
    /// (the DNN host) must assign <see cref="HttpContextAccessor"/> once during startup, e.g.
    /// </para>
    /// <code>
    /// log4net.Util.AspNetContextAccessor.HttpContextAccessor =
    ///     app.Services.GetRequiredService&lt;Microsoft.AspNetCore.Http.IHttpContextAccessor&gt;();
    /// </code>
    /// <para>
    /// Every member of this class is safe to call when no accessor has been assigned and when
    /// no request is in flight (background threads, console hosts); in that case
    /// <see cref="Current"/> simply returns <c>null</c> and the ASP.NET pattern converters and
    /// appenders degrade to their documented "not available" output.
    /// </para>
    /// </remarks>
    public static class AspNetContextAccessor
    {
        private static IHttpContextAccessor accessor;

        /// <summary>
        /// Gets or sets the <see cref="IHttpContextAccessor"/> used to resolve the current request.
        /// </summary>
        /// <value>The accessor supplied by the host, or <c>null</c> when log4net is not hosted in ASP.NET.</value>
        [System.CLSCompliant(false)]
        public static IHttpContextAccessor HttpContextAccessor
        {
            get { return accessor; }
            set { accessor = value; }
        }

        /// <summary>
        /// Gets the <see cref="HttpContext"/> for the request being processed on this thread,
        /// or <c>null</c> when there is no accessor configured or no active request.
        /// </summary>
        [System.CLSCompliant(false)]
        public static HttpContext Current
        {
            get
            {
                IHttpContextAccessor current = accessor;
                if (current == null)
                {
                    return null;
                }

                try
                {
                    return current.HttpContext;
                }
                catch (System.ObjectDisposedException)
                {
                    // The request has already completed and its context was disposed.
                    return null;
                }
            }
        }
    }
}
