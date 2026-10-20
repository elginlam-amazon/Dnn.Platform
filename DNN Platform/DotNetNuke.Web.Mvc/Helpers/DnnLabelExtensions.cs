// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text.Encodings.Web;

    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Renders DNN form labels with an inline help tooltip.</summary>
    public static class DnnLabelExtensions
    {
        /// <summary>Renders a DNN label with help text for the given expression.</summary>
        /// <param name="dnnHelper">The DNN helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="labelText">The label text.</param>
        /// <param name="helpText">The help text.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <returns>The label HTML.</returns>
        public static IHtmlContent Label<TModel>(this DnnHelper<TModel> dnnHelper, string expression, string labelText, string helpText)
        {
            return Label(dnnHelper, expression, labelText, helpText, null);
        }

        /// <summary>Renders a DNN label with help text and HTML attributes for the given expression.</summary>
        /// <param name="dnnHelper">The DNN helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="labelText">The label text.</param>
        /// <param name="helpText">The help text.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <returns>The label HTML.</returns>
        public static IHtmlContent Label<TModel>(this DnnHelper<TModel> dnnHelper, string expression, string labelText, string helpText, object htmlAttributes)
        {
            return Label(dnnHelper, expression, labelText, helpText, HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes));
        }

        /// <summary>Renders a DNN label with help text and HTML attributes for the given expression.</summary>
        /// <param name="dnnHelper">The DNN helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="labelText">The label text.</param>
        /// <param name="helpText">The help text.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <returns>The label HTML.</returns>
        public static IHtmlContent Label<TModel>(this DnnHelper<TModel> dnnHelper, string expression, string labelText, string helpText, IDictionary<string, object> htmlAttributes)
        {
            var htmlHelper = (IHtmlHelper<TModel>)dnnHelper.HtmlHelper;
            var fieldName = htmlHelper.ViewData.TemplateInfo.GetFullHtmlFieldName(expression);
            return LabelHelper(TagBuilder.CreateSanitizedId(fieldName, "_"), labelText, helpText, htmlAttributes);
        }

        /// <summary>Renders a DNN label for the given model expression.</summary>
        /// <param name="dnnHelper">The DNN helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns>The label HTML.</returns>
        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression)
        {
            return LabelFor(dnnHelper, expression, null, null, null);
        }

        /// <summary>Renders a DNN label for the given model expression with HTML attributes.</summary>
        /// <param name="dnnHelper">The DNN helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns>The label HTML.</returns>
        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, object htmlAttributes)
        {
            return LabelFor(dnnHelper, expression, null, null, htmlAttributes);
        }

        /// <summary>Renders a DNN label for the given model expression with HTML attributes.</summary>
        /// <param name="dnnHelper">The DNN helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns>The label HTML.</returns>
        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, IDictionary<string, object> htmlAttributes)
        {
            return LabelFor(dnnHelper, expression, null, null, htmlAttributes);
        }

        /// <summary>Renders a DNN label for the given model expression with help text.</summary>
        /// <param name="dnnHelper">The DNN helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="helpText">The help text.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns>The label HTML.</returns>
        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, string helpText)
        {
            return LabelFor(dnnHelper, expression, null, helpText, null);
        }

        /// <summary>Renders a DNN label for the given model expression with help text and HTML attributes.</summary>
        /// <param name="dnnHelper">The DNN helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="helpText">The help text.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns>The label HTML.</returns>
        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, string helpText, object htmlAttributes)
        {
            return LabelFor(dnnHelper, expression, null, helpText, htmlAttributes);
        }

        /// <summary>Renders a DNN label for the given model expression with help text and HTML attributes.</summary>
        /// <param name="dnnHelper">The DNN helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="helpText">The help text.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns>The label HTML.</returns>
        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, string helpText, IDictionary<string, object> htmlAttributes)
        {
            return LabelFor(dnnHelper, expression, null, helpText, htmlAttributes);
        }

        /// <summary>Renders a DNN label for the given model expression with label text and help text.</summary>
        /// <param name="dnnHelper">The DNN helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="labelText">The label text.</param>
        /// <param name="helpText">The help text.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns>The label HTML.</returns>
        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, string labelText, string helpText)
        {
            return LabelFor(dnnHelper, expression, labelText, helpText, null);
        }

        /// <summary>Renders a DNN label for the given model expression with label text, help text, and HTML attributes.</summary>
        /// <param name="dnnHelper">The DNN helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="labelText">The label text.</param>
        /// <param name="helpText">The help text.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns>The label HTML.</returns>
        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, string labelText, string helpText, object htmlAttributes)
        {
            return LabelFor(dnnHelper, expression, labelText, helpText, HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes));
        }

        /// <summary>Renders a DNN label for the given model expression with label text, help text, and HTML attributes.</summary>
        /// <param name="dnnHelper">The DNN helper.</param>
        /// <param name="expression">The model expression.</param>
        /// <param name="labelText">The label text.</param>
        /// <param name="helpText">The help text.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns>The label HTML.</returns>
        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, string labelText, string helpText, IDictionary<string, object> htmlAttributes)
        {
            var htmlHelper = (IHtmlHelper<TModel>)dnnHelper.HtmlHelper;
            var expressionProvider = htmlHelper.ViewContext.HttpContext.RequestServices.GetRequiredService<ModelExpressionProvider>();
            var modelExpression = expressionProvider.CreateModelExpression(htmlHelper.ViewData, expression);
            var fieldName = htmlHelper.ViewData.TemplateInfo.GetFullHtmlFieldName(modelExpression.Name);

            var resolvedLabelText = labelText
                ?? modelExpression.Metadata.DisplayName
                ?? modelExpression.Metadata.PropertyName
                ?? modelExpression.Name.Split('.').Last();
            var resolvedHelpText = helpText ?? modelExpression.Metadata.Description;

            return LabelHelper(TagBuilder.CreateSanitizedId(fieldName, "_"), resolvedLabelText, resolvedHelpText, htmlAttributes);
        }

        private static IHtmlContent LabelHelper(string htmlFieldName, string labelText, string helpText, IDictionary<string, object> htmlAttributes)
        {
            if (string.IsNullOrEmpty(labelText))
            {
                return HtmlString.Empty;
            }

            var divTag = new TagBuilder("div");
            divTag.Attributes["class"] = "dnnLabel";
            divTag.Attributes["style"] = "position:relative;";
            if (htmlAttributes != null)
            {
                divTag.MergeAttributes(htmlAttributes, true);
            }

            var labelTag = new TagBuilder("label");
            labelTag.Attributes["for"] = htmlFieldName;

            var spanTag = new TagBuilder("span");
            spanTag.InnerHtml.SetContent(labelText);
            labelTag.InnerHtml.SetHtmlContent(spanTag);

            divTag.InnerHtml.SetHtmlContent(labelTag);

            var aTag = new TagBuilder("a");
            aTag.Attributes["class"] = "dnnFormHelp";
            aTag.Attributes["href"] = "#";
            aTag.Attributes["tabIndex"] = "-1";
            divTag.InnerHtml.AppendHtml(aTag);

            var toolTipTag = new TagBuilder("div");
            toolTipTag.Attributes["class"] = "dnnTooltip";

            var toolTipContentTag = new TagBuilder("div");
            toolTipContentTag.Attributes["class"] = "dnnFormHelpContent dnnClear";

            var helpSpanTag = new TagBuilder("span");
            helpSpanTag.Attributes["class"] = "dnnHelpText";
            helpSpanTag.InnerHtml.SetContent(helpText ?? string.Empty);

            var pinTag = new TagBuilder("a");
            pinTag.Attributes["class"] = "pinHelp";
            pinTag.Attributes["href"] = "#";
            pinTag.Attributes["aria-label"] = "Pin";

            toolTipContentTag.InnerHtml.SetHtmlContent(helpSpanTag);
            toolTipContentTag.InnerHtml.AppendHtml(pinTag);
            toolTipTag.InnerHtml.SetHtmlContent(toolTipContentTag);
            divTag.InnerHtml.AppendHtml(toolTipTag);

            using var writer = new StringWriter();
            divTag.WriteTo(writer, HtmlEncoder.Default);
            return new HtmlString(writer.ToString());
        }
    }
}
