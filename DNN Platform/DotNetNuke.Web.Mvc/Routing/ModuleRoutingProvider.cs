// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    using DotNetNuke.Common;
    using DotNetNuke.ComponentModel;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.UI.Modules;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    public abstract class ModuleRoutingProvider
    {
        /// <summary>Gets the module routing provider of the application.</summary>
        /// <returns>The <see cref="ModuleRoutingProvider"/> to generate module URLs and route data with.</returns>
        /// <remarks>
        /// The legacy implementation located the provider through DNN's <see cref="ComponentFactory"/>.
        /// That static locator keeps working (a module may still register its own provider into it), but
        /// it is now a bridge to the host's container rather than a second container: when nothing was
        /// registered, the provider is resolved from DI (registered by
        /// <c>DotNetNuke.Web.Mvc.Startup.ConfigureServices</c>) and published back into the
        /// <see cref="ComponentFactory"/> so legacy callers see the very same instance.
        /// </remarks>
        public static ModuleRoutingProvider Instance()
        {
            var component = ComponentFactory.GetComponent<ModuleRoutingProvider>();

            if (component == null)
            {
                var serviceProvider = Globals.GetCurrentServiceProvider();
                var requestServices = serviceProvider?.GetService<IHttpContextAccessor>()?.HttpContext?.RequestServices;
                component = (requestServices ?? serviceProvider)?.GetService<ModuleRoutingProvider>()
                    ?? new StandardModuleRoutingProvider();
                ComponentFactory.RegisterComponentInstance<ModuleRoutingProvider>(component);
            }

            return component;
        }

        public abstract string GenerateUrl(RouteValueDictionary routeValues, ModuleInstanceContext moduleContext);

        public abstract RouteData GetRouteData(HttpContext httpContext, ModuleControlInfo moduleControl);
    }
}
