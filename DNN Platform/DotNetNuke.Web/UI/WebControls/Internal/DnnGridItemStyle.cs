// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The styling of a part of a <see cref="DnnGrid"/>.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.TableItemStyle</c>; DNN only ever assigned a
    /// <c>CssClass</c> through it.
    /// </remarks>
    public class DnnGridItemStyle
    {
        /// <summary>Gets or sets the CSS class applied to the part.</summary>
        public string CssClass { get; set; }
    }
}
