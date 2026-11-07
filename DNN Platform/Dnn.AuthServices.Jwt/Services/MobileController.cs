// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.AuthServices.Jwt.Services
{
    using System.Net;
    using System.Net.Http.Headers;

    using Dnn.AuthServices.Jwt.Components.Common.Controllers;
    using Dnn.AuthServices.Jwt.Components.Entity;
    using DotNetNuke.Web.Api;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Net.Http.Headers;
    using Newtonsoft.Json;

    // DNN's own ValidateAntiForgeryTokenAttribute (DotNetNuke.Web.Api) must win over
    // Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute, which the Core MVC namespaces now
    // bring into scope. The alias keeps [ValidateAntiForgeryToken] on TestPost bound to the DNN
    // attribute the legacy controller used, so the anti-forgery check is the same one as before.
    using ValidateAntiForgeryToken = DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute;

    /// <summary>API controller for JWT services (usually mobile).</summary>
    /// <remarks>
    /// Was a Web API 2 controller. <c>DnnApiController</c> is now an ASP.NET Core
    /// <c>ControllerBase</c> and every action returns an <see cref="IActionResult"/> instead of an
    /// <c>IHttpActionResult</c>; the action bodies, the route (registered by
    /// <see cref="ServiceRouteMapper"/>) and the authorization attributes are unchanged.
    /// </remarks>
    [DnnAuthorize(AuthTypes = "JWT")]
    public class MobileController : DnnApiController
    {
        /// <summary>Clients that used JWT login should use this API call to log out and invalidate the tokens.</summary>
        /// <returns>An asynchronous HTTP response.</returns>
        [HttpGet]
        public IActionResult Logout()
        {
            return JwtController.Instance.LogoutUser(this.Request)
                ? this.Ok(new { success = true, })
                : this.Unauthorized();
        }

        /// <summary>
        /// Clients that want to go cookie-less should call this API to log in and receive
        /// a JSON Web Token (JWT) that allows them to authenticate the users to other
        /// secure API endpoints afterward.
        /// </summary>
        /// <remarks>AllowAnonymous attribute must stay in this call even though the
        /// DnnAuthorize attribute is present at a class level.</remarks>
        /// <param name="loginData">The information usd for login, <see cref="LoginData"/>.</param>
        /// <returns>An asynchronous HTTP response.</returns>
        [HttpPost]
        [AllowAnonymous]
        public IActionResult Login(LoginData loginData)
        {
            var result = JwtController.Instance.LoginUser(this.Request, loginData);
            return this.ReplyWith(result);
        }

        /// <summary>
        /// Extends the token expiry. A new JWT is returned to the caller which must be used in
        /// new API requests. The caller must pass the renewal token received at the login time.
        /// The header still needs to pass the current token for validation even when it is expired.
        /// </summary>
        /// <remarks>The access token is allowed to get renewed one time only.<br />
        /// AllowAnonymous attribute must stay in this call even though the
        /// DnnAuthorize attribute is present at a class level.
        /// </remarks>
        /// <param name="rtoken">The renewal token information, <see cref="RenewalDto"/>.</param>
        /// <returns>An asynchronous HTTP response.</returns>
        [HttpPost]
        [AllowAnonymous]
        public IActionResult ExtendToken(RenewalDto rtoken)
        {
            var result = JwtController.Instance.RenewToken(this.Request, rtoken.RenewalToken);
            return this.ReplyWith(result);
        }

        /// <summary>Tests a get HTTP request.</summary>
        /// <returns>Basic information about the identity.</returns>
        /// <remarks>
        /// Keeps reading <see cref="System.Threading.Thread.CurrentPrincipal"/>, which
        /// <c>AuthMessageHandlerBase.SetCurrentPrincipal</c> still assigns. Reporting
        /// <c>this.User</c> instead would change what this endpoint echoes back.
        /// </remarks>
        [HttpGet]
        public IActionResult TestGet()
        {
            var identity = System.Threading.Thread.CurrentPrincipal.Identity;
            var reply = $"Hello {identity.Name}! You are authenticated through {identity.AuthenticationType}.";
            return this.Ok(new { reply, });
        }

        /// <summary>Tests a POST api method.</summary>
        /// <param name="something"><see cref="TestPostData"/>.</param>
        /// <returns>Basic information about the identity and the text provided in the POST.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TestPost(TestPostData something)
        {
            var identity = System.Threading.Thread.CurrentPrincipal.Identity;
            var reply = $"Hello {identity.Name}! You are authenticated through {identity.AuthenticationType}." +
                        $" You said: ({something.Text})";
            return this.Ok(new { reply, });
        }

        private IActionResult ReplyWith(LoginResultData result)
        {
            if (result == null)
            {
                return this.Unauthorized();
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                // HACK: this will return the scheme with the error message as a challenge; non-standard method
                // ControllerBase.Unauthorized has no challenge overload (Web API 2's
                // Unauthorized(AuthenticationHeaderValue) did), so the WWW-Authenticate header is appended
                // to the 401 directly, the same idiom BasicAuthMessageHandler.OnSendResponse uses. The
                // error string still reaches the client in that header.
                this.Response.Headers.Append(
                    HeaderNames.WWWAuthenticate,
                    new AuthenticationHeaderValue(JwtController.AuthScheme, result.Error).ToString());
                return DnnApiResults.Response(HttpStatusCode.Unauthorized);
            }

            return this.Ok(result);
        }

        /// <summary>Represents the request data for a test POST.</summary>
        [JsonObject]
        public class TestPostData
        {
#pragma warning disable SA1401 // Field should be private
            /// <summary>The text used in the test.</summary>
            [JsonProperty("text")]
            public string Text;
#pragma warning restore SA1401 // Field should be private
        }
    }
}
