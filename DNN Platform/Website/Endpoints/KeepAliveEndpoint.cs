// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable once CheckNamespace
namespace DotNetNuke.Common.Utilities
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    /// <summary>
    /// The endpoint which replaces <c>KeepAlive.aspx</c>: a request handler that does minimal work
    /// but keeps the site's application pool active.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was a <c>System.Web.UI.Page</c> (<c>DotNetNuke.Common.Utilities.KeepAlive</c>) whose
    /// entire server side behaviour was <c>OnInit</c> → <c>InitializeComponent()</c> (an empty
    /// designer method) and <c>OnLoad</c> → <c>base.OnLoad(e)</c>. Both are reproduced below:
    /// there is nothing left to do once the WebForms page lifecycle is gone, which is exactly why
    /// this is a minimal API endpoint and not a component. Merely being routed and executed is
    /// what warms the application - every DNN service graph initialization happens in the host's
    /// startup path, which this request goes through.
    /// </para>
    /// <para>
    /// The response body is the markup the .aspx produced verbatim - an <c>html</c> document whose
    /// <c>meta http-equiv="refresh"</c> makes the browser (or an uptime monitor rendering the page)
    /// re-request the URL every 300 seconds, followed by <c>DateTime.Now</c>. External warm-up
    /// monitors are configured with this URL, so the path stays the literal legacy
    /// <c>/KeepAlive.aspx</c>.
    /// </para>
    /// </remarks>
    public static class KeepAliveEndpoint
    {
        /// <summary>The preserved keep-alive URL, so existing monitors and warm-up rules keep working.</summary>
        public const string KeepAlivePath = "/KeepAlive.aspx";

        /// <summary>Maps the keep-alive endpoint onto its original URL.</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        public static IEndpointRouteBuilder MapDnnKeepAlive(this IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(KeepAlivePath, HandleKeepAliveAsync);
            return endpoints;
        }

        private static async Task HandleKeepAliveAsync(HttpContext context)
        {
            // Was: the page's <head> meta refresh and <%=DateTime.Now%> expression. The page also
            // emitted no cache directives of its own; a keep-alive ping must never be served from a
            // cache, otherwise it stops reaching the application at all.
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store, no-cache";

            await context.Response.WriteAsync("<html>\r\n");
            await context.Response.WriteAsync("<head><meta http-equiv=\"refresh\" content=\"300\"></head>\r\n");
            await context.Response.WriteAsync(DateTime.Now.ToString(CultureInfo.CurrentCulture));
            await context.Response.WriteAsync("\r\n</html>\r\n");
        }
    }
}
