// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>A row of a <see cref="DNNDataGrid"/>.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.DataGridItem</c>. It is a plain data holder now rather
    /// than a control: the grid creates one per bound record and the column render fragments read
    /// <see cref="DataItem"/> off it, exactly as the WebForms templates read it off their naming
    /// container.
    /// </remarks>
    public class DataGridItem
    {
        private readonly List<DataGridCell> cells = new List<DataGridCell>();

        /// <summary>Initializes a new instance of the <see cref="DataGridItem"/> class.</summary>
        /// <param name="owner">The grid this row belongs to.</param>
        /// <param name="itemIndex">The zero based index of this row within the grid's rows, or -1 for the header and footer.</param>
        /// <param name="dataSetIndex">The zero based index of this row within the data source.</param>
        /// <param name="itemType">The kind of row.</param>
        public DataGridItem(DNNDataGrid owner, int itemIndex, int dataSetIndex, ListItemType itemType)
        {
            this.Owner = owner;
            this.ItemIndex = itemIndex;
            this.DataSetIndex = dataSetIndex;
            this.ItemType = itemType;
        }

        /// <summary>Gets the grid this row belongs to.</summary>
        public DNNDataGrid Owner { get; }

        /// <summary>Gets the zero based index of this row within the grid's rows, or -1 for the header and footer.</summary>
        public int ItemIndex { get; }

        /// <summary>Gets the zero based index of this row within the data source.</summary>
        public int DataSetIndex { get; }

        /// <summary>Gets or sets the kind of row.</summary>
        public ListItemType ItemType { get; set; }

        /// <summary>Gets or sets the record this row is bound to.</summary>
        public object DataItem { get; set; }

        /// <summary>Gets the cells of this row, one per visible column.</summary>
        public IReadOnlyList<DataGridCell> Cells => this.cells;

        /// <summary>Gets or sets the style of this row.</summary>
        public TableItemStyle Style { get; set; } = new TableItemStyle();

        /// <summary>Adds a cell to this row.</summary>
        /// <param name="cell">The cell to add.</param>
        public void AddCell(DataGridCell cell) => this.cells.Add(cell);

        /// <summary>Removes every cell of this row.</summary>
        public void ClearCells() => this.cells.Clear();
    }
}
