// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.Groups
{
    using DotNetNuke.Web.Api;

    public class ServiceRouteMapper : IServiceRouteMapper
    {
        private static readonly string[] RouteNamespaces = { "DotNetNuke.Modules.Groups" };

        /// <inheritdoc />
        public void RegisterRoutes(IMapRoute mapRouteManager)
        {
            mapRouteManager.MapHttpRoute("SocialGroups", "default", "{controller}/{action}", RouteNamespaces);
        }
    }
}
