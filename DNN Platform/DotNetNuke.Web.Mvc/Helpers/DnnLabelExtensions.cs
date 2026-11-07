// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.Rendering;

    using CoreHtmlHelper = Microsoft.AspNetCore.Mvc.ViewFeatures.HtmlHelper;

    public static class DnnLabelExtensions
    {
        public static IHtmlContent Label<TModel>(this DnnHelper<TModel> dnnHelper, string expression, string labelText, string helpText)
        {
            return Label(dnnHelper, expression, labelText, helpText, null);
        }

        public static IHtmlContent Label<TModel>(this DnnHelper<TModel> dnnHelper, string expression, string labelText, string helpText, object htmlAttributes)
        {
            return Label(dnnHelper, expression, labelText, helpText, CoreHtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes));
        }

        public static IHtmlContent Label<TModel>(this DnnHelper<TModel> dnnHelper, string expression, string labelText, string helpText, IDictionary<string, object> htmlAttributes)
        {
            var htmlHelper = dnnHelper.HtmlHelper;

            return LabelHelper(
                htmlHelper,
                ModelMetadataHelper.GetMetadata(htmlHelper, expression),
                expression,
                labelText,
                helpText,
                htmlAttributes);
        }

        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression)
        {
            return LabelFor(dnnHelper, expression, null, null, (IDictionary<string, object>)null);
        }

        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, object htmlAttributes)
        {
            return LabelFor(dnnHelper, expression, null, null, htmlAttributes);
        }

        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, IDictionary<string, object> htmlAttributes)
        {
            return LabelFor(dnnHelper, expression, null, null, htmlAttributes);
        }

        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, string helpText)
        {
            return LabelFor(dnnHelper, expression, null, helpText, (IDictionary<string, object>)null);
        }

        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, string helpText, object htmlAttributes)
        {
            return LabelFor(dnnHelper, expression, null, helpText, htmlAttributes);
        }

        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, string helpText, IDictionary<string, object> htmlAttributes)
        {
            return LabelFor(dnnHelper, expression, null, helpText, htmlAttributes);
        }

        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, string labelText, string helpText)
        {
            return LabelFor(dnnHelper, expression, labelText, helpText, (IDictionary<string, object>)null);
        }

        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, string labelText, string helpText, object htmlAttributes)
        {
            return LabelFor(dnnHelper, expression, labelText, helpText, CoreHtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes));
        }

        public static IHtmlContent LabelFor<TModel, TValue>(this DnnHelper<TModel> dnnHelper, Expression<Func<TModel, TValue>> expression, string labelText, string helpText, IDictionary<string, object> htmlAttributes)
        {
            var htmlHelper = dnnHelper.HtmlHelper;
            var modelExpression = ModelMetadataHelper.CreateModelExpression(htmlHelper, expression);

            return LabelHelper(
                htmlHelper,
                modelExpression.Metadata,
                modelExpression.Name,
                labelText,
                helpText,
                htmlAttributes);
        }

        /// <summary>Renders the DNN label markup (label, help icon and tooltip).</summary>
        /// <param name="html">The HTML helper.</param>
        /// <param name="htmlFieldName">The resolved HTML field id the label points at.</param>
        /// <param name="labelText">The label text.</param>
        /// <param name="helpText">The help text rendered into the tooltip.</param>
        /// <param name="htmlAttributes">Additional HTML attributes merged onto the wrapping div.</param>
        /// <returns>The label markup.</returns>
        public static IHtmlContent LabelHelper(IHtmlHelper html, string htmlFieldName, string labelText, string helpText = null, IDictionary<string, object> htmlAttributes = null)
        {
            if (string.IsNullOrEmpty(labelText))
            {
                // Matches the legacy behavior: without a label text there is nothing to render.
                return new HtmlContentBuilder();
            }

            var divTag = new TagBuilder("div");
            divTag.Attributes.Add("class", "dnnLabel");
            divTag.Attributes.Add("style", "position:relative;");
            divTag.MergeAttributes(htmlAttributes, true);

            var labelTag = new TagBuilder("label");
            labelTag.Attributes.Add("for", htmlFieldName);

            var spanTag = new TagBuilder("span");
            spanTag.InnerHtml.SetContent(labelText);

            labelTag.InnerHtml.AppendHtml(spanTag);

            divTag.InnerHtml.AppendHtml(labelTag);

            var aTag = new TagBuilder("a");
            aTag.Attributes.Add("class", "dnnFormHelp");
            aTag.Attributes.Add("href", "#");
            aTag.Attributes.Add("tabIndex", "-1");

            divTag.InnerHtml.AppendHtml(aTag);

            var toolTipTag = new TagBuilder("div");
            toolTipTag.Attributes.Add("class", "dnnTooltip");

            var toolTipContentTag = new TagBuilder("div");
            toolTipContentTag.Attributes.Add("class", "dnnFormHelpContent dnnClear");

            spanTag = new TagBuilder("span");
            spanTag.Attributes.Add("class", "dnnHelpText");
            spanTag.InnerHtml.SetContent(helpText ?? string.Empty);

            aTag = new TagBuilder("a");
            aTag.Attributes.Add("class", "pinHelp");
            aTag.Attributes.Add("href", "#");
            aTag.Attributes.Add("aria-label", "Pin");

            toolTipContentTag.InnerHtml.AppendHtml(spanTag);
            toolTipContentTag.InnerHtml.AppendHtml(aTag);

            toolTipTag.InnerHtml.AppendHtml(toolTipContentTag);

            divTag.InnerHtml.AppendHtml(toolTipTag);

            return divTag;
        }

        internal static IHtmlContent LabelHelper(IHtmlHelper html, ModelMetadata metadata, string htmlFieldName, string labelText = null, string helpText = null, IDictionary<string, object> htmlAttributes = null)
        {
            var resolvedLabelText = labelText ?? metadata?.DisplayName ?? metadata?.PropertyName ?? htmlFieldName?.Split('.').Last();
            var resolvedHelpText = helpText ?? metadata?.Description;
            var resolvedId = TagBuilder.CreateSanitizedId(
                html.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(htmlFieldName),
                html.IdAttributeDotReplacement);

            return LabelHelper(html, resolvedId, resolvedLabelText, resolvedHelpText, htmlAttributes);
        }
    }
}
