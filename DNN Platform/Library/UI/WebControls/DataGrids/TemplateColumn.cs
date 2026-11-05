// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A column whose cells are rendered by <see cref="IGridTemplate"/>s.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.TemplateColumn</c>.</remarks>
    public class TemplateColumn : DataGridColumn
    {
        /// <summary>Gets or sets the template of the data cells.</summary>
        public IGridTemplate ItemTemplate { get; set; }

        /// <summary>Gets or sets the template of the data cell of the row being edited.</summary>
        public IGridTemplate EditItemTemplate { get; set; }

        /// <summary>Gets or sets the template of the header cell.</summary>
        public IGridTemplate HeaderTemplate { get; set; }

        /// <summary>Gets or sets the template of the footer cell.</summary>
        public IGridTemplate FooterTemplate { get; set; }

        /// <inheritdoc />
        public override RenderFragment RenderCell(DataGridCell cell)
        {
            var template = this.GetTemplate(cell?.Item?.ItemType ?? ListItemType.Item);
            if (template == null)
            {
                return static (RenderTreeBuilder builder) => { };
            }

            return template.Render(cell);
        }

        /// <summary>Gets the template that renders the given kind of cell.</summary>
        /// <param name="itemType">The kind of row the cell belongs to.</param>
        /// <returns>The template, or <see langword="null"/> when this column has none for it.</returns>
        protected IGridTemplate GetTemplate(ListItemType itemType)
        {
            switch (itemType)
            {
                case ListItemType.Header:
                    return this.HeaderTemplate;
                case ListItemType.Footer:
                    return this.FooterTemplate;
                case ListItemType.EditItem:
                    return this.EditItemTemplate ?? this.ItemTemplate;
                default:
                    return this.ItemTemplate;
            }
        }
    }
}
