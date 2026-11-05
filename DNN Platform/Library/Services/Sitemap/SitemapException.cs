// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.Sitemap;

using System;

/// <summary>An exception thrown when there's an issue generating a site map.</summary>
/// <remarks>
/// The binary-serialization constructor was removed: formatter-based serialization of exceptions
/// is obsolete (SYSLIB0051) and non-functional on modern .NET. [Serializable] is kept so the type
/// still reports itself as serializable to callers that check the attribute.
/// </remarks>
[Serializable]
public class SitemapException : Exception
{
    /// <inheritdoc />
    public SitemapException()
    {
    }

    /// <inheritdoc />
    public SitemapException(string message)
        : base(message)
    {
    }

    /// <inheritdoc />
    public SitemapException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
