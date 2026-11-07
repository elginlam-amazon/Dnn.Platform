// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    /// <summary>The tracing level of a services framework trace record.</summary>
    /// <remarks>Replaces <c>System.Web.Http.Tracing.TraceLevel</c>, with the same members and order.</remarks>
    public enum DnnTraceLevel
    {
        /// <summary>Tracing is disabled.</summary>
        Off = 0,

        /// <summary>Debug level.</summary>
        Debug = 1,

        /// <summary>Informational level.</summary>
        Info = 2,

        /// <summary>Warning level.</summary>
        Warn = 3,

        /// <summary>Error level.</summary>
        Error = 4,

        /// <summary>Fatal level.</summary>
        Fatal = 5,
    }
}
