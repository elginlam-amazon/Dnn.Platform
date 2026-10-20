// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System.Collections.Generic;

    /// <summary>Extension methods for services framework routes.</summary>
    public static class RouteExtensions
    {
        /// <summary>Gets the name of the route.</summary>
        /// <param name="route">The route.</param>
        /// <returns>The name.</returns>
        public static string GetName(this DnnServiceRoute route)
        {
            return route?.Name;
        }

        /// <summary>Sets the namespaces of the route.</summary>
        /// <param name="route">The route.</param>
        /// <param name="namespaces">The namespaces.</param>
        internal static void SetNameSpaces(this DnnServiceRoute route, string[] namespaces)
        {
            route.SetNamespaces(namespaces);
        }

        /// <summary>Gets the namespaces of the route.</summary>
        /// <param name="route">The route.</param>
        /// <returns>The namespaces.</returns>
        internal static IReadOnlyList<string> GetNameSpaces(this DnnServiceRoute route)
        {
            return route.Namespaces;
        }
    }
}
