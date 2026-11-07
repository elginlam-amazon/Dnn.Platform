// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Security.Authentication
{
    /// <summary>Constants for the platform's cookie authentication.</summary>
    public static class DnnAuthenticationDefaults
    {
        /// <summary>
        /// The authentication scheme the platform signs in and out of. It is the same value as
        /// <c>CookieAuthenticationDefaults.AuthenticationScheme</c>, so a host that calls
        /// <c>AddAuthentication(DnnAuthenticationDefaults.AuthenticationScheme).AddCookie()</c>
        /// gets the platform's scheme wired with no extra configuration.
        /// </summary>
        public const string AuthenticationScheme = "Cookies";

        /// <summary>The <c>AuthenticationProperties</c> key under which a portal-group cookie domain is remembered.</summary>
        public const string CookieDomainPropertyKey = "DNN.CookieDomain";

        /// <summary>The cookie written alongside the auth cookie when a portal belongs to a portal group.</summary>
        public const string SiteGroupCookieName = "SiteGroup";

        /// <summary>The <c>HttpContext.Items</c> key holding the signed-in <c>UserInfo</c> for the current request.</summary>
        public const string UserInfoContextKey = "UserInfo";

        /// <summary>The <c>HttpContext.Items</c> key set when the platform itself performed the sign-in.</summary>
        public const string UserSignInContextKey = "DNN_UserSignIn";

        /// <summary>The claim type carrying the portal (tenant) identifier.</summary>
        public const string PortalIdClaimType = "dnn:portalid";

        /// <summary>
        /// The claim type carrying the one-time token that identifies this cookie in DNN's
        /// <c>AuthCookie</c> table, which is how a sign-out invalidates a cookie that is still
        /// held by a browser.
        /// </summary>
        public const string AuthTokenClaimType = "dnn:authtoken";

        /// <summary>The claim type carrying the user's display name.</summary>
        public const string DisplayNameClaimType = "dnn:displayname";

        /// <summary>The claim type marking a host (super) user.</summary>
        public const string SuperUserClaimType = "dnn:superuser";
    }
}
