// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Extensions
{
    using System;
    using System.Linq;

    using DotNetNuke.Web.Api.Internal;

    using Microsoft.AspNetCore.Routing;

    /// <summary>Endpoint routing extensions of the services framework.</summary>
    public static class ServicesFrameworkEndpointRouteBuilderExtensions
    {
        /// <summary>Maps the services framework routes registered by the <see cref="DotNetNuke.Web.Api.IServiceRouteMapper"/> implementations (<c>/DesktopModules/{module}/API/{controller}/{action}</c>).</summary>
        /// <param name="endpoints">The endpoint route builder of the application.</param>
        /// <returns>The <paramref name="endpoints"/> instance.</returns>
        public static IEndpointRouteBuilder MapDnnServicesRoutes(this IEndpointRouteBuilder endpoints)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            if (!endpoints.DataSources.OfType<ServicesFrameworkEndpointDataSource>().Any())
            {
                endpoints.DataSources.Add(new ServicesFrameworkEndpointDataSource(0));
            }

            return endpoints;
        }
    }
}
