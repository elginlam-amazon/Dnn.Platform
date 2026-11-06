// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;
    using System.Linq.Expressions;

    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Mvc.Rendering;

    /// <summary>Gets the HTML ID and name attributes of the <see cref="DnnHtmlHelper"/> string.</summary>
    public static class HtmlNameExtensions
    {
        /// <summary>Gets the ID of the <see cref="DnnHtmlHelper"/> string.</summary>
        /// <returns>The HTML ID attribute value for the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">An expression that identifies the object that contains the ID.</param>
        public static IHtmlContent Id(this DnnHtmlHelper html, string name)
        {
            return new HtmlString(html.HtmlHelper.Id(name));
        }

        /// <summary>Gets the ID of the <see cref="DnnHtmlHelper"/> string.</summary>
        /// <returns>The HTML ID attribute value for the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the ID.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam><typeparam name="TProperty">The type of the property.</typeparam>
        public static IHtmlContent IdFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression)
        {
            return new HtmlString(html.HtmlHelper.IdFor(expression));
        }

        /// <summary>Gets the ID of the <see cref="DnnHtmlHelper"/> string.</summary>
        /// <returns>The HTML ID attribute value for the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        public static IHtmlContent IdForModel(this DnnHtmlHelper html)
        {
            return new HtmlString(html.HtmlHelper.Id(string.Empty));
        }

        /// <summary>Gets the full HTML field name for the object that is represented by the expression.</summary>
        /// <returns>The full HTML field name for the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">An expression that identifies the object that contains the name.</param>
        public static IHtmlContent Name(this DnnHtmlHelper html, string name)
        {
            return new HtmlString(html.HtmlHelper.Name(name));
        }

        /// <summary>Gets the full HTML field name for the object that is represented by the expression.</summary>
        /// <returns> The full HTML field name for the object that is represented by the expression. </returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the name.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        public static IHtmlContent NameFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression)
        {
            return new HtmlString(html.HtmlHelper.NameFor(expression));
        }

        /// <summary>Gets the full HTML field name for the object that is represented by the expression.</summary>
        /// <returns>The full HTML field name for the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        public static IHtmlContent NameForModel(this DnnHtmlHelper html)
        {
            return new HtmlString(html.HtmlHelper.Name(string.Empty));
        }
    }
}
