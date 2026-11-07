// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.Controls
{
    using System;

    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Web.Client.Cdf;

    using Microsoft.AspNetCore.Razor.TagHelpers;

    /// <summary>Represents a Tag Helper that excludes a client resource from being included in the page.</summary>
    public abstract class ClientResourceExcludeTagHelper : TagHelper
    {
        private readonly IClientResourceController clientResourceController;

        /// <summary>Initializes a new instance of the <see cref="ClientResourceExcludeTagHelper"/> class.</summary>
        /// <param name="clientResourceController">The client resources controller.</param>
        protected ClientResourceExcludeTagHelper(IClientResourceController clientResourceController)
        {
            this.clientResourceController = clientResourceController;
        }

        /// <summary>Gets or sets the name of the client resource to exclude.</summary>
        [HtmlAttributeName("Name")]
        public string Name { get; set; }

        /// <summary>Gets the dependency type of the client resource to exclude.</summary>
        [HtmlAttributeNotBound]
        public ClientDependencyType DependencyType { get; internal set; }

        /// <inheritdoc />
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            // Ported from OnLoad.
            switch (this.DependencyType)
            {
                case ClientDependencyType.Css:
                    this.clientResourceController.RemoveStylesheetByName(this.Name);
                    break;
                case ClientDependencyType.Javascript:
                    this.clientResourceController.RemoveScriptByName(this.Name);
                    break;
                default:
                    throw new InvalidOperationException("DependencyType must be either Css or Javascript");
            }

            // The exclude control never rendered any markup.
            output.SuppressOutput();
        }
    }
}
