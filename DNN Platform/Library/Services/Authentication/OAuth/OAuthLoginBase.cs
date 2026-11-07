// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Authentication.OAuth
{
    using System;
    using System.Collections.Specialized;
    using System.Threading.Tasks;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Http;

    /// <summary>A base Blazor component for OAuth login components.</summary>
    /// <remarks>
    /// On WebForms the OAuth round-trip ran from <c>OnLoad</c> behind an <c>IsPostBack</c> check.
    /// In Blazor the equivalent "first time this component runs for this request" hook is
    /// <see cref="OnInitializedAsync"/>, which is also the only place where
    /// <c>HttpContext</c> is guaranteed to be available (it is <see langword="null"/> once the
    /// SignalR circuit takes over).
    /// </remarks>
    public abstract class OAuthLoginBase : AuthenticationLoginBase
    {
        /// <inheritdoc />
        public override bool Enabled
        {
            get { return OAuthConfigBase.GetConfig(this.AuthSystemApplicationName, this.PortalId).Enabled; }
        }

        protected virtual string AuthSystemApplicationName
        {
            get { return string.Empty; }
        }

        protected OAuthClientBase OAuthClient { get; set; }

        /// <summary>Gets the current user's data from the OAuth provider.</summary>
        /// <returns>The user data.</returns>
        protected abstract Task<UserData> GetCurrentUserAsync();

        protected virtual void AddCustomProperties(NameValueCollection properties)
        {
        }

        /// <inheritdoc />
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            // Save the return Url in the cookie. This must happen while the HTTP response is
            // still open, i.e. during the initial (static) render — never from an event handler.
            var response = HttpContextSource.Current?.Response;
            response?.Cookies.Append(
                "returnurl",
                this.RedirectURL ?? string.Empty,
                new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddMinutes(5),
                    Path = !string.IsNullOrEmpty(Globals.ApplicationPath) ? Globals.ApplicationPath : "/",
                });

            var shouldAuthorize = this.OAuthClient.IsCurrentService() && this.OAuthClient.HaveVerificationCode();
            if (this.Mode == AuthMode.Login)
            {
                shouldAuthorize = shouldAuthorize || this.OAuthClient.IsCurrentUserAuthorized();
            }

            if (shouldAuthorize)
            {
                if (await this.OAuthClient.AuthorizeAsync() == AuthorisationResult.Authorized)
                {
                    await this.OAuthClient.AuthenticateUserAsync(
                        await this.GetCurrentUserAsync(),
                        this.PortalSettings,
                        this.IPAddress,
                        this.AddCustomProperties,
                        this.OnUserAuthenticated);
                }
            }
        }
    }
}
