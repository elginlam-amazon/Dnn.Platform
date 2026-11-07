// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.InternalServices;

using System;

/// <summary>An exception thrown when attempting to share a module that does not support sharing.</summary>
/// <remarks>
/// The <c>[Serializable]</c> attribute and the <c>SerializationInfo</c> constructor were dropped: they only
/// served <c>BinaryFormatter</c>-based serialization, which is removed in modern .NET (SYSLIB0051). Nothing
/// serializes this exception; it is thrown and caught in-process.
/// </remarks>
public class SharingUnsupportedException : Exception
{
    /// <inheritdoc />
    public SharingUnsupportedException()
    {
    }

    /// <inheritdoc />
    public SharingUnsupportedException(string message)
        : base(message)
    {
    }

    /// <inheritdoc />
    public SharingUnsupportedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
