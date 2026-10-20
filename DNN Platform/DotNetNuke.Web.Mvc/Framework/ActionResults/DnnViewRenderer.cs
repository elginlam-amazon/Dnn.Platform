// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionResults
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.Razor;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Renders an MVC view into a <see cref="TextWriter"/> using the ambient ASP.NET Core services.</summary>
    internal static class DnnViewRenderer
    {
        /// <summary>Renders the named view (or the current action's view) into <paramref name="writer"/>.</summary>
        /// <param name="context">The action context.</param>
        /// <param name="writer">The writer into which to render.</param>
        /// <param name="viewName">The view name, or <see langword="null"/> to use the current action name.</param>
        /// <param name="viewData">The view data.</param>
        /// <param name="tempData">The temp data.</param>
        /// <param name="isPartial"><see langword="true"/> to render a partial view.</param>
        /// <returns>A task that completes when rendering is done.</returns>
        public static async Task RenderAsync(
            ActionContext context,
            TextWriter writer,
            string viewName,
            ViewDataDictionary viewData,
            ITempDataDictionary tempData,
            bool isPartial)
        {
            var services = context.HttpContext.RequestServices;
            var viewEngine = services.GetRequiredService<ICompositeViewEngine>();
            var tempDataProvider = services.GetRequiredService<ITempDataProvider>();
            var modelMetadataProvider = services.GetRequiredService<IModelMetadataProvider>();

            if (string.IsNullOrEmpty(viewName))
            {
                viewName = (context.RouteData.Values.TryGetValue("action", out var action) ? action : null)?.ToString();
            }

            var viewEngineResult = isPartial
                ? viewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: false)
                : viewEngine.FindView(context, viewName, isMainPage: true);

            if (!viewEngineResult.Success)
            {
                viewEngineResult = viewEngine.FindView(context, viewName, isMainPage: !isPartial);
            }

            if (!viewEngineResult.Success)
            {
                throw new InvalidOperationException($"The view '{viewName}' was not found.");
            }

            var view = viewEngineResult.View;

            viewData ??= new ViewDataDictionary(modelMetadataProvider, context.ModelState);
            tempData ??= new TempDataDictionary(context.HttpContext, tempDataProvider);

            var viewContext = new ViewContext(
                context,
                view,
                viewData,
                tempData,
                writer,
                new HtmlHelperOptions());

            await view.RenderAsync(viewContext);
        }
    }
}
