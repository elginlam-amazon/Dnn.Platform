// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.Journal
{
    using System;

    using DotNetNuke.Web.Api;

    public class JournalRouteMapper : IServiceRouteMapper
    {
        /// <summary>The namespaces the Journal service route resolves controllers from.</summary>
        private static readonly string[] ControllerNamespaces = { "DotNetNuke.Modules.Journal" };

        public void RegisterRoutes(IMapRoute mapRouteManager)
        {
            mapRouteManager.MapHttpRoute("Journal", "default", "{controller}/{action}", ControllerNamespaces);
        }
    }
}
