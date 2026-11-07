// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Common
{
    using System.Threading.Tasks;

    /// <summary>Provides access to the DNN anti-forgery token configuration and validation.</summary>
    public interface IAntiForgery
    {
        /// <summary>Gets the name of the cookie that carries the anti-forgery cookie token.</summary>
        string CookieName { get; }

        /// <summary>Validates the supplied anti-forgery token pair.</summary>
        /// <param name="cookieToken">The cookie token.</param>
        /// <param name="headerToken">The request (header/form) token.</param>
        /// <returns>A task that completes when the token pair has been validated, and faults when validation fails.</returns>
        Task ValidateAsync(string cookieToken, string headerToken);
    }
}
