// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor
{
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Web.Razor.Helpers;

    using Microsoft.AspNetCore.Mvc.Razor;

    /// <summary>A razor web page model.</summary>
    /// <remarks>
    /// Was a <c>System.Web.WebPages.WebPageBase</c>; it is now an ASP.NET Core Razor page
    /// (<see cref="RazorPage{TModel}"/> of <see langword="dynamic"/>), which is the replacement for
    /// the Web Pages rendering pipeline. Module <c>.cshtml</c>/<c>.vbhtml</c> files that declare
    /// <c>@inherits DotNetNuke.Web.Razor.DotNetNukeWebPage</c> keep compiling against this type.
    /// </remarks>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public abstract partial class DotNetNukeWebPage : RazorPage<dynamic>
    {
        private dynamic model;

        /// <summary>Gets or sets the rendering context of this page.</summary>
        /// <remarks>Replaces <c>WebPageBase.PageContext</c>.</remarks>
        public RazorPageContext PageContext { get; set; }

        /// <summary>Gets or sets the virtual directory of the razor file this page was compiled from.</summary>
        /// <remarks>Replaces <c>WebPageBase.VirtualPath</c>.</remarks>
        public string VirtualPath { get; set; }

        /// <summary>Gets or sets the model of this page.</summary>
        public new dynamic Model
        {
            get { return this.model ?? (this.model = this.PageContext != null ? this.PageContext.Model : this.ViewData?.Model); }
            set { this.model = value; }
        }

        /// <summary>Gets the DNN context helper.</summary>
        protected internal DnnHelper Dnn { get; internal set; }

        /// <summary>Gets the HTML helper.</summary>
        protected internal HtmlHelper Html { get; internal set; }

        /// <summary>Gets the URL helper.</summary>
        protected internal UrlHelper Url { get; internal set; }

        /// <summary>Configures this page from the page that renders it.</summary>
        /// <param name="parentPage">The parent page.</param>
        /// <remarks>Replaces <c>WebPageBase.ConfigurePage(WebPageBase)</c>.</remarks>
        protected virtual void ConfigurePage(DotNetNukeWebPage parentPage)
        {
            if (parentPage == null)
            {
                return;
            }

            // Child pages need to get their context from the Parent
            this.PageContext = parentPage.PageContext;
            this.ViewContext = parentPage.ViewContext;
            this.VirtualPath = this.VirtualPath ?? parentPage.VirtualPath;
            this.Dnn = this.Dnn ?? parentPage.Dnn;
            this.Html = this.Html ?? parentPage.Html;
            this.Url = this.Url ?? parentPage.Url;
        }
    }
}
