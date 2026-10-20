// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor
{
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Web.Razor.Helpers;

    using Microsoft.AspNetCore.Mvc.Razor;

    /// <summary>A razor web page model.</summary>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public abstract partial class DotNetNukeWebPage : RazorPage
    {
        private dynamic model;

        /// <summary>Gets or sets the model for the page.</summary>
        public dynamic Model
        {
            get { return this.model ?? (this.model = this.ViewContext?.ViewData?.Model); }
            set { this.model = value; }
        }

        /// <summary>Gets the DNN helper.</summary>
        protected internal DnnHelper Dnn { get; internal set; }

        /// <summary>Gets the HTML helper.</summary>
        protected internal HtmlHelper Html { get; internal set; }

        /// <summary>Gets the URL helper.</summary>
        protected internal UrlHelper Url { get; internal set; }
    }
}
