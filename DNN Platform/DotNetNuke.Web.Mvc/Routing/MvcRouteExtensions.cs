// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    /// <summary>Extension methods for <see cref="DnnMvcRoute"/>.</summary>
    public static class MvcRouteExtensions
    {
        /// <summary>Get the name of the route.</summary>
        /// <param name="route">The route.</param>
        /// <returns>Route name.</returns>
        public static string GetName(this DnnMvcRoute route)
        {
            return route.Name;
        }

        internal static void SetNameSpaces(this DnnMvcRoute route, string[] namespaces)
        {
            route.Namespaces = namespaces;
        }

        internal static string[] GetNameSpaces(this DnnMvcRoute route)
        {
            return route.Namespaces;
        }

        internal static void SetName(this DnnMvcRoute route, string name)
        {
            route.Name = name;
        }
    }
}
