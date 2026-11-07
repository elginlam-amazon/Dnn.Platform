// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client;

using System;

/// <summary>An exception thrown from an operation that requires an ASP.NET AJAX <c>ScriptManager</c> when it is not present.</summary>
/// <remarks>
/// The <c>System.Web.UI.ScriptManager</c> type this originally referenced is a WebForms type that does not exist on
/// modern .NET, so the documentation reference is no longer a <c>cref</c>. The exception type itself is still thrown by
/// the converted Tag Helpers when the WebForms-era async post-back host is required but unavailable.
/// </remarks>
[Serializable]
public class ScriptManagerRequiredException : Exception
{
    /// <inheritdoc />
    public ScriptManagerRequiredException()
    {
    }

    /// <inheritdoc />
    public ScriptManagerRequiredException(string message)
        : base(message)
    {
    }

    /// <inheritdoc />
    public ScriptManagerRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    // The legacy `protected ScriptManagerRequiredException(SerializationInfo, StreamingContext)` constructor
    // implemented the BinaryFormatter-era ISerializable pattern. Binary serialization of exceptions is obsolete
    // (SYSLIB0051) and removed in .NET 10, so the constructor is intentionally not carried over. The type and its
    // three public constructors are unchanged.
}
