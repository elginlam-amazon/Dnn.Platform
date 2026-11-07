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

    /// <summary>This control is only for internal use, please don't reference it in any other place as it may be removed in the future.</summary>
    /// <remarks>
    /// <para>
    /// This was a <c>System.Web.UI.WebControls.GridView</c>, which has no ASP.NET Core equivalent,
    /// so the grid is a component that renders a <c>table</c> row by row from
    /// <see cref="DataSource"/> and <see cref="Columns"/>. Everything DNN's grids depend on is
    /// preserved: paging (<see cref="AllowPaging"/>, <see cref="PageSize"/>,
    /// <see cref="PageIndex"/>, <see cref="PageIndexChanged"/>), sorting
    /// (<see cref="AllowSorting"/>, <see cref="Sorting"/>, <see cref="SortExpression"/>), the
    /// per-row data binding notification (<see cref="RowDataBound"/>) and the per-row commands
    /// (<see cref="RowCommand"/>), which a column template raises through
    /// <see cref="RaiseRowCommand"/> instead of through a postback.
    /// </para>
    /// <para>The skin and the <c>dnnPlugins</c> registration of the original are unchanged.</para>
    /// </remarks>
    public class DnnGrid : DnnComponentBase
    {
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;
        private readonly IClientResourceController clientResourceController;

        private readonly List<DnnGridRow> rows = new List<DnnGridRow>();

        private object dataSource;

        /// <summary>Initializes a new instance of the <see cref="DnnGrid"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IPortalAliasService. Scheduled removal in v12.0.0.")]
        public DnnGrid()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnGrid"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        public DnnGrid(IApplicationStatusInfo appStatus, IEventLogger eventLogger, IClientResourceController clientResourceController)
        {
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
        }

        /// <summary>Occurs for every data row as it is bound.</summary>
        public event EventHandler<DnnGridRowEventArgs> RowDataBound;

        /// <summary>Occurs when a command is raised from a row.</summary>
        public event EventHandler<DnnGridCommandEventArgs> RowCommand;

        /// <summary>Occurs after the current page changed.</summary>
        public event EventHandler PageIndexChanged;

        /// <summary>Occurs when the grid is asked to sort.</summary>
        public event EventHandler Sorting;

        /// <summary>Gets the columns of the grid.</summary>
        public List<DnnGridColumn> Columns { get; } = new List<DnnGridColumn>();

        /// <summary>Gets the rows of the current page.</summary>
        public IReadOnlyList<DnnGridRow> Rows => this.rows;

        /// <summary>Gets or sets the data the grid shows.</summary>
        public object DataSource
        {
            get => this.dataSource;
            set
            {
                this.dataSource = value;
                this.RequiresDataBinding = true;
            }
        }

        /// <summary>Gets or sets a value indicating whether the grid still has to be bound.</summary>
        public bool RequiresDataBinding { get; set; }

        /// <summary>Gets or sets a value indicating whether a column is generated for every field of the data item.</summary>
        [Parameter]
        public bool AutoGenerateColumns { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the grid pages its data.</summary>
        [Parameter]
        public bool AllowPaging { get; set; }

        /// <summary>Gets or sets the number of rows per page.</summary>
        [Parameter]
        public int PageSize { get; set; } = 10;

        /// <summary>Gets or sets a value indicating whether clicking a header sorts the grid.</summary>
        [Parameter]
        public bool AllowSorting { get; set; }

        /// <summary>Gets or sets the expression the grid is sorted by.</summary>
        [Parameter]
        public string SortExpression { get; set; }

        /// <summary>Gets or sets a value indicating whether the sort is descending.</summary>
        [Parameter]
        public bool SortDescending { get; set; }

        /// <summary>Gets or sets the text shown when the data source is empty.</summary>
        [Parameter]
        public string EmptyDataText { get; set; }

        /// <summary>Gets or sets the current page index.</summary>
        [Parameter]
        public int PageIndex { get; set; }

        /// <summary>Gets or sets the screen row number.</summary>
        [Parameter]
        public int ScreenRowNumber { get; set; }

        /// <summary>Gets or sets the row height.</summary>
        [Parameter]
        public int RowHeight { get; set; }

        /// <summary>Gets or sets the current page index.</summary>
        public int CurrentPageIndex
        {
            get => this.PageIndex;
            set => this.PageIndex = value;
        }

        /// <summary>Gets the number of pages the data source spans.</summary>
        public int PageCount
        {
            get
            {
                var count = this.AllItems().Count;
                if (!this.AllowPaging || this.PageSize <= 0)
                {
                    return count == 0 ? 0 : 1;
                }

                return (int)Math.Ceiling(count / (double)this.PageSize);
            }
        }

        /// <summary>Gets the style of a data row.</summary>
        public DnnGridItemStyle RowStyle { get; } = new DnnGridItemStyle();

        /// <summary>Gets the style of an alternating data row.</summary>
        public DnnGridItemStyle AlternatingRowStyle { get; } = new DnnGridItemStyle();

        /// <summary>Gets the style of the row being edited.</summary>
        public DnnGridItemStyle EditRowStyle { get; } = new DnnGridItemStyle();

        /// <summary>Gets the style of the selected row.</summary>
        public DnnGridItemStyle SelectedRowStyle { get; } = new DnnGridItemStyle();

        /// <summary>Gets the style of the header row.</summary>
        public DnnGridItemStyle HeaderStyle { get; } = new DnnGridItemStyle();

        /// <summary>Gets the item style.</summary>
        public DnnGridItemStyle ItemStyle => this.RowStyle;

        /// <summary>Gets the alternating item style.</summary>
        public DnnGridItemStyle AlternatingItemStyle => this.AlternatingRowStyle;

        /// <summary>Gets the edit item style.</summary>
        public DnnGridItemStyle EditItemStyle => this.EditRowStyle;

        /// <summary>Gets the selected item style.</summary>
        public DnnGridItemStyle SelectedItemStyle => this.SelectedRowStyle;

        /// <summary>Reads a field off a data item and formats it.</summary>
        /// <param name="dataItem">The data item.</param>
        /// <param name="fieldName">The name of the field.</param>
        /// <param name="formatString">An optional format string.</param>
        /// <returns>The formatted value.</returns>
        /// <remarks>Replaces <c>DataBinder.Eval(item, field, format)</c>.</remarks>
        public static string FormatFieldValue(object dataItem, string fieldName, string formatString)
        {
            var value = ReadField(dataItem, fieldName);
            if (value == null)
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(formatString)
                ? Convert.ToString(value, CultureInfo.CurrentCulture)
                : string.Format(CultureInfo.CurrentCulture, formatString, value);
        }

        /// <summary>Rebuilds the rows from the data source.</summary>
        /// <remarks>Replaces <c>GridView.DataBind()</c>, including its per-row
        /// <c>RowDataBound</c> notification.</remarks>
        public void DataBind()
        {
            this.RequiresDataBinding = false;
            this.rows.Clear();

            var items = this.AllItems();
            if (!string.IsNullOrEmpty(this.SortExpression))
            {
                items = items
                    .OrderBy(item => ReadField(item, this.SortExpression), Comparer<object>.Create(CompareValues))
                    .ToList();
                if (this.SortDescending)
                {
                    items.Reverse();
                }
            }

            if (this.AllowPaging && this.PageSize > 0)
            {
                if (this.PageIndex * this.PageSize >= items.Count && items.Count > 0)
                {
                    this.PageIndex = Math.Max(0, (int)Math.Ceiling(items.Count / (double)this.PageSize) - 1);
                }

                items = items.Skip(this.PageIndex * this.PageSize).Take(this.PageSize).ToList();
            }

            if (this.AutoGenerateColumns && this.Columns.Count == 0 && items.Count > 0)
            {
                foreach (var property in items[0].GetType().GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                {
                    this.Columns.Add(new DnnGridBoundColumn { DataField = property.Name, HeaderText = property.Name, SortExpression = property.Name, });
                }
            }

            var index = 0;
            foreach (var item in items)
            {
                var row = new DnnGridRow(index, item, DnnGridRowType.DataRow)
                {
                    CssClass = index % 2 == 0 ? this.RowStyle.CssClass : this.AlternatingRowStyle.CssClass,
                };
                this.rows.Add(row);
                this.RowDataBound?.Invoke(this, new DnnGridRowEventArgs(row));
                index++;
            }

            this.NotifyTreeChanged();
        }

        /// <summary>Raises the <see cref="RowCommand"/> event.</summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="commandArgument">The argument of the command.</param>
        /// <param name="row">The row the command came from.</param>
        /// <remarks>
        /// Replaces the command bubbling of the Web Forms grid: a button in a template column calls
        /// this instead of posting back with a <c>CommandName</c>/<c>CommandArgument</c> pair.
        /// </remarks>
        public void RaiseRowCommand(string commandName, string commandArgument, DnnGridRow row)
        {
            this.RowCommand?.Invoke(this, new DnnGridCommandEventArgs(commandName, commandArgument, row));
        }

        /// <summary>Moves to the given page and rebinds.</summary>
        /// <param name="pageIndex">The zero based index of the page.</param>
        public void GoToPage(int pageIndex)
        {
            var pageCount = this.PageCount;
            var target = Math.Max(0, pageCount == 0 ? 0 : Math.Min(pageIndex, pageCount - 1));
            if (target == this.PageIndex)
            {
                return;
            }

            this.PageIndex = target;
            this.DataBind();
            this.PageIndexChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Sorts by the given expression, flipping the direction when it is the current one.</summary>
        /// <param name="sortExpression">The expression to sort by.</param>
        public void SortBy(string sortExpression)
        {
            if (string.IsNullOrEmpty(sortExpression))
            {
                return;
            }

            if (string.Equals(this.SortExpression, sortExpression, StringComparison.Ordinal))
            {
                this.SortDescending = !this.SortDescending;
            }
            else
            {
                this.SortExpression = sortExpression;
                this.SortDescending = false;
            }

            this.Sorting?.Invoke(this, EventArgs.Empty);
            this.DataBind();
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            this.CssClass = "dnn-grid";
            Utilities.ApplyControlSkin(this.clientResourceController, this, string.Empty, string.Empty);

            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, PortalSettings.Current, CommonJs.DnnPlugins);
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            this.AlternatingRowStyle.CssClass = "alter-row";
            this.Style.Remove("border-collapse");
        }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (this.RequiresDataBinding)
            {
                this.DataBind();
            }

            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;
            builder.OpenElement(sequence++, "table");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            foreach (var attribute in this.Attributes)
            {
                builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
            }

            var inlineStyle = this.BuildStyleAttribute();
            if (!string.IsNullOrEmpty(inlineStyle))
            {
                builder.AddAttribute(sequence++, "style", inlineStyle);
            }

            var columns = this.Columns.Where(column => column.Visible).ToList();

            // header
            builder.OpenElement(sequence++, "thead");
            builder.OpenElement(sequence++, "tr");
            if (!string.IsNullOrEmpty(this.HeaderStyle.CssClass))
            {
                builder.AddAttribute(sequence++, "class", this.HeaderStyle.CssClass);
            }

            foreach (var column in columns)
            {
                var current = column;
                builder.OpenElement(sequence, "th");
                if (!string.IsNullOrEmpty(current.HeaderStyleCssClass))
                {
                    builder.AddAttribute(sequence + 1, "class", current.HeaderStyleCssClass);
                }

                var sortExpression = current.SortExpression
                    ?? (current as DnnGridBoundColumn)?.DataField;

                if (this.AllowSorting && !string.IsNullOrEmpty(sortExpression))
                {
                    builder.OpenElement(sequence + 2, "a");
                    builder.AddAttribute(sequence + 3, "href", "javascript:void(0);");
                    builder.AddAttribute(
                        sequence + 4,
                        "onclick",
                        EventCallback.Factory.Create<MouseEventArgs>(this, _ => this.SortBy(sortExpression)));
                    builder.AddContent(sequence + 5, current.HeaderText);
                    builder.CloseElement();
                }
                else if (current is DnnGridTemplateColumn templateColumn && templateColumn.HeaderTemplate != null)
                {
                    builder.AddContent(sequence + 6, templateColumn.HeaderTemplate);
                }
                else
                {
                    builder.AddContent(sequence + 7, current.HeaderText);
                }

                builder.CloseElement();
            }

            sequence += 8;
            builder.CloseElement();
            builder.CloseElement();

            // body
            builder.OpenElement(sequence++, "tbody");
            if (this.rows.Count == 0 && !string.IsNullOrEmpty(this.EmptyDataText))
            {
                builder.OpenElement(sequence++, "tr");
                builder.OpenElement(sequence++, "td");
                builder.AddAttribute(sequence++, "colspan", Math.Max(columns.Count, 1).ToString(CultureInfo.InvariantCulture));
                builder.AddContent(sequence++, this.EmptyDataText);
                builder.CloseElement();
                builder.CloseElement();
            }

            foreach (var row in this.rows.Where(r => r.Visible))
            {
                builder.OpenElement(sequence, "tr");
                if (!string.IsNullOrEmpty(row.CssClass))
                {
                    builder.AddAttribute(sequence + 1, "class", row.CssClass);
                }

                if (this.RowHeight > 0)
                {
                    builder.AddAttribute(sequence + 2, "style", $"height:{this.RowHeight.ToString(CultureInfo.InvariantCulture)}px;");
                }

                foreach (var column in columns)
                {
                    builder.OpenElement(sequence + 3, "td");
                    if (!string.IsNullOrEmpty(column.ItemStyleCssClass))
                    {
                        builder.AddAttribute(sequence + 4, "class", column.ItemStyleCssClass);
                    }

                    builder.AddContent(sequence + 5, column.RenderCell(row));
                    builder.CloseElement();
                }

                builder.CloseElement();
            }

            sequence += 6;
            builder.CloseElement();

            // pager
            if (this.AllowPaging && this.PageCount > 1)
            {
                builder.OpenElement(sequence++, "tfoot");
                builder.OpenElement(sequence++, "tr");
                builder.OpenElement(sequence++, "td");
                builder.AddAttribute(sequence++, "colspan", Math.Max(columns.Count, 1).ToString(CultureInfo.InvariantCulture));
                builder.AddAttribute(sequence++, "class", "dnn-grid-pager");

                for (var page = 0; page < this.PageCount; page++)
                {
                    var target = page;
                    builder.OpenElement(sequence, "a");
                    builder.AddAttribute(sequence + 1, "href", "javascript:void(0);");
                    if (target == this.PageIndex)
                    {
                        builder.AddAttribute(sequence + 2, "class", "dnn-grid-pager-current");
                    }

                    builder.AddAttribute(
                        sequence + 3,
                        "onclick",
                        EventCallback.Factory.Create<MouseEventArgs>(this, _ => this.GoToPage(target)));
                    builder.AddContent(sequence + 4, (target + 1).ToString(CultureInfo.CurrentCulture));
                    builder.CloseElement();
                }

                sequence += 5;
                builder.CloseElement();
                builder.CloseElement();
                builder.CloseElement();
            }

            builder.CloseElement();
        }
        #pragma warning restore ASP0006

        private static object ReadField(object dataItem, string fieldName)
        {
            if (dataItem == null || string.IsNullOrEmpty(fieldName))
            {
                return dataItem;
            }

            if (dataItem is IDictionary dictionary)
            {
                return dictionary.Contains(fieldName) ? dictionary[fieldName] : null;
            }

            var property = dataItem.GetType().GetProperty(fieldName);
            return property?.GetValue(dataItem, null);
        }

        private static int CompareValues(object left, object right)
        {
            if (left is IComparable comparable && right != null)
            {
                return comparable.CompareTo(right);
            }

            if (left == null && right == null)
            {
                return 0;
            }

            return left == null ? -1 : 1;
        }

        private List<object> AllItems()
        {
            return this.dataSource is IEnumerable enumerable
                ? enumerable.Cast<object>().Where(item => item != null).ToList()
                : new List<object>();
        }
    }
}
