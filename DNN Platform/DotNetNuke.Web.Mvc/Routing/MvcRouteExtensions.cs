// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    /// <summary>Extension methods to read and write the DNN route metadata (name, controller namespaces).</summary>
    /// <remarks>
    /// The legacy versions of these methods hung off <c>System.Web.Routing.Route</c> and used its
    /// <c>DataTokens</c>. The ASP.NET Core equivalent of the route's data tokens are the endpoint metadata,
    /// which are populated from <see cref="DnnMvcRoute.DataTokens"/> when the route is mapped.
    /// </remarks>
    public static class MvcRouteExtensions
    {
        private const string NamespaceKey = "namespaces";
        private const string NameKey = "name";

        /// <summary>Get the name of the route.</summary>
        /// <param name="route">The route.</param>
        /// <returns>Route name.</returns>
        public static string GetName(this DnnMvcRoute route)
        {
            return route.DataTokens.TryGetValue(NameKey, out var name) ? (string)name : null;
        }

        internal static void SetNameSpaces(this DnnMvcRoute route, string[] namespaces)
        {
            route.DataTokens[NamespaceKey] = namespaces;
        }

        /// <summary>Get Namespaces that are searched for controllers for this route.</summary>
        /// <param name="route">The route.</param>
        /// <returns>Namespaces.</returns>
        internal static string[] GetNameSpaces(this DnnMvcRoute route)
        {
            return route.DataTokens.TryGetValue(NamespaceKey, out var namespaces) ? (string[])namespaces : null;
        }

        internal static void SetName(this DnnMvcRoute route, string name)
        {
            route.DataTokens[NameKey] = name;
            route.Name = name;
        }
    }
}
