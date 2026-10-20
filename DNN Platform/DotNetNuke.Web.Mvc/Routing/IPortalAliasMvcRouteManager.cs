// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    using System.Collections.Generic;

    using DotNetNuke.Entities.Portals;

    using Microsoft.AspNetCore.Routing;

    internal interface IPortalAliasMvcRouteManager
    {
        IEnumerable<int> GetRoutePrefixCounts();

        string GetRouteName(string moduleFolderName, string routeName, int count);

        string GetRouteName(string moduleFolderName, string routeName, PortalAliasInfo portalAlias);

        RouteValueDictionary GetAllRouteValues(PortalAliasInfo portalAliasInfo, object routeValues);

        string GetRouteUrl(string moduleFolderName, string url, int count);

        void ClearCachedData();
    }
}
