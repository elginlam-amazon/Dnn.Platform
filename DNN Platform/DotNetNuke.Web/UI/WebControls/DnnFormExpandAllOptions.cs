// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    /// <summary>The options of the "expand all" link of a form tab.</summary>
    /// <remarks>
    /// Replaces the hand-built <c>dnnExpandAll</c> call that <see cref="DnnFormEditor"/> used to
    /// concatenate into an inline script block; the same values are now passed to
    /// <c>dnn.setupFormEditor</c> through JS interop.
    /// </remarks>
    public class DnnFormExpandAllOptions
    {
        /// <summary>Gets or sets the selector of the "expand all" anchor.</summary>
        public string Selector { get; set; }

        /// <summary>Gets or sets the text shown while the sections are collapsed.</summary>
        public string ExpandText { get; set; }

        /// <summary>Gets or sets the text shown while the sections are expanded.</summary>
        public string CollapseText { get; set; }

        /// <summary>Gets or sets the selector of the area whose sections are expanded.</summary>
        public string TargetArea { get; set; }
    }
}
