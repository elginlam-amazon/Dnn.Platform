// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using Microsoft.AspNetCore.Components;

    /// <summary>Renders the content of one grid cell.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.ITemplate</c>: instead of instantiating controls into a container
    /// (<c>InstantiateIn</c>) and binding them later through a <c>DataBinding</c> event, a template
    /// now returns the <see cref="RenderFragment"/> for a single cell. Both phases collapse into
    /// this one call, because the cell carries the bound record
    /// (<see cref="DataGridCell.Item"/>.<see cref="DataGridItem.DataItem"/>) up front.
    /// </remarks>
    public interface IGridTemplate
    {
        /// <summary>Renders the content of the given cell.</summary>
        /// <param name="cell">The cell being rendered. Its <see cref="DataGridCell.Controls"/> is the
        /// place to register any component reference the caller needs to read back.</param>
        /// <returns>The markup of the cell's content.</returns>
        RenderFragment Render(DataGridCell cell);
    }
}
