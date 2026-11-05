// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Syndication
{
    using System.Threading.Tasks;

    using DotNetNuke.Services.Search.Controllers;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// The endpoint which serves the portal RSS feed. It is the ASP.NET Core replacement for the
    /// <c>RSSHandler</c> <c>IHttpHandler</c> registration in <c>web.config</c>
    /// (<c>path="RSS.aspx" verb="*"</c>).
    /// </summary>
    public static class RssEndpoint
    {
        /// <summary>The preserved RSS feed URL, so existing feed subscriptions keep working.</summary>
        public const string RssPath = "/RSS.aspx";

        /// <summary>
        /// The preserved RSS feed URL when it appears below a portal/tab folder, e.g.
        /// <c>/Home/RSS.aspx?moduleid=123</c>. The IIS handler mapping matched the file name at
        /// any folder depth, so both shapes must be routed.
        /// </summary>
        public const string NestedRssPath = "/{**dnnRssPrefix}/RSS.aspx";

        /// <summary>Maps the RSS feed endpoint onto its original URL(s).</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        public static IEndpointRouteBuilder MapDnnRss(this IEndpointRouteBuilder endpoints)
        {
            // Map (rather than MapGet): the handler was registered with verb="*".
            endpoints.Map(RssPath, HandleRssAsync);
            endpoints.Map(NestedRssPath, HandleRssAsync);
            return endpoints;
        }

        private static Task HandleRssAsync(HttpContext context)
        {
            // A new handler per request: the handler holds per-request state (channel, context).
            var handler = new RssHandler(context.RequestServices.GetRequiredService<ISearchController>());
            return handler.HandleRequestAsync(context);
        }
    }
}
