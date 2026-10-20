// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.UI.WebControls
{
    using System.ComponentModel;

    /// <summary>A <see cref="DataGrid"/> column which displays its content using templates.</summary>
    public class TemplateColumn : DataGridColumn
    {
        /// <summary>Gets or sets the template of the header.</summary>
        [Browsable(false)]
        [PersistenceMode(PersistenceMode.InnerProperty)]
        [TemplateContainer(typeof(DataGridItem))]
        public virtual ITemplate HeaderTemplate { get; set; }

        /// <summary>Gets or sets the template of the footer.</summary>
        [Browsable(false)]
        [PersistenceMode(PersistenceMode.InnerProperty)]
        [TemplateContainer(typeof(DataGridItem))]
        public virtual ITemplate FooterTemplate { get; set; }

        /// <summary>Gets or sets the template of the items.</summary>
        [Browsable(false)]
        [PersistenceMode(PersistenceMode.InnerProperty)]
        [TemplateContainer(typeof(DataGridItem))]
        public virtual ITemplate ItemTemplate { get; set; }

        /// <summary>Gets or sets the template of the item in edit mode.</summary>
        [Browsable(false)]
        [PersistenceMode(PersistenceMode.InnerProperty)]
        [TemplateContainer(typeof(DataGridItem))]
        public virtual ITemplate EditItemTemplate { get; set; }

        /// <inheritdoc />
        public override void InitializeCell(TableCell cell, int columnIndex, ListItemType itemType)
        {
            if (cell == null)
            {
                throw new ArgumentNullException(nameof(cell));
            }

            ITemplate template = null;
            switch (itemType)
            {
                case ListItemType.Header:
                    template = this.HeaderTemplate;
                    if (template == null)
                    {
                        base.InitializeCell(cell, columnIndex, itemType);
                        return;
                    }

                    break;
                case ListItemType.Footer:
                    template = this.FooterTemplate;
                    if (template == null)
                    {
                        base.InitializeCell(cell, columnIndex, itemType);
                        return;
                    }

                    break;
                case ListItemType.Item:
                case ListItemType.AlternatingItem:
                case ListItemType.SelectedItem:
                    template = this.ItemTemplate;
                    break;
                case ListItemType.EditItem:
                    template = this.EditItemTemplate ?? this.ItemTemplate;
                    break;
            }

            if (template != null)
            {
                cell.Text = string.Empty;
                template.InstantiateIn(cell);
            }
            else if (itemType == ListItemType.Item || itemType == ListItemType.AlternatingItem || itemType == ListItemType.SelectedItem || itemType == ListItemType.EditItem)
            {
                cell.Text = "&nbsp;";
            }
        }
    }
}
