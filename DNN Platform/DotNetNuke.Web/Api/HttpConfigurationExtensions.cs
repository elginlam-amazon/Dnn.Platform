// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

#nullable enable
namespace DotNetNuke.Web.Api
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
    /// <c>HttpConfiguration</c>, so the <c>HttpConfiguration.Properties["TabAndModuleInfoProvider"]</c>
    /// <see cref="ConcurrentQueue{T}"/> became a DI registered, ordered collection:
    /// <see cref="AddTabAndModuleInfoProvider(IServiceCollection, ITabAndModuleInfoProvider)"/> registers a
    /// provider and the <c>GetTabAndModuleInfoProviders</c> overloads return the registered providers in
    /// registration order, exactly like the legacy queue did (including the empty-array fallback).
    /// </summary>
    public static class HttpConfigurationExtensions
    {
        private static readonly ConcurrentQueue<ITabAndModuleInfoProvider> RuntimeProviders = new();

        /// <summary>Adds the <paramref name="tabAndModuleInfoProvider"/> to the service collection.</summary>
        /// <param name="services">The service collection.</param>
        /// <param name="tabAndModuleInfoProvider">The provider to add.</param>
        /// <returns>The <paramref name="services"/>, for chaining.</returns>
        public static IServiceCollection AddTabAndModuleInfoProvider(this IServiceCollection services, ITabAndModuleInfoProvider tabAndModuleInfoProvider)
        {
            Requires.NotNull("services", services);
            Requires.NotNull("tabAndModuleInfoProvider", tabAndModuleInfoProvider);

            if (!RuntimeProviders.Contains(tabAndModuleInfoProvider))
            {
                RuntimeProviders.Enqueue(tabAndModuleInfoProvider);
            }

            services.AddSingleton(tabAndModuleInfoProvider);
            return services;
        }

        /// <summary>
        /// Adds the <paramref name="tabAndModuleInfoProvider"/> after the container has been built (the
        /// container is immutable by then, so the provider is added to the runtime collection the
        /// <c>GetTabAndModuleInfoProviders</c> overloads also read).
        /// </summary>
        /// <param name="serviceProvider">The application's service provider.</param>
        /// <param name="tabAndModuleInfoProvider">The provider to add.</param>
        public static void AddTabAndModuleInfoProvider(this IServiceProvider serviceProvider, ITabAndModuleInfoProvider tabAndModuleInfoProvider)
        {
            Requires.NotNull("serviceProvider", serviceProvider);
            Requires.NotNull("tabAndModuleInfoProvider", tabAndModuleInfoProvider);

            if (!RuntimeProviders.Contains(tabAndModuleInfoProvider))
            {
                RuntimeProviders.Enqueue(tabAndModuleInfoProvider);
            }
        }

        /// <summary>Get the <see cref="ITabAndModuleInfoProvider"/> instances registered with the application.</summary>
        /// <param name="serviceProvider">The service provider of the current request scope, may be <see langword="null"/>.</param>
        /// <returns>A sequence of <see cref="ITabAndModuleInfoProvider"/> instances, in registration order.</returns>
        public static IEnumerable<ITabAndModuleInfoProvider> GetTabAndModuleInfoProviders(this IServiceProvider? serviceProvider)
        {
            var registered = serviceProvider?.GetServices<ITabAndModuleInfoProvider>().ToArray() ?? [];
            var runtime = RuntimeProviders.ToArray();
            if (runtime.Length == 0)
            {
                return registered;
            }

            // Registration order: the DI registered providers first, then the ones added at runtime.
            return registered.Concat(runtime.Where(provider => !registered.Contains(provider))).ToArray();
        }

        /// <summary>Get the <see cref="ITabAndModuleInfoProvider"/> instances for the current request.</summary>
        /// <param name="httpContext">The current request context.</param>
        /// <returns>A sequence of <see cref="ITabAndModuleInfoProvider"/> instances.</returns>
        public static IEnumerable<ITabAndModuleInfoProvider> GetTabAndModuleInfoProviders(this HttpContext? httpContext)
        {
            return GetTabAndModuleInfoProviders(httpContext?.RequestServices);
        }

        /// <summary>Get the <see cref="ITabAndModuleInfoProvider"/> instances for the given request.</summary>
        /// <param name="request">The current request.</param>
        /// <returns>A sequence of <see cref="ITabAndModuleInfoProvider"/> instances.</returns>
        public static IEnumerable<ITabAndModuleInfoProvider> GetTabAndModuleInfoProviders(this HttpRequest? request)
        {
            return GetTabAndModuleInfoProviders(request?.HttpContext?.RequestServices);
        }
    }
}
