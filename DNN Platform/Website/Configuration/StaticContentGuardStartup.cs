// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Website.Configuration
{
    using DotNetNuke.DependencyInjection;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>
    /// Registers the static-content guards that replace <c>Portals/Web.config</c>,
    /// <c>DesktopModules/MVC/Web.config</c> and <c>Install/Web.config</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is an <see cref="IDnnStartup"/>, which <c>AddDnnPlatform</c> discovers and runs against
    /// the host's service collection. Registering through the DNN startup convention (rather than a
    /// line in <c>Program.cs</c>) is deliberate: a security control that depends on someone
    /// remembering to call it is a control that will eventually be dropped during a pipeline
    /// reorder. The guard installs itself through an <see cref="IStartupFilter"/>, so it runs ahead
    /// of every middleware the host composes — in particular ahead of <c>UseStaticFiles</c>.
    /// </para>
    /// </remarks>
    public sealed class StaticContentGuardStartup : IDnnStartup
    {
        /// <inheritdoc />
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions<StaticContentGuardOptions>()
                .BindConfiguration(StaticContentGuardOptions.SectionName);

            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IStartupFilter, StaticContentGuardStartupFilter>());
        }

        /// <summary>Inserts <see cref="StaticContentGuardMiddleware"/> at the head of the pipeline.</summary>
        private sealed class StaticContentGuardStartupFilter : IStartupFilter
        {
            /// <inheritdoc />
            public System.Action<IApplicationBuilder> Configure(System.Action<IApplicationBuilder> next)
            {
                return app =>
                {
                    app.UseMiddleware<StaticContentGuardMiddleware>();
                    next(app);
                };
            }
        }
    }
}
