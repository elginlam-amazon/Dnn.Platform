// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Helpers
{
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;

    /// <summary>Renders a partial view directly to the response.</summary>
    public static class HtmlRenderPartialExtensions
    {
        /// <summary>Renders the specified partial view by using the specified HTML helper.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="partialViewName">The name of the partial view.</param>
        public static void RenderPartial(this DnnHtmlHelper html, string partialViewName)
        {
            html.HtmlHelper.RenderPartialAsync(partialViewName, html.HtmlHelper.ViewData.Model, html.HtmlHelper.ViewData).GetAwaiter().GetResult();
        }

        /// <summary>Renders the specified partial view, replacing its ViewData with the specified dictionary.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="partialViewName">The name of the partial view.</param>
        /// <param name="viewData">The view data.</param>
        public static void RenderPartial(this DnnHtmlHelper html, string partialViewName, ViewDataDictionary viewData)
        {
            html.HtmlHelper.RenderPartialAsync(partialViewName, viewData.Model, viewData).GetAwaiter().GetResult();
        }

        /// <summary>Renders the specified partial view, passing it a copy of the current ViewData but with the specified model.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="model">The model.</param>
        /// <param name="partialViewName">The name of the partial view.</param>
        public static void RenderPartial(this DnnHtmlHelper html, object model, string partialViewName)
        {
            html.HtmlHelper.RenderPartialAsync(partialViewName, model, html.HtmlHelper.ViewData).GetAwaiter().GetResult();
        }

        /// <summary>Renders the specified partial view, replacing its ViewData and setting the model.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="partialViewName">The name of the partial view.</param>
        /// <param name="model">The model for the partial view.</param>
        /// <param name="viewData">The view data for the partial view.</param>
        public static void RenderPartial(this DnnHtmlHelper html, string partialViewName, object model, ViewDataDictionary viewData)
        {
            html.HtmlHelper.RenderPartialAsync(partialViewName, model, viewData).GetAwaiter().GetResult();
        }
    }
}
