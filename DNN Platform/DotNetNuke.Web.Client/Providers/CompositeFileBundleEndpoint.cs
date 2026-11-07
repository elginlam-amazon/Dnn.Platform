// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.Providers
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// The endpoint that serves the composite (bundled and minified) client resource files.
    /// </summary>
    /// <remarks>
    /// This replaces the legacy <c>DependencyHandler.axd</c> handler which was the only URL exposed by the
    /// ClientDependency pipeline. Call <see cref="MapClientResourceBundles"/> from the application's endpoint
    /// configuration to expose the bundles produced by <see cref="ICompositeFileProcessor.ProcessCompositeList"/>.
    /// </remarks>
    public static class CompositeFileBundleEndpoint
    {
        /// <summary>The route prefix under which the composite bundles are served.</summary>
        public const string BundleRoutePath = "/dnn-client-bundles";

        /// <summary>Maps the composite client resource bundle endpoint.</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The endpoint convention builder so that the call can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
        public static IEndpointConventionBuilder MapClientResourceBundles(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            return endpoints.MapGet(BundleRoutePath + "/{fileName}", (RequestDelegate)HandleRequestAsync);
        }

        private static async Task HandleRequestAsync(HttpContext context)
        {
            var fileName = context.Request.RouteValues.TryGetValue("fileName", out var routeValue)
                ? routeValue?.ToString()
                : null;

            var processor = context.RequestServices.GetService<ICompositeFileProcessor>();
            if (processor is null || !processor.TryGetBundle(fileName, out var content, out var contentType))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = contentType;
            context.Response.Headers.CacheControl = "public, max-age=31536000";
            await context.Response.WriteAsync(content).ConfigureAwait(false);
        }
    }
}
