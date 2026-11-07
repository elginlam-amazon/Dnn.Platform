// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc
{
    using System;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Controllers;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.HttpModules.Membership;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.UserRequest;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Common;
    using DotNetNuke.Web.Mvc.Extensions;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Executes a DNN MVC module for a single request. This is the ASP.NET Core replacement of the
    /// legacy <c>HttpTaskAsyncHandler</c> based handler; it is invoked from an endpoint/middleware
    /// (see <see cref="DnnMvcRouteHandler"/>) rather than from the <c>System.Web</c> handler pipeline.</summary>
    public class DnnMvcHandler
    {
        public static readonly string MvcVersionHeaderName = "X-AspNetMvc-Version";

        private static readonly string MvcVersion =
            typeof(DnnMvcHandler).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            ?? typeof(DnnMvcHandler).Assembly.GetName().Version?.ToString()
            ?? string.Empty;

        /// <summary>Initializes a new instance of the <see cref="DnnMvcHandler"/> class.</summary>
        /// <param name="httpContext">The current request context.</param>
        /// <param name="routeData">The route data matched for the request.</param>
        public DnnMvcHandler(HttpContext httpContext, RouteData routeData)
        {
            this.HttpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
            this.RouteData = routeData ?? throw new ArgumentNullException(nameof(routeData));
        }

        /// <summary>Gets or sets a value indicating whether the <c>X-AspNetMvc-Version</c> response header is suppressed.</summary>
        public static bool DisableMvcResponseHeader { get; set; }

        /// <summary>Gets the current request context.</summary>
        public HttpContext HttpContext { get; }

        /// <summary>Gets the route data matched for the request.</summary>
        public RouteData RouteData { get; }

        /// <summary>Processes the request: sets the thread culture, authenticates and executes the module.</summary>
        /// <returns>A task.</returns>
        public async Task ProcessRequestAsync()
        {
            var context = this.HttpContext;
            var serviceProvider = context.RequestServices ?? Globals.GetCurrentServiceProvider();

            SetThreadCulture();
            MembershipModule.AuthenticateRequest(
                serviceProvider.GetRequiredService<IHostSettingsService>(),
                PortalController.Instance,
                UserRequestIPAddressController.Instance,
                RoleController.Instance,
                serviceProvider.GetRequiredService<IEventLogger>(),
                serviceProvider.GetRequiredService<IHostSettings>(),
                context,
                allowUnknownExtensions: true);

            AddVersionHeader(context);

            await this.ProcessRequestAsync(context, context.RequestAborted);
        }

        protected internal virtual async Task ProcessRequestAsync(HttpContext httpContext, CancellationToken cancellationToken)
        {
            var moduleExecutionEngine = GetModuleExecutionEngine(httpContext);

            // Check if the controller supports IDnnController
            var moduleResult =
                await moduleExecutionEngine.ExecuteModuleAsync(this.GetModuleRequestContext(httpContext), cancellationToken);
            httpContext.SetModuleRequestResult(moduleResult);
            await this.RenderModuleAsync(moduleResult);
        }

        private static void AddVersionHeader(HttpContext httpContext)
        {
            if (DisableMvcResponseHeader)
            {
                return;
            }

            httpContext.Response.Headers[MvcVersionHeaderName] = MvcVersion;
        }

        private static void SetThreadCulture()
        {
            var portalSettings = PortalController.Instance.GetCurrentSettings();
            if (portalSettings is null)
            {
                return;
            }

            var pageLocale = Localization.GetPageLocale(portalSettings);
            if (pageLocale is null)
            {
                return;
            }

            Localization.SetThreadCultures(pageLocale, portalSettings);
        }

        private static IModuleExecutionEngine GetModuleExecutionEngine(HttpContext httpContext)
        {
            return ModuleExecutionExtensions.GetModuleExecutionEngine(httpContext?.RequestServices);
        }

        private ModuleRequestContext GetModuleRequestContext(HttpContext httpContext)
        {
            var moduleInfo = httpContext.Request.FindModuleInfo();
            var moduleContext = new ModuleInstanceContext() { Configuration = moduleInfo };
            var desktopModule = DesktopModuleControllerAdapter.Instance.GetDesktopModule(moduleInfo.DesktopModuleID, moduleInfo.PortalID);
            var moduleRequestContext = new ModuleRequestContext
            {
                HttpContext = httpContext,
                RouteData = this.RouteData,
                ModuleContext = moduleContext,
                ModuleApplication = new ModuleApplication(new ActionContext(httpContext, this.RouteData, new ActionDescriptor()))
                {
                    ModuleName = desktopModule.ModuleName,
                    FolderPath = desktopModule.FolderName,
                },
            };

            return moduleRequestContext;
        }

        private async Task RenderModuleAsync(ModuleRequestResult moduleResult)
        {
            var moduleExecutionEngine = GetModuleExecutionEngine(this.HttpContext);

            await using var writer = new HttpResponseStreamWriter(this.HttpContext.Response.Body, Encoding.UTF8);
            await moduleExecutionEngine.ExecuteModuleResultAsync(moduleResult, writer);
            await writer.FlushAsync();
        }
    }
}
