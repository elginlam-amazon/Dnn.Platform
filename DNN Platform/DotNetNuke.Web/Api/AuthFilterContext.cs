// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System.Net;
    using System.Security.Principal;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>Context information passed to an authorization filter.</summary>
    /// <remarks>
    /// The legacy context wrapped a Web API 2 <c>HttpActionContext</c> and produced an
    /// <c>HttpResponseMessage</c>. In ASP.NET Core an authorization filter receives an
    /// <see cref="AuthorizationFilterContext"/> and short-circuits by assigning its
    /// <see cref="AuthorizationFilterContext.Result"/>, which is what
    /// <see cref="HandleUnauthorizedRequest"/> does now, with the same 401 status code and the same
    /// <c>{"Message":"…"}</c> body.
    /// </remarks>
    public class AuthFilterContext
    {
        /// <summary>
        /// The <see cref="HttpContext.Items"/> key which tells the host's authentication/status-code
        /// middleware to return the raw 401 instead of redirecting to the login page.
        /// </summary>
        public const string SuppressAuthenticationRedirectKey = "DotNetNuke.SuppressAuthenticationRedirect";

        /// <summary>Initializes a new instance of the <see cref="AuthFilterContext"/> class.</summary>
        /// <param name="actionContext">The authorization filter context.</param>
        /// <param name="authFailureMessage">The message to include in the response if there's an auth failure.</param>
        public AuthFilterContext(AuthorizationFilterContext actionContext, string authFailureMessage)
        {
            this.ActionContext = actionContext;
            this.AuthFailureMessage = authFailureMessage;
        }

        /// <summary>Gets the action context.</summary>
        public AuthorizationFilterContext ActionContext { get; private set; }

        /// <summary>Gets or sets the failure message.</summary>
        public string AuthFailureMessage { get; set; }

        /// <summary>Gets the request being authorized.</summary>
        public HttpRequest Request => this.ActionContext?.HttpContext?.Request;

        /// <summary>Gets the principal whose authorization is being decided.</summary>
        /// <remarks>
        /// The legacy filters read <see cref="System.Threading.Thread.CurrentPrincipal"/>, which the
        /// <c>System.Web</c> pipeline kept in sync with the request's user. ASP.NET Core only populates
        /// <see cref="HttpContext.User"/>, so that is the primary source here; the DNN authentication
        /// handlers additionally keep <see cref="System.Threading.Thread.CurrentPrincipal"/> populated
        /// (see <c>AuthMessageHandlerBase.SetCurrentPrincipal</c>), and it stays the fallback so that the
        /// decision table behaves identically no matter which handler authenticated the request.
        /// </remarks>
        public IPrincipal Principal =>
            this.ActionContext?.HttpContext?.User ?? System.Threading.Thread.CurrentPrincipal;

        /// <summary>
        /// Processes requests that fail authorization. This default implementation creates a new response with the
        /// Unauthorized status code. Override this method to provide your own handling for unauthorized requests.
        /// </summary>
        /// <remarks>
        /// The outcome is <see cref="HttpStatusCode.Unauthorized"/> (401), never 403: the legacy filter
        /// also produced a 401 for every denied request, and the DNN services framework relies on it so
        /// that the authentication handler chain can turn the 401 into a challenge. The
        /// <c>WWW-Authenticate</c> header is deliberately NOT added here; exactly as on net48, it is
        /// appended while the response is being sent by whichever authentication handler supports the
        /// request (see <c>BasicAuthMessageHandler</c> / <c>DigestAuthMessageHandler</c>
        /// <c>OnSendResponse</c>), which is what keeps the challenge headers byte-identical.
        /// </remarks>
        public virtual void HandleUnauthorizedRequest()
        {
            if (this.ActionContext?.HttpContext != null)
            {
                this.ActionContext.HttpContext.Items[SuppressAuthenticationRedirectKey] = true;
            }

            this.ActionContext.Result = DnnApiResults.Error(HttpStatusCode.Unauthorized, this.AuthFailureMessage);
        }
    }
}
