// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Registers and initializes the services the DNN Web Utility library needs.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the services the DNN Web Utility library needs: the memory cache that replaces
        /// <c>HttpRuntime.Cache</c>, the <c>IHttpContextAccessor</c> that replaces
        /// <c>HttpContext.Current</c> and the <see cref="DnnWebUtilityOptions"/> that replace the
        /// <c>ClientAPI</c> app setting.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="configuration">The configuration holding the <c>DnnWebUtility</c> section.</param>
        /// <returns>The <paramref name="services"/>, for chaining.</returns>
        public static IServiceCollection AddDnnWebUtility(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddMemoryCache();
            services.AddHttpContextAccessor();
            services.Configure<DnnWebUtilityOptions>(configuration.GetSection(DnnWebUtilityOptions.SectionName));

            return services;
        }

        /// <summary>
        /// Initializes the library's static accessors from the application's services, so that the
        /// static Client API members can reach the current request, the cache and the settings.
        /// </summary>
        /// <param name="app">The application builder of the host.</param>
        /// <returns>The <paramref name="app"/>, for chaining.</returns>
        /// <remarks>
        /// The callback pipeline resolves the page it dispatches to from the request services, so the
        /// host must register its own <see cref="IClientApiPage"/> implementation (typically scoped) for
        /// Client API callbacks to be handled. Hosts that build the page themselves should call the
        /// <see cref="UseDnnWebUtility(IApplicationBuilder, Func{HttpContext, IClientApiPage})"/>
        /// overload instead. Either way the public <see cref="ClientAPI.IsInCallback(HttpContext)"/> and
        /// <see cref="ClientAPI.HandleClientAPICallbackEvent(IClientApiPage)"/> entry points remain
        /// available for a host that drives the callback itself, as the WebForms page lifecycle did.
        /// </remarks>
        public static IApplicationBuilder UseDnnWebUtility(this IApplicationBuilder app)
        {
            return app.UseDnnWebUtility(context => context.RequestServices?.GetService<IClientApiPage>());
        }

        /// <summary>
        /// Initializes the library's static accessors and wires the Client API callback pipeline to a
        /// host supplied factory that produces the <see cref="IClientApiPage"/> a callback targets.
        /// </summary>
        /// <param name="app">The application builder of the host.</param>
        /// <param name="pageFactory">
        /// Produces the page a callback request targets, or <see langword="null"/> when the host has no
        /// page for the request, in which case the request continues down the pipeline.
        /// </param>
        /// <returns>The <paramref name="app"/>, for chaining.</returns>
        public static IApplicationBuilder UseDnnWebUtility(this IApplicationBuilder app, Func<HttpContext, IClientApiPage> pageFactory)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(pageFactory);

            DnnWebUtilityContext.Initialize(app.ApplicationServices);

            // Keeps the Client API callback pipeline reachable: a callback request is recognized
            // here and dispatched to the control it targets through HandleClientAPICallbackEvent,
            // which writes the ClientAPICallBackResponse and completes the response. Requests that
            // are not callbacks, and callbacks for which the host does not supply a page, continue
            // down the pipeline untouched.
            app.Use(async (context, next) =>
            {
                if (ClientAPI.IsInCallback(context))
                {
                    var page = pageFactory(context);
                    if (page is not null)
                    {
                        ClientAPI.HandleClientAPICallbackEvent(page);
                        return;
                    }
                }

                await next().ConfigureAwait(false);
            });

            return app;
        }
    }
}
