// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.JSInterop;

    /// <summary>The DNNDataGrid control provides an Enhanced Data Grid, that supports other column types.</summary>
    /// <remarks>
    /// Was a <c>System.Web.UI.WebControls.DataGrid</c>. On ASP.NET Core it is a
    /// <see cref="ComponentBase"/> that renders the <c>table</c> itself: the control hierarchy the
    /// WebForms grid created (<c>CreateControlHierarchy</c>/<c>PrepareControlHierarchy</c>) is now
    /// the <see cref="DataGridItem"/>/<see cref="DataGridCell"/> model the columns render into, and
    /// the per-cell markup comes from <see cref="DataGridColumn.RenderCell"/>.
    /// </remarks>
    public class DNNDataGrid : ComponentBase
    {
        private readonly DataGridColumnCollection columns = new DataGridColumnCollection();
        private readonly DataGridItemCollection items = new DataGridItemCollection();
        private readonly List<object> dataKeys = new List<object>();
        private bool initialized;

        /// <summary>Occurs when a check box of a <see cref="CheckBoxColumn"/> of this grid changed.</summary>
        public event DNNDataGridCheckedColumnEventHandler ItemCheckedChanged;

        /// <summary>Occurs when a command is raised from one of this grid's cells.</summary>
        public event DataGridCommandEventHandler ItemCommand;

        /// <summary>Occurs for every row this grid binds.</summary>
        public event DataGridItemEventHandler ItemDataBound;

        /// <summary>Gets or sets the records this grid renders.</summary>
        [Parameter]
        public IEnumerable DataSource { get; set; }

        /// <summary>Gets or sets the field that provides the key of each row.</summary>
        [Parameter]
        public string DataKeyField { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class rendered onto the <c>table</c>.</summary>
        [Parameter]
        public string CssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether a column is generated for every field of the bound records.</summary>
        [Parameter]
        public bool AutoGenerateColumns { get; set; }

        /// <summary>Gets or sets the spacing between the cells.</summary>
        [Parameter]
        public int CellSpacing { get; set; }

        /// <summary>Gets or sets the padding inside the cells.</summary>
        [Parameter]
        public int CellPadding { get; set; } = -1;

        /// <summary>Gets or sets the grid lines this grid renders.</summary>
        [Parameter]
        public GridLines GridLines { get; set; } = GridLines.Both;

        /// <summary>Gets or sets the index of the row being edited, or -1 when no row is edited.</summary>
        [Parameter]
        public int EditItemIndex { get; set; } = -1;

        /// <summary>Gets or sets the index of the selected row, or -1 when no row is selected.</summary>
        [Parameter]
        public int SelectedIndex { get; set; } = -1;

        /// <summary>Gets or sets a value indicating whether the header row renders.</summary>
        [Parameter]
        public bool ShowHeader { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the footer row renders.</summary>
        [Parameter]
        public bool ShowFooter { get; set; }

        /// <summary>Gets or sets the callback raised when a check box of a <see cref="CheckBoxColumn"/> changed.</summary>
        /// <remarks>The Blazor equivalent of the <see cref="ItemCheckedChanged"/> event, for markup consumers.</remarks>
        [Parameter]
        public EventCallback<DNNDataGridCheckChangedEventArgs> OnItemCheckedChanged { get; set; }

        /// <summary>Gets or sets the callback raised when a command comes from one of this grid's cells.</summary>
        /// <remarks>The Blazor equivalent of the <see cref="ItemCommand"/> event, for markup consumers.</remarks>
        [Parameter]
        public EventCallback<DataGridCommandEventArgs> OnItemCommand { get; set; }

        /// <summary>Gets the columns of this grid.</summary>
        public DataGridColumnCollection Columns => this.columns;

        /// <summary>Gets the rows this grid rendered, excluding the header and the footer.</summary>
        public DataGridItemCollection Items => this.items;

        /// <summary>Gets the key of every row, in render order.</summary>
        public IReadOnlyList<object> DataKeys => this.dataKeys;

        /// <summary>Gets or sets the style of the header row.</summary>
        public TableItemStyle HeaderStyle { get; set; } = new TableItemStyle();

        /// <summary>Gets or sets the style of the footer row.</summary>
        public TableItemStyle FooterStyle { get; set; } = new TableItemStyle();

        /// <summary>Gets or sets the style of the data rows.</summary>
        public TableItemStyle ItemStyle { get; set; } = new TableItemStyle();

        /// <summary>Gets or sets the style of the alternating data rows.</summary>
        public TableItemStyle AlternatingItemStyle { get; set; } = new TableItemStyle();

        /// <summary>Gets or sets the style of the selected data row.</summary>
        public TableItemStyle SelectedItemStyle { get; set; } = new TableItemStyle();

        /// <summary>Gets or sets the JS interop runtime, used to issue the command confirmation prompt.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Rebuilds the rows of this grid from <see cref="DataSource"/>.</summary>
        /// <remarks>Replaces <c>DataBind()</c>, which in WebForms also rebuilt the control tree.</remarks>
        public void DataBind()
        {
            this.OnDataBinding(EventArgs.Empty);
            this.CreateControlHierarchy(true);
            this.PrepareControlHierarchy();
        }

        /// <summary>Gets the markup of this grid's table.</summary>
        /// <returns>The table, header row, data rows and footer row.</returns>
        /// <remarks>
        /// Exposed so that a composite control (such as <see cref="RolesSelectionGrid"/>) can embed
        /// the very same table in its own render tree, the way it used to add the WebForms
        /// <c>DataGrid</c> to its control tree.
        /// </remarks>
        public RenderFragment RenderTable() => this.BuildTable;

        /// <summary>Raises <see cref="ItemCommand"/> and <see cref="OnItemCommand"/>.</summary>
        /// <param name="e">The command.</param>
        public void RaiseItemCommand(DataGridCommandEventArgs e)
        {
            this.ItemCommand?.Invoke(this, e);
            if (this.OnItemCommand.HasDelegate)
            {
                this.OnItemCommand.InvokeAsync(e);
            }
        }

        /// <summary>Raises <see cref="ItemCheckedChanged"/> and <see cref="OnItemCheckedChanged"/>.</summary>
        /// <param name="sender">The object raising the event.</param>
        /// <param name="e">The change.</param>
        public void RaiseItemCheckedChanged(object sender, DNNDataGridCheckChangedEventArgs e)
        {
            this.ItemCheckedChanged?.Invoke(sender, e);
            if (this.OnItemCheckedChanged.HasDelegate)
            {
                this.OnItemCheckedChanged.InvokeAsync(e);
            }
        }

        /// <summary>Raises a command, after asking the user to confirm it.</summary>
        /// <param name="e">The command.</param>
        /// <param name="confirmMessage">The confirmation prompt, which may be empty.</param>
        /// <returns>A task that completes once the command was raised, or skipped by the user.</returns>
        /// <remarks>
        /// Replaces the inline <c>onclick="return confirm(...)"</c> the WebForms columns attached
        /// through <c>ClientAPI.AddButtonConfirm</c>; a Blazor element cannot carry both an inline
        /// handler and a component handler for the same event.
        /// </remarks>
        public async Task RaiseItemCommandAsync(DataGridCommandEventArgs e, string confirmMessage)
        {
            if (!string.IsNullOrEmpty(confirmMessage) && this.JsRuntime != null)
            {
                var confirmed = await this.JsRuntime.InvokeAsync<bool>("confirm", confirmMessage);
                if (!confirmed)
                {
                    return;
                }
            }

            this.RaiseItemCommand(e);
        }

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            this.OnDataBinding(EventArgs.Empty);
            this.CreateControlHierarchy(true);
            this.PrepareControlHierarchy();
        }

        /// <summary>Called when the grid is Data Bound.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnDataBinding(EventArgs e)
        {
            if (this.initialized)
            {
                return;
            }

            this.initialized = true;
            foreach (var column in this.columns)
            {
                column.Owner = this;
                if (column is CheckBoxColumn cbColumn)
                {
                    // Manage CheckBox column events
                    cbColumn.CheckedChanged += this.OnItemCheckedChangedInternal;
                }
            }
        }

        /// <summary>Rebuilds the <see cref="Items"/> and their cells from the data source.</summary>
        /// <param name="useDataSource">Whether the rows are rebuilt from <see cref="DataSource"/>.</param>
        /// <remarks>Replaces <c>DataGrid.CreateControlHierarchy</c>.</remarks>
        protected virtual void CreateControlHierarchy(bool useDataSource)
        {
            if (!useDataSource)
            {
                return;
            }

            this.items.Clear();
            this.dataKeys.Clear();
            if (this.AutoGenerateColumns)
            {
                this.GenerateColumns();
            }

            foreach (var column in this.columns)
            {
                column.Owner = this;
                column.Initialize();
            }

            if (this.DataSource == null)
            {
                return;
            }

            var index = 0;
            foreach (var record in Enumerate(this.DataSource))
            {
                var itemType = this.GetItemType(index);
                var item = new DataGridItem(this, index, index, itemType) { DataItem = record };
                this.BuildCells(item);
                this.items.Add(item);
                this.dataKeys.Add(string.IsNullOrEmpty(this.DataKeyField)
                    ? null
                    : GridDataBinder.Eval(record, this.DataKeyField));
                this.ItemDataBound?.Invoke(this, new DataGridItemEventArgs(item));
                index++;
            }
        }

        /// <summary>Applies the grid level styles onto the rows and cells.</summary>
        /// <remarks>Replaces <c>DataGrid.PrepareControlHierarchy</c>.</remarks>
        protected virtual void PrepareControlHierarchy()
        {
            foreach (var item in this.items)
            {
                switch (item.ItemType)
                {
                    case ListItemType.AlternatingItem:
                        item.Style.CopyFrom(this.ItemStyle);
                        item.Style.CopyFrom(this.AlternatingItemStyle);
                        break;
                    case ListItemType.SelectedItem:
                        item.Style.CopyFrom(this.ItemStyle);
                        item.Style.CopyFrom(this.SelectedItemStyle);
                        break;
                    default:
                        item.Style.CopyFrom(this.ItemStyle);
                        break;
                }
            }
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.BuildTable(builder);
        }

        private static IEnumerable<object> Enumerate(IEnumerable dataSource)
        {
            switch (dataSource)
            {
                case DataTable table:
                    foreach (DataRowView rowView in table.DefaultView)
                    {
                        yield return rowView;
                    }

                    break;
                case DataView view:
                    foreach (DataRowView rowView in view)
                    {
                        yield return rowView;
                    }

                    break;
                default:
                    foreach (var record in dataSource)
                    {
                        yield return record;
                    }

                    break;
            }
        }

        private static string GridLineStyle(GridLines gridLines)
        {
            switch (gridLines)
            {
                case GridLines.Horizontal:
                    return "border-collapse:collapse;border-style:solid none";
                case GridLines.Vertical:
                    return "border-collapse:collapse;border-style:none solid";
                case GridLines.Both:
                    return "border-collapse:collapse;border-style:solid";
                default:
                    return "border-collapse:collapse;border-style:none";
            }
        }

        private void BuildTable(RenderTreeBuilder builder)
        {
            var sequence = 0;
            builder.OpenElement(sequence++, "table");
            if (!string.IsNullOrEmpty(this.CssClass))
            {
                builder.AddAttribute(sequence++, "class", this.CssClass);
            }

            builder.AddAttribute(sequence++, "cellspacing", this.CellSpacing.ToString(CultureInfo.InvariantCulture));
            if (this.CellPadding >= 0)
            {
                builder.AddAttribute(sequence++, "cellpadding", this.CellPadding.ToString(CultureInfo.InvariantCulture));
            }

            builder.AddAttribute(sequence++, "style", GridLineStyle(this.GridLines));

            if (this.ShowHeader)
            {
                var header = new DataGridItem(this, -1, -1, ListItemType.Header);
                this.BuildCells(header);
                header.Style.CopyFrom(this.HeaderStyle);
                this.RenderRow(builder, sequence++, header);
            }

            foreach (var item in this.items)
            {
                this.RenderRow(builder, sequence++, item);
            }

            if (this.ShowFooter)
            {
                var footer = new DataGridItem(this, -1, -1, ListItemType.Footer);
                this.BuildCells(footer);
                footer.Style.CopyFrom(this.FooterStyle);
                this.RenderRow(builder, sequence++, footer);
            }

            builder.CloseElement();
        }

        private void BuildCells(DataGridItem item)
        {
            item.ClearCells();
            var columnIndex = 0;
            foreach (var column in this.columns)
            {
                if (!column.Visible)
                {
                    columnIndex++;
                    continue;
                }

                var cell = new DataGridCell(item, columnIndex) { Style = column.GetStyle(item.ItemType) };
                item.AddCell(cell);
                columnIndex++;
            }
        }

        private void RenderRow(RenderTreeBuilder builder, int sequence, DataGridItem item)
        {
            builder.OpenElement(sequence, item.ItemType == ListItemType.Header ? "tr" : "tr");
            builder.AddMultipleAttributes(sequence + 1, item.Style.GetAttributes());
            var cellSequence = sequence + 2;
            foreach (var cell in item.Cells)
            {
                var column = this.ColumnAt(cell.ColumnIndex);
                builder.OpenElement(cellSequence, item.ItemType == ListItemType.Header ? "th" : "td");
                builder.AddMultipleAttributes(cellSequence + 1, cell.Style.GetAttributes());
                if (column != null)
                {
                    builder.AddContent(cellSequence + 2, column.RenderCell(cell));
                }

                builder.CloseElement();
                cellSequence += 4;
            }

            builder.CloseElement();
        }

        private DataGridColumn ColumnAt(int columnIndex) =>
            columnIndex >= 0 && columnIndex < this.columns.Count ? this.columns[columnIndex] : null;

        private ListItemType GetItemType(int index)
        {
            if (index == this.EditItemIndex)
            {
                return ListItemType.EditItem;
            }

            if (index == this.SelectedIndex)
            {
                return ListItemType.SelectedItem;
            }

            return index % 2 == 0 ? ListItemType.Item : ListItemType.AlternatingItem;
        }

        private void GenerateColumns()
        {
            if (this.columns.Count > 0 || this.DataSource == null)
            {
                return;
            }

            if (this.DataSource is DataTable table)
            {
                foreach (DataColumn column in table.Columns)
                {
                    this.columns.Add(new BoundColumn { DataField = column.ColumnName, HeaderText = column.ColumnName });
                }

                return;
            }

            foreach (var record in Enumerate(this.DataSource))
            {
                foreach (var property in record.GetType().GetProperties())
                {
                    this.columns.Add(new BoundColumn { DataField = property.Name, HeaderText = property.Name });
                }

                break;
            }
        }

        /// <summary>Centralised Event that is raised whenever a check box is changed.</summary>
        private void OnItemCheckedChangedInternal(object sender, DNNDataGridCheckChangedEventArgs e)
        {
            this.RaiseItemCheckedChanged(sender, e);
        }
    }
}
