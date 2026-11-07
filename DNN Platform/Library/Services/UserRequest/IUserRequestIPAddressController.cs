// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

#nullable enable
namespace DotNetNuke.Services.UserRequest
{
    using Microsoft.AspNetCore.Http;

    /// <summary>Resolves the IP address of the client making the current request.</summary>
    public interface IUserRequestIPAddressController
    {
        /// <summary> To retrieve IPv4 of user making request to application.</summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>IP address or <see cref="string.Empty"/>.</returns>
        string GetUserRequestIPAddress(HttpRequest request);

        /// <summary> To retrieve IPv4/IPv6 of user making request to application.</summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="ipFamily">The type of IP address to get (<see cref="string.Empty"/> will be returned if the address does not match).</param>
        /// <returns>IP address or <see cref="string.Empty"/>.</returns>
        string GetUserRequestIPAddress(HttpRequest request, IPAddressFamily ipFamily);

        /// <summary>
        /// To retrieve IPv4 of the user making the request currently in flight, resolved through
        /// <see cref="DotNetNuke.Common.HttpContextSource"/>.
        /// </summary>
        /// <returns>IP address or <see cref="string.Empty"/> when there is no request in flight.</returns>
        string GetUserRequestIPAddress();

        /// <summary>
        /// To retrieve the IP address of the user making the request currently in flight, resolved
        /// through <see cref="DotNetNuke.Common.HttpContextSource"/>.
        /// </summary>
        /// <param name="ipFamily">The type of IP address to get.</param>
        /// <returns>IP address or <see cref="string.Empty"/> when there is no request in flight.</returns>
        string GetUserRequestIPAddress(IPAddressFamily ipFamily);
    }
}
