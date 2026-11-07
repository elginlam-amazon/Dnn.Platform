// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.HttpModules.Localization
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.Entities.Portals;

    using Microsoft.AspNetCore.Http;

    using DnnLocalization = DotNetNuke.Services.Localization.Localization;

    /// <summary>
    /// Sets up localization for all page requests. Replaces the
    /// <c>PreRequestHandlerExecute</c> handler of the legacy <c>LocalizationModule</c> HTTP module.
    /// </summary>
    /// <remarks>
    /// Runs after authentication, so the identity of the user is known. This allows for the
    /// accurate retrieval of the locale which depends not just on the site and page locale, but
    /// also on the user's preference.
    /// </remarks>
    public class DnnRequestLocalizationMiddleware
    {
        private readonly RequestDelegate next;

        /// <summary>Initializes a new instance of the <see cref="DnnRequestLocalizationMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public DnnRequestLocalizationMiddleware(RequestDelegate next)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>Sets the thread cultures for the request, then runs the rest of the pipeline.</summary>
        /// <param name="context">The current request.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            SetThreadCultures(context);

            return this.next(context);
        }

        private static void SetThreadCultures(HttpContext context)
        {
            var isInstallPage = context.Request.Path.Value?.Contains("installwizard.aspx", StringComparison.OrdinalIgnoreCase) == true;
            if (isInstallPage)
            {
                return;
            }

            // We need to ensure that we don't run this code when not requesting a page as otherwise we may not have
            // an accurate portal identified (e.g. resources). The legacy check was
            // `HttpContext.Current.CurrentHandler is Framework.PageBase`; on ASP.NET Core there is
            // no handler instance, so a request counts as a page request when the URL rewriter has
            // resolved both the portal and the active tab for it.
            var portalSettings = PortalController.Instance.GetCurrentSettings();
            var activeTabId = (portalSettings as PortalSettings)?.ActiveTab?.TabID ?? -1;
            var isPage = activeTabId > 0;
            if (!isPage)
            {
                return;
            }

            // The portalSettings should always be correct at this point, but in case we can't find a portal
            // we need to insure we're not setting the thread locale.
            DnnLocalization.SetThreadCultures(DnnLocalization.GetPageLocale(portalSettings), portalSettings);
        }
    }
}
