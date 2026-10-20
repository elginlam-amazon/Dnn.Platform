// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Internal;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.Reflections;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Api.Auth;
    using DotNetNuke.Web.Api.Internal.Auth;
    using DotNetNuke.Web.ConfigSection;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>Allows registering web API routes.</summary>
    public sealed partial class ServicesRoutingManager : IMapRoute, IRoutingManager
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<ServicesRoutingManager>();
        private static readonly object InitializationLock = new object();
        private static readonly List<DnnServiceRoute> RegisteredRoutes = new List<DnnServiceRoute>();
        private static readonly List<AuthMessageHandlerBase> RegisteredAuthHandlers = new List<AuthMessageHandlerBase>();
        private static bool initialized;

        private readonly IServiceProvider serviceProvider;
        private readonly List<DnnServiceRoute> routes;
        private readonly PortalAliasRouteManager portalAliasRouteManager;

        /// <summary>Initializes a new instance of the <see cref="ServicesRoutingManager"/> class.</summary>
        /// <param name="serviceProvider">The dependency injection container.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IPortalAliasService. Scheduled removal in v12.0.0.")]
        public ServicesRoutingManager(IServiceProvider serviceProvider)
            : this(serviceProvider, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ServicesRoutingManager"/> class.</summary>
        /// <param name="serviceProvider">The dependency injection container.</param>
        /// <param name="portalAliasService">The portal alias service.</param>
        public ServicesRoutingManager(IServiceProvider serviceProvider, IPortalAliasService portalAliasService)
            : this(serviceProvider, portalAliasService, RegisteredRoutes)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ServicesRoutingManager"/> class.</summary>
        /// <param name="serviceProvider">The dependency injection container.</param>
        /// <param name="portalAliasService">The portal alias service.</param>
        /// <param name="routes">The route collection.</param>
        internal ServicesRoutingManager(IServiceProvider serviceProvider, IPortalAliasService portalAliasService, List<DnnServiceRoute> routes)
        {
            this.serviceProvider = serviceProvider;
            this.routes = routes;
            portalAliasService ??= Globals.GetCurrentServiceProvider().GetRequiredService<IPortalAliasService>();
            this.portalAliasRouteManager = new PortalAliasRouteManager(portalAliasService);
            this.TypeLocator = new TypeLocator();
        }

        /// <summary>Gets the message handler which authenticates the requests with the forms authentication cookie (runs after all other auth message handlers).</summary>
        internal static WebFormsAuthMessageHandler WebFormsAuthHandler { get; } = new WebFormsAuthMessageHandler();

        /// <summary>Gets the message handler which sets up the portal context of the services framework requests.</summary>
        internal static DnnContextMessageHandler ContextHandler { get; } = new DnnContextMessageHandler();

        /// <summary>Gets or sets the type locator.</summary>
        internal ITypeLocator TypeLocator { get; set; }

        /// <summary>Gets a snapshot of the registered services framework routes.</summary>
        /// <returns>The routes.</returns>
        public static IReadOnlyList<DnnServiceRoute> GetRegisteredRoutes()
        {
            lock (RegisteredRoutes)
            {
                return RegisteredRoutes.ToArray();
            }
        }

        /// <inheritdoc />
        public IList<DnnServiceRoute> MapHttpRoute(string moduleFolderName, string routeName, string url, object defaults, object constraints, string[] namespaces)
        {
            if (namespaces == null || namespaces.Length == 0 || string.IsNullOrEmpty(namespaces[0]))
            {
                throw new ArgumentException(Localization.GetExceptionMessage(
                    "ArgumentCannotBeNullOrEmpty",
                    "The argument '{0}' cannot be null or empty.",
                    "namespaces"));
            }

            Requires.NotNullOrEmpty("moduleFolderName", moduleFolderName);

            url = url.Trim('/', '\\');

            var prefixCounts = this.portalAliasRouteManager.GetRoutePrefixCounts();
            var mappedRoutes = new List<DnnServiceRoute>();

            foreach (var count in prefixCounts)
            {
                var fullRouteName = this.portalAliasRouteManager.GetRouteName(moduleFolderName, routeName, count);
                var routeUrl = this.portalAliasRouteManager.GetRouteUrl(moduleFolderName, url, count);
                var route = this.MapHttpRouteWithNamespace(fullRouteName, routeUrl, defaults, constraints, namespaces);
                mappedRoutes.Add(route);
                Logger.ServicesRoutingManagerMappingRoute(fullRouteName, routeUrl);

                // compatible with old service path: DesktopModules/{namespace}/API/{controller}/{action}.
                var oldRouteName = $"{fullRouteName}-old";

#pragma warning disable CS0618 // Type or member is obsolete
                var oldRouteUrl = PortalAliasRouteManager.GetOldRouteUrl(moduleFolderName, url, count);
#pragma warning restore CS0618 // Type or member is obsolete

                var oldRoute = this.MapHttpRouteWithNamespace(oldRouteName, oldRouteUrl, defaults, constraints, namespaces);
                mappedRoutes.Add(oldRoute);
                Logger.ServicesRoutingManagerMappingOldRoute(oldRouteName, oldRouteUrl);
            }

            return mappedRoutes;
        }

        /// <inheritdoc />
        public IList<DnnServiceRoute> MapHttpRoute(string moduleFolderName, string routeName, string url, object defaults, string[] namespaces)
        {
            return this.MapHttpRoute(moduleFolderName, routeName, url, defaults, null, namespaces);
        }

        /// <inheritdoc />
        public IList<DnnServiceRoute> MapHttpRoute(string moduleFolderName, string routeName, string url, string[] namespaces)
        {
            return this.MapHttpRoute(moduleFolderName, routeName, url, null, null, namespaces);
        }

        /// <inheritdoc />
        public void RegisterRoutes()
        {
            // register routes is ONLY called from within DNN application initialization
            // which is well protected from races
            lock (InitializationLock)
            {
                if (!initialized)
                {
                    // Everything in this block is run one time at startup
                    // the dnnContext message handler runs before any auth message handlers and the web forms
                    // auth message handler runs after all other auth message handlers (see ServicesFrameworkEndpoint)
                    this.RegisterAuthenticationHandlers();
                    DnnAuthorizeAttribute.AppendToDefaultAuthTypes(WebFormsAuthHandler.AuthScheme);

                    // Add Windows Authentication type to make API request works when windows authentication enabled.
                    DnnAuthorizeAttribute.AppendToDefaultAuthTypes("Negotiate");

                    // add standard tab and module id provider
                    TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(new StandardTabAndModuleInfoProvider());
                    initialized = true;
                }
            }

            lock (this.routes)
            {
                this.routes.Clear();
                this.LocateServicesAndMapRoutes();
            }

            // let the ASP.NET Core endpoint routing pick up the new routes
            ServicesFrameworkEndpointDataSource.NotifyRoutesChanged();

            Logger.ServicesRoutingManagerRegisteredRoutes(this.routes.Count);
        }

        /// <summary>Gets the authentication message handlers registered from the configuration (in execution order).</summary>
        /// <returns>The handlers.</returns>
        internal static IReadOnlyList<AuthMessageHandlerBase> GetAuthHandlers()
        {
            lock (RegisteredAuthHandlers)
            {
                return RegisteredAuthHandlers.ToArray();
            }
        }

        /// <summary>Determines whether the given <paramref name="type"/> is a valid <see cref="IServiceRouteMapper"/>.</summary>
        /// <param name="type">The type to check.</param>
        /// <returns><see langword="true"/> if the <paramref name="type"/> is an accessible concrete class, otherwise <see langword="false"/>.</returns>
        internal static bool IsValidServiceRouteMapper(Type type)
        {
            return type is { IsClass: true, IsAbstract: false, IsVisible: true, } && typeof(IServiceRouteMapper).IsAssignableFrom(type);
        }

        private static void RegisterSystemRoutes()
        {
            ////_routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
        }

        private void RegisterAuthenticationHandlers()
        {
            // authentication message handlers from web.config file
            var authSvcCfg = AuthServicesConfiguration.GetConfig();
            if (authSvcCfg?.MessageHandlers is not { Count: > 0 })
            {
                return;
            }

            var registeredSchemes = new List<string>();
            foreach (var handlerEntry in authSvcCfg.MessageHandlers.Cast<MessageHandlerEntry>())
            {
                if (!handlerEntry.Enabled)
                {
                    Logger.ServicesRoutingManagerHandlerIsDisabled(handlerEntry.ClassName);
                    continue;
                }

                try
                {
                    var type = Reflection.CreateType(handlerEntry.ClassName, false);
                    if (ActivatorUtilities.CreateInstance(this.serviceProvider, type, handlerEntry.DefaultInclude, handlerEntry.ForceSsl) is not AuthMessageHandlerBase handler)
                    {
                        throw new InvalidAuthHandlerException("The handler is not a descendant of AuthMessageHandlerBase abstract class");
                    }

                    var schemeName = handler.AuthScheme.ToUpperInvariant();
                    if (registeredSchemes.Contains(schemeName))
                    {
                        Logger.ServicesRoutingManagerHandlerIsAlreadyAdded(handlerEntry.ClassName);
                        handler.Dispose();
                        continue;
                    }

                    lock (RegisteredAuthHandlers)
                    {
                        RegisteredAuthHandlers.Add(handler);
                    }

                    registeredSchemes.Add(schemeName);
                    Logger.ServicesRoutingManagerHandlerIsActivated(handler.AuthScheme, handler.GetType().FullName);

                    if (handlerEntry.DefaultInclude)
                    {
                        DnnAuthorizeAttribute.AppendToDefaultAuthTypes(handler.AuthScheme);
                    }

                    if (handler.BypassAntiForgeryToken)
                    {
                        ValidateAntiForgeryTokenAttribute.AppendToBypassAuthTypes(handler.AuthScheme);
                    }
                }
                catch (Exception ex)
                {
                    Logger.ServicesRoutingManagerCannotInstantiateInstanceOf(ex, handlerEntry.ClassName);
                }
            }
        }

        private void LocateServicesAndMapRoutes()
        {
            RegisterSystemRoutes();
            this.ClearCachedRouteData();

            using var serviceScope = Globals.DependencyProvider.CreateScope();
            foreach (var routeMapper in this.GetServiceRouteMappers(serviceScope.ServiceProvider))
            {
                try
                {
                    routeMapper.RegisterRoutes(this);
                }
                catch (Exception e)
                {
                    Logger.ServicesRoutingManagerRegisterRoutesThrewAnException(e, routeMapper.GetType().FullName);
                }
            }
        }

        private void ClearCachedRouteData()
        {
            this.portalAliasRouteManager.ClearCachedData();
        }

        private IEnumerable<IServiceRouteMapper> GetServiceRouteMappers(IServiceProvider scopedServiceProvider)
        {
            foreach (var routeMapperType in this.GetAllServiceRouteMapperTypes())
            {
                IServiceRouteMapper routeMapper;
                try
                {
                    routeMapper = ActivatorUtilities.CreateInstance(scopedServiceProvider, routeMapperType) as IServiceRouteMapper;
                }
                catch (Exception e)
                {
                    Logger.ServicesRoutingManagerUnableToCreateRouteMapper(e, routeMapperType.FullName);
                    routeMapper = null;
                }

                if (routeMapper != null)
                {
                    yield return routeMapper;
                }
            }
        }

        private IEnumerable<Type> GetAllServiceRouteMapperTypes()
        {
            return this.TypeLocator.GetAllMatchingTypes(IsValidServiceRouteMapper);
        }

        private DnnServiceRoute MapHttpRouteWithNamespace(string name, string url, object defaults, object constraints, string[] namespaces)
        {
            var route = new DnnServiceRoute(name, url, defaults, constraints, namespaces);
            this.routes.RemoveAll(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            this.routes.Add(route);
            return route;
        }
    }
}
