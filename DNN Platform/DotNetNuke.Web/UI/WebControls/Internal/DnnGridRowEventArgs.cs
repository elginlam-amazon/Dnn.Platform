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

    /// <summary>The arguments of the <see cref="DnnGrid.RowDataBound"/> event.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.GridViewRowEventArgs</c>.</remarks>
    public class DnnGridRowEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DnnGridRowEventArgs"/> class.</summary>
        /// <param name="row">The row being bound.</param>
        public DnnGridRowEventArgs(DnnGridRow row)
        {
            this.Row = row;
        }

        /// <summary>Gets the row being bound.</summary>
        public DnnGridRow Row { get; }
    }
}
