// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Text.Json;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common.Internal;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Localization.Internal;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Sets up DNN context information upon a request.</summary>
    /// <remarks>
    /// This was the <c>DnnContextMessageHandler</c> (a <c>MessageProcessingHandler</c>) which the legacy
    /// <c>ServicesRoutingManager.RegisterRoutes</c> added to <c>GlobalConfiguration.Configuration.MessageHandlers</c>
    /// as the very first handler, before any authentication handler. It is a pipeline-wide concern, so it
    /// is middleware now; <c>UseDnnWebApiInfrastructure</c> registers it after <c>UseRouting</c> (the
    /// tab/module lookup reads route values) and before authentication/authorization, which preserves the
    /// original ordering.
    /// </remarks>
    public class DnnContextMiddleware
    {
        private readonly RequestDelegate next;

        /// <summary>Initializes a new instance of the <see cref="DnnContextMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public DnnContextMiddleware(RequestDelegate next)
        {
            ArgumentNullException.ThrowIfNull(next);

            this.next = next;
        }

        /// <summary>Invokes the middleware.</summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // SCOPE GATE. The legacy DnnContextMessageHandler was a Web API MessageProcessingHandler in
            // HttpConfiguration.MessageHandlers, so it only ran for services framework requests. In the single
            // Core pipeline this middleware sees every request, and running it for a page request would
            // overwrite the PortalSettings the platform's page pipeline already put in HttpContext.Items,
            // re-set the thread culture from API logic, and could short-circuit the page with a 400.
            if (!DnnServiceEndpoint.IsServiceRequest(context))
            {
                await this.next(context);
                return;
            }

            if (!TrySetupPortalSettings(context, out var portalSettings, out var statusCode, out var message))
            {
                context.Response.StatusCode = (int)statusCode;

                // The legacy handler produced this through Request.CreateErrorResponse, i.e. an HttpError
                // body: {"Message":"..."} as application/json. Keep that shape so a client reading .Message
                // still finds it.
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(new Dictionary<string, string> { ["Message"] = message }));
                return;
            }

            SetThreadCulture(portalSettings);

            await this.next(context);
        }

        private static void SetThreadCulture(PortalSettings portalSettings)
        {
            if (portalSettings == null)
            {
                return;
            }

            CultureInfo pageLocale = TestableLocalization.Instance.GetPageLocale(portalSettings);
            if (pageLocale != null)
            {
                TestableLocalization.Instance.SetThreadCultures(pageLocale, portalSettings);
            }
        }

        private static bool TrySetupPortalSettings(HttpContext context, out PortalSettings portalSettings, out HttpStatusCode statusCode, out string message)
        {
            portalSettings = null;
            statusCode = HttpStatusCode.OK;
            message = null;

#pragma warning disable CS0618 // Type or member is obsolete
            var domainName = TestableGlobals.Instance.GetDomainName(new Uri(context.Request.GetDisplayUrl()));
            var alias = PortalAliasController.Instance.GetPortalAlias(domainName);

            if (alias == null)
            {
                // No portal alias matches the host header, so there is no DNN context to establish. The
                // legacy MessageProcessingHandler never reached this state for non-API traffic; returning
                // true with a null PortalSettings lets the request continue to the endpoint (which resolves
                // its own 404) instead of throwing a NullReferenceException on alias.PortalID.
                statusCode = HttpStatusCode.OK;
                return true;
            }

            if (!ValidateTabAndModuleContext(context.Request, alias.PortalID, out var tabId, out statusCode, out message))
            {
                return false;
            }

            portalSettings = new PortalSettings(tabId, alias);

            context.Items["PortalSettings"] = portalSettings;
            context.RequestServices.GetRequiredService<LogRequestContext>().AddToLogContext("PortalId", portalSettings.PortalId);
            return true;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private static bool TabIsInPortalOrHost(int tabId, int portalId)
        {
            var tab = TabController.Instance.GetTab(tabId, portalId);

            return tab != null && (IsHostTab(tab) || tab.PortalID == portalId || InSamePortalGroup(tab.PortalID, portalId));
        }

        private static bool InSamePortalGroup(int portalId1, int portalId2)
        {
            IPortalInfo portal1 = PortalController.Instance.GetPortal(portalId1);
            IPortalInfo portal2 = PortalController.Instance.GetPortal(portalId2);

            return portal1 != null &&
                   portal2 != null &&
                   portal1.PortalGroupId > Null.NullInteger &&
                   portal2.PortalGroupId > Null.NullInteger &&
                   portal1.PortalGroupId == portal2.PortalGroupId;
        }

        private static bool IsHostTab(TabInfo tab)
        {
            return tab.PortalID == Null.NullInteger;
        }

        private static bool ValidateTabAndModuleContext(HttpRequest request, int portalId, out int tabId, out HttpStatusCode statusCode, out string message)
        {
            statusCode = HttpStatusCode.OK;
            message = null;
            tabId = request.FindTabId();

            if (tabId != Null.NullInteger)
            {
                if (!TabIsInPortalOrHost(tabId, portalId))
                {
                    statusCode = HttpStatusCode.BadRequest;
                    message = Localization.GetString("TabNotInPortal", Localization.ExceptionsResourceFile);
                    return false;
                }

                int moduleId = request.FindModuleId();

                if (moduleId != Null.NullInteger)
                {
                    var module = ModuleController.Instance.GetModule(moduleId, tabId, false);
                    if (module == null)
                    {
                        statusCode = HttpStatusCode.BadRequest;
                        message = Localization.GetString("TabModuleNotExist", Localization.ExceptionsResourceFile);
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
