// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Framework;

using System;
using System.Runtime.Serialization;

/// <summary>An exception thrown when the view state cache key is missing or invalid.</summary>
[Serializable]
public class InvalidViewStateCacheKeyException : ApplicationException
{
    /// <inheritdoc />
    public InvalidViewStateCacheKeyException()
    {
    }

    /// <inheritdoc />
    public InvalidViewStateCacheKeyException(string message)
        : base(message)
    {
    }

    /// <inheritdoc />
    public InvalidViewStateCacheKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// Kept for binary compatibility with the serializable-exception pattern. Formatter-based
    /// serialization is obsolete on modern .NET (SYSLIB0051) but removing the constructor would be a
    /// public surface change, so the diagnostic is suppressed rather than the member dropped.
    /// </remarks>
#pragma warning disable SYSLIB0051
    protected InvalidViewStateCacheKeyException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
#pragma warning restore SYSLIB0051
}
