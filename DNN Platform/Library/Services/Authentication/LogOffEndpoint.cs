// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Authentication
{
    using System.Threading.Tasks;

    using DotNetNuke.Common.Internal;
    using DotNetNuke.Security;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    /// <summary>
    /// The endpoint that replaces the LogOff page. It is the ASP.NET Core replacement for the
    /// legacy <c>DotNetNuke.Services.Authentication.LogOffHandler</c> <c>IHttpHandler</c>, which
    /// was registered in <c>web.config</c> for the path <c>Logoff.aspx</c> (any verb, any folder).
    /// </summary>
    public static class LogOffEndpoint
    {
        /// <summary>The preserved logoff URL, so existing links and bookmarks keep working.</summary>
        public const string LogOffPath = "/Logoff.aspx";

        /// <summary>
        /// The preserved logoff URL when it appears below a portal/tab folder, e.g.
        /// <c>/Home/ctl/Logoff.aspx</c>. The IIS handler mapping matched the file name at any
        /// depth, so both shapes must be routed.
        /// </summary>
        public const string NestedLogOffPath = "/{**dnnLogOffPrefix}/Logoff.aspx";

        /// <summary>Maps the logoff endpoint onto its original URL(s).</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        public static IEndpointRouteBuilder MapDnnLogOff(this IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(LogOffPath, HandleLogOffAsync);
            endpoints.Map(NestedLogOffPath, HandleLogOffAsync);
            return endpoints;
        }

        private static async Task HandleLogOffAsync(HttpContext context)
        {
            // The legacy handler redirected to the LogOff control, which performed the
            // FormsAuthentication sign-out. Signing out here keeps the cookie removal on the
            // logoff URL itself (and is idempotent with respect to the LogOff page).
            await PortalSecurity.Instance.SignOutAsync();

            context.Response.Redirect(TestableGlobals.Instance.NavigateURL("LogOff"));
        }
    }
}
