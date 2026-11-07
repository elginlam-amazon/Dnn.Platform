// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.UserControls;

using System;
using System.Runtime.Serialization;

/// <summary>An exception that's thrown when there's an error rendering a control.</summary>
[Serializable]
public class RenderException : Exception
{
    /// <inheritdoc />
    public RenderException()
    {
    }

    /// <inheritdoc />
    public RenderException(string message)
        : base(message)
    {
    }

    /// <inheritdoc />
    public RenderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    #pragma warning disable SYSLIB0051 // Kept for API compatibility: the formatter-based serialization constructor is part
        // of this public exception type's surface and dependent projects compile against
        // it. Nothing in the platform serializes these exceptions.
    /// <inheritdoc />
    protected RenderException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
    #pragma warning restore SYSLIB0051
}
