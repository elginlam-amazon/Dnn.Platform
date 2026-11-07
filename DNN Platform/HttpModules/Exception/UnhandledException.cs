// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.HttpModules.Exceptions;

using System;

/// <summary>An exception which wraps an exception that was not handled.</summary>
/// <remarks>
/// The <c>[Serializable]</c> attribute and the <c>(SerializationInfo, StreamingContext)</c>
/// constructor are gone: binary serialization of exceptions is obsolete (SYSLIB0051) and
/// disabled on modern .NET. Every other constructor is unchanged.
/// </remarks>
public class UnhandledException : Exception
{
    /// <inheritdoc cref="Exception()" />
    public UnhandledException()
    {
    }

    /// <inheritdoc cref="Exception(string)" />
    public UnhandledException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="Exception(string, Exception)" />
    public UnhandledException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
