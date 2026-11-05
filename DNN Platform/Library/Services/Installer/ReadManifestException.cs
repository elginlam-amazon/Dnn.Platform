// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.Installer;

using System;
using System.Runtime.Serialization;

/// <summary>An exception thrown when there's an issue reading a manifest.</summary>
[Serializable]
public class ReadManifestException : Exception
{
    /// <inheritdoc />
    public ReadManifestException()
    {
    }

    /// <inheritdoc />
    public ReadManifestException(string message)
        : base(message)
    {
    }

    /// <inheritdoc />
    public ReadManifestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

#pragma warning disable SYSLIB0051 // Kept for API compatibility: the formatter-based
// serialization constructor is part of this public exception type's surface, and 59
// dependent projects compile against it. Nothing in the platform serializes it.
    /// <inheritdoc />
    protected ReadManifestException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
#pragma warning restore SYSLIB0051
}
