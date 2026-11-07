// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.HttpModules
{
    using DotNetNuke.Common;
    using DotNetNuke.HttpModules.Analytics;
    using DotNetNuke.HttpModules.DependencyInjection;
    using DotNetNuke.HttpModules.Exceptions;
    using DotNetNuke.HttpModules.Localization;
    using DotNetNuke.HttpModules.Membership;
    using DotNetNuke.HttpModules.OutputCaching;
    using DotNetNuke.HttpModules.Personalization;
    using DotNetNuke.HttpModules.RequestFilter;
    using DotNetNuke.HttpModules.Services;
    using DotNetNuke.HttpModules.UsersOnline;

    using Microsoft.AspNetCore.Builder;

    /// <summary>
    /// The host application's entry point into the request pipeline stages that used to be the
    /// <c>IHttpModule</c> registrations of <c>DotNetNuke.HttpModules</c> in
    /// <c>web.config/&lt;system.webServer&gt;/&lt;modules&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>DotNetNuke.Library</c> cannot reference this project, so <c>UseDnnPlatform</c> cannot
    /// add these middleware itself. Instead it exposes two hook delegates, and this project
    /// exposes exactly two extension methods that fill them — one per pipeline position that the
    /// original <c>HttpApplication</c> events map to:
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="UseDnnEarlyRequestMiddleware"/> — the <c>BeginRequest</c> modules, which must run before routing;</description></item>
    /// <item><description><see cref="UseDnnPostAuthenticationMiddleware"/> — the <c>AuthenticateRequest</c>/<c>AuthorizeRequest</c>/cache/<c>EndRequest</c> modules, which must run once the user is known.</description></item>
    /// </list>
    /// <para>
    /// Neither method adds routing, authentication or authorization: those are owned by
    /// <c>UseDnnPlatform</c> and adding them twice would run the whole pipeline stage twice.
    /// </para>
    /// </remarks>
    public static class DnnHttpModulesApplicationExtensions
    {
        /// <summary>
        /// Adds the middleware that replace the <c>BeginRequest</c> HTTP modules, in the order in
        /// which those modules were registered in <c>web.config</c>.
        /// </summary>
        /// <param name="app">The host's application builder.</param>
        /// <returns>The same <paramref name="app"/>, for chaining.</returns>
        /// <remarks>
        /// Must be installed BEFORE <c>UseRouting</c>: the DI scope has to exist before anything
        /// else resolves a scoped DNN service, the request filter must be able to block a request
        /// before an endpoint is selected, and the mobile redirect must answer with a 302 instead
        /// of letting the page render.
        /// <list type="number">
        /// <item><description><see cref="Exceptions.DnnUnhandledExceptionMiddleware"/> — writes unhandled failures to the DNN exception log (<c>ExceptionModule</c>, <c>Application_Error</c>), then re-throws so <c>UseDnnExceptionHandler</c> still renders the error page.</description></item>
        /// <item><description><see cref="DnnServiceScopeMiddleware"/> — creates and publishes the per-request DI scope (<c>ServiceRequestScopeModule</c>).</description></item>
        /// <item><description><see cref="DnnRequestFilterMiddleware"/> — the host's request-filter rules (<c>RequestFilterModule</c>).</description></item>
        /// <item><description><see cref="DnnServicesFrameworkMiddleware"/> — Services Framework initialization and the <c>Server</c> header scrub (<c>ServicesModule</c>).</description></item>
        /// </list>
        /// <para>
        /// <c>DnnMobileRedirectMiddleware</c> was also a <c>BeginRequest</c> module, but its rules
        /// read the resolved portal and active tab, so it lives in
        /// <see cref="UseDnnPostAuthenticationMiddleware"/> instead — see the note there.
        /// </para>
        /// </remarks>
        public static IApplicationBuilder UseDnnEarlyRequestMiddleware(this IApplicationBuilder app)
        {
            Requires.NotNull(nameof(app), app);

            // First, so that it observes every failure raised by anything downstream. It runs
            // inside UseDnnExceptionHandler's scope (the hook is invoked after it), which is why
            // the exception log keeps receiving entries that the legacy ExceptionModule wrote.
            app.UseDnnUnhandledExceptionLogging();

            app.UseMiddleware<DnnServiceScopeMiddleware>();
            app.UseMiddleware<DnnRequestFilterMiddleware>();
            app.UseMiddleware<DnnServicesFrameworkMiddleware>();

            return app;
        }

        /// <summary>
        /// Adds the middleware that replace the HTTP modules which needed the authenticated user,
        /// the resolved portal/tab or the end of the request.
        /// </summary>
        /// <param name="app">The host's application builder.</param>
        /// <returns>The same <paramref name="app"/>, for chaining.</returns>
        /// <remarks>
        /// Must be installed immediately AFTER <c>UseDnnUrlRewriter</c> (which itself runs after
        /// <c>UseAuthorization</c>) and therefore still before the endpoints. The rewriter is what
        /// publishes the resolved <c>PortalSettings</c> into <c>HttpContext.Items</c>, and every
        /// middleware below reads it:
        /// <list type="number">
        /// <item><description><see cref="DnnMembershipMiddleware"/> — DNN membership authentication (<c>MembershipModule.OnAuthenticateRequest</c>).</description></item>
        /// <item><description><see cref="DnnMobileRedirectMiddleware"/> — the mobile/device redirection rules (<c>MobileRedirectModule</c>); a <c>BeginRequest</c> module whose rules nevertheless need the resolved portal and <c>TabController.CurrentPage</c>, so it sits here and still short-circuits with its 302 before any endpoint runs.</description></item>
        /// <item><description><see cref="DnnUsersOnlineMiddleware"/> — users-online tracking (<c>UsersOnlineModule</c>, <c>AuthorizeRequest</c>).</description></item>
        /// <item><description><see cref="DnnRequestLocalizationMiddleware"/> — per-request thread cultures from the page locale (<c>LocalizationModule</c>).</description></item>
        /// <item><description><see cref="DnnPersonalizationMiddleware"/> — saves the personalization profile after the response (<c>PersonalizationModule</c>, <c>EndRequest</c>).</description></item>
        /// <item><description><see cref="DnnOutputCacheMiddleware"/> — per-tab output caching; wraps the endpoint execution (<c>OutputCacheModule</c>, <c>ResolveRequestCache</c>/<c>UpdateRequestCache</c>).</description></item>
        /// <item><description><see cref="DnnAnalyticsMiddleware"/> — injects the configured analytics scripts into the rendered page (<c>AnalyticsModule</c>).</description></item>
        /// </list>
        /// The two response-rewriting middleware come last on purpose: each one wraps
        /// <c>Response.Body</c>, and the analytics injection has to see the HTML that the output
        /// cache will store.
        /// </remarks>
        public static IApplicationBuilder UseDnnPostAuthenticationMiddleware(this IApplicationBuilder app)
        {
            Requires.NotNull(nameof(app), app);

            app.UseMiddleware<DnnMembershipMiddleware>();
            app.UseMiddleware<DnnMobileRedirectMiddleware>();

            // DnnUsersOnlineMiddleware is marked deprecated upstream (users-online tracking is
            // scheduled for removal in v11), but it is still the functional replacement for the
            // legacy UsersOnlineModule, so the registration has to stay for behavioral parity.
#pragma warning disable CS0618 // Type or member is obsolete
            app.UseMiddleware<DnnUsersOnlineMiddleware>();
#pragma warning restore CS0618 // Type or member is obsolete
            app.UseMiddleware<DnnRequestLocalizationMiddleware>();
            app.UseMiddleware<DnnPersonalizationMiddleware>();
            app.UseMiddleware<DnnOutputCacheMiddleware>();
            app.UseMiddleware<DnnAnalyticsMiddleware>();

            return app;
        }
    }
}
