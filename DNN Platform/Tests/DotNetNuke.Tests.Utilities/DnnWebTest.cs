// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Utilities;

using System;
using System.Net;

using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
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
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Services.Log.EventLog;
using DotNetNuke.Services.ModuleCache;
using DotNetNuke.Services.OutputCache;
using DotNetNuke.Services.Scheduling;
using DotNetNuke.Services.Sitemap;
using DotNetNuke.Services.Url.FriendlyUrl;
using DotNetNuke.Web;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using MembershipProvider = DotNetNuke.Security.Membership.MembershipProvider;
using RoleProvider = DotNetNuke.Security.Roles.RoleProvider;

/// <summary>Base class for test fixtures that need a simulated DNN web request and the DNN provider stack.</summary>
public class DnnWebTest : DnnUnitTest
{
    private static bool alreadyLoaded;

    /// <summary>Gets the portal id of the simulated portal.</summary>
    public int PortalId { get; private set; }

    /// <inheritdoc />
    public override void OneTimeSetUp()
    {
        base.OneTimeSetUp();

        var simulator = new Instance.Utilities.HttpSimulator.HttpSimulator("/", this.WebsitePhysicalAppPath);
        simulator.SimulateRequest(new Uri(this.WebsiteAppPath));

        // SimulateRequest publishes the simulated Microsoft.AspNetCore.Http.HttpContext through
        // HttpContextSource (replacing `new HttpContextWrapper(HttpContext.Current)`), and owns the
        // fake IWebHostEnvironment that backs HttpContextSource.MapPath.
        SetupContainer(simulator.HostingEnvironment);
        InstallComponents();

        var httpContext = HttpContextSource.CurrentOrThrow;

        LoadDnnProviders("data", "logging", "caching", "authentication", "members", "roles", "profiles", "permissions", "folder", "clientcapability");

        // fix membership
        SetupMembershipProvider();

        var objPortalAliasInfo = new Entities.Portals.PortalAliasInfo { PortalID = Constants.PORTAL_Zero, };
        var ps = new Entities.Portals.PortalSettings(59, objPortalAliasInfo);
        httpContext.Items["PortalSettings"] = ps;

        this.PortalId = Constants.PORTAL_Zero;
    }

    private static void SetupContainer(IWebHostEnvironment environment)
    {
        // The net48 fixture called DependencyInjectionInitialize.BuildServiceProvider(), which built
        // a second container out of every IDnnStartup. That method now just returns the host's
        // container (which does not exist in a test host), so the fixture builds the platform
        // container itself — same registrations, through the supported entry point.
        var services = new ServiceCollection();
        services.AddDnnPlatform(Instance.Utilities.TestConfiguration.Configuration, environment);
        Globals.DependencyProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Makes the DNN membership provider the active one.
    /// </summary>
    /// <remarks>
    /// The net48 fixture reflected <c>System.Web.Security.Membership.s_Provider</c> and forced it to
    /// <c>Membership.Providers["AspNetSqlMembershipProvider"]</c>, because ASP.NET resolved the
    /// membership provider from <c>&lt;system.web&gt;&lt;membership&gt;</c> lazily and the simulated
    /// request did not trigger it. <c>System.Web.Security</c> does not exist on modern .NET: the
    /// membership store is now the <c>IAspNetMembershipStore</c> service (same database, same stored
    /// procedures) and <see cref="MembershipProvider.Instance"/> resolves
    /// <see cref="AspNetMembershipProvider"/> from the container. This therefore resolves the real
    /// provider (and its store) from the container built in <c>SetupContainer</c> and also publishes
    /// it to <see cref="ComponentFactory"/>, which is the fallback <c>MembershipProvider.Instance()</c>
    /// uses and the registration the <c>ProviderInstaller("members", …)</c> call above populates.
    /// </remarks>
    private static void SetupMembershipProvider()
    {
        var provider = Globals.DependencyProvider.GetService<MembershipProvider>()
                       ?? ActivatorUtilities.CreateInstance<AspNetMembershipProvider>(Globals.DependencyProvider);

        ComponentFactory.RegisterComponentInstance<MembershipProvider>(provider);
        ComponentFactory.RegisterComponentInstance<IAspNetMembershipStore>(
            Globals.DependencyProvider.GetRequiredService<IAspNetMembershipStore>());
    }

    private static void InstallComponents()
    {
        Globals.ServerName = string.IsNullOrEmpty(Config.GetSetting("ServerName"))
            ? Dns.GetHostName()
            : Config.GetSetting("ServerName");

        ComponentFactory.Container = new SimpleContainer();

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
        RegisterIfNotAlreadyRegistered<FolderProvider, StandardFolderProvider>("StandardFolderProvider");
        RegisterIfNotAlreadyRegistered<FolderProvider, SecureFolderProvider>("SecureFolderProvider");
        RegisterIfNotAlreadyRegistered<FolderProvider, DatabaseFolderProvider>("DatabaseFolderProvider");
        RegisterIfNotAlreadyRegistered<PermissionProvider>();
        ComponentFactory.InstallComponents(new ProviderInstaller("htmlEditor", typeof(HtmlEditorProvider), ComponentLifeStyleType.Transient));
        ComponentFactory.InstallComponents(new ProviderInstaller("navigationControl", typeof(NavigationProvider), ComponentLifeStyleType.Transient));
        ComponentFactory.InstallComponents(new ProviderInstaller("clientcapability", typeof(ClientCapabilityProvider)));
    }

    private static void RegisterIfNotAlreadyRegistered<TConcrete>()
        where TConcrete : class, new()
    {
        RegisterIfNotAlreadyRegistered<TConcrete, TConcrete>(string.Empty);
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

    /// <summary>
    /// This proc loads up specified DNN providers, because the BuildManager doesn't get the context right
    /// The providers are cached so that the DNN base buildManager calls don't have to load up the providers.
    /// </summary>
    private static void LoadDnnProviders(params string[] providers)
    {
        if (alreadyLoaded)
        {
            return;
        }

        alreadyLoaded = true;
        foreach (var provider in providers)
        {
            if (provider.Length <= 0)
            {
                continue;
            }

            var config = DotNetNuke.Framework.Providers.ProviderConfiguration.GetProviderConfiguration(provider);
            if (config == null)
            {
                continue;
            }

            foreach (string providerName in config.Providers.Keys)
            {
                var providerValue = (DotNetNuke.Framework.Providers.Provider)config.Providers[providerName];
                var type = providerValue.Type;

                if (type.Contains(", ")) // get the straight typename, no assembly, for the cache key
                {
                    type = type.Substring(0, type.IndexOf(", ", StringComparison.Ordinal));
                }

                var cacheKey = type;

                DotNetNuke.Framework.Reflection.CreateType(providerValue.Type, cacheKey, true, false);
            }
        }
    }
}
