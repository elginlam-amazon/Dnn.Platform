// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Syndication
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;

    /// <summary>Dependency injection registration for the syndication services.</summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>Registers the DNN syndication services.</summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The <paramref name="services"/>, for chaining.</returns>
        /// <remarks>
        /// The host MUST call this at start-up: it supplies the ambient
        /// <see cref="IHttpContextAccessor"/> and the cache-path resolver used by the library's
        /// static entry points (which previously relied on <c>HttpContext.Current</c>), and it
        /// registers <see cref="RssHttpHandlerHelper"/> and <see cref="RssDataSource"/> so that the
        /// feed endpoints and the <c>RssHyperLink</c> component can resolve them.
        /// </remarks>
        public static IServiceCollection AddDnnSyndication(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddHttpContextAccessor();
            services.AddDataProtection();

            services.TryAddSingleton<RssHttpHandlerHelper>();
            services.TryAddTransient<RssDataSource>();

            // Initialize the library's static entry points once the application's services exist.
            services.AddSingleton<IHostedService, SyndicationInitializer>();

            return services;
        }

        /// <summary>Initializes the syndication library's ambient context at application start-up.</summary>
        private sealed class SyndicationInitializer : IHostedService
        {
            private readonly IHttpContextAccessor httpContextAccessor;
            private readonly IWebHostEnvironment environment;

            /// <summary>Initializes a new instance of the <see cref="SyndicationInitializer"/> class.</summary>
            /// <param name="httpContextAccessor">The HTTP context accessor.</param>
            /// <param name="environment">The hosting environment, used to resolve the feed cache directories.</param>
            public SyndicationInitializer(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment environment)
            {
                this.httpContextAccessor = httpContextAccessor;
                this.environment = environment;
            }

            /// <inheritdoc />
            public Task StartAsync(CancellationToken cancellationToken)
            {
                SyndicationRequestContext.Configure(this.httpContextAccessor);

                var contentRoot = this.environment.ContentRootPath ?? AppContext.BaseDirectory;
                SyndicationCachePath.Configure(appRelativePath => SyndicationCachePath.Combine(contentRoot, appRelativePath));

                return Task.CompletedTask;
            }

            /// <inheritdoc />
            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
