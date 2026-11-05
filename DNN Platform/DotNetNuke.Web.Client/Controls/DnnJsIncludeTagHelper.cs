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

    /// <summary>Registers a JavaScript resource.</summary>
    [HtmlTargetElement("DnnJsInclude")]
    [HtmlTargetElement("dnn-js-include")]
    public class DnnJsIncludeTagHelper : ClientResourceIncludeTagHelper
    {
        private readonly IClientResourceController clientResourceController;
        private readonly IHostSettings hostSettings;

        /// <summary>Initializes a new instance of the <see cref="DnnJsIncludeTagHelper"/> class with default settings.</summary>
        /// <param name="clientResourceController">The client resources controller.</param>
        /// <param name="hostSettings">The host settings, used to detect debug mode.</param>
        public DnnJsIncludeTagHelper(IClientResourceController clientResourceController, IHostSettings hostSettings)
        {
            this.clientResourceController = clientResourceController;
            this.hostSettings = hostSettings;
            this.ForceProvider = ClientResourceProviders.DefaultJsProvider;
            this.DependencyType = ClientDependencyType.Javascript;
            this.Priority = (int)FileOrder.Js.DefaultPriority;
        }

        /// <inheritdoc cref="IScriptResource.Async" />
        [HtmlAttributeName("Async")]
        public bool Async { get; set; }

        /// <inheritdoc cref="IScriptResource.Defer" />
        [HtmlAttributeName("Defer")]
        public bool Defer { get; set; }

        /// <inheritdoc cref="IScriptResource.NoModule" />
        [HtmlAttributeName("NoModule")]
        public bool NoModule { get; set; }

        /// <inheritdoc />
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            // Ported from OnLoad.
            var script = this.clientResourceController.CreateScript(this.FilePath, this.PathNameAlias);
            if (this.Async)
            {
                script = script.SetAsync();
            }

            if (this.Defer)
            {
                script = script.SetDefer();
            }

            if (this.NoModule)
            {
                script = script.SetNoModule();
            }

            this.RegisterResource(script);

            // Ported from Render.
            output.TagName = null;
            output.TagMode = TagMode.StartTagAndEndTag;
            if (this.AddTag || (this.hostSettings is not null && this.hostSettings.DebugMode))
            {
                output.Content.SetHtmlContent(string.Format(
                    CultureInfo.InvariantCulture,
                    "<!--CDF(Javascript|{0}|{1}|{2})-->",
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
