// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Entities.Urls
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Entities.Portals;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Runs DNN's URL rewriter for every request. This is the ASP.NET Core replacement for the
    /// <c>UrlRewriteModule</c> <c>IHttpModule</c> that used to hook <c>HttpApplication.BeginRequest</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The middleware resolves the portal alias, tab and module for the incoming URL, rewrites
    /// <see cref="HttpRequest.Path"/>/<see cref="HttpRequest.QueryString"/> into DNN's canonical
    /// <c>/Default.aspx?TabId=…</c> shape and publishes the resolved
    /// <see cref="PortalSettings"/> into <c>HttpContext.Items</c> under
    /// <see cref="PortalController.PortalSettingsContextKey"/> — which is the single slot
    /// <see cref="PortalSettings.Current"/> reads, so every downstream tenant-aware lookup
    /// depends on this middleware having run.
    /// </para>
    /// <para>
    /// <b>Pipeline position:</b> it must run AFTER <c>UseRouting()</c>,
    /// <c>UseAuthentication()</c> and <c>UseAuthorization()</c> (the rewriter reads
    /// <see cref="HttpContext.User"/> and its DNN auth-token claim) and BEFORE the endpoint
    /// middleware (<c>MapRazorComponents</c>/<c>MapControllers</c>), because it decides which
    /// endpoint the request reaches. It must also run after <c>UseStaticFiles()</c> so that
    /// static assets are served without paying for a rewrite.
    /// </para>
    /// </remarks>
    public sealed class DnnUrlRewriterMiddleware
    {
        private readonly RequestDelegate next;

        /// <summary>Initializes a new instance of the <see cref="DnnUrlRewriterMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public DnnUrlRewriterMiddleware(RequestDelegate next)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>Rewrites the current request and then invokes the rest of the pipeline.</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="portalAliasService">The portal alias service.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="portalGroupController">The portal group controller.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public async Task InvokeAsync(
            HttpContext context,
            IHostSettings hostSettings,
            IPortalAliasService portalAliasService,
            IHostSettingsService hostSettingsService,
            IPortalController portalController,
            IApplicationStatusInfo appStatus,
            IPortalGroupController portalGroupController)
        {
            ArgumentNullException.ThrowIfNull(context);

            // The rewriter the installation is configured for. DotNetNuke.HttpModules registers
            // UrlRewriterBase from its UrlRewriterFactory, which picks the basic or the advanced
            // rewriter from Config.GetFriendlyUrlProvider() — so a site using the basic provider
            // gets BasicUrlRewriter here. When that registration is absent (a host that does not
            // reference DotNetNuke.HttpModules), fall back to the advanced rewriter, which is the
            // DNN default.
            var rewriter = context.RequestServices?.GetService<UrlRewriterBase>()
                           ?? new AdvancedUrlRewriter(
                               hostSettings,
                               portalAliasService,
                               hostSettingsService,
                               portalController,
                               appStatus,
                               portalGroupController);

            rewriter.RewriteUrl(context);

            // The rewriter cannot write to the response body itself (ASP.NET Core disallows
            // synchronous body writes), so it buffers any error-page or client-redirect HTML it
            // produced; flush it here.
            if (context.Items.TryGetValue(AdvancedUrlRewriter.PendingOutputKey, out var pendingOutput)
                && pendingOutput is string output
                && output.Length > 0)
            {
                context.Items.Remove(AdvancedUrlRewriter.PendingOutputKey);
                await context.Response.WriteAsync(output);
            }

            if (RequestWasCompleted(context))
            {
                // Replaces the legacy Response.End() / Response.Redirect(url, endResponse: true):
                // the rewriter fully handled the request, so no endpoint must run.
                return;
            }

            await this.next(context);
        }

        private static bool RequestWasCompleted(HttpContext context)
        {
            if (context.Response.HasStarted)
            {
                return true;
            }

            if (context.Items.TryGetValue(AdvancedUrlRewriter.RequestCompletedKey, out var completed)
                && completed is true)
            {
                return true;
            }

            // A redirect written by the rewriter (301/302) is a complete response.
            return context.Response.StatusCode is >= 300 and <= 399
                   && context.Response.Headers.ContainsKey("Location");
        }
    }

    /// <summary>Extension methods for adding <see cref="DnnUrlRewriterMiddleware"/> to the pipeline.</summary>
    public static class DnnUrlRewriterMiddlewareExtensions
    {
        /// <summary>Adds DNN's URL rewriter to the request pipeline.</summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The same <paramref name="app"/>, for chaining.</returns>
        /// <remarks>
        /// Call this after <c>UseStaticFiles()</c>, <c>UseRouting()</c>,
        /// <c>UseAuthentication()</c> and <c>UseAuthorization()</c>, and before the endpoint
        /// middleware. See <see cref="DnnUrlRewriterMiddleware"/> for the rationale.
        /// </remarks>
        public static IApplicationBuilder UseDnnUrlRewriter(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            return app.UseMiddleware<DnnUrlRewriterMiddleware>();
        }
    }
}
