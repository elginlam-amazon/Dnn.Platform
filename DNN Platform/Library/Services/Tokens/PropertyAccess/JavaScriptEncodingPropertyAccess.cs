// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.Tokens;

using System.Globalization;
using System.Text.Encodings.Web;

using DotNetNuke.Entities.Users;

/// <summary>An <see cref="IPropertyAccess"/> implementation that wraps an existing <see cref="IPropertyAccess"/>, JavaScript string encoding its result.</summary>
/// <param name="implementation">The <see cref="IPropertyAccess"/> implementation that is being wrapped.</param>
public class JavaScriptEncodingPropertyAccess(IPropertyAccess implementation) : IPropertyAccess
{
    /// <inheritdoc />
    public CacheLevel Cacheability
    {
        get => implementation.Cacheability;
    }

    /// <inheritdoc />
    public string GetProperty(
        string propertyName,
        string format,
        CultureInfo formatProvider,
        UserInfo accessingUser,
        Scope accessLevel,
        ref bool propertyNotFound)
    {
        var result = implementation.GetProperty(
            propertyName,
            format,
            formatProvider,
            accessingUser,
            accessLevel,
            ref propertyNotFound);

        // System.Web.HttpUtility.JavaScriptStringEncode is replaced by
        // System.Text.Encodings.Web.JavaScriptEncoder on modern .NET. Neither adds surrounding
        // quotes; the null-to-empty behaviour of the old API is preserved explicitly.
        return result is null ? string.Empty : JavaScriptEncoder.Default.Encode(result);
    }
}
