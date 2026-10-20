// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;
    using System.Linq.Expressions;

    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Mvc.Rendering;

    /// <summary>Provides a way to render object values as display text.</summary>
    public static class HtmlDisplayTextExtensions
    {
        /// <summary>Returns display text for the property identified by the specified name.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">An expression that identifies the object that contains the properties to display.</param>
        /// <returns>The display text.</returns>
        public static IHtmlContent DisplayText(this DnnHtmlHelper html, string name)
        {
            return new HtmlString(html.HtmlHelper.DisplayText(name));
        }

        /// <summary>Returns display text for the property identified by the specified expression.</summary>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the properties to display.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <returns>The display text.</returns>
        public static IHtmlContent DisplayTextFor<TModel, TResult>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TResult>> expression)
        {
            return new HtmlString(html.HtmlHelper.DisplayTextFor(expression));
        }
    }
}
