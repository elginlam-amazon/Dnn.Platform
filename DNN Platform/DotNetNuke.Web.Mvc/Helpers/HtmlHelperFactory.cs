// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Helpers
{
    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Creates ASP.NET Core <see cref="IHtmlHelper"/> instances bound to a <see cref="ViewContext"/>.</summary>
    /// <remarks>
    /// In ASP.NET MVC 5 the HTML helpers were created with <c>new HtmlHelper(viewContext, viewDataContainer, routes)</c>.
    /// ASP.NET Core resolves them from the request container and binds them to the view context through
    /// <see cref="IViewContextAware"/>.
    /// </remarks>
    internal static class HtmlHelperFactory
    {
        /// <summary>Creates a non generic HTML helper for the given view context.</summary>
        /// <param name="viewContext">The view context.</param>
        /// <returns>An <see cref="IHtmlHelper"/> instance.</returns>
        internal static IHtmlHelper Create(ViewContext viewContext)
        {
            Requires.NotNull("viewContext", viewContext);

            var htmlHelper = GetServices(viewContext).GetRequiredService<IHtmlHelper>();
            Contextualize(htmlHelper, viewContext);
            return htmlHelper;
        }

        /// <summary>Creates a strongly typed HTML helper for the given view context.</summary>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <param name="viewContext">The view context.</param>
        /// <returns>An <see cref="IHtmlHelper{TModel}"/> instance.</returns>
        internal static IHtmlHelper<TModel> Create<TModel>(ViewContext viewContext)
        {
            Requires.NotNull("viewContext", viewContext);

            var htmlHelper = GetServices(viewContext).GetRequiredService<IHtmlHelper<TModel>>();
            Contextualize(htmlHelper, viewContext);
            return htmlHelper;
        }

        private static void Contextualize(object htmlHelper, ViewContext viewContext)
        {
            if (htmlHelper is IViewContextAware viewContextAware)
            {
                viewContextAware.Contextualize(viewContext);
            }
        }

        private static System.IServiceProvider GetServices(ViewContext viewContext)
            => viewContext.HttpContext?.RequestServices ?? Globals.GetCurrentServiceProvider();
    }
}
