// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.AuthServices.Jwt.Services
{
    using System.Net.Http.Headers;

    using Dnn.AuthServices.Jwt.Components.Common.Controllers;
    using Dnn.AuthServices.Jwt.Components.Entity;

    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    using Newtonsoft.Json;

    using ValidateAntiForgeryTokenAttribute = DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute;

    /// <summary>API controller for JWT services (usually mobile).</summary>
    [DnnAuthorize(AuthTypes = "JWT")]
    public class MobileController : DnnApiController
    {
        /// <summary>Clients that used JWT login should use this API call to log out and invalidate the tokens.</summary>
        /// <returns>An asynchronous HTTP response.</returns>
        [HttpGet]
        public IActionResult Logout()
        {
            return JwtController.Instance.LogoutUser(this.GetRequestMessage())
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
            var result = JwtController.Instance.LoginUser(this.GetRequestMessage(), loginData);
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
            var result = JwtController.Instance.RenewToken(this.GetRequestMessage(), rtoken.RenewalToken);
            return this.ReplyWith(result);
        }

        /// <summary>Tests a get HTTP request.</summary>
        /// <returns>Basic information about the identity.</returns>
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
                this.Response.Headers.WWWAuthenticate = new AuthenticationHeaderValue(JwtController.AuthScheme, result.Error).ToString();
                return this.Unauthorized();
            }

            return this.Ok(result);
        }

        private System.Net.Http.HttpRequestMessage GetRequestMessage()
        {
            return ServicesFrameworkRequestMessage.Create(this.HttpContext);
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
