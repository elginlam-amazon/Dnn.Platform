// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

#nullable enable
namespace DotNetNuke.Services.UserRequest
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Framework;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Utilities to handle IP address of user making request to application.</summary>
    public class UserRequestIPAddressController : ServiceLocator<IUserRequestIPAddressController, UserRequestIPAddressController>, IUserRequestIPAddressController
    {
        private readonly IHostSettingsService hostSettingsService;

        /// <summary>Initializes a new instance of the <see cref="UserRequestIPAddressController"/> class.</summary>
        public UserRequestIPAddressController()
            : this(Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettingsService>())
        {
        }

        /// <summary>Initializes a new instance of the <see cref="UserRequestIPAddressController"/> class.</summary>
        /// <param name="hostSettingsService">Provides access to host settings.</param>
        public UserRequestIPAddressController(IHostSettingsService hostSettingsService)
        {
            this.hostSettingsService = hostSettingsService;
        }

        /// <inheritdoc />
        public string GetUserRequestIPAddress(HttpRequest request)
        {
            return this.GetUserRequestIPAddress(request, IPAddressFamily.IPv4);
        }

        /// <inheritdoc />
        public string GetUserRequestIPAddress(HttpRequest request, IPAddressFamily ipFamily)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // A host setting names the (proxy) header that carries the real client address,
            // e.g. X-Forwarded-For. When set it wins over the transport-level address, exactly
            // as it did on ASP.NET.
            var userRequestIpHeader = this.hostSettingsService.GetString("UserRequestIPHeader");
            var userIpAddress = string.Empty;

            if (!string.IsNullOrEmpty(userRequestIpHeader) && request.Headers.ContainsKey(userRequestIpHeader))
            {
                userIpAddress = request.Headers[userRequestIpHeader].ToString();
                userIpAddress = userIpAddress.Split(',')[0].Trim();
                if (ipFamily == IPAddressFamily.IPv4 && userIpAddress.Contains(':'))
                {
                    // strip the "host:port" port part
                    userIpAddress = userIpAddress.Split(':')[0];
                }
                else if (ipFamily == IPAddressFamily.IPv6
                    && userIpAddress.StartsWith("[", StringComparison.Ordinal) && userIpAddress.Contains(']'))
                {
                    // "[::1]:port" -> "::1"
                    userIpAddress = userIpAddress.Split(']')[0].Substring(1);
                }
            }

            if (string.IsNullOrEmpty(userIpAddress))
            {
                // the Core equivalent of the REMOTE_ADDR server variable / Request.UserHostAddress
                var remoteIpAddress = request.HttpContext?.Connection?.RemoteIpAddress;
                if (remoteIpAddress != null)
                {
                    if (remoteIpAddress.IsIPv4MappedToIPv6)
                    {
                        remoteIpAddress = remoteIpAddress.MapToIPv4();
                    }

                    userIpAddress = remoteIpAddress.ToString();
                }
            }

            if (string.IsNullOrEmpty(userIpAddress) || userIpAddress.Trim() == "::1")
            {
                userIpAddress = string.Empty;
            }

            if (!string.IsNullOrEmpty(userIpAddress) && !ValidateIp(userIpAddress, ipFamily))
            {
                userIpAddress = string.Empty;
            }

            return userIpAddress;
        }

        /// <inheritdoc />
        public string GetUserRequestIPAddress()
        {
            return this.GetUserRequestIPAddress(IPAddressFamily.IPv4);
        }

        /// <inheritdoc />
        public string GetUserRequestIPAddress(IPAddressFamily ipFamily)
        {
            var request = HttpContextSource.Current?.Request;
            return request is null ? string.Empty : this.GetUserRequestIPAddress(request, ipFamily);
        }

        /// <inheritdoc />
        protected override Func<IUserRequestIPAddressController> GetFactory()
        {
            return () => new UserRequestIPAddressController();
        }

        private static bool ValidateIp(string ipString, IPAddressFamily ipFamily)
        {
            if (IPAddress.TryParse(ipString, out var address))
            {
                if (ipFamily == IPAddressFamily.IPv4 &&
                    address.AddressFamily == AddressFamily.InterNetwork &&
                    ipString.Split('.').Length == 4)
                {
                    return true;
                }

                if (ipFamily == IPAddressFamily.IPv6 &&
                    address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
