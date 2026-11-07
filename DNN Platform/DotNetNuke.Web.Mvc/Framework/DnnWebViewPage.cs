// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework
{
    using System;

    using DotNetNuke.Common;
    using DotNetNuke.Framework;
    using DotNetNuke.Web.Mvc.Helpers;
    using Microsoft.AspNetCore.Mvc.Razor;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The base class of DNN MVC Razor views.</summary>
    /// <remarks>Was a <c>System.Web.Mvc.WebViewPage</c>; ASP.NET Core Razor views derive from <see cref="RazorPage"/>.</remarks>
    public abstract class DnnWebViewPage : RazorPage
    {
        private readonly IServicesFramework servicesFramework;

        private DnnHelper<object> dnn;
        private DnnHtmlHelper<object> html;
        private DnnUrlHelper url;

        /// <summary>Initializes a new instance of the <see cref="DnnWebViewPage"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        protected DnnWebViewPage()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnWebViewPage"/> class.</summary>
        /// <param name="servicesFramework">The web API service framework.</param>
        protected DnnWebViewPage(IServicesFramework servicesFramework)
        {
            this.servicesFramework = servicesFramework ?? Globals.GetCurrentServiceProvider().GetRequiredService<IServicesFramework>();
        }

        /// <summary>Gets or sets the DNN helper of this view.</summary>
        /// <remarks>The helpers are created on first use: ASP.NET Core has no <c>InitHelpers</c> hook and
        /// the view context is only assigned after the page has been activated.</remarks>
        public DnnHelper<object> Dnn
        {
            get => this.dnn ??= ActivatorUtilities.CreateInstance<DnnHelper<object>>(this.GetServiceProvider(), this.ViewContext);
            set => this.dnn = value;
        }

        /// <summary>Gets or sets the DNN HTML helper of this view.</summary>
        public DnnHtmlHelper<object> Html
        {
            get => this.html ??= new DnnHtmlHelper<object>(this.ViewContext, this.servicesFramework);
            set => this.html = value;
        }

        /// <summary>Gets or sets the DNN URL helper of this view.</summary>
        public DnnUrlHelper Url
        {
            get => this.url ??= new DnnUrlHelper(this.ViewContext);
            set => this.url = value;
        }

        private IServiceProvider GetServiceProvider()
            => this.ViewContext?.HttpContext?.RequestServices ?? Globals.GetCurrentServiceProvider();
    }
}
