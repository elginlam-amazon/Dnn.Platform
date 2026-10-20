// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.AspNetCore.Routing.Patterns;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    /// The ASP.NET Core endpoints of the services framework routes registered by the <see cref="IServiceRouteMapper"/> implementations
    /// (one endpoint per route, the controller action is selected by <see cref="ServicesFrameworkEndpoint"/> like the former
    /// DNN Web API controller selector, i.e. respecting the namespaces of the route).
    /// </summary>
    public sealed class ServicesFrameworkEndpointDataSource : EndpointDataSource
    {
        private static readonly object ChangeLock = new object();
        private static CancellationTokenSource changeTokenSource = new CancellationTokenSource();
        private static int routesVersion;

        private readonly int order;
        private IReadOnlyList<Endpoint> endpoints;
        private int endpointsVersion = -1;

        /// <summary>Initializes a new instance of the <see cref="ServicesFrameworkEndpointDataSource"/> class.</summary>
        /// <param name="order">The order of the endpoints.</param>
        public ServicesFrameworkEndpointDataSource(int order)
        {
            this.order = order;
        }

        /// <inheritdoc />
        public override IReadOnlyList<Endpoint> Endpoints
        {
            get
            {
                var currentVersion = Volatile.Read(ref routesVersion);
                if (this.endpoints == null || this.endpointsVersion != currentVersion)
                {
                    this.endpoints = BuildEndpoints(ServicesRoutingManager.GetRegisteredRoutes(), this.order);
                    this.endpointsVersion = currentVersion;
                }

                return this.endpoints;
            }
        }

        /// <inheritdoc />
        public override IChangeToken GetChangeToken()
        {
            lock (ChangeLock)
            {
                return new CancellationChangeToken(changeTokenSource.Token);
            }
        }

        /// <summary>Signals that the services framework routes were (re)registered.</summary>
        internal static void NotifyRoutesChanged()
        {
            CancellationTokenSource previous;
            lock (ChangeLock)
            {
                previous = changeTokenSource;
                changeTokenSource = new CancellationTokenSource();
                Interlocked.Increment(ref routesVersion);
            }

            previous.Cancel();
            previous.Dispose();
        }

        private static IReadOnlyList<Endpoint> BuildEndpoints(IReadOnlyList<DnnServiceRoute> routes, int order)
        {
            var result = new List<Endpoint>(routes.Count);
            foreach (var route in routes)
            {
                var pattern = RoutePatternFactory.Parse(route.Url, route.Defaults, route.Constraints);
                var handler = new ServicesFrameworkEndpoint(route);
                var builder = new RouteEndpointBuilder(handler.InvokeAsync, pattern, order)
                {
                    DisplayName = "DNN services framework route " + route.Name,
                };

                builder.Metadata.Add(new RouteNameMetadata(route.Name));
                builder.Metadata.Add(new EndpointNameMetadata(route.Name));
                builder.Metadata.Add(route);
                result.Add(builder.Build());
            }

            return result.Where(endpoint => endpoint != null).ToArray();
        }
    }
}
