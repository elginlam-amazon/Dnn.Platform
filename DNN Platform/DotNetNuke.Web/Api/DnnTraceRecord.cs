// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using Microsoft.AspNetCore.Http;

    /// <summary>A single services framework trace record.</summary>
    /// <remarks>Replaces <c>System.Web.Http.Tracing.TraceRecord</c>; the request is the server-side <see cref="HttpRequest"/>.</remarks>
    public class DnnTraceRecord
    {
        /// <summary>Initializes a new instance of the <see cref="DnnTraceRecord"/> class.</summary>
        /// <param name="request">The request being traced.</param>
        /// <param name="category">The trace category.</param>
        /// <param name="level">The trace level.</param>
        public DnnTraceRecord(HttpRequest request, string category, DnnTraceLevel level)
        {
            this.Request = request;
            this.Category = category;
            this.Level = level;
        }

        /// <summary>Gets the request being traced.</summary>
        public HttpRequest Request { get; }

        /// <summary>Gets the trace category.</summary>
        public string Category { get; }

        /// <summary>Gets the trace level.</summary>
        public DnnTraceLevel Level { get; }

        /// <summary>Gets or sets the trace message.</summary>
        public string Message { get; set; }
    }
}
