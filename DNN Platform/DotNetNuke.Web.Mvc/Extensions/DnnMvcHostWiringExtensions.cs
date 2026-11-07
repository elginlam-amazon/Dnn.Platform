// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Common.Internal;
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// The host facing wiring of the DNN MVC module host. These two calls replace the
    /// <c>MvcHttpModule</c> HTTP module and the <c>DnnMvcRouteHandler</c> route handler which the
    /// legacy <c>RouteTable.Routes</c>/<c>web.config</c> registration used.
    /// </summary>
    /// <remarks>
    /// <para>The DNN host application wires the MVC module host up like this:</para>
    /// <code>
    /// // ConfigureServices: handled by DotNetNuke.Web.Mvc.Startup (IDnnStartup), which is discovered
    /// // by the DNN startup scan. Nothing else has to be registered by the host.
    ///
    /// // Configure:
    /// app.UseExceptionHandler("/Error");
    /// app.UseHsts();
    /// app.UseHttpsRedirection();
    /// app.UseStaticFiles();
    /// app.UseSession();              // MUST come before UseRouting: DnnMvcRouteHandler checks
    ///                                // session availability for the module request.
    /// app.UseRouting();
    /// app.UseRequestLocalization();
    /// app.UseAuthentication();       // MUST come before UseAuthorization.
    /// app.UseAuthorization();
    /// app.UseDnnMvcModuleHost();     // the former MvcHttpModule (DNN init + X-Frame-Options rule).
    ///                                // Place it after UseRouting so endpoint routing is in effect, and
    ///                                // before the endpoints are executed.
    /// app.UseEndpoints(endpoints =>
    /// {
    ///     endpoints.MapDnnMvcModuleRoutes();   // the named DNN MVC module endpoints.
    ///     endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
    /// });
    /// </code>
    /// <para>Ordering requirements which the host MUST honor:</para>
    /// <list type="number">
    /// <item><description><c>UseSession()</c> before <c>UseRouting()</c> —
    /// <c>DnnMvcRouteHandler.IsSessionAvailable</c> replaces the legacy
    /// <c>IRequiresSessionState</c>/<c>SetSessionStateBehavior</c> handling and needs the session feature.</description></item>
    /// <item><description><c>UseAuthentication()</c> before <c>UseAuthorization()</c>; the DNN
    /// authorization filters (<c>DnnAuthorizeAttribute</c>, <c>DnnModuleAuthorizeAttribute</c>,
    /// <c>RequireHostAttribute</c>) run inside the MVC pipeline and rely on the authenticated user.
    /// The original application had no global <c>AuthorizeAttribute</c>, so do NOT configure an
    /// authorization <c>FallbackPolicy</c>, and do not add a policy which would short-circuit the DNN
    /// filters.</description></item>
    /// <item><description><see cref="MapDnnMvcModuleRoutes"/> before any host catch-all/default route —
    /// the DNN module routes are named endpoints and the first match wins.</description></item>
    /// <item><description>The host's status-code/authentication middleware must honor the
    /// <c>AuthFilterContext.SuppressAuthenticationRedirectKey</c> flag in
    /// <c>HttpContext.Items</c>: when it is set, return the raw 401 instead of redirecting to the
    /// login page (this is what <c>AuthFilterContext.HandleUnauthorizedRequest</c> relies on).</description></item>
    /// <item><description><c>Startup.ConfigureServices</c> calls <c>AddHttpContextAccessor()</c>; the host
    /// must not remove it, because the module host control (<c>MvcHostControl</c>) injects
    /// <c>IHttpContextAccessor</c>.</description></item>
    /// </list>
    /// </remarks>
    public static class DnnMvcHostWiringExtensions
    {
        /// <summary>
        /// Maps the DNN MVC module routes (the routes contributed by every <see cref="IMvcRouteMapper"/>
        /// of the installed modules) as named endpoints. This replaces the legacy registration of
        /// <c>DnnMvcRouteHandler</c> based routes into <c>RouteTable.Routes</c>.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder of the host.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        /// <remarks>Must be called before the host maps its catch-all route; see the type remarks for the full ordering.</remarks>
        public static IEndpointRouteBuilder MapDnnMvcModuleRoutes(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            GetRoutingManager(endpoints.ServiceProvider).MapRoutes(endpoints);

            return endpoints;
        }

        /// <summary>Gets the MVC routing manager of the application.</summary>
        /// <param name="services">The application's service provider.</param>
        /// <returns>The <see cref="MvcRoutingManager"/>.</returns>
        private static MvcRoutingManager GetRoutingManager(IServiceProvider services)
        {
            // MvcRoutingManager is registered as one of the IRoutingManager implementations by
            // DotNetNuke.Web.Mvc.Startup.ConfigureServices.
            var fromContainer = services?.GetService<IEnumerable<IRoutingManager>>()
                ?.OfType<MvcRoutingManager>()
                .FirstOrDefault();

            // Not registered (the host built its own container without the DNN startup scan): construct
            // one, taking the IPortalAliasService from the container the endpoints were built with.
            return fromContainer
                ?? ActivatorUtilities.CreateInstance<MvcRoutingManager>(
                    services ?? DotNetNuke.Common.Globals.GetCurrentServiceProvider());
        }
    }
}
