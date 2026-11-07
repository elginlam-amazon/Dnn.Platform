// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Common.Internal
{
    using System;

    using DotNetNuke.ComponentModel;
    using DotNetNuke.Data;
    using DotNetNuke.Modules.HTMLEditorProvider;
    using DotNetNuke.Modules.NavigationProvider;
    using DotNetNuke.Security.Membership;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Security.Profile;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Cache;
    using DotNetNuke.Services.ClientCapability;
    using DotNetNuke.Services.Cryptography;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.Log.EventLog;
    using DotNetNuke.Services.Mail;
    using DotNetNuke.Services.ModuleCache;
    using DotNetNuke.Services.OutputCache;
    using DotNetNuke.Services.Scheduling;
    using DotNetNuke.Services.Sitemap;
    using DotNetNuke.Services.Tokens;
    using DotNetNuke.Services.Url.FriendlyUrl;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>
    /// Installs the DNN <c>&lt;providers&gt;</c> model into the host container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the port of the static constructor of the former
    /// <c>DotNetNuke.Web.Common.Internal.DotNetNukeHttpApplication</c> (<c>Global.asax</c>'s
    /// code-behind base type), which was the real application-start task: it installed every
    /// configured provider into <c>ComponentFactory</c> and then built a SECOND service provider.
    /// </para>
    /// <para>
    /// There is now exactly ONE container — the host's, built by
    /// <c>builder.Services.AddDnnPlatform(...)</c>. <see cref="EnsureInstalled"/> points
    /// <see cref="ComponentFactory.Container"/> at that same provider and runs the 20
    /// <see cref="ProviderInstaller"/> installs plus the three named <see cref="FolderProvider"/>
    /// registrations, one for one and with the original life styles.
    /// <see cref="AddDnnProviders"/> additionally projects each provider contract into the Core DI
    /// graph (with the matching lifetime) so that constructor injection sees the same instances the
    /// legacy <see cref="ComponentFactory"/> seam hands out.
    /// </para>
    /// </remarks>
    internal static class DnnWebProviderInstaller
    {
        private static readonly object InstallLock = new object();
        private static bool installed;

        /// <summary>
        /// Projects every DNN provider contract into the Core DI graph, preserving the life style
        /// each <see cref="ProviderInstaller"/> used (singleton, except the HTML editor and the
        /// navigation control, which are transient).
        /// </summary>
        /// <param name="services">The host's service collection.</param>
        /// <returns>The same <paramref name="services"/>, for chaining.</returns>
        /// <remarks>
        /// The registrations are factories over <see cref="ComponentFactory"/> rather than concrete
        /// types because which implementation is active is a configuration decision
        /// (<c>DotNetNuke:&lt;provider&gt;:defaultProvider</c>), exactly as it was in
        /// <c>web.config</c>. <c>TryAdd</c> is used throughout so that the registrations
        /// <c>AddDnnPlatform</c> already makes (<see cref="DataProvider"/>,
        /// <see cref="MembershipProvider"/>, <see cref="RoleProvider"/>,
        /// <see cref="ProfileProvider"/>) keep winning and no contract ends up registered twice.
        /// </remarks>
        internal static IServiceCollection AddDnnProviders(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            // 1-12: the singletons installed with an explicit default provider type.
            AddSingleton<DatabaseConnectionProvider>(services);
            AddSingleton<DataProvider>(services);
            AddSingleton<CachingProvider>(services);
            AddSingleton<LoggingProvider>(services);
            AddSingleton<SchedulingProvider>(services);
            AddSingleton<MembershipProvider>(services);
            AddSingleton<RoleProvider>(services);
            AddSingleton<ProfileProvider>(services);
            AddSingleton<PermissionProvider>(services);
            AddSingleton<OutputCachingProvider>(services);
            AddSingleton<ModuleCachingProvider>(services);
            AddSingleton<SitemapProvider>(services);

            // 13-14: friendly URLs and the folder providers.
            AddSingleton<FriendlyUrlProvider>(services);
            AddSingleton<FolderProvider>(services);

            // The three named FolderProvider registrations. A named registration in the legacy
            // container is a KEYED service in Core DI; they are deliberately NOT collapsed into one,
            // because FolderManager resolves a folder's provider BY NAME
            // (FolderMappingInfo.FolderProviderType).
            services.TryAddKeyedSingleton<FolderProvider, StandardFolderProvider>(nameof(StandardFolderProvider));
            services.TryAddKeyedSingleton<FolderProvider, SecureFolderProvider>(nameof(SecureFolderProvider));
            services.TryAddKeyedSingleton<FolderProvider, DatabaseFolderProvider>(nameof(DatabaseFolderProvider));

            // 15-16: the two TRANSIENT providers. Both are stateful UI providers: a new instance per
            // resolution is required (the HTML editor carries the per-control settings, the
            // navigation provider the per-menu node tree).
            AddTransient<HtmlEditorProvider>(services);
            AddTransient<NavigationProvider>(services);

            // 17-20.
            AddSingleton<ClientCapabilityProvider>(services);
#pragma warning disable CS0618 // Type or member is obsolete
            AddSingleton<CryptographyProvider>(services);
#pragma warning restore CS0618 // Type or member is obsolete
            AddSingleton<TokenProvider>(services);
            AddSingleton<MailProvider>(services);

            return services;
        }

        /// <summary>
        /// Points <see cref="ComponentFactory.Container"/> at the host's service provider and
        /// installs every configured provider. Idempotent: the application-initialization hosted
        /// service calls it at startup, and the DI factories above call it defensively in case a
        /// provider is resolved before that.
        /// </summary>
        /// <param name="hostProvider">The host's (root) service provider — the single container.</param>
        internal static void EnsureInstalled(IServiceProvider hostProvider)
        {
            ArgumentNullException.ThrowIfNull(hostProvider);

            if (installed)
            {
                return;
            }

            lock (InstallLock)
            {
                if (installed)
                {
                    return;
                }

                Install(hostProvider);
                installed = true;
            }
        }

        private static void Install(IServiceProvider hostProvider)
        {
            // The legacy ComponentFactory seam, backed by the host container. The fallback wrapper
            // makes any contract that was never installed as a "provider" resolve from the host
            // container instead, which is what lets ported code keep calling
            // ComponentFactory.GetComponent<T>() while there is only one container.
            ComponentFactory.Container = new ContainerWithServiceProviderFallback(
                new SimpleContainer(hostProvider),
                hostProvider);

            ComponentFactory.InstallComponents(new ProviderInstaller("databaseConnection", typeof(DatabaseConnectionProvider), typeof(SqlDatabaseConnectionProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("data", typeof(DataProvider), typeof(SqlDataProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("caching", typeof(CachingProvider), typeof(FBCachingProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("logging", typeof(LoggingProvider), typeof(DBLoggingProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("scheduling", typeof(SchedulingProvider), typeof(DNNScheduler)));
            ComponentFactory.InstallComponents(new ProviderInstaller("members", typeof(MembershipProvider), typeof(AspNetMembershipProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("roles", typeof(RoleProvider), typeof(DNNRoleProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("profiles", typeof(ProfileProvider), typeof(DNNProfileProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("permissions", typeof(PermissionProvider), typeof(CorePermissionProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("outputCaching", typeof(OutputCachingProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("moduleCaching", typeof(ModuleCachingProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("sitemap", typeof(SitemapProvider), typeof(CoreSitemapProvider)));

            ComponentFactory.InstallComponents(new ProviderInstaller("friendlyUrl", typeof(FriendlyUrlProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("folder", typeof(FolderProvider)));
            RegisterIfNotAlreadyRegistered<FolderProvider, StandardFolderProvider>(nameof(StandardFolderProvider));
            RegisterIfNotAlreadyRegistered<FolderProvider, SecureFolderProvider>(nameof(SecureFolderProvider));
            RegisterIfNotAlreadyRegistered<FolderProvider, DatabaseFolderProvider>(nameof(DatabaseFolderProvider));

            ComponentFactory.InstallComponents(new ProviderInstaller("htmlEditor", typeof(HtmlEditorProvider), ComponentLifeStyleType.Transient));
            ComponentFactory.InstallComponents(new ProviderInstaller("navigationControl", typeof(NavigationProvider), ComponentLifeStyleType.Transient));
            ComponentFactory.InstallComponents(new ProviderInstaller("clientcapability", typeof(ClientCapabilityProvider)));
#pragma warning disable CS0618 // Type or member is obsolete
            ComponentFactory.InstallComponents(new ProviderInstaller("cryptography", typeof(CryptographyProvider), typeof(FipsCompilanceCryptographyProvider)));
#pragma warning restore CS0618 // Type or member is obsolete
            ComponentFactory.InstallComponents(new ProviderInstaller("tokens", typeof(TokenProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("mail", typeof(MailProvider)));
        }

        private static void AddSingleton<TProvider>(IServiceCollection services)
            where TProvider : class
        {
            services.TryAddSingleton(serviceProvider => GetComponent<TProvider>(serviceProvider));
        }

        private static void AddTransient<TProvider>(IServiceCollection services)
            where TProvider : class
        {
            services.TryAddTransient(serviceProvider => GetComponent<TProvider>(serviceProvider));
        }

        private static TProvider GetComponent<TProvider>(IServiceProvider serviceProvider)
            where TProvider : class
        {
            EnsureInstalled(serviceProvider);
            return ComponentFactory.GetComponent<TProvider>();
        }

        private static void RegisterIfNotAlreadyRegistered<TAbstract, TConcrete>(string name)
            where TAbstract : class
            where TConcrete : class, new()
        {
            var provider = ComponentFactory.GetComponent<TAbstract>();
            if (provider == null)
            {
                if (string.IsNullOrEmpty(name))
                {
                    ComponentFactory.RegisterComponentInstance<TAbstract>(new TConcrete());
                }
                else
                {
                    ComponentFactory.RegisterComponentInstance<TAbstract>(name, new TConcrete());
                }
            }
        }
    }
}
