// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.HttpModules
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Urls;
    using DotNetNuke.HttpModules.Services;
    using DotNetNuke.Services.Mobile;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Redirects requests from mobile devices to the portal's configured mobile destination. This
    /// is the ASP.NET Core replacement for the <c>MobileRedirectModule</c> <c>IHttpModule</c>,
    /// which hooked <c>HttpApplication.BeginRequest</c>.
    /// </summary>
    /// <remarks>
    /// <b>Pipeline position:</b> pre-routing, so the 302 is produced instead of letting the page
    /// render.
    /// </remarks>
    public sealed class DnnMobileRedirectMiddleware
    {
        private static readonly List<string> SpecialPages = ["/login.aspx", "/register.aspx", "/terms.aspx", "/privacy.aspx", "/login", "/register", "/terms", "/privacy",];
        private static readonly Regex MvcServicePath = new Regex(@"DesktopModules/MVC/", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly RequestDelegate next;
        private readonly IRedirectionController redirectionController;
        private readonly IPortalController portalController;
        private readonly IHostSettings hostSettings;
        private readonly IHostSettingsService hostSettingsService;

        /// <summary>Initializes a new instance of the <see cref="DnnMobileRedirectMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="redirectionController">The redirection controller.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        public DnnMobileRedirectMiddleware(
            RequestDelegate next,
            IRedirectionController redirectionController,
            IPortalController portalController,
            IHostSettings hostSettings,
            IHostSettingsService hostSettingsService)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.redirectionController = redirectionController;
            this.portalController = portalController ?? throw new ArgumentNullException(nameof(portalController));
            this.hostSettings = hostSettings ?? throw new ArgumentNullException(nameof(hostSettings));
            this.hostSettingsService = hostSettingsService ?? throw new ArgumentNullException(nameof(hostSettingsService));
        }

        /// <summary>Gets the name the module was registered under in <c>web.config</c>.</summary>
        public static string ModuleName => "MobileRedirectModule";

        /// <summary>Redirects mobile devices, or runs the rest of the pipeline.</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (this.TryRedirect(context))
            {
                // Response.Redirect() ended the request in the legacy module; in Core the
                // pipeline is short-circuited by not calling the next middleware.
                return;
            }

            await this.next(context);
        }

        private static bool IsSpecialPage(string url, IPortalSettings portalSettings)
        {
            var tabPath = url.ToLowerInvariant();
            var idx = tabPath.IndexOf('?');
            if (idx >= 0)
            {
                tabPath = tabPath.Substring(0, idx);
            }

            if (portalSettings == null)
            {
                return true;
            }

            var alias = ((IPortalAliasInfo)PortalSettings.Current.PortalAlias).HttpAlias.ToLowerInvariant();
            idx = alias.IndexOf('/');
            if (idx >= 0)
            {
                tabPath = tabPath.Replace(alias.Substring(idx), string.Empty);
            }

            return SpecialPages.Contains(tabPath);
        }

        private bool TryRedirect(HttpContext context)
        {
            var request = context.Request;
            var portalSettings = this.portalController.GetCurrentSettings();

            // First check if we are upgrading/installing
            var rawUrl = request.GetRawUrl();
            var userAgent = request.Headers.UserAgent.ToString();
            if (!Initialize.ProcessHttpModule(request, false, false)
                    || HttpMethods.IsPost(request.Method)
                    || DnnServicesFrameworkMiddleware.ServiceApi.IsMatch(rawUrl)
                    || MvcServicePath.IsMatch(rawUrl)
                    || IsSpecialPage(rawUrl, portalSettings)
                    || (portalSettings != null && !this.IsRedirectAllowed(rawUrl, request, portalSettings)))
            {
                return false;
            }

            // Check if redirection has been disabled for the session
            // This method inspects cookie and query string. It can also setup / clear cookies.
            if (this.redirectionController == null ||
                TabController.CurrentPage == null ||
                string.IsNullOrEmpty(userAgent) ||
                !this.redirectionController.IsRedirectAllowedForTheSession(context))
            {
                return false;
            }

            var redirectUrl = this.redirectionController.GetRedirectUrl(userAgent);
            if (string.IsNullOrEmpty(redirectUrl))
            {
                return false;
            }

            // append the query string from original url
            var idx = rawUrl.IndexOf('?');
            var queryString = idx >= 0 ? rawUrl.Substring(idx + 1) : string.Empty;
            if (!string.IsNullOrEmpty(queryString))
            {
                redirectUrl = string.Concat(redirectUrl, redirectUrl.Contains('?') ? "&" : "?", queryString);
            }

            context.Response.Redirect(redirectUrl);
            return true;
        }

        private bool IsRedirectAllowed(string url, HttpRequest request, IPortalSettings portalSettings)
        {
            var urlAction = new UrlAction(request);
            urlAction.SetRedirectAllowed(url, new FriendlyUrlSettings(this.portalController, this.hostSettings, this.hostSettingsService, portalSettings.PortalId));
            return urlAction.RedirectAllowed;
        }
    }
}
