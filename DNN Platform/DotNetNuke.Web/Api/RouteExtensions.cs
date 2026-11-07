// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    /// <summary>Extension methods to read and write the DNN route metadata (name, controller namespaces).</summary>
    /// <remarks>
    /// The legacy versions of these methods hung off <c>System.Web.Routing.Route</c>/<c>IHttpRoute</c> and
    /// used their <c>DataTokens</c>. The ASP.NET Core equivalent of a route's data tokens is the endpoint
    /// metadata, which is populated from <see cref="DnnServiceRoute.DataTokens"/> when the route is mapped
    /// by <c>ServicesRoutingManager.MapRoutes</c>.
    /// </remarks>
    public static class RouteExtensions
    {
        private const string NamespaceKey = "namespaces";
        private const string NameKey = "name";

        /// <summary>Get the name of the route.</summary>
        /// <param name="route">The route.</param>
        /// <returns>Route name.</returns>
        public static string GetName(this DnnServiceRoute route)
        {
            return route.DataTokens.TryGetValue(NameKey, out var name) ? (string)name : null;
        }

        /// <summary>Set the namespaces for the route.</summary>
        /// <param name="route">The route.</param>
        /// <param name="namespaces">The namespaces.</param>
        internal static void SetNameSpaces(this DnnServiceRoute route, string[] namespaces)
        {
            route.DataTokens[NamespaceKey] = namespaces;
        }

        /// <summary>Get Namespaces that are searched for controllers for this route.</summary>
        /// <param name="route">The route.</param>
        /// <returns>Namespaces.</returns>
        internal static string[] GetNameSpaces(this DnnServiceRoute route)
        {
            return route.DataTokens.TryGetValue(NamespaceKey, out var namespaces) ? (string[])namespaces : null;
        }

        /// <summary>Set the name for the route.</summary>
        /// <param name="route">The route.</param>
        /// <param name="name">The name.</param>
        internal static void SetName(this DnnServiceRoute route, string name)
        {
            route.DataTokens[NameKey] = name;
            route.Name = name;
        }
    }
}
