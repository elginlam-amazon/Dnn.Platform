// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.AuthServices.Jwt.Auth
{
    using System;
    using System.Security.Principal;

    using Dnn.AuthServices.Jwt.Components.Common.Controllers;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Web.Api.Auth;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// This class implements Json Web Token (JWT) authentication scheme.
    /// For detailed description of JWT refer to:
    /// <para>- JTW standard https://tools.ietf.org/html/rfc7519. </para>
    /// <para>- Introduction to JSON Web Tokens http://jwt.io/introduction/. </para>
    /// </summary>
    public class JwtAuthMessageHandler : AuthMessageHandlerBase
    {
        private readonly ILogger<JwtAuthMessageHandler> logger;

        private readonly IJwtController jwtController = JwtController.Instance;

        /// <summary>Initializes a new instance of the <see cref="JwtAuthMessageHandler"/> class.</summary>
        /// <param name="includeByDefault">A value indicating whether this handler should be included by default on all API endpoints.</param>
        /// <param name="forceSsl">A value indicating whether this handler should enforce SSL usage.</param>
        public JwtAuthMessageHandler(bool includeByDefault, bool forceSsl)
            : base(includeByDefault, forceSsl)
        {
            // Once an instance is enabled and gets registered in
            // ServicesRoutingManager.RegisterAuthenticationHandlers()
            // this scheme gets marked as enabled.
            IsEnabled = true;
            this.logger = DnnLoggingController.GetLogger<JwtAuthMessageHandler>();
        }

        /// <inheritdoc />
        public override string AuthScheme => this.jwtController.SchemeType;

        /// <inheritdoc />
        public override bool BypassAntiForgeryToken => true;

        /// <summary>Gets or sets a value indicating whether this handler is enabled.</summary>
        internal static bool IsEnabled { get; set; }

        /// <inheritdoc />
        /// <remarks>
        /// Was <c>OnInboundRequest(HttpRequestMessage, CancellationToken)</c>. The inbound phase of the
        /// former <c>DelegatingHandler</c> is now <c>OnAuthenticateRequest(HttpContext)</c>, driven by
        /// <c>DnnAuthMessageHandlerMiddleware</c>. The gate and the outcome are unchanged: authenticate
        /// only when <see cref="AuthMessageHandlerBase.NeedsAuthentication"/> allows it, then defer to the
        /// base, which does not short-circuit the pipeline (the legacy override likewise returned the
        /// base's <see langword="null"/> response).
        /// </remarks>
        public override bool OnAuthenticateRequest(HttpContext context)
        {
            if (this.NeedsAuthentication(context))
            {
                this.TryToAuthenticate(context);
            }

            return base.OnAuthenticateRequest(context);
        }

        private void TryToAuthenticate(HttpContext context)
        {
            try
            {
                var username = this.jwtController.ValidateToken(context.Request);
                if (!string.IsNullOrEmpty(username))
                {
                    this.logger.LogTrace("Authenticated user '{userName}'", username);

                    SetCurrentPrincipal(new GenericPrincipal(new GenericIdentity(username, this.AuthScheme), null), context);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Unexpected error in authenticating the user.");
            }
        }
    }
}
