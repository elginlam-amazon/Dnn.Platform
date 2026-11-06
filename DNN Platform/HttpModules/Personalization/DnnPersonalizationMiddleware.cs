// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.HttpModules.Personalization
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Services.Personalization;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Middleware which handles user personalization. Replaces the <c>EndRequest</c> handler of
    /// the legacy <c>PersonalizationModule</c> HTTP module, so the profile is saved once the rest
    /// of the pipeline (and therefore the page) has run.
    /// </summary>
    public class DnnPersonalizationMiddleware
    {
        private readonly RequestDelegate next;

        /// <summary>Initializes a new instance of the <see cref="DnnPersonalizationMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public DnnPersonalizationMiddleware(RequestDelegate next)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>Gets the HttpModule module name.</summary>
        public static string ModuleName => "PersonalizationModule";

        /// <summary>Saves the personalization profile for the current request (the legacy <c>EndRequest</c> work).</summary>
        /// <param name="context">The current request.</param>
        public static void SaveProfile(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!Initialize.ProcessHttpModule(context.Request, false, false))
            {
                return;
            }

            // Obtain PortalSettings from Current Context
            context.Items.TryGetValue("PortalSettings", out var portalSettingsItem);
            var portalSettings = portalSettingsItem as PortalSettings;
            if (portalSettings != null)
            {
                // load the user info object
                UserInfo userInfo = UserController.Instance.GetCurrentUserInfo();
                var serviceProvider = context.GetScope()?.ServiceProvider ?? context.RequestServices;
                var personalizationController = serviceProvider.GetRequiredService<PersonalizationController>();
                personalizationController.SaveProfile(context, userInfo.UserID, portalSettings.PortalId);
            }
        }

        /// <summary>Runs the rest of the pipeline, then saves the personalization profile.</summary>
        /// <param name="context">The current request.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            try
            {
                await this.next(context).ConfigureAwait(false);
            }
            finally
            {
                // EndRequest ran even when the request failed, so the save happens in a finally.
                SaveProfile(context);
            }
        }
    }
}
