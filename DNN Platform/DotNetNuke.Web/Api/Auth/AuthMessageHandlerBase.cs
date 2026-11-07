// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Auth
{
    using System;
    using System.Security.Principal;
    using System.Threading;

    using DotNetNuke.Instrumentation;
    using DotNetNuke.Web.Api.Internal;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    /// <summary>Base class for authentication providers message handlers.</summary>
    /// <remarks>
    /// <para>
    /// This was a <c>System.Net.Http.DelegatingHandler</c> installed into
    /// <c>HttpConfiguration.MessageHandlers</c>. ASP.NET Core has no message handler chain, so the handlers
    /// are now driven by <see cref="DnnAuthMessageHandlerMiddleware"/>, which walks the ordered
    /// <see cref="IDnnAuthMessageHandlerCollection"/>. The <c>DelegatingHandler.SendAsync</c> split into
    /// an inbound and an outbound phase is preserved one for one:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>OnInboundRequest</c> → <see cref="OnAuthenticateRequest"/>. It returned an
    /// <c>HttpResponseMessage</c> to terminate inbound processing; it now returns <see langword="true"/>
    /// after writing the response onto the context, which short-circuits the pipeline the same way.</description></item>
    /// <item><description><c>OnOutboundResponse</c> → <see cref="OnSendResponse"/>, invoked just before
    /// the response headers are sent (and therefore still able to add the <c>WWW-Authenticate</c>
    /// challenge to a 401 produced anywhere downstream, including by the authorization filters).</description></item>
    /// </list>
    /// <para>
    /// <see cref="BypassAntiForgeryToken"/>, <see cref="DefaultInclude"/>, <see cref="ForceSsl"/>,
    /// <see cref="AuthScheme"/>, <see cref="IsXmlHttpRequest"/> and <see cref="NeedsAuthentication"/> keep
    /// their former semantics exactly, including the <see cref="ForceSsl"/>/scheme validation and its log
    /// message. The constructor signature is unchanged, so the <c>&lt;messageHandlers&gt;</c> entries keep
    /// activating third-party handlers through <c>(includeByDefault, forceSsl)</c>.
    /// </para>
    /// </remarks>
    public abstract partial class AuthMessageHandlerBase : IDnnAuthMessageHandler
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<AuthMessageHandlerBase>();

        /// <summary>Initializes a new instance of the <see cref="AuthMessageHandlerBase"/> class.</summary>
        /// <param name="includeByDefault">A value indicating whether this handler should be included by default in all API endpoints.</param>
        /// <param name="forceSsl">A value indicating whether this handler should enforce SSL usage.</param>
        protected AuthMessageHandlerBase(bool includeByDefault, bool forceSsl)
        {
            this.DefaultInclude = includeByDefault;
            this.ForceSsl = forceSsl;
        }

        /// <summary>Gets the name of the authentication scheme.</summary>
        public abstract string AuthScheme { get; }

        /// <summary>Gets a value indicating whether this handler should bypass the anti-forgery token check.</summary>
        public virtual bool BypassAntiForgeryToken => false;

        /// <summary>Gets a value indicating whether this handler should be included by default on all API endpoints.</summary>
        public bool DefaultInclude { get; }

        /// <summary>Gets a value indicating whether this handler should enforce SSL usage on it's endpoints.</summary>
        public bool ForceSsl { get; }

        /// <summary>A chance to process inbound requests.</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>
        /// <see langword="false"/> normally. When <see langword="true"/> is returned all inbound processing
        /// is terminated and the response the handler wrote onto <paramref name="context"/> is returned.
        /// </returns>
        public virtual bool OnAuthenticateRequest(HttpContext context)
        {
            return false;
        }

        /// <summary>A chance to process outbound responses, just before the response headers are sent.</summary>
        /// <param name="context">The current HTTP context.</param>
        public virtual void OnSendResponse(HttpContext context)
        {
        }

        /// <summary>Checks if the current request is an XmlHttpRequest.</summary>
        /// <param name="request">The HTTP Request.</param>
        /// <returns>A value indicating whether the request is an XmlHttpRequest.</returns>
        protected static bool IsXmlHttpRequest(HttpRequest request)
        {
            var value = request?.Headers["X-REQUESTED-WITH"].ToString();

            return !string.IsNullOrEmpty(value) &&
                   value.Equals("XmlHttpRequest", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Sets the current principal for the request.</summary>
        /// <param name="principal">The principal to set.</param>
        /// <param name="context">The current HTTP context.</param>
        /// <remarks>
        /// Both <see cref="Thread.CurrentPrincipal"/> and <see cref="HttpContext.User"/> are set, exactly
        /// as the legacy handler did, because DNN code (and third-party modules) read either one.
        /// </remarks>
        protected static void SetCurrentPrincipal(IPrincipal principal, HttpContext context)
        {
            Thread.CurrentPrincipal = principal;
            if (context != null && principal is System.Security.Claims.ClaimsPrincipal claimsPrincipal)
            {
                context.User = claimsPrincipal;
            }
            else if (context != null && principal != null)
            {
                context.User = new System.Security.Claims.ClaimsPrincipal(principal.Identity);
            }
        }

        /// <summary>Checks if the current request requires authentication.</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A value indication whether the current request needs authentication.</returns>
        protected bool NeedsAuthentication(HttpContext context)
        {
            if (this.MustEnforceSslInRequest(context?.Request))
            {
                return !IsAuthenticated(context);
            }

            Logger.AuthMessageHandlerBaseValidatingRequestVsSslModeFailed(this.AuthScheme, this.ForceSsl);

            // will let callers return without authenticating the user
            return false;
        }

        private static bool IsAuthenticated(HttpContext context)
        {
            return context?.User?.Identity?.IsAuthenticated == true
                   || Thread.CurrentPrincipal?.Identity?.IsAuthenticated == true;
        }

        /// <summary>Validated the <see cref="ForceSsl"/> setting of the instance against the HTTP(S) request.</summary>
        /// <returns>True if <see cref="ForceSsl"/> matcher the request scheme; false otherwise.</returns>
        private bool MustEnforceSslInRequest(HttpRequest request)
        {
            return !this.ForceSsl
                   || string.Equals(request?.Scheme, "HTTPS", StringComparison.OrdinalIgnoreCase);
        }
    }
}
