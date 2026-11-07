// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.AuthServices.Jwt.Components.Common.Controllers
{
    using Dnn.AuthServices.Jwt.Components.Entity;

    using Microsoft.AspNetCore.Http;

    /// <summary>Controls JWT features.</summary>
    public interface IJwtController
    {
        /// <summary>Gets the name of the authentication Scheme Type.</summary>
        string SchemeType { get; }

        /// <summary>Validates the JWT token for the request.</summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>Returns the UserName if the token is valid or null if not.</returns>
        string ValidateToken(HttpRequest request);

        /// <summary>Logs the user out.</summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>A value indicating whether the logout attempt succeeded.</returns>
        bool LogoutUser(HttpRequest request);

        /// <summary>Logs the user in.</summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="loginData">The login information, <see cref="LoginData"/>.</param>
        /// <returns><see cref="LoginResultData"/>.</returns>
        LoginResultData LoginUser(HttpRequest request, LoginData loginData);

        /// <summary>Attempts to renew a JWT token.</summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="renewalToken">The JWT renewal token.</param>
        /// <returns><see cref="LoginResultData"/>.</returns>
        LoginResultData RenewToken(HttpRequest request, string renewalToken);
    }
}
