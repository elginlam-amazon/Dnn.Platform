// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Security
{
    using System;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Lists;
    using DotNetNuke.Common.Options;
    using DotNetNuke.Framework;
    using DotNetNuke.Security.Authentication;
    using DotNetNuke.Security.Membership;
    using DotNetNuke.Security.Profile;
    using DotNetNuke.Security.Roles;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>
    /// Wires the platform's security, membership, role, profile and cookie-authentication
    /// services into the host's service collection.
    /// </summary>
    /// <remarks>
    /// These are the registrations that used to be implied by the <c>&lt;membership&gt;</c>,
    /// <c>&lt;roleManager&gt;</c>, <c>&lt;authentication&gt;</c> and
    /// <c>&lt;dotnetnuke&gt;&lt;members|roles|profiles&gt;</c> configuration elements. Nothing
    /// is resolved by reflection from configuration any more: the default providers are
    /// registered here, and a site that configures a different provider replaces the
    /// registration (or keeps using the legacy <c>ComponentFactory</c> registration, which the
    /// provider base classes still fall back to).
    /// </remarks>
    public static class DnnSecurityServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the platform's security services and the configuration sections they read.
        /// </summary>
        /// <param name="services">The host's service collection.</param>
        /// <param name="configuration">The host's configuration.</param>
        /// <returns>The same <paramref name="services"/>, for chaining.</returns>
        public static IServiceCollection AddDnnPlatformSecurity(this IServiceCollection services, IConfiguration configuration)
        {
            Requires.NotNull(nameof(services), services);
            Requires.NotNull(nameof(configuration), configuration);

            // Configuration that replaced <system.web><membership> and <system.web><forms>.
            services.Configure<MembershipOptions>(configuration.GetSection(MembershipOptions.SectionName));
            services.Configure<AuthenticationCookieOptions>(configuration.GetSection(AuthenticationCookieOptions.SectionName));

            // Data protection backs the authentication cookie and the platform's own tickets.
            services.AddDataProtection();

            // The membership store that replaces System.Web.Security.Membership /
            // SqlMembershipProvider (same database, same stored procedures, same hashes).
            services.TryAddSingleton<IAspNetMembershipStore, AspNetMembershipStore>();

            // The cookie-authentication seam that replaces FormsAuthentication.
            services.TryAddSingleton<IAuthenticationCookieService, AuthenticationCookieService>();

            // The DNN provider contracts, now resolved from this container rather than from
            // reflection over the <providers> configuration.
            services.TryAddSingleton<MembershipProvider, AspNetMembershipProvider>();
            services.TryAddSingleton<RoleProvider, DNNRoleProvider>();
            services.TryAddSingleton<ProfileProvider, DNNProfileProvider>();

            // PortalSecurity is resolved by the platform through ActivatorUtilities, but is also
            // injectable so that new code can take it as a constructor dependency.
            services.TryAddSingleton<ListController>();
            services.TryAddSingleton<PortalSecurity>();

            return services;
        }

        /// <summary>
        /// Connects the platform's static service locator to the application's service provider.
        /// </summary>
        /// <param name="serviceProvider">The application's root service provider (<c>app.Services</c>).</param>
        /// <remarks>
        /// <para>
        /// Call this once, immediately after the host is built and before any request is served:
        /// <c>app.Services.UseDnnPlatformServices();</c>.
        /// </para>
        /// <para>
        /// There is exactly ONE container. Scoped services resolve from the current request's
        /// <c>RequestServices</c> and fall back to the root provider outside a request.
        /// </para>
        /// </remarks>
        public static void UseDnnPlatformServices(this IServiceProvider serviceProvider)
        {
            Requires.NotNull(nameof(serviceProvider), serviceProvider);

            ServiceLocator.SetServiceProvider(serviceProvider);
            Globals.DependencyProvider = serviceProvider;
        }
    }
}
