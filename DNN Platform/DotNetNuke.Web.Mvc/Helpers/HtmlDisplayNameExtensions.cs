// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;

    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Mvc.Rendering;

    /// <summary>Provides a mechanism to get display names.</summary>
    public static class HtmlDisplayNameExtensions
    {
        /// <summary>Gets the display name for the given expression.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the display name.</param>
        /// <returns>The display name.</returns>
        public static IHtmlContent DisplayName(this DnnHtmlHelper html, string expression)
        {
            return new HtmlString(html.HtmlHelper.DisplayName(expression));
        }

        /// <summary>Gets the display name for an item in a collection model.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the display name.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <returns>The display name.</returns>
        public static IHtmlContent DisplayNameFor<TModel, TValue>(this DnnHtmlHelper<IEnumerable<TModel>> html, Expression<Func<TModel, TValue>> expression)
        {
            return new HtmlString(html.HtmlHelper.DisplayNameForInnerType(expression));
        }

        /// <summary>Gets the display name for the model expression.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the display name.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <returns>The display name.</returns>
        public static IHtmlContent DisplayNameFor<TModel, TValue>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression)
        {
            return new HtmlString(html.HtmlHelper.DisplayNameFor(expression));
        }

        /// <summary>Gets the display name for the current model.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <returns>The display name.</returns>
        public static IHtmlContent DisplayNameForModel(this DnnHtmlHelper html)
        {
            return new HtmlString(html.HtmlHelper.DisplayNameForModel());
        }
    }
}
