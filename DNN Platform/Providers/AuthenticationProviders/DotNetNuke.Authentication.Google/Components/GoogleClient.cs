// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Authentication.Google.Components
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Services.Authentication;
    using DotNetNuke.Services.Authentication.OAuth;

    /// <inheritdoc />
    public class GoogleClient : OAuthClientBase
    {
        /// <summary>Initializes a new instance of the <see cref="GoogleClient"/> class.</summary>
        /// <param name="portalId">The portal ID.</param>
        /// <param name="mode">The auth mode.</param>
        public GoogleClient(int portalId, AuthMode mode)
            : base(portalId, mode, "Google")
        {
            this.TokenEndpoint = new Uri("https://accounts.google.com/o/oauth2/token");
            this.TokenMethod = HttpMethod.POST;
            this.AuthorizationEndpoint = new Uri("https://accounts.google.com/o/oauth2/auth");
            this.MeGraphEndpoint = new Uri("https://www.googleapis.com/oauth2/v1/userinfo");

            // System.Web.HttpUtility does not exist on net10.0 and System.Web must not be
            // reintroduced, so the equivalent System.Net.WebUtility.UrlEncode is used. Both
            // encode a space as "+" and differ only in the letter case of the percent-hex
            // escapes, which RFC 3986 defines as case-insensitive. The scope string itself is
            // unchanged, character for character.
            this.Scope = System.Net.WebUtility.UrlEncode("https://www.googleapis.com/auth/userinfo.profile https://www.googleapis.com/auth/userinfo.email");

            this.AuthTokenName = "GoogleUserToken";

            this.OAuthVersion = "2.0";

            this.LoadTokenCookie(string.Empty);
        }

        /// <inheritdoc />
        protected override TimeSpan GetExpiry(string responseText)
        {
            // System.Web.Script.Serialization.JavaScriptSerializer does not exist on net10.0;
            // the platform's own Json helper is the replacement. The control flow is kept
            // exactly as it was - deserialize, then index - with no added null/empty guard, so
            // an empty or malformed token response still fails the same way it did on net48.
            var tokenDictionary = Json.Deserialize<IDictionary<string, object>>(responseText);

            return new TimeSpan(0, 0, Convert.ToInt32(tokenDictionary["expires_in"]));
        }

        /// <inheritdoc />
        protected override string GetToken(string responseText)
        {
            var tokenDictionary = Json.Deserialize<IDictionary<string, object>>(responseText);
            return Convert.ToString(tokenDictionary["access_token"]);
        }
    }
}
