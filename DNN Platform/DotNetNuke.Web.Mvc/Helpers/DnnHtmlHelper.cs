// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Encodings.Web;

    using DotNetNuke.Common;
    using DotNetNuke.Framework;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    public class DnnHtmlHelper
    {
        private readonly IServicesFramework servicesFramework;

        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper"/> class.</summary>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public DnnHtmlHelper(ViewContext viewContext)
            : this(viewContext, (IServicesFramework)null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper"/> class.</summary>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        public DnnHtmlHelper(ViewContext viewContext, IServicesFramework servicesFramework)
            : this(HtmlHelperFactory.Create(viewContext), null, servicesFramework)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper"/> class.</summary>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        /// <param name="routeCollection">The route collection.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public DnnHtmlHelper(ViewContext viewContext, RouteCollection routeCollection)
            : this(viewContext, routeCollection, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper"/> class.</summary>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        /// <param name="routeCollection">The route collection.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        public DnnHtmlHelper(ViewContext viewContext, RouteCollection routeCollection, IServicesFramework servicesFramework)
            : this(HtmlHelperFactory.Create(viewContext), routeCollection, servicesFramework)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper"/> class.</summary>
        /// <param name="htmlHelper">The HTML helper to wrap.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        protected DnnHtmlHelper(IHtmlHelper htmlHelper)
            : this(htmlHelper, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper"/> class.</summary>
        /// <param name="htmlHelper">The HTML helper to wrap.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        protected DnnHtmlHelper(IHtmlHelper htmlHelper, IServicesFramework servicesFramework)
            : this(htmlHelper, null, servicesFramework)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper"/> class.</summary>
        /// <param name="htmlHelper">The HTML helper to wrap.</param>
        /// <param name="routeCollection">The route collection.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        /// <exception cref="InvalidOperationException">The DnnHtmlHelper class can only be used in Views that inherit from DnnWebViewPage.</exception>
        protected DnnHtmlHelper(IHtmlHelper htmlHelper, RouteCollection routeCollection, IServicesFramework servicesFramework)
        {
            Requires.NotNull("htmlHelper", htmlHelper);

            this.HtmlHelper = htmlHelper;
            this.RouteCollection = routeCollection;
            this.servicesFramework = servicesFramework ?? this.ServiceProvider.GetRequiredService<IServicesFramework>();

            var controller = ModuleApplication.GetCurrentDnnController(htmlHelper.ViewContext?.HttpContext);

            if (controller == null)
            {
                throw new InvalidOperationException("The DnnHtmlHelper class can only be used in Views that inherit from DnnWebViewPage");
            }

            this.ModuleContext = controller.ModuleContext;
        }

        /// <summary>Gets the route collection this helper was created with, if any.</summary>
        /// <remarks>ASP.NET Core uses endpoint routing, there is no ambient route table, so this is only
        /// populated when a route collection was passed to the constructor.</remarks>
        public RouteCollection RouteCollection { get; }

        public dynamic ViewBag => this.HtmlHelper.ViewBag;

        public ViewContext ViewContext => this.HtmlHelper.ViewContext;

        public ViewDataDictionary ViewData => this.HtmlHelper.ViewData;

        public ModuleInstanceContext ModuleContext { get; set; }

        internal IHtmlHelper HtmlHelper { get; set; }

        private IServiceProvider ServiceProvider
            => this.HtmlHelper.ViewContext?.HttpContext?.RequestServices ?? Globals.GetCurrentServiceProvider();

        /// <summary>Gets the HTML encoder configured for the application this helper is rendering in.</summary>
        /// <remarks>Falls back to <see cref="HtmlEncoder.Default"/> when no encoder has been registered.</remarks>
        private HtmlEncoder Encoder
            => this.ServiceProvider?.GetService<HtmlEncoder>() ?? HtmlEncoder.Default;

        /// <summary>Renders the anti-forgery token for the current request and enables AJAX anti-forgery support.</summary>
        /// <returns>The hidden anti-forgery form field.</returns>
        public IHtmlContent AntiForgeryToken()
        {
            this.servicesFramework.RequestAjaxAntiForgerySupport();
            return this.HtmlHelper.AntiForgeryToken();
        }

        /// <summary>HTML attribute encodes the given value.</summary>
        /// <param name="value">The value to encode.</param>
        /// <returns>The encoded value.</returns>
        public string AttributeEncode(string value)
            => string.IsNullOrEmpty(value) ? value : this.Encoder.Encode(value);

        /// <summary>HTML attribute encodes the given value.</summary>
        /// <param name="value">The value to encode.</param>
        /// <returns>The encoded value.</returns>
        public string AttributeEncode(object value)
            => this.AttributeEncode(Convert.ToString(value, CultureInfo.CurrentCulture));

        /// <summary>HTML encodes the given value.</summary>
        /// <param name="value">The value to encode.</param>
        /// <returns>The encoded value.</returns>
        public string Encode(string value) => this.HtmlHelper.Encode(value);

        /// <summary>HTML encodes the given value.</summary>
        /// <param name="value">The value to encode.</param>
        /// <returns>The encoded value.</returns>
        public string Encode(object value) => this.HtmlHelper.Encode(value);

        /// <summary>Formats the given value with the given format string.</summary>
        /// <param name="value">The value to format.</param>
        /// <param name="format">The format string.</param>
        /// <returns>The formatted value.</returns>
        public string FormatValue(object value, string format) => this.HtmlHelper.FormatValue(value, format);

        /// <summary>Renders the hidden field that overrides the HTTP method of a form post.</summary>
        /// <param name="httpMethod">The HTTP method to use.</param>
        /// <returns>The hidden form field.</returns>
        public IHtmlContent HttpMethodOverride(System.Net.Http.HttpMethod httpMethod)
        {
            Requires.NotNull("httpMethod", httpMethod);
            return this.HttpMethodOverride(httpMethod.Method);
        }

        /// <summary>Renders the hidden field that overrides the HTTP method of a form post.</summary>
        /// <param name="httpVerb">The HTTP method to use.</param>
        /// <returns>The hidden form field.</returns>
        /// <remarks>ASP.NET Core has no <c>Html.HttpMethodOverride</c>, the <c>X-HTTP-Method-Override</c>
        /// field is written directly (it is read by the HTTP method override middleware).</remarks>
        public IHtmlContent HttpMethodOverride(string httpVerb)
        {
            if (string.IsNullOrEmpty(httpVerb))
            {
                throw new ArgumentException("A HTTP verb is required.", nameof(httpVerb));
            }

            var builder = new TagBuilder("input");
            builder.TagRenderMode = TagRenderMode.SelfClosing;
            builder.MergeAttribute("type", "hidden");
            builder.MergeAttribute("name", "X-HTTP-Method-Override");
            builder.MergeAttribute("value", httpVerb);

            // The field is rendered with the encoder configured for this helper's application.
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            builder.WriteTo(writer, this.Encoder);
            return new HtmlString(writer.ToString());
        }

        /// <summary>Wraps the given markup string so that it is rendered without HTML encoding.</summary>
        /// <param name="value">The markup string to render as-is.</param>
        /// <returns>HTML content which writes the given value verbatim.</returns>
        public IHtmlContent Raw(string value) => this.HtmlHelper.Raw(value);

        /// <summary>Wraps the string representation of the given object so that it is rendered without HTML encoding.</summary>
        /// <param name="value">The object whose string representation is rendered as-is.</param>
        /// <returns>HTML content which writes the given value verbatim.</returns>
        public IHtmlContent Raw(object value) => this.HtmlHelper.Raw(value);

        /// <summary>Gets the unobtrusive validation attributes of the given field.</summary>
        /// <param name="name">The HTML field name.</param>
        /// <returns>The validation attributes.</returns>
        public IDictionary<string, object> GetUnobtrusiveValidationAttributes(string name)
            => this.GetUnobtrusiveValidationAttributes(name, null);

        /// <summary>Gets the unobtrusive validation attributes of the given field.</summary>
        /// <param name="name">The HTML field name.</param>
        /// <param name="metadata">The model metadata of the field, or <see langword="null"/> to resolve it from the view data.</param>
        /// <returns>The validation attributes.</returns>
        /// <remarks>ASP.NET Core exposes this through the <see cref="ValidationHtmlAttributeProvider"/> service.</remarks>
        public IDictionary<string, object> GetUnobtrusiveValidationAttributes(string name, ModelMetadata metadata)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var serviceProvider = this.ServiceProvider;
            var attributeProvider = serviceProvider.GetRequiredService<ValidationHtmlAttributeProvider>();
            var modelExplorer = this.GetModelExplorer(name, metadata, serviceProvider);

            attributeProvider.AddAndTrackValidationAttributes(this.ViewContext, modelExplorer, name ?? string.Empty, attributes);

            return attributes.ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Enables client side validation for the current view.</summary>
        public void EnableClientValidation() => this.EnableClientValidation(true);

        /// <summary>Enables or disables client side validation for the current view.</summary>
        /// <param name="enabled">Whether client side validation is enabled.</param>
        public void EnableClientValidation(bool enabled) => this.ViewContext.ClientValidationEnabled = enabled;

        /// <summary>Enables unobtrusive client side validation for the current view.</summary>
        public void EnableUnobtrusiveJavaScript() => this.EnableUnobtrusiveJavaScript(true);

        /// <summary>Enables or disables unobtrusive client side validation for the current view.</summary>
        /// <param name="enabled">Whether unobtrusive JavaScript is enabled.</param>
        /// <remarks>ASP.NET Core only supports unobtrusive client validation, so this toggles the
        /// client validation of the view context.</remarks>
        public void EnableUnobtrusiveJavaScript(bool enabled) => this.ViewContext.ClientValidationEnabled = enabled;

        private ModelExplorer GetModelExplorer(string name, ModelMetadata metadata, IServiceProvider serviceProvider)
        {
            var metadataProvider = serviceProvider.GetRequiredService<IModelMetadataProvider>();

            if (metadata != null)
            {
                return new ModelExplorer(metadataProvider, metadata, null);
            }

            // The MVC 5 helper resolved the metadata from the string expression against the view data;
            // walking the expression with GetExplorerForProperty is the Core equivalent of that lookup.
            var container = this.ViewData.ModelExplorer ?? metadataProvider.GetModelExplorerForType(typeof(object), null);
            if (string.IsNullOrEmpty(name))
            {
                return container;
            }

            var explorer = container;
            foreach (var part in name.Split('.'))
            {
                var next = explorer.GetExplorerForProperty(part);
                if (next == null)
                {
                    return explorer;
                }

                explorer = next;
            }

            return explorer;
        }
    }
}
