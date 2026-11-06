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
    using Microsoft.AspNetCore.Mvc.ViewFeatures;

    /// <summary>Provides a way to render object values as HTML.</summary>
    public static class HtmlDisplayTextExtensions
    {
        /// <summary>Returns HTML markup for each property in the object that is represented by the specified expression.</summary>
        ///
        /// <returns>
        /// The HTML markup for each property in the object that is represented by the expression.
        /// </returns>
        /// <param name="html">The HTML helper instance that this method extends.</param><param name="name">An expression that identifies the object that contains the properties to display.</param>
        public static IHtmlContent DisplayText(this DnnHtmlHelper html, string name)
        {
            return new HtmlString(html.HtmlHelper.Encode(html.HtmlHelper.DisplayText(name)));
        }

        /// <summary>Returns HTML markup for each property in the object that is represented by the specified expression.</summary>
        ///
        /// <returns>
        /// The HTML markup for each property.
        /// </returns>
        /// <param name="html">The HTML helper instance that this method extends.</param><param name="expression">An expression that identifies the object that contains the properties to display.</param><typeparam name="TModel">The type of the model.</typeparam><typeparam name="TResult">The type of the result.</typeparam>
        public static IHtmlContent DisplayTextFor<TModel, TResult>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TResult>> expression)
        {
            var htmlHelper = html.HtmlHelper;
            return new HtmlString(htmlHelper.Encode(htmlHelper.DisplayTextFor(expression)));
        }
    }
}
