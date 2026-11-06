// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// The seam which identifies a DNN services framework authentication handler, so that the handler
    /// chain (<see cref="IDnnAuthMessageHandlerCollection"/>) can be composed without depending on the
    /// handler implementations.
    /// </summary>
    /// <remarks>
    /// Implemented by <c>DotNetNuke.Web.Api.Auth.AuthMessageHandlerBase</c> (the base of all configured
    /// handlers) and by <c>DotNetNuke.Web.Api.Internal.Auth.WebFormsAuthMessageHandler</c> (the cookie-auth
    /// bridge which is always appended last). <see cref="DnnAuthMessageHandlerMiddleware"/> drives the
    /// chain through the two members below, which replace the inbound/outbound phases of the Web API 2
    /// <c>DelegatingHandler</c> the handlers used to be.
    /// </remarks>
    public interface IDnnAuthMessageHandler
    {
        /// <summary>Gets the authentication scheme this handler authenticates.</summary>
        string AuthScheme { get; }

        /// <summary>Gives the handler a chance to authenticate the inbound request.</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>
        /// <see langword="true"/> when the handler has produced the response itself and the rest of the
        /// pipeline must be skipped; <see langword="false"/> to continue.
        /// </returns>
        bool OnAuthenticateRequest(HttpContext context) => false;

        /// <summary>Gives the handler a chance to amend the response just before its headers are sent.</summary>
        /// <param name="context">The current HTTP context.</param>
        void OnSendResponse(HttpContext context)
        {
        }
    }
}
