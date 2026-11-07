// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Auth
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Text;

    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Security.Membership;
    using DotNetNuke.Web.Api.Internal.Auth;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Net.Http.Headers;

    /// <summary>Digest authentication message handler.</summary>
    /// <remarks>
    /// The wire behaviour is identical to net48: the same nonce construction (a Base64 of the invariant
    /// "G" formatted expiry one minute out, with the padding trimmed), the same
    /// <c>realm</c>/<c>nonce</c>/<c>opaque</c>/<c>stale</c>/<c>algorithm</c>/<c>qop</c> parameter string
    /// including its double space before <c>opaque</c>, and the same stale handling (a bare 401 carrying
    /// <c>stale=True</c> is returned immediately when the nonce has expired).
    /// </remarks>
    [DnnDeprecated(10, 2, 2, "Use JWT or API token authentication")]
    public partial class DigestAuthMessageHandler : AuthMessageHandlerBase
    {
        /// <summary>Initializes a new instance of the <see cref="DigestAuthMessageHandler"/> class.</summary>
        /// <param name="includeByDefault">Should this handler be included by default on all routes.</param>
        /// <param name="forceSsl">Should this handler enforce SSL usage.</param>
        public DigestAuthMessageHandler(bool includeByDefault, bool forceSsl)
            : base(includeByDefault, forceSsl)
        {
        }

        /// <inheritdoc />
        public override string AuthScheme => DigestAuthentication.AuthenticationScheme;

        /// <inheritdoc />
        public override bool OnAuthenticateRequest(HttpContext context)
        {
            if (this.NeedsAuthentication(context))
            {
                var portalSettings = PortalController.Instance.GetCurrentSettings();
                if (portalSettings != null)
                {
                    var isStale = TryToAuthenticate(context, portalSettings.PortalId);

                    if (isStale)
                    {
                        // The legacy handler returned request.CreateResponse(Unauthorized) with the stale
                        // challenge, terminating inbound processing.
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        this.AddStaleWwwAuthenticateHeader(context.Response);
                        return true;
                    }
                }
            }

            return base.OnAuthenticateRequest(context);
        }

        /// <inheritdoc />
        public override void OnSendResponse(HttpContext context)
        {
            if (context?.Response?.StatusCode == (int)HttpStatusCode.Unauthorized && SupportsDigestAuth(context.Request))
            {
                this.AddWwwAuthenticateHeader(context.Response);
            }

            base.OnSendResponse(context);
        }

        private static string CreateNewNonce()
        {
            DateTime nonceTime = DateTime.Now + TimeSpan.FromMinutes(1);
            string expireStr = nonceTime.ToString("G", CultureInfo.InvariantCulture);

            byte[] expireBytes = Encoding.Default.GetBytes(expireStr);
            string nonce = Convert.ToBase64String(expireBytes);

            nonce = nonce.TrimEnd('=');
            return nonce;
        }

        private static bool TryToAuthenticate(HttpContext context, int portalId)
        {
            var request = context.Request;
            var authHeader = request.Headers[HeaderNames.Authorization].ToString();
            if (string.IsNullOrEmpty(authHeader))
            {
                return false;
            }

            var digestAuthentication = new DigestAuthentication(
                new DigestAuthenticationRequest(authHeader, request.Method),
                portalId,
                request.GetIPAddress());

            if (digestAuthentication.IsValid)
            {
                SetCurrentPrincipal(digestAuthentication.User, context);
            }
            else if (digestAuthentication.IsNonceStale)
            {
                return true;
            }

            return false;
        }

        private static bool SupportsDigestAuth(HttpRequest request)
        {
            return !IsXmlHttpRequest(request) && MembershipProviderConfig.PasswordRetrievalEnabled;
        }

        private void AddStaleWwwAuthenticateHeader(HttpResponse response)
        {
            this.AddWwwAuthenticateHeader(response, true);
        }

        private void AddWwwAuthenticateHeader(HttpResponse response, bool isStale = false)
        {
            var value = $"realm=\"DNNAPI\", nonce=\"{CreateNewNonce()}\",  opaque=\"0000000000000000\", stale={isStale}, algorithm=MD5, qop=\"auth\"";
            response.Headers.Append(
                HeaderNames.WWWAuthenticate,
                new AuthenticationHeaderValue(this.AuthScheme, value).ToString());
        }
    }
}
