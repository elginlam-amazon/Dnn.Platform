// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    using DotNetNuke.ComponentModel;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    /// <summary>Provides route data and URL generation for MVC modules.</summary>
    public abstract class ModuleRoutingProvider
    {
        /// <summary>Gets the registered <see cref="ModuleRoutingProvider"/> instance.</summary>
        /// <returns>The provider instance.</returns>
        public static ModuleRoutingProvider Instance()
        {
            var component = ComponentFactory.GetComponent<ModuleRoutingProvider>();

            if (component == null)
            {
                component = new StandardModuleRoutingProvider();
                ComponentFactory.RegisterComponentInstance<ModuleRoutingProvider>(component);
            }

            return component;
        }

        /// <summary>Generates a URL for the given route values and module context.</summary>
        /// <param name="routeValues">The route values.</param>
        /// <param name="moduleContext">The module context.</param>
        /// <returns>The URL.</returns>
        public abstract string GenerateUrl(RouteValueDictionary routeValues, ModuleInstanceContext moduleContext);

        /// <summary>Gets route data for the given module control.</summary>
        /// <param name="httpContext">The HTTP context, or <see langword="null"/>.</param>
        /// <param name="moduleControl">The module control.</param>
        /// <returns>The route data.</returns>
        public abstract RouteData GetRouteData(HttpContext httpContext, ModuleControlInfo moduleControl);
    }
}
