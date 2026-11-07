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

    /// <summary>A row of a <see cref="DnnGrid"/>.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.GridViewRow</c> as DNN used it: the data
    /// item, the row index and the row kind.</remarks>
    public class DnnGridRow
    {
        /// <summary>Initializes a new instance of the <see cref="DnnGridRow"/> class.</summary>
        /// <param name="rowIndex">The index of the row in the current page.</param>
        /// <param name="dataItem">The bound data item.</param>
        /// <param name="rowType">The kind of row.</param>
        public DnnGridRow(int rowIndex, object dataItem, DnnGridRowType rowType)
        {
            this.RowIndex = rowIndex;
            this.DataItem = dataItem;
            this.RowType = rowType;
        }

        /// <summary>Gets the index of the row within the current page.</summary>
        public int RowIndex { get; }

        /// <summary>Gets the data item bound to this row.</summary>
        public object DataItem { get; }

        /// <summary>Gets the kind of row.</summary>
        public DnnGridRowType RowType { get; }

        /// <summary>Gets or sets the CSS class applied to this row.</summary>
        public string CssClass { get; set; }

        /// <summary>Gets or sets a value indicating whether this row renders.</summary>
        public bool Visible { get; set; } = true;
    }
}
