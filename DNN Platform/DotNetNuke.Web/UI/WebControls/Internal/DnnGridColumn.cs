// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls.Internal
{
    using Microsoft.AspNetCore.Components;

    /// <summary>The base of a column of a <see cref="DnnGrid"/>.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.DataControlField</c>, the base of <c>BoundField</c>
    /// and <c>TemplateField</c>. Only the members DNN's grids actually declare are kept.
    /// </remarks>
    public abstract class DnnGridColumn
    {
        /// <summary>Gets or sets the text of the column's header.</summary>
        public string HeaderText { get; set; }

        /// <summary>Gets or sets the CSS class applied to the column's header cell.</summary>
        public string HeaderStyleCssClass { get; set; }

        /// <summary>Gets or sets the CSS class applied to the column's data cells.</summary>
        public string ItemStyleCssClass { get; set; }

        /// <summary>Gets or sets a value indicating whether the column renders.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Gets or sets the expression the grid sorts by when the header is clicked.</summary>
        public string SortExpression { get; set; }

        /// <summary>Renders the content of this column's cell for the given row.</summary>
        /// <param name="row">The row being rendered.</param>
        /// <returns>The content of the cell.</returns>
        public abstract RenderFragment RenderCell(DnnGridRow row);
    }
}
