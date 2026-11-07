// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using Microsoft.AspNetCore.Components;

    /// <summary>A column of a <see cref="DNNDataGrid"/>.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.DataGridColumn</c>. The column abstraction is DNN
    /// public API, so it survives the move to Blazor; only its render contract changed, from
    /// "initialize a cell control tree" to "return the <see cref="RenderFragment"/> of a cell".
    /// </remarks>
    public abstract class DataGridColumn
    {
        /// <summary>Gets or sets the text of the header cell.</summary>
        public string HeaderText { get; set; } = string.Empty;

        /// <summary>Gets or sets the text of the footer cell.</summary>
        public string FooterText { get; set; } = string.Empty;

        /// <summary>Gets or sets the expression this column sorts by.</summary>
        public string SortExpression { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether this column renders.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Gets the style of this column's header cell.</summary>
        public TableItemStyle HeaderStyle { get; } = new TableItemStyle();

        /// <summary>Gets the style of this column's footer cell.</summary>
        public TableItemStyle FooterStyle { get; } = new TableItemStyle();

        /// <summary>Gets the style of this column's data cells.</summary>
        public TableItemStyle ItemStyle { get; } = new TableItemStyle();

        /// <summary>Gets or sets the grid this column belongs to.</summary>
        public DNNDataGrid Owner { get; set; }

        /// <summary>Gets or sets a value indicating whether this column renders as if it were hosted by a designer.</summary>
        /// <remarks>
        /// Replaces the <c>HttpContext.Current == null</c> test the WebForms columns used to detect
        /// the Visual Studio designer. ASP.NET Core has no designer host — and a null
        /// <c>HttpContext</c> now simply means "rendering over a Blazor circuit" — so the flag is
        /// explicit and defaults to <see langword="false"/>.
        /// </remarks>
        public bool DesignMode { get; set; }

        /// <summary>Initialises the column, which is called once by the grid before it renders.</summary>
        public virtual void Initialize()
        {
        }

        /// <summary>Renders the content of one of this column's cells.</summary>
        /// <param name="cell">The cell being rendered.</param>
        /// <returns>The markup of the cell's content.</returns>
        public abstract RenderFragment RenderCell(DataGridCell cell);

        /// <summary>Gets the style that applies to the given kind of cell.</summary>
        /// <param name="itemType">The kind of row the cell belongs to.</param>
        /// <returns>The style of the cell.</returns>
        public TableItemStyle GetStyle(ListItemType itemType)
        {
            switch (itemType)
            {
                case ListItemType.Header:
                    return this.HeaderStyle;
                case ListItemType.Footer:
                    return this.FooterStyle;
                default:
                    return this.ItemStyle;
            }
        }
    }
}
