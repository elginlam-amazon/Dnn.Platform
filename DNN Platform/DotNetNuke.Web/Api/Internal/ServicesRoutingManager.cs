// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
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

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>Allows registering web API routes.</summary>
    /// <remarks>
    /// The legacy implementation added <c>System.Web.Routing.Route</c> instances straight into the global
    /// <c>RouteTable.Routes</c> collection and mutated <c>GlobalConfiguration.Configuration</c>. ASP.NET Core
    /// uses endpoint routing, where routes can only be added while the host builds its endpoint pipeline, so
    /// <see cref="RegisterRoutes"/> collects the route definitions of every <see cref="IServiceRouteMapper"/>
    /// and <see cref="MapRoutes"/> maps them as endpoints (see <c>MapDnnServiceRoutes</c>). The per-portal-alias
    /// route names and the controller namespaces are preserved on the endpoints (as the conventional route name
    /// and as <see cref="DnnServiceRoute"/> endpoint metadata, which
    /// <see cref="DnnControllerNamespaceMatcherPolicy"/> uses to scope controller selection to the namespaces).
    /// </remarks>
    public sealed partial class ServicesRoutingManager : IMapRoute, IRoutingManager
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<ServicesRoutingManager>();
        private readonly IServiceProvider serviceProvider;
        private readonly IList<DnnServiceRoute> routes;
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
            : this(serviceProvider, portalAliasService, new List<DnnServiceRoute>())
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ServicesRoutingManager"/> class.</summary>
        /// <param name="serviceProvider">The dependency injection container.</param>
        /// <param name="portalAliasService">The portal alias service.</param>
        /// <param name="routes">The route collection.</param>
        internal ServicesRoutingManager(IServiceProvider serviceProvider, IPortalAliasService portalAliasService, IList<DnnServiceRoute> routes)
        {
            this.serviceProvider = serviceProvider;
            this.routes = routes;

            // The obsolete constructor above passes null; the portal alias service then comes from the
            // injected container rather than from the Globals.GetCurrentServiceProvider() service locator.
            portalAliasService ??= serviceProvider.GetRequiredService<IPortalAliasService>();
            this.portalAliasRouteManager = new PortalAliasRouteManager(portalAliasService);
            this.TypeLocator = new TypeLocator();
        }

        /// <summary>Gets the routes which have been registered by the service route mappers.</summary>
        public IReadOnlyList<DnnServiceRoute> Routes => new ReadOnlyCollection<DnnServiceRoute>(this.routes);

        /// <summary>Gets or sets the type locator.</summary>
        internal ITypeLocator TypeLocator { get; set; }

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
            // allowing us to not worry about multi-threading threats here
            this.RegisterOneTimeInfrastructure();

            // No write lock: unlike RouteTable.Routes, the route definitions are local to this manager and
            // are only published to the host's endpoint data source by MapRoutes.
            this.routes.Clear();
            this.LocateServicesAndMapRoutes();

            Logger.ServicesRoutingManagerRegisteredRoutes(this.routes.Count);
        }

        /// <summary>Maps the registered service routes onto the host's endpoint route builder.</summary>
        /// <param name="endpoints">The endpoint route builder of the host.</param>
        /// <remarks>
        /// Call this before the host maps its catch-all/default route: the DNN service routes are
        /// conventional controller routes which keep their legacy route names, so that
        /// <c>Url.Link</c>/<c>UrlHelperExtensions.DnnLink</c> can resolve them by name.
        /// </remarks>
        public void MapRoutes(IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            if (this.routes.Count == 0)
            {
                this.RegisterRoutes();
            }

            foreach (var route in this.routes)
            {
                endpoints
                    .MapControllerRoute(route.Name, route.Url, route.Defaults, route.Constraints)
                    .WithMetadata(route);
            }
        }

        /// <summary>Determines whether the given <paramref name="type"/> is a valid <see cref="IServiceRouteMapper"/>.</summary>
        /// <param name="type">The type to check.</param>
        /// <returns><see langword="true"/> if the <paramref name="type"/> is an accessible concrete class, otherwise <see langword="false"/>.</returns>
        internal static bool IsValidServiceRouteMapper(Type type)
        {
            return type is { IsClass: true, IsAbstract: false, IsVisible: true, } && typeof(IServiceRouteMapper).IsAssignableFrom(type);
        }

        /// <summary>Gets a value indicating whether services framework tracing is enabled.</summary>
        /// <returns><see langword="true"/> when tracing is enabled, otherwise <see langword="false"/>.</returns>
        internal static bool IsTracingEnabled()
        {
            var configValue = Config.GetSetting("EnableServicesFrameworkTracing");

            if (!string.IsNullOrEmpty(configValue))
            {
                return Convert.ToBoolean(configValue);
            }

            return false;
        }

        private static void RegisterSystemRoutes()
        {
            ////_routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
        }

        private static RouteValueDictionary CreateRouteValueDictionary(object values)
        {
            return values switch
            {
                null => new RouteValueDictionary(),
                RouteValueDictionary routeValues => new RouteValueDictionary(routeValues),
                IDictionary<string, object> dictionary => new RouteValueDictionary(dictionary),
                _ => new RouteValueDictionary(values),
            };
        }

        private static DnnServiceRoute MapHttpRouteCore(string name, string url, object defaults, object constraints)
        {
            var route = new DnnServiceRoute
            {
                Url = url,
                Defaults = CreateRouteValueDictionary(defaults),
                Constraints = CreateRouteValueDictionary(constraints),
            };

            ConstraintValidation.Validate(route);
            return route;
        }

        /// <summary>
        /// The one-time startup work the legacy implementation did against
        /// <c>GlobalConfiguration.Configuration</c>. Everything that used to be a Web API service
        /// replacement (controller selector, dependency resolver, trace writer, filter provider, media type
        /// formatter) is now registered into the host's container by
        /// <c>DnnWebApiServiceCollectionExtensions.AddDnnWebApiInfrastructure</c>; what remains here is the
        /// configuration-driven work: the authentication handler chain, the default auth types and the
        /// standard tab/module info provider.
        /// </summary>
        private void RegisterOneTimeInfrastructure()
        {
            var authMessageHandlers = this.serviceProvider.GetService<IDnnAuthMessageHandlerCollection>();
            if (authMessageHandlers is null)
            {
                return;
            }

            // The baseline guarded this with
            // GlobalConfiguration.Configuration.MessageHandlers.Any(x => x is BasicAuthMessageHandler),
            // which never trips when the BasicAuth <messageHandlers> entry is disabled -- so the chain was
            // re-registered on every RegisterRoutes call and the handlers piled up. The collection tracks
            // whether it has been populated instead, which is what the guard was reaching for.
            if (authMessageHandlers.Count > 0)
            {
                return;
            }

            // Everything in this block is run one time at startup.
            // The dnnContext message handler became DnnContextMiddleware, which UseDnnWebApiInfrastructure
            // registers before the authentication handlers run, preserving the legacy handler order.
            this.RegisterAuthenticationHandlers(authMessageHandlers);

            // this must run after all other auth message handlers
            var handler = ActivatorUtilities.CreateInstance<WebFormsAuthMessageHandler>(this.serviceProvider);
            authMessageHandlers.Add(handler);
            DnnAuthorizeAttribute.AppendToDefaultAuthTypes(handler.AuthScheme);

            // Add Windows Authentication type to make API request works when windows authentication enabled.
            DnnAuthorizeAttribute.AppendToDefaultAuthTypes("Negotiate");

            // NOTE: the standard tab and module id provider is NOT added here. On .NET Framework this method
            // was the only place it could be registered (HttpConfiguration.AddTabAndModuleInfoProvider). It is
            // now registered once at composition time by AddDnnWebApiInfrastructure; adding it here as well
            // put two instances in the provider collection, so every tab/module probe ran twice on every
            // services request.
        }

        private void RegisterAuthenticationHandlers(IDnnAuthMessageHandlerCollection authMessageHandlers)
        {
            // authentication message handlers from the DotNetNuke:authServices:messageHandlers
            // configuration section (formerly <dotnetnuke><authServices><messageHandlers> of
            // web.config, read through ConfigurationManager.GetSection).
            var authSvcCfg = this.serviceProvider.GetService<IOptions<AuthServicesOptions>>()?.Value;
            var handlerEntries = authSvcCfg?.MessageHandlers?.ToList();
            if (handlerEntries is not { Count: > 0 })
            {
                return;
            }

            var registeredSchemes = new List<string>();
            foreach (var handlerEntry in handlerEntries)
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

                        // The legacy handlers were DelegatingHandlers and therefore IDisposable; the
                        // ASP.NET Core handlers hold no unmanaged resources, so the duplicate is simply
                        // dropped (if a third-party handler needs cleanup it can implement IDisposable).
                        (handler as System.IDisposable)?.Dispose();
                        continue;
                    }

                    authMessageHandlers.Add(handler);
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

            using var serviceScope = this.serviceProvider.CreateScope();
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
            var route = MapHttpRouteCore(name, url, defaults, constraints);
            route.DataTokens ??= new RouteValueDictionary();

            route.SetNameSpaces(namespaces);
            route.SetName(name);
            this.routes.Add(route);
            return route;
        }
    }
}
