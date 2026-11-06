// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Internal;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Framework.Reflections;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Mvc.Common;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.AspNetCore.Routing.Patterns;
    using Microsoft.Extensions.Logging;

    /// <summary>An <see cref="IRoutingManager"/> for MVC extensions.</summary>
    /// <remarks>
    /// The legacy implementation added <c>System.Web.Routing.Route</c> instances straight into the global
    /// <c>RouteTable.Routes</c> collection. ASP.NET Core uses endpoint routing, where routes can only be
    /// added while the host builds its endpoint pipeline, so <see cref="RegisterRoutes"/> collects the route
    /// definitions of all module route mappers and <see cref="MapRoutes"/> maps them as named endpoints.
    /// The per-portal-alias route names and the module folder namespaces are preserved on the endpoints
    /// (as endpoint name and as <see cref="DnnMvcRoute"/> endpoint metadata).
    /// </remarks>
    public sealed partial class MvcRoutingManager : IMapRoute, IRoutingManager
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<MvcRoutingManager>();
        private readonly IList<DnnMvcRoute> routes;
        private readonly PortalAliasMvcRouteManager portalAliasMvcRouteManager;

        /// <summary>Initializes a new instance of the <see cref="MvcRoutingManager"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IPortalAliasService. Scheduled removal in v12.0.0.")]
        public MvcRoutingManager()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MvcRoutingManager"/> class.</summary>
        /// <param name="portalAliasService">The portal alias service.</param>
        public MvcRoutingManager(IPortalAliasService portalAliasService)
            : this(portalAliasService, new List<DnnMvcRoute>())
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MvcRoutingManager"/> class.</summary>
        /// <param name="portalAliasService">The portal alias service.</param>
        /// <param name="routes">The route collection to register the routes into.</param>
        internal MvcRoutingManager(IPortalAliasService portalAliasService, IList<DnnMvcRoute> routes)
        {
            this.routes = routes;
            this.portalAliasMvcRouteManager = new PortalAliasMvcRouteManager(portalAliasService);
            this.TypeLocator = new TypeLocator();
        }

        /// <summary>Gets the routes which have been registered by the module route mappers.</summary>
        public IReadOnlyList<DnnMvcRoute> Routes => new ReadOnlyCollection<DnnMvcRoute>(this.routes);

        internal ITypeLocator TypeLocator { get; set; }

        /// <inheritdoc />
        public DnnMvcRoute MapRoute(string moduleFolderName, string routeName, string url, string[] namespaces)
        {
            return this.MapRoute(moduleFolderName, routeName, url, null /* defaults */, null /* constraints */, namespaces);
        }

        /// <inheritdoc />
        public DnnMvcRoute MapRoute(string moduleFolderName, string routeName, string url, object defaults, string[] namespaces)
        {
            return this.MapRoute(moduleFolderName, routeName, url, defaults, null /* constraints */, namespaces);
        }

        /// <inheritdoc />
        public DnnMvcRoute MapRoute(string moduleFolderName, string routeName, string url, object defaults, object constraints, string[] namespaces)
        {
            if (namespaces == null || namespaces.Length == 0 || string.IsNullOrEmpty(namespaces[0]))
            {
                throw new ArgumentException(Localization.GetExceptionMessage(
                    "ArgumentCannotBeNullOrEmpty",
                    "The argument '{0}' cannot be null or empty.",
                    "namespaces"));
            }

            Requires.NotNullOrEmpty("moduleFolderName", moduleFolderName);

            ArgumentNullException.ThrowIfNull(url);

            url = url.Trim('/', '\\');

            var prefixCounts = this.portalAliasMvcRouteManager.GetRoutePrefixCounts();
            DnnMvcRoute route = null;

            foreach (var count in prefixCounts)
            {
                var fullRouteName = this.portalAliasMvcRouteManager.GetRouteName(moduleFolderName, routeName, count);
                var routeUrl = this.portalAliasMvcRouteManager.GetRouteUrl(moduleFolderName, url, count);
                route = MapRouteWithNamespace(fullRouteName, routeUrl, defaults, constraints, namespaces);
                this.routes.Add(route);
                Logger.MvcRoutingManagerMappingRoute(fullRouteName, routeUrl);
            }

            return route;
        }

        /// <summary>Registers the routes of all module route mappers.</summary>
        public void RegisterRoutes()
        {
            // The standard tab and module id provider is registered into DI by Startup.ConfigureServices
            // (HttpConfiguration.AddTabAndModuleInfoProvider no longer exists in ASP.NET Core); its
            // registration is idempotent, so registering it again here would be a no-op.
            RegisterSystemRoutes();
            this.ClearCachedRouteData();

            // No write lock: unlike RouteTable.Routes, the route definitions are local to this manager and
            // are only published to the host's endpoint data source by MapRoutes.
            this.LocateServicesAndMapRoutes();

            Logger.MvcRoutingManagerRegisteredRoutes(this.routes.Count);
        }

        /// <summary>Maps the registered module routes onto the host's endpoint route builder.</summary>
        /// <param name="endpoints">The endpoint route builder of the host.</param>
        /// <remarks>
        /// Call this before the host maps its catch-all/default route: endpoint routing picks the most
        /// specific match, but the DNN module routes must keep their own (named) endpoints so that
        /// <c>Url.RouteUrl</c>/<c>RedirectToRoute</c> can resolve them by name.
        /// </remarks>
        public void MapRoutes(IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            if (this.routes.Count == 0)
            {
                this.RegisterRoutes();
            }

            var handler = new DnnMvcRouteHandler();
            var requestDelegate = handler.GetRequestDelegate();

            foreach (var route in this.routes)
            {
                var pattern = RoutePatternFactory.Parse(route.Url, route.Defaults, route.Constraints);

                endpoints.Map(pattern, requestDelegate)
                    .WithName(route.GetName() ?? route.Name)
                    .WithDisplayName(route.GetName() ?? route.Name)
                    .WithMetadata(route);
            }
        }

        internal static bool IsValidServiceRouteMapper(Type t)
        {
            return t is { IsClass: true, IsAbstract: false, IsVisible: true, } && typeof(IMvcRouteMapper).IsAssignableFrom(t);
        }

        private static bool IsTracingEnabled()
        {
            var configValue = Config.GetSetting("EnableServicesFrameworkTracing");

            return !string.IsNullOrEmpty(configValue) && Convert.ToBoolean(configValue);
        }

        private static void RegisterSystemRoutes()
        {
            ////routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
        }

        private static DnnMvcRoute MapRouteWithNamespace(string name, string url, object defaults, object constraints, string[] namespaces)
        {
            var route = new DnnMvcRoute
            {
                Url = url,
                Defaults = CreateRouteValueDictionaryUncached(defaults),
                Constraints = CreateRouteValueDictionaryUncached(constraints),
            };

            route.DataTokens ??= new RouteValueDictionary();

            ConstraintValidation.Validate(route);
            if ((namespaces != null) && (namespaces.Length > 0))
            {
                route.SetNameSpaces(namespaces);
            }

            route.SetName(name);
            return route;
        }

        private static RouteValueDictionary CreateRouteValueDictionaryUncached(object values)
        {
            return values is IDictionary<string, object> dictionary ? new RouteValueDictionary(dictionary) : TypeHelper.ObjectToDictionary(values);
        }

        private void LocateServicesAndMapRoutes()
        {
            foreach (var routeMapper in this.GetServiceRouteMappers())
            {
                try
                {
                    routeMapper.RegisterRoutes(this);
                }
                catch (Exception e)
                {
                    Logger.MvcRoutingManagerRegisterRoutesThrewException(e, routeMapper.GetType().FullName);
                }
            }
        }

        private void ClearCachedRouteData()
        {
            this.portalAliasMvcRouteManager.ClearCachedData();
        }

        private IEnumerable<IMvcRouteMapper> GetServiceRouteMappers()
        {
            IEnumerable<Type> types = this.GetAllServiceRouteMapperTypes();

            foreach (var routeMapperType in types)
            {
                IMvcRouteMapper routeMapper;
                try
                {
                    routeMapper = Activator.CreateInstance(routeMapperType) as IMvcRouteMapper;
                }
                catch (Exception e)
                {
                    Logger.MvcRoutingManagerUnableToCreateMapper(e, routeMapperType.FullName);
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
    }
}
