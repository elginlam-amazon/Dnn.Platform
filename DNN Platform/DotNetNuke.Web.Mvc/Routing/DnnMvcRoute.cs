// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    using Microsoft.AspNetCore.Routing;

    /// <summary>The definition of a DNN MVC module route, as registered by the <see cref="MvcRoutingManager"/>.</summary>
    /// <remarks>
    /// MVC 5 modules received back a <c>System.Web.Routing.Route</c> which had been added to the global
    /// <c>RouteTable.Routes</c> collection. ASP.NET Core uses endpoint routing: routes are only known to the
    /// host's <see cref="IEndpointRouteBuilder"/>, which is not available while the modules register their
    /// routes. The routing manager therefore collects these definitions and maps them as endpoints
    /// (<see cref="MvcRoutingManager.MapRoutes"/>) when the host builds its endpoint pipeline.
    /// </remarks>
    public class DnnMvcRoute
    {
        /// <summary>Gets or sets the unique name of the route (the endpoint name once mapped).</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the route URL (the endpoint route pattern).</summary>
        public string Url { get; set; }

        /// <summary>Gets or sets the default route values.</summary>
        public RouteValueDictionary Defaults { get; set; } = new RouteValueDictionary();

        /// <summary>Gets or sets the route constraints. Values are either a constraint expression or an <c>IRouteConstraint</c>.</summary>
        public RouteValueDictionary Constraints { get; set; } = new RouteValueDictionary();

        /// <summary>Gets or sets the data tokens of the route (holds the route name and the controller namespaces).</summary>
        public RouteValueDictionary DataTokens { get; set; } = new RouteValueDictionary();
    }
}
