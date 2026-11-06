// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Framework
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Routing;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.AspNetCore.Routing;

    /// <summary>A View Engine that will delegate to whatever ViewEngine(s) the module application defines.</summary>
    public class ModuleDelegatingViewEngine : IViewEngine
    {
        private readonly IHostSettings hostSettings;
        private readonly IHttpContextAccessor httpContextAccessor;

        /// <summary>Initializes a new instance of the <see cref="ModuleDelegatingViewEngine"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public ModuleDelegatingViewEngine()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ModuleDelegatingViewEngine"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        public ModuleDelegatingViewEngine(IHostSettings hostSettings)
            : this(hostSettings, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ModuleDelegatingViewEngine"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor, used by <see cref="GetView"/> which gets no action context.</param>
        public ModuleDelegatingViewEngine(IHostSettings hostSettings, IHttpContextAccessor httpContextAccessor)
        {
            this.hostSettings = hostSettings;
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <summary>Finds the specified view by using the specified action context.</summary>
        /// <param name="context">The action context.</param>
        /// <param name="viewName">The name of the view.</param>
        /// <param name="isMainPage"><see langword="true"/> when locating a page (view), <see langword="false"/> for a partial view.</param>
        /// <returns>The view engine result.</returns>
        public ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage)
        {
            ArgumentNullException.ThrowIfNull(context);

            return RunAgainstModuleViewEngines(
                context,
                viewName,
                engines => isMainPage
                    ? engines.FindView(this.hostSettings, context, viewName, null, true)
                    : engines.FindPartialView(this.hostSettings, context, viewName, true));
        }

        /// <summary>Gets the view at the given path.</summary>
        /// <param name="executingFilePath">The path of the view which is executing (may be <see langword="null"/>).</param>
        /// <param name="viewPath">The path of the view to get.</param>
        /// <param name="isMainPage"><see langword="true"/> when locating a page (view), <see langword="false"/> for a partial view.</param>
        /// <returns>The view engine result.</returns>
        public ViewEngineResult GetView(string executingFilePath, string viewPath, bool isMainPage)
        {
            // GetView receives no ActionContext, so the module's engines are resolved from the current request.
            var httpContext = this.httpContextAccessor?.HttpContext;
            var moduleRequestResult = GetCurrentModuleRequestResult(httpContext);
            var engines = ViewEngineCollectionExt.ResolveViewEngines(
                ModuleApplication.GetCurrentDnnController(httpContext)?.ViewEngines,
                moduleRequestResult?.ActionContext ?? CreateActionContext(httpContext));

            var searchedLocations = new List<string>();
            foreach (var engine in engines)
            {
                var result = engine.GetView(executingFilePath, viewPath, isMainPage);
                if (result.Success)
                {
                    return result;
                }

                if (result.SearchedLocations is not null)
                {
                    searchedLocations.AddRange(result.SearchedLocations);
                }
            }

            return ViewEngineResult.NotFound(viewPath, searchedLocations);
        }

        private static ModuleRequestResult GetCurrentModuleRequestResult(HttpContext httpContext)
        {
            if (httpContext is not null && httpContext.HasModuleRequestResult())
            {
                return httpContext.GetModuleRequestResult();
            }

            return null;
        }

        private static ActionContext CreateActionContext(HttpContext httpContext)
        {
            return httpContext is null
                ? null
                : new ActionContext(httpContext, httpContext.GetRouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        }

        private static ViewEngineResult RunAgainstModuleViewEngines(ActionContext context, string viewName, Func<IReadOnlyList<IViewEngine>, ViewEngineResult> engineRequest)
        {
            var controller = ModuleApplication.GetCurrentDnnController(context.HttpContext);
            if (controller?.ViewEngines is null || controller.ViewEngines.Count == 0)
            {
                // Not a module request (or the module defined no engines): let the next engine in the
                // MvcViewOptions.ViewEngines collection handle the lookup.
                return ViewEngineResult.NotFound(viewName ?? string.Empty, []);
            }

            return engineRequest(controller.ViewEngines);
        }
    }
}
