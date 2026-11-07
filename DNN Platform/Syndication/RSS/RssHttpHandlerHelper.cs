// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Syndication
{
    using System;
    using System.Globalization;

    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Http;

    /// <summary>Helper class (for <see cref="RssHttpHandlerBase{TRssChannelType,TRssItemType,TRssImageType}"/>) to pack and unpack user name and channel to from/to query string.</summary>
    /// <remarks>
    /// The user name and channel name used to be packed into a (deliberately already expired)
    /// <c>FormsAuthenticationTicket</c>. Forms authentication does not exist in ASP.NET Core, so the
    /// payload is protected with the Data Protection API instead, which provides the same
    /// "opaque, tamper-proof, server-only readable" property.
    /// </remarks>
    public class RssHttpHandlerHelper
    {
        /// <summary>The Data Protection purpose string used to protect the channel link token.</summary>
        internal const string DataProtectionPurpose = "DotNetNuke.Services.Syndication.ChannelLink";

        private readonly IDataProtector protector;
        private readonly IHttpContextAccessor httpContextAccessor;

        /// <summary>Initializes a new instance of the <see cref="RssHttpHandlerHelper"/> class.</summary>
        /// <param name="dataProtectionProvider">The data protection provider used to protect the channel link token.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor, used to resolve application-relative handler paths.</param>
        [CLSCompliant(false)]
        public RssHttpHandlerHelper(IDataProtectionProvider dataProtectionProvider, IHttpContextAccessor httpContextAccessor)
        {
            ArgumentNullException.ThrowIfNull(dataProtectionProvider);
            ArgumentNullException.ThrowIfNull(httpContextAccessor);

            this.protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <summary>Generate the link (to the feed endpoint) containing the channel name and (encoded) userName.</summary>
        /// <param name="handlerPath">The handler path.</param>
        /// <param name="channelName">The channel name.</param>
        /// <param name="userName">The user name.</param>
        /// <returns>A URL to the channel.</returns>
        public string GenerateChannelLink(string handlerPath, string channelName, string userName)
        {
            string link = this.ToAbsolute(handlerPath);

            if (string.IsNullOrEmpty(userName))
            {
                if (!string.IsNullOrEmpty(channelName))
                {
                    link += "?c=" + Uri.EscapeDataString(channelName);
                }
            }
            else
            {
                channelName ??= string.Empty;

                userName = "." + userName; // not to confuse the protected string with a real auth token for a real user

                link += "?t=" + Uri.EscapeDataString(this.protector.Protect(PackTicket(userName, channelName)));
            }

            return link;
        }

        /// <summary>Parses the channel's query-string to extract the channel name and user name.</summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="channelName">The channel name.</param>
        /// <param name="userName">The user name or <see cref="string.Empty"/>.</param>
        internal void ParseChannelQueryString(HttpRequest request, out string channelName, out string userName)
        {
            ArgumentNullException.ThrowIfNull(request);

            string ticket = request.Query["t"];

            if (!string.IsNullOrEmpty(ticket))
            {
                // protected user name and channel name
                try
                {
                    UnpackTicket(this.protector.Unprotect(ticket), out var protectedUserName, out channelName);
                    userName = protectedUserName.Substring(1); // remove extra prepended '.'
                    return;
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    // an invalid or no-longer-decryptable token is treated as if no token was supplied
                }
                catch (FormatException)
                {
                    // a token which does not carry the expected payload is treated as if no token was supplied
                }
            }

            userName = string.Empty;

            // optional unprotected channel name
            channelName = request.Query["c"];
        }

        /// <summary>Packs the user name and channel name into a single string which can be unpacked unambiguously.</summary>
        /// <param name="userName">The user name (already prefixed with '.').</param>
        /// <param name="channelName">The channel name.</param>
        /// <returns>The packed payload.</returns>
        private static string PackTicket(string userName, string channelName)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}{2}",
                userName.Length,
                userName,
                channelName);
        }

        /// <summary>Unpacks a payload created by <see cref="PackTicket"/>.</summary>
        /// <param name="payload">The packed payload.</param>
        /// <param name="userName">The user name (still prefixed with '.').</param>
        /// <param name="channelName">The channel name.</param>
        private static void UnpackTicket(string payload, out string userName, out string channelName)
        {
            int separator = payload.IndexOf(':', StringComparison.Ordinal);
            if (separator < 0
                || !int.TryParse(payload.AsSpan(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out var userNameLength)
                || userNameLength < 1
                || separator + 1 + userNameLength > payload.Length)
            {
                throw new FormatException("The channel link token payload is not in the expected format.");
            }

            userName = payload.Substring(separator + 1, userNameLength);
            channelName = payload.Substring(separator + 1 + userNameLength);
        }

        /// <summary>Resolves an application-relative path (<c>~/…</c>) to an absolute path, honoring the application's path base.</summary>
        /// <param name="virtualPath">The (possibly application-relative) path.</param>
        /// <returns>The absolute path.</returns>
        private string ToAbsolute(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath))
            {
                return virtualPath;
            }

            var pathBase = this.httpContextAccessor.HttpContext?.Request.PathBase ?? default;
            var basePath = pathBase.HasValue ? pathBase.Value.TrimEnd('/') : string.Empty;

            if (virtualPath.StartsWith("~/", StringComparison.Ordinal))
            {
                return string.Concat(basePath, "/", virtualPath.AsSpan(2));
            }

            if (virtualPath.StartsWith('/'))
            {
                return virtualPath;
            }

            return basePath + "/" + virtualPath;
        }
    }
}
