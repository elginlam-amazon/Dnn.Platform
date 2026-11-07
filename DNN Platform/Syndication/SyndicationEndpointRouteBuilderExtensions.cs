// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Syndication
{
    using System;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Endpoint registration for the syndication feed handlers.</summary>
    /// <remarks>
    /// Replaces the <c>.ashx</c> HTTP handler registration. Call
    /// <see cref="MapGenericRssHandler(IEndpointRouteBuilder, string)"/> from the host's
    /// <c>Program.cs</c> with the path the feed used to be served from, so existing feed URLs keep
    /// working.
    /// </remarks>
    public static class SyndicationEndpointRouteBuilderExtensions
    {
        /// <summary>Maps the late-bound generic RSS handler to the given route pattern.</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="pattern">The route pattern, e.g. <c>/RSS.aspx</c> or <c>/Rss.ashx</c>.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        [CLSCompliant(false)]
        public static IEndpointConventionBuilder MapGenericRssHandler(this IEndpointRouteBuilder endpoints, string pattern)
        {
            return endpoints.MapGenericRssHandler(pattern, () => new GenericRssHttpHandlerBase());
        }

        /// <summary>Maps an RSS handler to the given route pattern.</summary>
        /// <typeparam name="THandler">The handler type.</typeparam>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="pattern">The route pattern, e.g. <c>/RSS.aspx</c> or <c>/Rss.ashx</c>.</param>
        /// <param name="handlerFactory">A factory which creates the handler for each request (the handler keeps per-request state, so it must not be shared).</param>
        /// <returns>The mapped endpoint, for further configuration.</returns>
        [CLSCompliant(false)]
        public static IEndpointConventionBuilder MapGenericRssHandler<THandler>(
            this IEndpointRouteBuilder endpoints,
            string pattern,
            Func<THandler> handlerFactory)
            where THandler : RssHttpHandlerBase<GenericRssChannel, GenericRssElement, GenericRssElement>
        {
            ArgumentNullException.ThrowIfNull(endpoints);
            ArgumentNullException.ThrowIfNull(handlerFactory);

            if (endpoints.ServiceProvider.GetService<RssHttpHandlerHelper>() is null)
            {
                throw new InvalidOperationException(
                    "The syndication services are not registered. Call services.AddDnnSyndication() before mapping a syndication endpoint.");
            }

            // MapMethods rather than MapGet: the .ashx handler this replaces answered any verb, and
            // feed readers routinely issue HEAD requests to check freshness.
            return endpoints.MapMethods(
                pattern,
                new[] { HttpMethods.Get, HttpMethods.Head },
                (HttpContext context) => handlerFactory().HandleRequestAsync(context));
        }
    }
}
