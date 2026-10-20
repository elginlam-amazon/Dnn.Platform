// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;

    using DotNetNuke.Common;
    using DotNetNuke.Framework;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A DNN wrapper over the ASP.NET Core <see cref="IHtmlHelper"/> for use in MVC module views.</summary>
    public class DnnHtmlHelper
    {
        private readonly IServicesFramework servicesFramework;

        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper"/> class.</summary>
        /// <param name="htmlHelper">The HTML helper to wrap.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        protected DnnHtmlHelper(IHtmlHelper htmlHelper, IServicesFramework servicesFramework)
        {
            this.servicesFramework = servicesFramework ?? Globals.GetCurrentServiceProvider().GetRequiredService<IServicesFramework>();
            this.HtmlHelper = htmlHelper;

            this.ModuleContext = htmlHelper.ViewContext.HttpContext.Items.TryGetValue(typeof(DnnModuleState), out var stateObj) && stateObj is DnnModuleState state
                ? state.ModuleContext
                : null;
        }

        /// <summary>Gets the view bag.</summary>
        public dynamic ViewBag => this.HtmlHelper.ViewBag;

        /// <summary>Gets the view context.</summary>
        public ViewContext ViewContext => this.HtmlHelper.ViewContext;

        /// <summary>Gets the view data.</summary>
        public ViewDataDictionary ViewData => this.HtmlHelper.ViewData;

        /// <summary>Gets or sets the module instance context.</summary>
        public ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets the wrapped HTML helper.</summary>
        internal IHtmlHelper HtmlHelper { get; }

        /// <summary>Renders the DNN anti-forgery token support.</summary>
        /// <returns>An empty HTML string, having requested AJAX anti-forgery support.</returns>
        public IHtmlContent AntiForgeryToken()
        {
            this.servicesFramework.RequestAjaxAntiForgerySupport();
            return HtmlString.Empty;
        }

        /// <summary>Encodes the given value.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The encoded value.</returns>
        public string Encode(string value) => this.HtmlHelper.Encode(value);

        /// <summary>Encodes the given value.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The encoded value.</returns>
        public string Encode(object value) => this.HtmlHelper.Encode(value);

        /// <summary>Wraps the given value as raw HTML.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The raw HTML content.</returns>
        public IHtmlContent Raw(string value) => this.HtmlHelper.Raw(value);

        /// <summary>Wraps the given value as raw HTML.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The raw HTML content.</returns>
        public IHtmlContent Raw(object value) => this.HtmlHelper.Raw(value);
    }
}
