// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal;

using System.Threading.Tasks;

/// <summary>A contract specifying the ability to validate an anti-forgery cookie.</summary>
public interface IAntiForgery
{
    /// <summary>Gets the cookie name.</summary>
    string CookieName { get; }

    /// <summary>Validates that the cookie token matches the header token.</summary>
    /// <param name="cookieToken">The CSRF token from the cookie.</param>
    /// <param name="headerToken">The CSRF token from the header.</param>
    /// <returns>A task that completes when the token pair has been validated, and faults when validation fails.</returns>
    /// <remarks>
    /// Was the synchronous <c>Validate(string, string)</c>, which wrapped
    /// <c>System.Web.Helpers.AntiForgery.Validate</c>. The ASP.NET Core anti-forgery API
    /// (<c>Microsoft.AspNetCore.Antiforgery.IAntiforgery.ValidateRequestAsync</c>) is asynchronous, hence
    /// the rename; the validation outcome (and the exception thrown on failure) is unchanged. This matches
    /// the <c>DotNetNuke.Web.Mvc</c> carry-forward of the same interface.
    /// </remarks>
    Task ValidateAsync(string cookieToken, string headerToken);
}
