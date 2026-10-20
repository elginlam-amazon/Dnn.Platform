// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;
    using System.Linq.Expressions;

    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Mvc.Rendering;

    /// <summary>Provides a mechanism to create custom HTML markup compatible with the model binders and templates.</summary>
    public static class HtmlValueExtensions
    {
        /// <summary>Gets the model value for the given name.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the model.</param>
        /// <returns>The HTML markup for the value.</returns>
        public static IHtmlContent Value(this DnnHtmlHelper html, string name)
        {
            return new HtmlString(html.HtmlHelper.Value(name, null));
        }

        /// <summary>Gets the formatted model value for the given name.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the model.</param>
        /// <param name="format">The format string.</param>
        /// <returns>The HTML markup for the value.</returns>
        public static IHtmlContent Value(this DnnHtmlHelper html, string name, string format)
        {
            return new HtmlString(html.HtmlHelper.Value(name, format));
        }

        /// <summary>Gets the model value for the object represented by the expression.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the properties to expose.</param>
        /// <typeparam name="TModel">The model.</typeparam>
        /// <typeparam name="TProperty">The property.</typeparam>
        /// <returns>The HTML markup for the value.</returns>
        public static IHtmlContent ValueFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression)
        {
            return new HtmlString(html.HtmlHelper.ValueFor(expression, null));
        }

        /// <summary>Gets the formatted model value for the object represented by the expression.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the properties to expose.</param>
        /// <param name="format">The format string.</param>
        /// <typeparam name="TModel">The model.</typeparam>
        /// <typeparam name="TProperty">The property.</typeparam>
        /// <returns>The HTML markup for the value.</returns>
        public static IHtmlContent ValueFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, string format)
        {
            return new HtmlString(html.HtmlHelper.ValueFor(expression, format));
        }

        /// <summary>Gets the model value for the current model.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <returns>The HTML markup for the value.</returns>
        public static IHtmlContent ValueForModel(this DnnHtmlHelper html)
        {
            return new HtmlString(html.HtmlHelper.ValueForModel(null));
        }

        /// <summary>Gets the formatted model value for the current model.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="format">The format string.</param>
        /// <returns>The HTML markup for the value.</returns>
        public static IHtmlContent ValueForModel(this DnnHtmlHelper html, string format)
        {
            return new HtmlString(html.HtmlHelper.ValueForModel(format));
        }
    }
}
