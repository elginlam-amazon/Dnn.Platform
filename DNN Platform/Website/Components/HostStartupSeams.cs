// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Website.Components
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Common;
    using DotNetNuke.Framework;
    using DotNetNuke.Services.Cache;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// The host's guard over the four <see langword="static"/> seams the platform resolves its
    /// ambient state through. They are wired by <c>AddDnnPlatform</c>/<c>UseDnnPlatform</c>; this
    /// type asserts the wiring actually happened, so a future reorder of <c>Program.cs</c> fails
    /// loudly at start-up instead of serving blank pages or logging a trace nobody reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On .NET Framework these four were ambient statics that the runtime itself populated:
    /// <c>HttpContext.Current</c> (<see cref="HttpContextSource"/>), <c>HttpRuntime.Cache</c>
    /// (<see cref="MemoryCacheSource"/>), <c>ConfigurationManager</c>
    /// (<see cref="ConfigurationSource"/>) and the <c>ServiceLocator</c>/<c>Globals</c> container
    /// bridge. On ASP.NET Core nothing populates them automatically, and every one of them is read
    /// from code that cannot take constructor injection.
    /// </para>
    /// <para>
    /// There is exactly ONE container: everything below reads <c>app.Services</c>. No service is
    /// registered here and no second provider is built.
    /// </para>
    /// </remarks>
    internal static class HostStartupSeams
    {
        /// <summary>
        /// Guarantees the static configuration seam is wired, before anything in the platform can
        /// read a setting.
        /// </summary>
        /// <param name="app">The built application.</param>
        /// <remarks>
        /// <c>AddDnnPlatformConfiguration</c> (reached through <c>AddDnnPlatform</c>) already calls
        /// <see cref="ConfigurationSource.RegisterConfiguration"/> with
        /// <c>builder.Configuration</c>. Re-registering the <see cref="IConfiguration"/> resolved
        /// from <c>app.Services</c> is idempotent — it is the same singleton — and keeps the seam
        /// alive even if the registration call moves. Without it
        /// <see cref="ConfigurationSource.Current"/> traces an error once and the value-returning
        /// helpers throw, which renders blank pages.
        /// </remarks>
        internal static void EnsureConfigurationSource(WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);

            ConfigurationSource.RegisterConfiguration(app.Services.GetRequiredService<IConfiguration>());
        }

        /// <summary>
        /// Asserts every static seam is wired and that the container bridge actually resolves,
        /// throwing a single actionable exception when one is missing.
        /// </summary>
        /// <param name="app">The built application, after <c>UseDnnPlatform</c> has run.</param>
        /// <exception cref="InvalidOperationException">A seam was not initialized.</exception>
        internal static void VerifyStaticSeams(WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);

            var missing = new List<string>();

            // HttpContext.Current replacement: the accessor AND the environment (virtual-path
            // mapping, MapPath, ApplicationVirtualPath).
            if (!HttpContextSource.IsRegistered)
            {
                missing.Add(
                    $"{nameof(HttpContextSource)}.{nameof(HttpContextSource.RegisterAccessor)}(IHttpContextAccessor) " +
                    "was not called; every HttpContext.Current replacement in the platform would read as null.");
            }

            if (HttpContextSource.WebHostEnvironment is null)
            {
                missing.Add(
                    $"{nameof(HttpContextSource)}.{nameof(HttpContextSource.RegisterWebHostEnvironment)}(IWebHostEnvironment) " +
                    "was not called; Server.MapPath/virtual-path resolution would fail.");
            }

            // HttpRuntime.Cache replacement.
            if (!MemoryCacheSource.IsRegistered)
            {
                missing.Add(
                    $"{nameof(MemoryCacheSource)}.{nameof(MemoryCacheSource.RegisterInstance)}(IMemoryCache) " +
                    "was not called; the platform's caching layer has no backing store.");
            }

            // ConfigurationManager replacement.
            if (!ConfigurationSource.IsRegistered)
            {
                missing.Add(
                    $"{nameof(ConfigurationSource)}.{nameof(ConfigurationSource.RegisterConfiguration)}(IConfiguration) " +
                    "was not called; every setting in the platform would read as unset.");
            }

            // The container bridge. Both halves matter: ServiceLocator<,>.Instance reads
            // ServiceLocator.ServiceProvider, and PortalModuleBase resolves module services
            // through ServiceScopeContainer.GetRequestOrCreateScope() off Globals.DependencyProvider.
            if (!ServiceLocator.IsInitialized)
            {
                missing.Add(
                    $"{nameof(ServiceLocator)}.{nameof(ServiceLocator.SetServiceProvider)}(app.Services) " +
                    "was not called; every ServiceLocator<,>.Instance would fall back to its legacy factory.");
            }

            // Globals.DependencyProvider is internal to the platform, so it cannot be read from
            // here; it is assigned by the very same call that sets ServiceLocator
            // (DnnSecurityServiceCollectionExtensions.UseDnnPlatformServices), so the check above
            // covers both halves of the bridge. What CAN be verified from outside is that the
            // bridge resolves — which is what the resolutions below do.
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "The DNN host is mis-composed: " + string.Join(" ", missing) +
                    " These seams are wired by AddDnnPlatform/UseDnnPlatform — restore those calls in Program.cs.");
            }

            // Prove the bridge resolves rather than merely being assigned: these are the services
            // the very first request needs, and a missing registration must surface here and not
            // as a blank page on the first hit.
            _ = app.Services.GetRequiredService<IHttpContextAccessor>();
            _ = app.Services.GetRequiredService<IWebHostEnvironment>();
            _ = app.Services.GetRequiredService<IMemoryCache>();
            _ = ServiceLocator.ServiceProvider.GetRequiredService<IConfiguration>();
        }
    }
}
