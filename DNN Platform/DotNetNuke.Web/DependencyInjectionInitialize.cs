// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web
{
    using System;

    using DotNetNuke.Common;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Exposes the application's service registrations for diagnostics.</summary>
    /// <remarks>
    /// <para>
    /// This type used to BUILD A SECOND SERVICE PROVIDER: <c>BuildServiceProvider()</c> created a fresh
    /// <see cref="ServiceCollection"/>, ran every <c>IDnnStartup</c> against it and built a provider which
    /// the <c>DotNetNukeHttpApplication</c> static constructor then published as
    /// <c>Globals.DependencyProvider</c>. The application therefore had two containers with two sets of
    /// singletons.
    /// </para>
    /// <para>
    /// There is now exactly ONE container: the host's, built by
    /// <c>builder.Services.AddDnnPlatform(...)</c> in <c>Website/Program.cs</c>, which runs every
    /// <c>IDnnStartup</c> (including this assembly's) against the HOST service collection.
    /// </para>
    /// </remarks>
    public static class DependencyInjectionInitialize
    {
        /// <summary>Gets the host's service collection (for logging/diagnostics, e.g. the Prompt <c>list-services</c> command).</summary>
        internal static IServiceCollection ServiceCollection { get; private set; }

        /// <summary>Gets the application's service provider.</summary>
        /// <returns>The host's <see cref="IServiceProvider"/> — the single container.</returns>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. The host container is the only container; inject IServiceProvider or IServiceScopeFactory instead. Scheduled removal in v12.0.0.")]
        public static IServiceProvider BuildServiceProvider()
        {
            return Globals.GetCurrentServiceProvider();
        }

        /// <summary>Publishes the host's service collection for diagnostics.</summary>
        /// <param name="services">The host's service collection.</param>
        internal static void RegisterServiceCollection(IServiceCollection services)
        {
            ServiceCollection = services;
        }
    }
}
