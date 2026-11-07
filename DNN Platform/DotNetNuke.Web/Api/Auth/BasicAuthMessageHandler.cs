// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Auth
{
    using System;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Security.Principal;
    using System.Text;

    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Security.Membership;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Net.Http.Headers;

    /// <summary>Basic authentication message handler.</summary>
    public class BasicAuthMessageHandler : AuthMessageHandlerBase
    {
        private static readonly char[] CredentialSeparator = [':',];
        private readonly Encoding encoding = Encoding.GetEncoding("iso-8859-1");

        /// <summary>Initializes a new instance of the <see cref="BasicAuthMessageHandler"/> class.</summary>
        /// <param name="includeByDefault">Should this handler be included by default on all routes.</param>
        /// <param name="forceSsl">Should this handler enforce SSL usage.</param>
        public BasicAuthMessageHandler(bool includeByDefault, bool forceSsl)
            : base(includeByDefault, forceSsl)
        {
        }

        /// <summary>Gets the name of the authentication scheme.</summary>
        public override string AuthScheme => "Basic";

        /// <inheritdoc />
        public override bool OnAuthenticateRequest(HttpContext context)
        {
            if (this.NeedsAuthentication(context))
            {
                var portalSettings = PortalController.Instance.GetCurrentSettings();
                if (portalSettings != null)
                {
                    this.TryToAuthenticate(context, portalSettings.PortalId);
                }
            }

            return base.OnAuthenticateRequest(context);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Adds the <c>WWW-Authenticate: Basic realm="DNNAPI"</c> challenge to a 401, exactly as the
        /// legacy <c>OnOutboundResponse</c> did (the realm string and the header shape are unchanged).
        /// </remarks>
        public override void OnSendResponse(HttpContext context)
        {
            if (context?.Response?.StatusCode == (int)HttpStatusCode.Unauthorized && SupportsBasicAuth(context.Request))
            {
                context.Response.Headers.Append(
                    HeaderNames.WWWAuthenticate,
                    new AuthenticationHeaderValue(this.AuthScheme, "realm=\"DNNAPI\"").ToString());
            }

            base.OnSendResponse(context);
        }

        private static bool SupportsBasicAuth(HttpRequest request)
        {
            return !IsXmlHttpRequest(request);
        }

        private void TryToAuthenticate(HttpContext context, int portalId)
        {
            UserCredentials credentials = this.GetCredentials(context.Request);

            if (credentials == null)
            {
                return;
            }

            var status = UserLoginStatus.LOGIN_FAILURE;
            string ipAddress = context.Request.GetIPAddress();

            UserInfo user = UserController.ValidateUser(
                portalId,
                credentials.UserName,
                credentials.Password,
                "DNN",
                string.Empty,
                "a portal",
                ipAddress ?? string.Empty,
                ref status);

            if (user != null)
            {
                SetCurrentPrincipal(new GenericPrincipal(new GenericIdentity(credentials.UserName, this.AuthScheme), null), context);
            }
        }

        private UserCredentials GetCredentials(HttpRequest request)
        {
            var authorizationHeader = request?.Headers[HeaderNames.Authorization].ToString();
            if (string.IsNullOrEmpty(authorizationHeader)
                || !AuthenticationHeaderValue.TryParse(authorizationHeader, out var authorization)
                || authorization == null)
            {
                return null;
            }

            if (!authorization.Scheme.Equals(this.AuthScheme, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.IsNullOrEmpty(authorization.Parameter))
            {
                return null;
            }

            string decoded;
            try
            {
                decoded = this.encoding.GetString(Convert.FromBase64String(authorization.Parameter));
            }
            catch (FormatException)
            {
                // A malformed Base64 parameter is not a credential; the legacy handler let the
                // FormatException escape into the handler chain, which produced no principal either.
                return null;
            }

            string[] parts = decoded.Split(CredentialSeparator, 2);
            if (parts.Length < 2)
            {
                return null;
            }

            return new UserCredentials(parts[0], parts[1]);
        }

        /// <summary>Represents the user credentials.</summary>
        internal class UserCredentials
        {
            /// <summary>Initializes a new instance of the <see cref="UserCredentials"/> class.</summary>
            /// <param name="userName">The user username.</param>
            /// <param name="password">The user password.</param>
            public UserCredentials(string userName, string password)
            {
                this.UserName = userName;
                this.Password = password;
            }

            /// <summary>Gets or sets the password for the user.</summary>
            public string Password { get; set; }

            /// <summary>Gets or sets the username for the user.</summary>
            public string UserName { get; set; }
        }
    }
}
