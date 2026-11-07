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

    public static class HtmlTextAreaExtensions
    {
        private const int TextAreaRows = 2;
        private const int TextAreaColumns = 20;

        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextArea(name, null, TextAreaRows, TextAreaColumns, null);
        }

        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, object htmlAttributes)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextArea(name, null, TextAreaRows, TextAreaColumns, htmlAttributes);
        }

        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, IDictionary<string, object> htmlAttributes)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextArea(name, null, TextAreaRows, TextAreaColumns, htmlAttributes);
        }

        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, string value)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextArea(name, value, TextAreaRows, TextAreaColumns, null);
        }

        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, string value, object htmlAttributes)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextArea(name, value, TextAreaRows, TextAreaColumns, htmlAttributes);
        }

        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, string value, IDictionary<string, object> htmlAttributes)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextArea(name, value, TextAreaRows, TextAreaColumns, htmlAttributes);
        }

        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, string value, int rows, int columns, object htmlAttributes)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextArea(name, value, rows, columns, htmlAttributes);
        }

        public static IHtmlContent TextArea(this DnnHtmlHelper html, string name, string value, int rows, int columns, IDictionary<string, object> htmlAttributes)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextArea(name, value, rows, columns, htmlAttributes);
        }

        public static IHtmlContent TextAreaFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextAreaFor(expression, TextAreaRows, TextAreaColumns, null);
        }

        public static IHtmlContent TextAreaFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, object htmlAttributes)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextAreaFor(expression, TextAreaRows, TextAreaColumns, htmlAttributes);
        }

        public static IHtmlContent TextAreaFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IDictionary<string, object> htmlAttributes)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextAreaFor(expression, TextAreaRows, TextAreaColumns, htmlAttributes);
        }

        public static IHtmlContent TextAreaFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, int rows, int columns, object htmlAttributes)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextAreaFor(expression, rows, columns, htmlAttributes);
        }

        public static IHtmlContent TextAreaFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, int rows, int columns, IDictionary<string, object> htmlAttributes)
        {
            var htmlHelper = html.HtmlHelper;
            return htmlHelper.TextAreaFor(expression, rows, columns, htmlAttributes);
        }
    }
}
