// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;

    using Microsoft.AspNetCore.Http;

    /// <summary>A contract specifying the ability to trace services framework requests.</summary>
    /// <remarks>Replaces <c>System.Web.Http.Tracing.ITraceWriter</c>.</remarks>
    public interface IDnnTraceWriter
    {
        /// <summary>Writes a trace record.</summary>
        /// <param name="request">The request being traced.</param>
        /// <param name="category">The trace category.</param>
        /// <param name="level">The trace level.</param>
        /// <param name="traceAction">The action which fills in the trace record.</param>
        void Trace(HttpRequest request, string category, DnnTraceLevel level, Action<DnnTraceRecord> traceAction);
    }
}
