// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.ClientResourceManagement
{
    using System.Globalization;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Web.Client.Cdf;
    using DotNetNuke.Web.Client.ResourceManager;

    using Microsoft.AspNetCore.Razor.TagHelpers;

    /// <summary>Registers a CSS resource.</summary>
    [HtmlTargetElement("DnnCssInclude")]
    [HtmlTargetElement("dnn-css-include")]
    public class DnnCssIncludeTagHelper : ClientResourceIncludeTagHelper
    {
        private readonly IClientResourceController clientResourceController;
        private readonly IHostSettings hostSettings;

        /// <summary>Initializes a new instance of the <see cref="DnnCssIncludeTagHelper"/> class.</summary>
        /// <param name="clientResourceController">The client resources controller.</param>
        /// <param name="hostSettings">The host settings, used to detect debug mode.</param>
        public DnnCssIncludeTagHelper(IClientResourceController clientResourceController, IHostSettings hostSettings)
        {
            this.clientResourceController = clientResourceController;
            this.hostSettings = hostSettings;
            this.ForceProvider = ClientResourceProviders.DefaultCssProvider;
            this.DependencyType = ClientDependencyType.Css;
            this.Priority = (int)FileOrder.Css.DefaultPriority;
        }

        /// <inheritdoc cref="ILinkResource.Media" />
        [HtmlAttributeName("CssMedia")]
        public string CssMedia { get; set; }

        /// <inheritdoc cref="ILinkResource.Preload" />
        [HtmlAttributeName("Preload")]
        public bool Preload { get; set; }

        /// <inheritdoc />
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            // Ported from OnLoad.
            var filePath = this.FilePath;
            if (filePath.IndexOf('?') > 0)
            {
                filePath = filePath.Substring(0, filePath.IndexOf('?'));
            }

            var extension = ".css";
            var hit = filePath.LastIndexOf('.');
            if (hit > 0)
            {
                extension = filePath.Substring(hit).ToLowerInvariant();
            }

            switch (extension)
            {
                case ".eot":
                case ".woff":
                case ".woff2":
                case ".ttf":
                case ".svg":
                case ".otf":
                    var font = this.clientResourceController.CreateFont(this.FilePath, this.PathNameAlias);
                    if (this.Preload)
                    {
                        font.SetPreload();
                    }

                    this.RegisterResource(font);
                    break;
                default:
                    var stylesheet = this.clientResourceController.CreateStylesheet(this.FilePath, this.PathNameAlias)
                        .SetMedia(this.CssMedia);
                    if (this.Preload)
                    {
                        stylesheet.SetPreload();
                    }

                    this.RegisterResource(stylesheet);
                    break;
            }

            // Ported from Render.
            output.TagName = null;
            output.TagMode = TagMode.StartTagAndEndTag;
            if (this.AddTag || (this.hostSettings is not null && this.hostSettings.DebugMode))
            {
                output.Content.SetHtmlContent(string.Format(
                    CultureInfo.InvariantCulture,
                    "<!--CDF(Css|{0}|{1}|{2})-->",
                    this.FilePath,
                    this.ForceProvider,
                    this.Priority));
            }
            else
            {
                output.Content.SetHtmlContent(string.Empty);
            }
        }
    }
}
