// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common
{
    using DotNetNuke.Common.Options;

    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Wires the platform's configuration seam and options into the host's service collection.</summary>
    public static class DnnConfigurationServiceCollectionExtensions
    {
        /// <summary>
        /// Registers every configuration section the platform reads, and initializes the static
        /// <see cref="ConfigurationSource"/> seam that the platform's many static call sites use
        /// in place of the removed <c>ConfigurationManager</c>.
        /// </summary>
        /// <param name="services">The host's service collection.</param>
        /// <param name="configuration">The host's configuration.</param>
        /// <returns>The same <paramref name="services"/>, for chaining.</returns>
        /// <remarks>
        /// Call this from <c>Startup</c>/<c>Program</c> before anything else in the platform runs:
        /// <c>builder.Services.AddDnnPlatformConfiguration(builder.Configuration);</c>.
        /// </remarks>
        public static IServiceCollection AddDnnPlatformConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            Requires.NotNull(nameof(services), services);
            Requires.NotNull(nameof(configuration), configuration);

            // The static seam: must happen here, because the platform reads configuration from
            // static members that cannot be constructor-injected.
            ConfigurationSource.RegisterConfiguration(configuration);

            services.Configure<MachineKeyOptions>(configuration.GetSection(MachineKeyOptions.SectionName));
            services.Configure<HttpRuntimeOptions>(configuration.GetSection(HttpRuntimeOptions.SectionName));
            services.Configure<CustomErrorsOptions>(configuration.GetSection(CustomErrorsOptions.SectionName));
            services.Configure<MobileRedirectOptions>(configuration.GetSection(MobileRedirectOptions.SectionName));
            services.Configure<ImageHandlerOptions>(configuration.GetSection(ImageHandlerOptions.SectionName));

            // <httpRuntime maxRequestLength="..."> used to cap uploads; on ASP.NET Core that is
            // FormOptions plus the server's own request-body limit.
            var httpRuntime = new HttpRuntimeOptions();
            configuration.GetSection(HttpRuntimeOptions.SectionName).Bind(httpRuntime);
            var maxRequestLengthBytes = httpRuntime.MaxRequestLengthBytes;

            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = maxRequestLengthBytes;
                options.MemoryBufferThreshold = (int)System.Math.Min(
                    httpRuntime.RequestLengthDiskThreshold * 1024L,
                    int.MaxValue);
            });

            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = maxRequestLengthBytes;
            });

            return services;
        }
    }
}
