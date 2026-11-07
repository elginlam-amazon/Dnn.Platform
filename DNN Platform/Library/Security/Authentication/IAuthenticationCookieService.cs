// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Security.Authentication
{
    using System;
    using System.Collections.Generic;
    using System.Security.Claims;
    using System.Threading.Tasks;

    using DotNetNuke.Common.Options;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// The platform's cookie-authentication seam. Replaces
    /// <c>System.Web.Security.FormsAuthentication</c>, which does not exist on modern .NET.
    /// </summary>
    public interface IAuthenticationCookieService
    {
        /// <summary>Gets the cookie settings (the replacement for <c>&lt;forms&gt;</c>).</summary>
        AuthenticationCookieOptions Options { get; }

        /// <summary>Gets the authentication cookie name (legacy <c>FormsAuthentication.FormsCookieName</c>).</summary>
        string CookieName { get; }

        /// <summary>Gets the configured cookie domain (legacy <c>FormsAuthentication.CookieDomain</c>).</summary>
        string CookieDomain { get; }

        /// <summary>Gets the cookie path (legacy <c>FormsAuthentication.FormsCookiePath</c>).</summary>
        string CookiePath { get; }

        /// <summary>Gets a value indicating whether the cookie requires HTTPS (legacy <c>FormsAuthentication.RequireSSL</c>).</summary>
        bool RequireSsl { get; }

        /// <summary>Gets the authentication timeout (legacy <c>FormsAuthentication.Timeout</c>).</summary>
        TimeSpan Timeout { get; }

        /// <summary>Protects a ticket into the opaque value stored in the authentication cookie (legacy <c>FormsAuthentication.Encrypt</c>).</summary>
        /// <param name="ticket">The ticket to protect.</param>
        /// <returns>The protected value.</returns>
        string Protect(DnnAuthenticationTicket ticket);

        /// <summary>Reads a protected cookie value back into a ticket (legacy <c>FormsAuthentication.Decrypt</c>).</summary>
        /// <param name="protectedValue">The protected value.</param>
        /// <returns>The ticket, or <see langword="null"/> when the value is not a valid, unexpired ticket.</returns>
        DnnAuthenticationTicket Unprotect(string protectedValue);

        /// <summary>
        /// Signs the user in by issuing the authentication cookie for the given claims
        /// (legacy <c>FormsAuthentication.SetAuthCookie</c>).
        /// </summary>
        /// <param name="context">The current request.</param>
        /// <param name="userName">The user name to authenticate as.</param>
        /// <param name="isPersistent">Whether the cookie survives browser restarts.</param>
        /// <param name="claims">Additional claims (user id, roles, portal) to carry in the cookie.</param>
        /// <param name="cookieDomain">The cookie domain to use, or <see langword="null"/> for the configured one.</param>
        /// <returns>A task that completes once the cookie has been written to the response.</returns>
        Task SignInAsync(HttpContext context, string userName, bool isPersistent, IEnumerable<Claim> claims, string cookieDomain);

        /// <summary>Signs the current user out (legacy <c>FormsAuthentication.SignOut</c>).</summary>
        /// <param name="context">The current request.</param>
        /// <returns>A task that completes once the cookie has been expired.</returns>
        Task SignOutAsync(HttpContext context);
    }
}
