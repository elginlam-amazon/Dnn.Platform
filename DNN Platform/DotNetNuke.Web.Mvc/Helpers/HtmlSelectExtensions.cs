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

    /// <summary>Represents support for making selections in a list.</summary>
    public static class HtmlSelectExtensions
    {
        // DropDownList

        /// <summary>Returns a single-selection select element using the specified HTML helper and the name of the form field.</summary>
        /// <returns>An HTML select element.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the form field to return.</param>
        /// <exception cref="System.ArgumentException">The <paramref name="name"/> parameter is null or empty.</exception>
        public static IHtmlContent DropDownList(this DnnHtmlHelper html, string name)
        {
            return html.HtmlHelper.DropDownList(name, null, null, null);
        }

        /// <summary>Returns a single-selection select element using the specified HTML helper, the name of the form field, and an option label.</summary>
        /// <returns>An HTML select element with an option subelement for each item in the list.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the form field to return.</param>
        /// <param name="optionLabel">The text for a default empty item. This parameter can be null.</param>
        /// <exception cref="System.ArgumentException">The <paramref name="name"/> parameter is null or empty.</exception>
        public static IHtmlContent DropDownList(this DnnHtmlHelper html, string name, string optionLabel)
        {
            return html.HtmlHelper.DropDownList(name, null, optionLabel, null);
        }

        /// <summary>Returns a single-selection select element using the specified HTML helper, the name of the form field, and the specified list items.</summary>
        /// <returns>An HTML select element with an option subelement for each item in the list.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the form field to return.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <exception cref="System.ArgumentException">The <paramref name="name"/> parameter is null or empty.</exception>
        public static IHtmlContent DropDownList(this DnnHtmlHelper html, string name, IEnumerable<SelectListItem> selectList)
        {
            return html.HtmlHelper.DropDownList(name, selectList, null, null);
        }

        /// <summary>Returns a single-selection select element using the specified HTML helper, the name of the form field, the specified list items, and the specified HTML attributes.</summary>
        /// <returns>An HTML select element with an option subelement for each item in the list.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the form field to return.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <exception cref="System.ArgumentException">The <paramref name="name"/> parameter is null or empty.</exception>
        public static IHtmlContent DropDownList(this DnnHtmlHelper html, string name, IEnumerable<SelectListItem> selectList, object htmlAttributes)
        {
            return html.HtmlHelper.DropDownList(name, selectList, null, htmlAttributes);
        }

        /// <summary>Returns a single-selection select element using the specified HTML helper, the name of the form field, the specified list items, and the specified HTML attributes.</summary>
        /// <returns>An HTML select element with an option subelement for each item in the list.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the form field to return.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <exception cref="System.ArgumentException">The <paramref name="name"/> parameter is null or empty.</exception>
        public static IHtmlContent DropDownList(this DnnHtmlHelper html, string name, IEnumerable<SelectListItem> selectList, IDictionary<string, object> htmlAttributes)
        {
            return html.HtmlHelper.DropDownList(name, selectList, null, htmlAttributes);
        }

        /// <summary>Returns a single-selection select element using the specified HTML helper, the name of the form field, the specified list items, and an option label.</summary>
        /// <returns>An HTML select element with an option subelement for each item in the list.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the form field to return.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="optionLabel">The text for a default empty item. This parameter can be null.</param>
        /// <exception cref="System.ArgumentException">The <paramref name="name"/> parameter is null or empty.</exception>
        public static IHtmlContent DropDownList(this DnnHtmlHelper html, string name, IEnumerable<SelectListItem> selectList, string optionLabel)
        {
            return html.HtmlHelper.DropDownList(name, selectList, optionLabel, null);
        }

        /// <summary>Returns a single-selection select element using the specified HTML helper, the name of the form field, the specified list items, an option label, and the specified HTML attributes.</summary>
        /// <returns>An HTML select element with an option subelement for each item in the list.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param><param name="name">The name of the form field to return.</param><param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="optionLabel">The text for a default empty item. This parameter can be null.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <exception cref="System.ArgumentException">The <paramref name="name"/> parameter is null or empty.</exception>
        public static IHtmlContent DropDownList(this DnnHtmlHelper html, string name, IEnumerable<SelectListItem> selectList, string optionLabel, object htmlAttributes)
        {
            return html.HtmlHelper.DropDownList(name, selectList, optionLabel, htmlAttributes);
        }

        /// <summary>Returns a single-selection select element using the specified HTML helper, the name of the form field, the specified list items, an option label, and the specified HTML attributes.</summary>
        /// <returns>An HTML select element with an option subelement for each item in the list.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the form field to return.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="optionLabel">The text for a default empty item. This parameter can be null.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <exception cref="System.ArgumentException">The <paramref name="name"/> parameter is null or empty.</exception>
        public static IHtmlContent DropDownList(this DnnHtmlHelper html, string name, IEnumerable<SelectListItem> selectList, string optionLabel, IDictionary<string, object> htmlAttributes)
        {
            return html.HtmlHelper.DropDownList(name, selectList, optionLabel, htmlAttributes);
        }

        /// <summary>Returns an HTML select element for each property in the object that is represented by the specified expression using the specified list items.</summary>
        /// <returns>An HTML select element for each property in the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the properties to display.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TProperty">The type of the value.</typeparam>
        /// <exception cref="System.ArgumentNullException">The <paramref name="expression"/> parameter is null.</exception>
        public static IHtmlContent DropDownListFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IEnumerable<SelectListItem> selectList)
        {
            return html.HtmlHelper.DropDownListFor(expression, selectList, null, null);
        }

        /// <summary>Returns an HTML select element for each property in the object that is represented by the specified expression using the specified list items and HTML attributes.</summary>
        /// <returns>An HTML select element for each property in the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the properties to display.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TProperty">The type of the value.</typeparam>
        /// <exception cref="System.ArgumentNullException">The <paramref name="expression"/> parameter is null.</exception>
        public static IHtmlContent DropDownListFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IEnumerable<SelectListItem> selectList, object htmlAttributes)
        {
            return html.HtmlHelper.DropDownListFor(expression, selectList, null, htmlAttributes);
        }

        /// <summary>Returns an HTML select element for each property in the object that is represented by the specified expression using the specified list items and HTML attributes.</summary>
        /// <returns>An HTML select element for each property in the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the properties to display.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TProperty">The type of the value.</typeparam>
        /// <exception cref="System.ArgumentNullException">The <paramref name="expression"/> parameter is null.</exception>
        public static IHtmlContent DropDownListFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IEnumerable<SelectListItem> selectList, IDictionary<string, object> htmlAttributes)
        {
            return html.HtmlHelper.DropDownListFor(expression, selectList, null, htmlAttributes);
        }

        /// <summary>Returns an HTML select element for each property in the object that is represented by the specified expression using the specified list items and option label.</summary>
        /// <returns>An HTML select element for each property in the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the properties to display.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="optionLabel">The text for a default empty item. This parameter can be null.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TProperty">The type of the value.</typeparam>
        /// <exception cref="System.ArgumentNullException">The <paramref name="expression"/> parameter is null.</exception>
        public static IHtmlContent DropDownListFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IEnumerable<SelectListItem> selectList, string optionLabel)
        {
            return html.HtmlHelper.DropDownListFor(expression, selectList, optionLabel, null);
        }

        /// <summary>Returns an HTML select element for each property in the object that is represented by the specified expression using the specified list items, option label, and HTML attributes.</summary>
        /// <returns>An HTML select element for each property in the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the properties to display.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="optionLabel">The text for a default empty item. This parameter can be null.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TProperty">The type of the value.</typeparam>
        /// <exception cref="System.ArgumentNullException">The <paramref name="expression"/> parameter is null.</exception>
        public static IHtmlContent DropDownListFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IEnumerable<SelectListItem> selectList, string optionLabel, object htmlAttributes)
        {
            return html.HtmlHelper.DropDownListFor(expression, selectList, optionLabel, htmlAttributes);
        }

        /// <summary>Returns an HTML select element for each property in the object that is represented by the specified expression using the specified list items, option label, and HTML attributes.</summary>
        /// <returns>An HTML select element for each property in the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the properties to display.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="optionLabel">The text for a default empty item. This parameter can be null.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TProperty">The type of the value.</typeparam>
        /// <exception cref="System.ArgumentNullException">The <paramref name="expression"/> parameter is null.</exception>
        public static IHtmlContent DropDownListFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IEnumerable<SelectListItem> selectList, string optionLabel, IDictionary<string, object> htmlAttributes)
        {
            return html.HtmlHelper.DropDownListFor(expression, selectList, optionLabel, htmlAttributes);
        }

        /// <summary>Returns an HTML select element for each value in the enumeration that is represented by the specified expression.</summary>
        /// <returns>An HTML select element for each value in the enumeration that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the values to display.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TEnum">The type of the value.</typeparam>
        public static IHtmlContent EnumDropDownListFor<TModel, TEnum>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TEnum>> expression)
        {
            return EnumDropDownListForHelper(html, expression, null, null);
        }

        /// <summary>Returns an HTML select element for each value in the enumeration that is represented by the specified expression.</summary>
        /// <returns>An HTML select element for each value in the enumeration that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the values to display.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TEnum">The type of the value.</typeparam>
        public static IHtmlContent EnumDropDownListFor<TModel, TEnum>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TEnum>> expression, object htmlAttributes)
        {
            return EnumDropDownListForHelper(html, expression, null, htmlAttributes);
        }

        /// <summary>Returns an HTML select element for each value in the enumeration that is represented by the specified expression.</summary>
        /// <returns>An HTML select element for each value in the enumeration that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the values to display.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TEnum">The type of the value.</typeparam>
        public static IHtmlContent EnumDropDownListFor<TModel, TEnum>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TEnum>> expression, IDictionary<string, object> htmlAttributes)
        {
            return EnumDropDownListForHelper(html, expression, null, htmlAttributes);
        }

        /// <summary>Returns an HTML select element for each value in the enumeration that is represented by the specified expression.</summary>
        /// <returns>An HTML select element for each value in the enumeration that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the values to display.</param>
        /// <param name="optionLabel">The text for a default empty item. This parameter can be null.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TEnum">The type of the value.</typeparam>
        public static IHtmlContent EnumDropDownListFor<TModel, TEnum>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TEnum>> expression, string optionLabel)
        {
            return EnumDropDownListForHelper(html, expression, optionLabel, null);
        }

        /// <summary>Returns an HTML select element for each value in the enumeration that is represented by the specified expression.</summary>
        /// <returns>An HTML select element for each value in the enumeration that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the values to display.</param>
        /// <param name="optionLabel">The text for a default empty item. This parameter can be null.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TEnum">The type of the value.</typeparam>
        public static IHtmlContent EnumDropDownListFor<TModel, TEnum>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TEnum>> expression, string optionLabel, object htmlAttributes)
        {
            return EnumDropDownListForHelper(html, expression, optionLabel, htmlAttributes);
        }

        /// <summary>Returns an HTML select element for each value in the enumeration that is represented by the specified expression.</summary>
        /// <returns>An HTML select element for each value in the enumeration that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the values to display.</param>
        /// <param name="optionLabel">The text for a default empty item. This parameter can be null.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TEnum">The type of the value.</typeparam>
        public static IHtmlContent EnumDropDownListFor<TModel, TEnum>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TEnum>> expression, string optionLabel, IDictionary<string, object> htmlAttributes)
        {
            return EnumDropDownListForHelper(html, expression, optionLabel, htmlAttributes);
        }

        // ListBox

        /// <summary>Returns a multi-select select element using the specified HTML helper and the name of the form field.</summary>
        /// <returns>An HTML select element.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the form field to return.</param>
        /// <exception cref="System.ArgumentException">The <paramref name="name"/> parameter is null or empty.</exception>
        public static IHtmlContent ListBox(this DnnHtmlHelper html, string name)
        {
            return html.HtmlHelper.ListBox(name, null, null);
        }

        /// <summary>Returns a multi-select select element using the specified HTML helper, the name of the form field, and the specified list items.</summary>
        /// <returns>An HTML select element with an option subelement for each item in the list.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the form field to return.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <exception cref="System.ArgumentException">The <paramref name="name"/> parameter is null or empty.</exception>
        public static IHtmlContent ListBox(this DnnHtmlHelper html, string name, IEnumerable<SelectListItem> selectList)
        {
            return html.HtmlHelper.ListBox(name, selectList, null);
        }

        /// <summary>Returns a multi-select select element using the specified HTML helper, the name of the form field, and the specified list items.</summary>
        /// <returns>An HTML select element with an option subelement for each item in the list.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the form field to return.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <exception cref="System.ArgumentException">The <paramref name="name"/> parameter is null or empty.</exception>
        public static IHtmlContent ListBox(this DnnHtmlHelper html, string name, IEnumerable<SelectListItem> selectList, object htmlAttributes)
        {
            return html.HtmlHelper.ListBox(name, selectList, htmlAttributes);
        }

        /// <summary>Returns a multi-select select element using the specified HTML helper, the name of the form field, the specified list items, and the specified HTML attributes.</summary>
        /// <returns>An HTML select element with an option subelement for each item in the list.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="name">The name of the form field to return.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <exception cref="System.ArgumentException">The <paramref name="name"/> parameter is null or empty.</exception>
        public static IHtmlContent ListBox(this DnnHtmlHelper html, string name, IEnumerable<SelectListItem> selectList, IDictionary<string, object> htmlAttributes)
        {
            return html.HtmlHelper.ListBox(name, selectList, htmlAttributes);
        }

        /// <summary>Returns an HTML select element for each property in the object that is represented by the specified expression and using the specified list items.</summary>
        /// <returns>An HTML select element for each property in the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the properties to display.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam><typeparam name="TProperty">The type of the property.</typeparam>
        /// <exception cref="System.ArgumentNullException">The <paramref name="expression"/> parameter is null.</exception>
        public static IHtmlContent ListBoxFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IEnumerable<SelectListItem> selectList)
        {
            return html.HtmlHelper.ListBoxFor(expression, selectList, null);
        }

        /// <summary>Returns an HTML select element for each property in the object that is represented by the specified expression using the specified list items and HTML attributes.</summary>
        /// <returns>An HTML select element for each property in the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the properties to display.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <exception cref="System.ArgumentNullException">The <paramref name="expression"/> parameter is null.</exception>
        public static IHtmlContent ListBoxFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IEnumerable<SelectListItem> selectList, object htmlAttributes)
        {
            return html.HtmlHelper.ListBoxFor(expression, selectList, htmlAttributes);
        }

        /// <summary>Returns an HTML select element for each property in the object that is represented by the specified expression using the specified list items and HTML attributes.</summary>
        /// <returns>An HTML select element for each property in the object that is represented by the expression.</returns>
        /// <param name="html">The HTML helper instance that this method extends.</param>
        /// <param name="expression">An expression that identifies the object that contains the properties to display.</param>
        /// <param name="selectList">A collection of <see cref="SelectListItem"/> objects that are used to populate the drop-down list.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for the element.</param>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <exception cref="System.ArgumentNullException">The <paramref name="expression"/> parameter is null.</exception>
        public static IHtmlContent ListBoxFor<TModel, TProperty>(this DnnHtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, IEnumerable<SelectListItem> selectList, IDictionary<string, object> htmlAttributes)
        {
            return html.HtmlHelper.ListBoxFor(expression, selectList, htmlAttributes);
        }

        /// <summary>Renders a select element for the values of the enumeration identified by the given expression.</summary>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TEnum">The type of the enumeration.</typeparam>
        /// <param name="html">The HTML helper instance.</param>
        /// <param name="expression">An expression that identifies the enumeration property.</param>
        /// <param name="optionLabel">The text for a default empty item, or <see langword="null"/>.</param>
        /// <param name="htmlAttributes">The HTML attributes to set for the element, or <see langword="null"/>.</param>
        /// <returns>An HTML select element.</returns>
        /// <remarks>ASP.NET Core replaced <c>EnumDropDownListFor</c> with
        /// <see cref="IHtmlHelper.GetEnumSelectList(System.Type)"/> combined with
        /// <c>DropDownListFor</c>, which is what this helper does.</remarks>
        private static IHtmlContent EnumDropDownListForHelper<TModel, TEnum>(DnnHtmlHelper<TModel> html, Expression<Func<TModel, TEnum>> expression, string optionLabel, object htmlAttributes)
        {
            var metadata = ModelMetadataHelper.CreateModelExpression(html.HtmlHelper, expression).Metadata;
            var selectList = html.HtmlHelper.GetEnumSelectList(metadata.UnderlyingOrModelType);
            return html.HtmlHelper.DropDownListFor(expression, selectList, optionLabel, htmlAttributes);
        }
    }
}
