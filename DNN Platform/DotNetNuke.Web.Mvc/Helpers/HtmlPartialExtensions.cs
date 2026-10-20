// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Helpers
{
    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;

    /// <summary>Represents the functionality to render a partial view as an HTML-encoded string.</summary>
    public static class HtmlPartialExtensions
    {
        /// <summary>Renders the specified partial view as an HTML-encoded string.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="partialViewName">The name of the partial view to render.</param>
        /// <returns>The partial view that is rendered as an HTML-encoded string.</returns>
        public static IHtmlContent Partial(this DnnHtmlHelper html, string partialViewName)
        {
            return html.HtmlHelper.Partial(partialViewName);
        }

        /// <summary>Renders the specified partial view as an HTML-encoded string.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="partialViewName">The name of the partial view to render.</param>
        /// <param name="viewData">The view data dictionary for the partial view.</param>
        /// <returns>The partial view that is rendered as an HTML-encoded string.</returns>
        public static IHtmlContent Partial(this DnnHtmlHelper html, string partialViewName, ViewDataDictionary viewData)
        {
            return html.HtmlHelper.Partial(partialViewName, viewData);
        }

        /// <summary>Renders the specified partial view as an HTML-encoded string.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="partialViewName">The name of the partial view to render.</param>
        /// <param name="model">The model for the partial view.</param>
        /// <returns>The partial view that is rendered as an HTML-encoded string.</returns>
        public static IHtmlContent Partial(this DnnHtmlHelper html, string partialViewName, object model)
        {
            return html.HtmlHelper.Partial(partialViewName, model, html.HtmlHelper.ViewData);
        }

        /// <summary>Renders the specified partial view as an HTML-encoded string.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="partialViewName">The name of the partial view.</param>
        /// <param name="model">The model for the partial view.</param>
        /// <param name="viewData">The view data dictionary for the partial view.</param>
        /// <returns>The partial view that is rendered as an HTML-encoded string.</returns>
        public static IHtmlContent Partial(this DnnHtmlHelper html, string partialViewName, object model, ViewDataDictionary viewData)
        {
            return html.HtmlHelper.Partial(partialViewName, model, viewData);
        }
    }
}
