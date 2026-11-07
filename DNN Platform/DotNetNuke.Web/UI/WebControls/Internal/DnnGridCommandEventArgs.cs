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

    /// <summary>The arguments of the <see cref="DnnGrid.RowCommand"/> event.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.GridViewCommandEventArgs</c>.</remarks>
    public class DnnGridCommandEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DnnGridCommandEventArgs"/> class.</summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="commandArgument">The argument of the command.</param>
        /// <param name="row">The row the command was raised from.</param>
        public DnnGridCommandEventArgs(string commandName, string commandArgument, DnnGridRow row)
        {
            this.CommandName = commandName;
            this.CommandArgument = commandArgument;
            this.Row = row;
        }

        /// <summary>Gets the name of the command.</summary>
        public string CommandName { get; }

        /// <summary>Gets the argument of the command.</summary>
        public string CommandArgument { get; }

        /// <summary>Gets the row the command was raised from.</summary>
        public DnnGridRow Row { get; }
    }
}
