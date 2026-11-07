// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionResults
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Web.Mvc.Framework;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Locates a view and renders it into a caller supplied <see cref="TextWriter"/>, which is what the
    /// legacy <c>ViewResultBase.ExecuteResult(ControllerContext, TextWriter)</c> of the DNN view results did.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core executes view results through <c>IViewExecutor</c>, which always writes into the
    /// response body. The DNN module host instead needs the module output in a writer so that it can be
    /// injected into the DNN page, so the view is rendered directly through
    /// <see cref="IView.RenderAsync(Microsoft.AspNetCore.Mvc.Rendering.ViewContext)"/> against a
    /// <see cref="Microsoft.AspNetCore.Mvc.Rendering.ViewContext"/> bound to that writer. The legacy
    /// <c>ViewEngine.ReleaseView</c> call has no Core counterpart: ASP.NET Core view engines do not own the
    /// view instances, the Razor view engine caches and disposes them itself.
    /// </remarks>
    internal static class DnnViewResultExecutor
    {
        /// <summary>Finds (when needed) and renders a view into the given writer.</summary>
        /// <param name="actionContext">The action context to execute against.</param>
        /// <param name="writer">The writer to render the view into.</param>
        /// <param name="view">The already resolved view, or <see langword="null"/> to look one up.</param>
        /// <param name="viewName">The name of the view to look up.</param>
        /// <param name="masterName">The name of the master/layout, may be <see langword="null"/>.</param>
        /// <param name="isMainPage"><see langword="true"/> for a view, <see langword="false"/> for a partial view.</param>
        /// <param name="viewData">The view data.</param>
        /// <param name="tempData">The temp data.</param>
        /// <param name="viewEngine">The explicit view engine of the result, may be <see langword="null"/>.</param>
        /// <param name="moduleViewEngines">The module's view engines, may be <see langword="null"/>.</param>
        /// <returns>The view which was rendered.</returns>
        internal static async Task<IView> RenderAsync(
            ActionContext actionContext,
            TextWriter writer,
            IView view,
            string viewName,
            string masterName,
            bool isMainPage,
            ViewDataDictionary viewData,
            ITempDataDictionary tempData,
            IViewEngine viewEngine,
            IReadOnlyList<IViewEngine> moduleViewEngines)
        {
            Requires.NotNull("actionContext", actionContext);
            Requires.NotNull("writer", writer);

            var services = actionContext.HttpContext?.RequestServices ?? Globals.GetCurrentServiceProvider();

            viewData ??= new ViewDataDictionary(
                services.GetRequiredService<IModelMetadataProvider>(),
                actionContext.ModelState);

            if (!string.IsNullOrEmpty(masterName))
            {
                // MVC 5 passed the master page name to the view engine. In ASP.NET Core a Razor view
                // selects its own layout, so the requested layout is published through the view data
                // (`Layout = (string)ViewData["Layout"]` in the view or its _ViewStart).
                // This is the only way to pass a requested layout to a Core Razor view; verified
                // against the module view/_ViewStart convention used by the DNN MVC modules.
                viewData["Layout"] = masterName;
            }

            ViewEngineResult result = null;
            if (view is null)
            {
                result = FindView(actionContext, services, viewName, masterName, isMainPage, viewEngine, moduleViewEngines);
                if (!result.Success)
                {
                    throw new InvalidOperationException(
                        $"The view '{viewName}' was not found. Searched locations: {string.Join(", ", result.SearchedLocations ?? [])}");
                }

                view = result.View;
            }

            var htmlHelperOptions = services.GetService<IOptions<MvcViewOptions>>()?.Value?.HtmlHelperOptions
                                    ?? new HtmlHelperOptions();

            var viewContext = new Microsoft.AspNetCore.Mvc.Rendering.ViewContext(
                actionContext,
                view,
                viewData,
                tempData ?? new TempDataDictionary(actionContext.HttpContext, services.GetRequiredService<ITempDataProvider>()),
                writer,
                htmlHelperOptions);

            await view.RenderAsync(viewContext);

            return view;
        }

        private static ViewEngineResult FindView(
            ActionContext actionContext,
            IServiceProvider services,
            string viewName,
            string masterName,
            bool isMainPage,
            IViewEngine viewEngine,
            IReadOnlyList<IViewEngine> moduleViewEngines)
        {
            if (viewEngine is not null)
            {
                return viewEngine.FindView(actionContext, viewName, isMainPage);
            }

            var engines = moduleViewEngines
                          ?? ModuleApplication.GetCurrentDnnController(actionContext.HttpContext)?.ViewEngines;
            var hostSettings = services.GetRequiredService<IHostSettings>();

            return isMainPage
                ? engines.FindView(hostSettings, actionContext, viewName, masterName, true)
                : engines.FindPartialView(hostSettings, actionContext, viewName, true);
        }
    }
}
