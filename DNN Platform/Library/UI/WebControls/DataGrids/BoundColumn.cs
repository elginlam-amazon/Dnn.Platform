// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A column that renders the value of one field of the bound record.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.BoundColumn</c>.</remarks>
    public class BoundColumn : DataGridColumn
    {
        /// <summary>Gets or sets the field of the bound record this column renders.</summary>
        public string DataField { get; set; } = string.Empty;

        /// <summary>Gets or sets the format the value is rendered with.</summary>
        public string DataFormatString { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the cells are read only while the row is edited.</summary>
        public bool ReadOnly { get; set; }

        /// <inheritdoc />
        public override RenderFragment RenderCell(DataGridCell cell)
        {
            if (cell == null)
            {
                return static (RenderTreeBuilder builder) => { };
            }

            switch (cell.Item.ItemType)
            {
                case ListItemType.Header:
                    cell.Text = this.HeaderText;
                    break;
                case ListItemType.Footer:
                    cell.Text = this.FooterText;
                    break;
                default:
                    cell.Text = GridDataBinder.Eval(cell.Item.DataItem, this.DataField, this.DataFormatString);
                    break;
            }

            var text = cell.Text;
            return builder => builder.AddMarkupContent(0, text);
        }
    }
}
