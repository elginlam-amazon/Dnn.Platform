// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.ClientResourceManagement
{
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Web.Client.Cdf;
    using DotNetNuke.Web.Client.Controls;

    using Microsoft.AspNetCore.Razor.TagHelpers;

    /// <summary>Excludes a CSS resource.</summary>
    [HtmlTargetElement("DnnCssExclude")]
    [HtmlTargetElement("dnn-css-exclude")]
    public class DnnCssExcludeTagHelper : ClientResourceExcludeTagHelper
    {
        /// <summary>Initializes a new instance of the <see cref="DnnCssExcludeTagHelper"/> class.</summary>
        /// <param name="clientResourceController">The controller used to manage client resources.</param>
        public DnnCssExcludeTagHelper(IClientResourceController clientResourceController)
            : base(clientResourceController)
        {
            this.DependencyType = ClientDependencyType.Css;
        }
    }
}
