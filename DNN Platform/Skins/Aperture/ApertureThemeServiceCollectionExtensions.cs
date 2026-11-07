// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Skins.Aperture
{
    using System;

    using Dnn.Skins.Aperture.Menus;
    using Dnn.Skins.Aperture.SkinObjects;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>
    /// Registers the services the Aperture theme components require.
    /// </summary>
    /// <remarks>
    /// The legacy theme needed no registration because every skin object was a WebForms user control
    /// resolved by the page lifecycle. The ported Blazor components consume data through dependency
    /// injection instead, so a host MUST call <see cref="AddApertureTheme"/> or the components have
    /// no services to inject.
    /// </remarks>
    public static class ApertureThemeServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the Aperture theme services to the host's service collection.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The same service collection so calls can be chained.</returns>
        /// <remarks>
        /// Both registrations use <c>TryAdd</c>, so a host that registers its own
        /// <see cref="IApertureSkinContext"/> or <see cref="IApertureMenuProvider"/> (for example one
        /// backed by its portal and page tree) keeps that implementation and only picks up the
        /// configuration-backed defaults for the services it did not supply.
        /// </remarks>
        public static IServiceCollection AddApertureTheme(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddScoped<IApertureSkinContext, DefaultApertureSkinContext>();
            services.TryAddScoped<IApertureMenuProvider, DefaultApertureMenuProvider>();

            return services;
        }
    }
}
