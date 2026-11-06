// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Providers.Caching.SimpleWebFarmCachingProvider
{
    using System;

    using DotNetNuke.Abstractions.Application;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    using ICryptographyProvider = DotNetNuke.Abstractions.Security.ICryptographyProvider;

    /// <summary>
    /// Maps the cache synchronization URL which was served by the <c>SimpleWebFarmSynchronizationHandler</c>
    /// <c>IHttpHandler</c> registration (<c>path="SimpleWebFarmSync.axd"</c>, <c>verb="GET"</c>) in
    /// <c>web.config</c>.
    /// </summary>
    /// <remarks>
    /// The path is preserved exactly: the other servers in the web farm build this URL themselves
    /// (see <c>SimpleWebFarmCachingProvider.NotifyOtherServers</c>), and a server which has not been
    /// upgraded yet will keep calling it. The endpoint is deliberately NOT authorized: its protection
    /// is the encrypted command/detail parameters (and the default credentials the caller sends), which
    /// is exactly the protection the handler had.
    /// </remarks>
    public static class SimpleWebFarmSyncEndpoint
    {
        /// <summary>The preserved cache synchronization URL.</summary>
        public const string SyncPath = "/SimpleWebFarmSync.axd";

        /// <summary>Maps the cache synchronization endpoint onto its original URL.</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The same <paramref name="endpoints"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
        public static IEndpointRouteBuilder MapSimpleWebFarmSyncEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            // MapGet: the handler was registered with verb="GET".
            endpoints.MapGet(SyncPath, HandleRequest);

            return endpoints;
        }

        private static void HandleRequest(HttpContext context)
        {
            // A handler per request, resolving the services from the request scope when the host has
            // them registered and falling back to the handler's own defaults when it does not.
            var handler = new SimpleWebFarmSynchronizationHandler(
                context.RequestServices.GetService<IHostSettings>(),
                context.RequestServices.GetService<ICryptographyProvider>());

            // Error handling is purposefully allowed to bubble up so the calling server is notified.
            handler.ProcessRequest(context);
        }
    }
}
