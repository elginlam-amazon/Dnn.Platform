// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Routing
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Internal;
    using DotNetNuke.Framework.Reflections;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Mvc.Common;

    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;

    /// <summary>An <see cref="IRoutingManager"/> for MVC extensions. Collects the routes registered by <see cref="IMvcRouteMapper"/> implementations so they can be mapped as ASP.NET Core endpoints.</summary>
    public sealed partial class MvcRoutingManager : IMapRoute, IRoutingManager
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<MvcRoutingManager>();
        private static readonly List<DnnMvcRoute> CollectedRoutes = new List<DnnMvcRoute>();
        private static readonly object CollectedRoutesLock = new object();

        private readonly List<DnnMvcRoute> routes = new List<DnnMvcRoute>();
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
        {
            this.portalAliasMvcRouteManager = new PortalAliasMvcRouteManager(portalAliasService);
            this.TypeLocator = new TypeLocator();
        }

        internal ITypeLocator TypeLocator { get; set; }

        /// <summary>Gets a snapshot of all routes collected by any <see cref="MvcRoutingManager"/>.</summary>
        /// <returns>The collected routes.</returns>
        public static IReadOnlyList<DnnMvcRoute> GetCollectedRoutes()
        {
            lock (CollectedRoutesLock)
            {
                return CollectedRoutes.ToArray();
            }
        }

        /// <inheritdoc />
        public DnnMvcRoute MapRoute(string moduleFolderName, string routeName, string url, string[] namespaces)
        {
            return this.MapRoute(moduleFolderName, routeName, url, null, null, namespaces);
        }

        /// <inheritdoc />
        public DnnMvcRoute MapRoute(string moduleFolderName, string routeName, string url, object defaults, string[] namespaces)
        {
            return this.MapRoute(moduleFolderName, routeName, url, defaults, null, namespaces);
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

            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            url = url.Trim('/', '\\');

            var prefixCounts = this.portalAliasMvcRouteManager.GetRoutePrefixCounts();
            DnnMvcRoute route = null;

            foreach (var count in prefixCounts)
            {
                var fullRouteName = this.portalAliasMvcRouteManager.GetRouteName(moduleFolderName, routeName, count);
                var routeUrl = this.portalAliasMvcRouteManager.GetRouteUrl(moduleFolderName, url, count);
                route = CreateRoute(fullRouteName, routeUrl, defaults, constraints, namespaces);
                this.routes.Add(route);
                lock (CollectedRoutesLock)
                {
                    CollectedRoutes.Add(route);
                }

                Logger.MvcRoutingManagerMappingRoute(fullRouteName, routeUrl);
            }

            return route;
        }

        /// <inheritdoc />
        public void RegisterRoutes()
        {
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(new StandardTabAndModuleInfoProvider());
            this.LocateServicesAndMapRoutes();
            Logger.MvcRoutingManagerRegisteredRoutes(this.routes.Count);
        }

        internal static bool IsValidServiceRouteMapper(Type t)
        {
            return t is { IsClass: true, IsAbstract: false, IsVisible: true, } && typeof(IMvcRouteMapper).IsAssignableFrom(t);
        }

        private static DnnMvcRoute CreateRoute(string name, string url, object defaults, object constraints, string[] namespaces)
        {
            var route = new DnnMvcRoute
            {
                Url = url,
                Defaults = CreateRouteValueDictionary(defaults),
                Constraints = CreateRouteValueDictionary(constraints),
            };

            ConstraintValidation.Validate(route);

            if (namespaces is { Length: > 0 })
            {
                route.SetNameSpaces(namespaces);
            }

            route.SetName(name);
            return route;
        }

        private static RouteValueDictionary CreateRouteValueDictionary(object values)
        {
            if (values == null)
            {
                return new RouteValueDictionary();
            }

            return values is IDictionary<string, object> dictionary
                ? new RouteValueDictionary(dictionary)
                : new RouteValueDictionary(values);
        }

        private void LocateServicesAndMapRoutes()
        {
            this.portalAliasMvcRouteManager.ClearCachedData();

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

        private IEnumerable<IMvcRouteMapper> GetServiceRouteMappers()
        {
            var types = this.TypeLocator.GetAllMatchingTypes(IsValidServiceRouteMapper);

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
    }
}
