// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>A rendered cell of a <see cref="DNNDataGrid"/>.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.TableCell</c> as it appeared through
    /// <c>DataGridItem.Cells</c>. There is no control tree on ASP.NET Core, so the cell carries the
    /// things the DNN code read off it: its rendered <see cref="Text"/> and the component (or state)
    /// instances a column template registered into <see cref="Controls"/> while rendering.
    /// </remarks>
    public class DataGridCell
    {
        private readonly Collection<object> controls = new Collection<object>();

        /// <summary>Initializes a new instance of the <see cref="DataGridCell"/> class.</summary>
        /// <param name="item">The row this cell belongs to.</param>
        /// <param name="columnIndex">The zero based index of the column this cell belongs to.</param>
        public DataGridCell(DataGridItem item, int columnIndex)
        {
            this.Item = item;
            this.ColumnIndex = columnIndex;
        }

        /// <summary>Gets the row this cell belongs to.</summary>
        public DataGridItem Item { get; }

        /// <summary>Gets the grid this cell belongs to.</summary>
        public DNNDataGrid Grid => this.Item?.Owner;

        /// <summary>Gets the zero based index of the column this cell belongs to.</summary>
        public int ColumnIndex { get; }

        /// <summary>Gets or sets the text this cell renders.</summary>
        /// <remarks>Replaces <c>TableCell.Text</c>, which DNN reads back to identify a row.</remarks>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets the components and state objects a column template registered in this cell.</summary>
        /// <remarks>
        /// Replaces <c>TableCell.Controls</c>. A template adds its component reference here while it
        /// renders (through <c>RenderTreeBuilder.AddComponentReferenceCapture</c>) or, for the
        /// templates whose control only carried posted state, the state object itself.
        /// </remarks>
        [SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists", Justification = "A live, mutable collection is required: templates register into it while rendering.")]
        public IList<object> Controls => this.controls;

        /// <summary>Gets or sets the style of this cell.</summary>
        public TableItemStyle Style { get; set; } = new TableItemStyle();
    }
}
