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

    /// <summary>The kind of row a <see cref="DnnGridRow"/> is.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.DataControlRowType</c>.</remarks>
    public enum DnnGridRowType
    {
        /// <summary>The header row.</summary>
        Header = 0,

        /// <summary>A data row.</summary>
        DataRow = 1,

        /// <summary>The footer row.</summary>
        Footer = 2,

        /// <summary>The pager row.</summary>
        Pager = 3,
    }
}
