// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Services.Cache;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    /// <summary>Extension methods for a collection of <see cref="IViewEngine"/>.</summary>
    /// <remarks>
    /// MVC 5's <c>System.Web.Mvc.ViewEngineCollection</c> does not exist in ASP.NET Core; the view engines
    /// are an ordered collection on <see cref="MvcViewOptions"/> resolved from DI. These extensions perform
    /// the same cached lookup across those engines that the legacy collection's internal <c>Find</c> did.
    /// </remarks>
    public static partial class ViewEngineCollectionExt
    {
        /// <summary>Finds the specified view by using the specified action context.</summary>
        /// <param name="viewEngineCollection">The view engines of the module (may be <see langword="null"/> to use the engines configured in DI).</param>
        /// <param name="actionContext">The action context.</param>
        /// <param name="viewName">The name of the view.</param>
        /// <param name="masterName">The name of the master (layout). Unused for lookup in ASP.NET Core, it only takes part in the cache key.</param>
        /// <param name="useCache"><see langword="true"/> to specify that the view engine returns the cached view, if a cached view exists; otherwise, <see langword="false"/>.</param>
        /// <returns>The page view.</returns>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload taking IHostSettings. Scheduled for removal in v12.0.0.")]
        public static ViewEngineResult FindView(this IReadOnlyList<IViewEngine> viewEngineCollection, ActionContext actionContext, string viewName, string masterName, bool useCache)
            => viewEngineCollection.FindView(Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>(), actionContext, viewName, masterName, useCache);

        /// <summary>Finds the specified view by using the specified action context.</summary>
        /// <param name="viewEngineCollection">The view engines of the module (may be <see langword="null"/> to use the engines configured in DI).</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="actionContext">The action context.</param>
        /// <param name="viewName">The name of the view.</param>
        /// <param name="masterName">The name of the master (layout). Unused for lookup in ASP.NET Core, it only takes part in the cache key.</param>
        /// <param name="useCache"><see langword="true"/> to specify that the view engine returns the cached view, if a cached view exists; otherwise, <see langword="false"/>.</param>
        /// <returns>The page view.</returns>
        public static ViewEngineResult FindView(this IReadOnlyList<IViewEngine> viewEngineCollection, IHostSettings hostSettings, ActionContext actionContext, string viewName, string masterName, bool useCache)
        {
            var engines = ResolveViewEngines(viewEngineCollection, actionContext);
            var find = new Func<IViewEngine, ViewEngineResult>(engine => engine.FindView(actionContext, viewName, true));
            try
            {
                var cacheKey = CreateCacheKey(actionContext, "View", viewName, masterName, GetPortalId(actionContext));
                var parameters = new object[]
                {
                    find,
                    viewName,
                };
                var cacheArg = new CacheItemArgs(cacheKey, 120, CacheItemPriority.Default, "Find", engines, parameters);

                return useCache ? CBO.GetCachedObject<ViewEngineResult>(hostSettings, cacheArg, CallFind) : CallFind(cacheArg);
            }
            catch (Exception)
            {
                return SearchViewEngines(engines, find, viewName);
            }
        }

        /// <summary>Finds the specified partial view by using the specified action context.</summary>
        /// <param name="viewEngineCollection">The view engines of the module (may be <see langword="null"/> to use the engines configured in DI).</param>
        /// <param name="actionContext">The action context.</param>
        /// <param name="partialViewName">The name of the partial view.</param>
        /// <param name="useCache"><see langword="true"/> to specify that the view engine returns the cached view, if a cached view exists; otherwise, <see langword="false"/>.</param>
        /// <returns>The partial view.</returns>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload taking IHostSettings. Scheduled for removal in v12.0.0.")]
        public static ViewEngineResult FindPartialView(this IReadOnlyList<IViewEngine> viewEngineCollection, ActionContext actionContext, string partialViewName, bool useCache)
            => viewEngineCollection.FindPartialView(Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>(), actionContext, partialViewName, useCache);

        /// <summary>Finds the specified partial view by using the specified action context.</summary>
        /// <param name="viewEngineCollection">The view engines of the module (may be <see langword="null"/> to use the engines configured in DI).</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="actionContext">The action context.</param>
        /// <param name="partialViewName">The name of the partial view.</param>
        /// <param name="useCache"><see langword="true"/> to specify that the view engine returns the cached view, if a cached view exists; otherwise, <see langword="false"/>.</param>
        /// <returns>The partial view.</returns>
        public static ViewEngineResult FindPartialView(this IReadOnlyList<IViewEngine> viewEngineCollection, IHostSettings hostSettings, ActionContext actionContext, string partialViewName, bool useCache)
        {
            var engines = ResolveViewEngines(viewEngineCollection, actionContext);
            var find = new Func<IViewEngine, ViewEngineResult>(engine => engine.FindView(actionContext, partialViewName, false));
            try
            {
                var cacheKey = CreateCacheKey(actionContext, "Partial", partialViewName, string.Empty, GetPortalId(actionContext));
                var parameters = new object[]
                {
                    find,
                    partialViewName,
                };
                var cacheArg = new CacheItemArgs(cacheKey, 120, CacheItemPriority.Default, "Find", engines, parameters);

                return useCache ? CBO.GetCachedObject<ViewEngineResult>(hostSettings, cacheArg, CallFind) : CallFind(cacheArg);
            }
            catch (Exception)
            {
                return SearchViewEngines(engines, find, partialViewName);
            }
        }

        /// <summary>Gets the view engines to search: the module's own engines, or else those configured in DI.</summary>
        /// <param name="viewEngineCollection">The module's view engines (may be <see langword="null"/> or empty).</param>
        /// <param name="actionContext">The action context used to resolve <see cref="MvcViewOptions"/> (may be <see langword="null"/>).</param>
        /// <returns>The view engines to search, never <see langword="null"/>.</returns>
        public static IReadOnlyList<IViewEngine> ResolveViewEngines(IReadOnlyList<IViewEngine> viewEngineCollection, ActionContext actionContext)
        {
            if (viewEngineCollection is { Count: > 0 })
            {
                return viewEngineCollection;
            }

            var services = actionContext?.HttpContext?.RequestServices ?? Globals.GetCurrentServiceProvider();
            var configured = services?.GetService<IOptions<MvcViewOptions>>()?.Value?.ViewEngines;

            // The delegating engine must be excluded, it would delegate straight back into the module.
            return configured is null
                ? []
                : [.. configured.Where(engine => engine is not ModuleDelegatingViewEngine)];
        }

        private static ViewEngineResult CallFind(CacheItemArgs cacheItem)
        {
            // Params mirror the legacy reflection call: [0] the operation name, [1] the engines to search
            // against (previously the ViewEngineCollection instance) and [2] the call arguments.
            var engines = cacheItem.Params[1] as IReadOnlyList<IViewEngine> ?? [];
            var parameters = (object[])cacheItem.Params[2];
            var find = (Func<IViewEngine, ViewEngineResult>)parameters[0];
            var name = (string)parameters[1];

            return SearchViewEngines(engines, find, name);
        }

        private static ViewEngineResult SearchViewEngines(IReadOnlyList<IViewEngine> engines, Func<IViewEngine, ViewEngineResult> find, string name)
        {
            // Allow SearchedLocations tracking to improve error messages up the stack.
            var searchedLocations = new List<string>();
            foreach (var engine in engines)
            {
                var result = find(engine);
                if (result.Success)
                {
                    return result;
                }

                if (result.SearchedLocations is not null)
                {
                    searchedLocations.AddRange(result.SearchedLocations);
                }
            }

            return ViewEngineResult.NotFound(name ?? string.Empty, searchedLocations);
        }

        private static int GetPortalId(ActionContext actionContext)
        {
            return ModuleApplication.GetCurrentDnnController(actionContext?.HttpContext)?.ModuleContext?.PortalId ?? 0;
        }

        private static string CreateCacheKey(ActionContext actionContext, string section, string name, string areaName, int portalId)
        {
            var namespaces = actionContext.RouteData.DataTokens.TryGetValue("namespaces", out var dataToken)
                ? (dataToken as IEnumerable<string>)?.FirstOrDefault()
                : null;

            return string.Format(
                CultureInfo.InvariantCulture,
                ":ViewCacheEntry:{0}:{1}:{2}:{3}:{4}:{5}",
                namespaces,
                section,
                name,
                actionContext.RouteData.Values["controller"],
                areaName,
                portalId);
        }
    }
}
