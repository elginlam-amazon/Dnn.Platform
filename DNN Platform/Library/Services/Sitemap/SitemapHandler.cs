// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Sitemap
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    using DotNetNuke.Entities.Portals;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    /// <summary>
    /// Serves the portal sitemap (and the individual files of a sitemap index). This is the
    /// ASP.NET Core replacement for the <c>SitemapHandler</c> <c>IHttpHandler</c> which was
    /// registered in <c>web.config</c> for <c>path="Sitemap.aspx" verb="*"</c>.
    /// </summary>
    public static class SitemapHandler
    {
        /// <summary>The preserved sitemap URL, so search engines keep finding the sitemap.</summary>
        public const string SitemapPath = "/Sitemap.aspx";

        /// <summary>
        /// The preserved sitemap URL when it appears below a portal/tab folder, e.g.
        /// <c>/MyChildPortal/Sitemap.aspx</c>. The IIS handler mapping matched the file name at
        /// any folder depth, so both shapes are routed.
        /// </summary>
        public const string NestedSitemapPath = "/{**dnnSitemapPrefix}/Sitemap.aspx";

        private const int StreamBufferSize = 16 * 1024;

        /// <summary>Maps the sitemap endpoint onto its original URL(s).</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        public static IEndpointRouteBuilder MapDnnSitemap(this IEndpointRouteBuilder endpoints)
        {
            // Map (rather than MapGet): the handler was registered with verb="*".
            endpoints.Map(SitemapPath, ProcessRequestAsync);
            endpoints.Map(NestedSitemapPath, ProcessRequestAsync);
            return endpoints;
        }

        /// <summary>Writes the sitemap XML to the response.</summary>
        /// <param name="context">The HTTP context of the request.</param>
        /// <returns>A task which completes once the sitemap has been written.</returns>
        public static async Task ProcessRequestAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            try
            {
                var response = context.Response;

                // ContentType + ContentEncoding(UTF8) in one header, which is how ASP.NET Core
                // expresses the response encoding.
                response.ContentType = "text/xml; charset=utf-8";

                var builder = new SitemapBuilder(PortalSettings.Current);

                // encoderShouldEmitUTF8Identifier: false — the net48 handler wrote through
                // Response.Output, which did not emit a byte-order mark.
                var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                await using (var output = new StreamWriter(response.Body, encoding, StreamBufferSize, leaveOpen: true))
                {
                    var index = context.Request.Query["i"];
                    if (index.Count > 0)
                    {
                        // This is a request for one of the files that build the sitemapindex
                        builder.GetSitemapIndexFile(index.ToString(), output);
                    }
                    else
                    {
                        builder.BuildSiteMap(output);
                    }

                    await output.FlushAsync().ConfigureAwait(false);
                }

                await response.Body.FlushAsync(context.RequestAborted).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Exceptions.Exceptions.LogException(exc);
            }
        }
    }
}
