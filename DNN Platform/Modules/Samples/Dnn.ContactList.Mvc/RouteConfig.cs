// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.ContactList.Mvc
{
    using DotNetNuke.Web.Mvc.Routing;

    /// <summary>Registers the routes for the ContactList MVC module.</summary>
    public class RouteConfig : IMvcRouteMapper
    {
        /// <inheritdoc />
        public void RegisterRoutes(IMapRoute mapRouteManager)
        {
            mapRouteManager.MapRoute("ContactList", "ContactList", "{controller}/{action}", new[]
            {
                "Dnn.ContactList.Mvc.Controllers",
            });
        }
    }
}
