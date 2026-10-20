// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    /// <summary>Provides methods for registering DNN MVC routes.</summary>
    public interface IMapRoute
    {
        /// <summary>Sets up the route(s) for DotNetNuke MVC Controls.</summary>
        /// <param name="moduleFolderName">The name of the folder under DesktopModules in which your module resides.</param>
        /// <param name="routeName">A unique name for the route.</param>
        /// <param name="url">The parameterized portion of the route.</param>
        /// <param name="namespaces">The namespace(s) in which to search for the controllers for this route.</param>
        /// <returns>The route that was registered.</returns>
        /// <remarks>The combination of moduleFolderName and routeName must be unique for each route.</remarks>
        DnnMvcRoute MapRoute(string moduleFolderName, string routeName, string url, string[] namespaces);

        /// <summary>Sets up the route(s) for DotNetNuke MVC Controls.</summary>
        /// <param name="moduleFolderName">The name of the folder under DesktopModules in which your module resides.</param>
        /// <param name="routeName">A unique name for the route.</param>
        /// <param name="url">The parameterized portion of the route.</param>
        /// <param name="defaults">Default values for the route parameters.</param>
        /// <param name="namespaces">The namespace(s) in which to search for the controllers for this route.</param>
        /// <returns>The route that was registered.</returns>
        /// <remarks>The combination of moduleFolderName and routeName must be unique for each route.</remarks>
        DnnMvcRoute MapRoute(string moduleFolderName, string routeName, string url, object defaults, string[] namespaces);

        /// <summary>Sets up the route(s) for DotNetNuke MVC Controls.</summary>
        /// <param name="moduleFolderName">The name of the folder under DesktopModules in which your module resides.</param>
        /// <param name="routeName">A unique name for the route.</param>
        /// <param name="url">The parameterized portion of the route.</param>
        /// <param name="defaults">Default values for the route parameters.</param>
        /// <param name="constraints">The constraints.</param>
        /// <param name="namespaces">The namespace(s) in which to search for the controllers for this route.</param>
        /// <returns>The route that was registered.</returns>
        /// <remarks>The combination of moduleFolderName and routeName must be unique for each route.</remarks>
        DnnMvcRoute MapRoute(string moduleFolderName, string routeName, string url, object defaults, object constraints, string[] namespaces);
    }
}
