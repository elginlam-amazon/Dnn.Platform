// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Registration and retrieval of the <see cref="ITabAndModuleInfoProvider"/>s. ASP.NET Core has no
    /// <c>HttpConfiguration</c>, so the configuration-property collection became a DI registered collection:
    /// <see cref="AddTabAndModuleInfoProvider(IServiceCollection, ITabAndModuleInfoProvider)"/> registers a
    /// provider and the <c>GetTabAndModuleInfoProviders</c> overloads return the registered providers in
    /// registration order, exactly like the legacy <c>ConcurrentQueue</c> did.
    /// </summary>
    public static class HttpConfigurationExtensions
    {
        private static readonly ConcurrentQueue<ITabAndModuleInfoProvider> FallbackProviders = new();

        /// <summary>Adds a tab and module info provider to the service collection.</summary>
        /// <param name="services">The service collection.</param>
        /// <param name="tabAndModuleInfoProvider">The provider to register.</param>
        /// <returns>The <paramref name="services"/>, for chaining.</returns>
        public static IServiceCollection AddTabAndModuleInfoProvider(this IServiceCollection services, ITabAndModuleInfoProvider tabAndModuleInfoProvider)
        {
            Requires.NotNull("services", services);
            Requires.NotNull("tabAndModuleInfoProvider", tabAndModuleInfoProvider);

            if (!FallbackProviders.Contains(tabAndModuleInfoProvider))
            {
                FallbackProviders.Enqueue(tabAndModuleInfoProvider);
            }

            services.AddSingleton(tabAndModuleInfoProvider);
            return services;
        }

        /// <summary>Gets the registered tab and module info providers.</summary>
        /// <param name="serviceProvider">The service provider of the current request scope, may be <see langword="null"/>.</param>
        /// <returns>The registered providers.</returns>
        public static IEnumerable<ITabAndModuleInfoProvider> GetTabAndModuleInfoProviders(this IServiceProvider serviceProvider)
        {
            var providers = serviceProvider?.GetServices<ITabAndModuleInfoProvider>().ToArray();
            if (providers is { Length: > 0 })
            {
                return providers;
            }

            // Shouldn't ever happen outside of unit tests or background (non-request) code paths.
            return FallbackProviders.ToArray();
        }

        /// <summary>Gets the registered tab and module info providers for the current request.</summary>
        /// <param name="httpContext">The current request context.</param>
        /// <returns>The registered providers.</returns>
        public static IEnumerable<ITabAndModuleInfoProvider> GetTabAndModuleInfoProviders(this HttpContext httpContext)
        {
            return GetTabAndModuleInfoProviders(httpContext?.RequestServices);
        }

        /// <summary>Gets the registered tab and module info providers for the given request.</summary>
        /// <param name="request">The current request.</param>
        /// <returns>The registered providers.</returns>
        public static IEnumerable<ITabAndModuleInfoProvider> GetTabAndModuleInfoProviders(this HttpRequest request)
        {
            return GetTabAndModuleInfoProviders(request?.HttpContext?.RequestServices);
        }
    }
}
