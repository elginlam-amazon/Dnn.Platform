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

    /// <summary>Renders textarea elements.</summary>
    public static class HtmlTextAreaExtensions
    {
        private const int TextAreaRows = 2;
        private const int TextAreaColumns = 20;

        /// <summary>Renders a textarea for the given name.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="name">The name.</param>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name)
        {
            return html.HtmlHelper.TextArea(name);
        }

        /// <summary>Renders a textarea for the given name with HTML attributes.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="name">The name.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, object htmlAttributes)
        {
            return html.HtmlHelper.TextArea(name, null, htmlAttributes);
        }

        /// <summary>Renders a textarea for the given name with HTML attributes.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="name">The name.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, IDictionary<string, object> htmlAttributes)
        {
            return html.HtmlHelper.TextArea(name, null, htmlAttributes);
        }

        /// <summary>Renders a textarea for the given name and value.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, string value)
        {
            return html.HtmlHelper.TextArea(name, value, null);
        }

        /// <summary>Renders a textarea for the given name and value with HTML attributes.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, string value, object htmlAttributes)
        {
            return html.HtmlHelper.TextArea(name, value, htmlAttributes);
        }

        /// <summary>Renders a textarea for the given name and value with HTML attributes.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, string value, IDictionary<string, object> htmlAttributes)
        {
            return html.HtmlHelper.TextArea(name, value, htmlAttributes);
        }

        /// <summary>Renders a textarea with the given rows, columns, and HTML attributes.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <param name="rows">The number of rows.</param>
        /// <param name="columns">The number of columns.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, string value, int rows, int columns, object htmlAttributes)
        {
            return html.HtmlHelper.TextArea(name, value, rows, columns, htmlAttributes);
        }

        /// <summary>Renders a textarea with the given rows, columns, and HTML attributes.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <param name="rows">The number of rows.</param>
        /// <param name="columns">The number of columns.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, string value, int rows, int columns, IDictionary<string, object> htmlAttributes)
        {
            return html.HtmlHelper.TextArea(name, value, rows, columns, htmlAttributes);
        }

        /// <summary>Renders a textarea for the given model expression.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TProperty">The property type.</typeparam>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextAreaFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression)
        {
            return html.HtmlHelper.TextAreaFor(expression, TextAreaRows, TextAreaColumns, null);
        }

        /// <summary>Renders a textarea for the given model expression with HTML attributes.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TProperty">The property type.</typeparam>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextAreaFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, object htmlAttributes)
        {
            return html.HtmlHelper.TextAreaFor(expression, TextAreaRows, TextAreaColumns, htmlAttributes);
        }

        /// <summary>Renders a textarea for the given model expression with HTML attributes.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TProperty">The property type.</typeparam>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextAreaFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IDictionary<string, object> htmlAttributes)
        {
            return html.HtmlHelper.TextAreaFor(expression, TextAreaRows, TextAreaColumns, htmlAttributes);
        }

        /// <summary>Renders a textarea for the given model expression with rows, columns, and HTML attributes.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="rows">The number of rows.</param>
        /// <param name="columns">The number of columns.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TProperty">The property type.</typeparam>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextAreaFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, int rows, int columns, object htmlAttributes)
        {
            return html.HtmlHelper.TextAreaFor(expression, rows, columns, htmlAttributes);
        }

        /// <summary>Renders a textarea for the given model expression with rows, columns, and HTML attributes.</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="rows">The number of rows.</param>
        /// <param name="columns">The number of columns.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TProperty">The property type.</typeparam>
        /// <returns>The textarea HTML.</returns>
        public static IHtmlContent TextAreaFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, int rows, int columns, IDictionary<string, object> htmlAttributes)
        {
            return html.HtmlHelper.TextAreaFor(expression, rows, columns, htmlAttributes);
        }
    }
}
