// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;

    using DotNetNuke.Common.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>The CheckBoxColumnTemplate provides a Template for the CheckBoxColumn.</summary>
    /// <remarks>
    /// Was an <c>ITemplate</c> that instantiated a <c>CheckBox</c> into the cell and bound it
    /// through its <c>DataBinding</c> event. It now renders the <c>input</c> itself and keeps the
    /// posted state in the <see cref="GridCheckBox"/> it registers in the cell, so that the grid's
    /// consumers can still read it back through <c>cell.Controls[0]</c>.
    /// </remarks>
    public class CheckBoxColumnTemplate : IGridTemplate
    {
        /// <summary>Initializes a new instance of the <see cref="CheckBoxColumnTemplate"/> class.</summary>
        public CheckBoxColumnTemplate()
            : this(ListItemType.Item)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="CheckBoxColumnTemplate"/> class.</summary>
        /// <param name="itemType">The list item type.</param>
        public CheckBoxColumnTemplate(ListItemType itemType)
        {
            this.ItemType = itemType;
        }

        /// <summary>Occurs when the state of a check box of this template changed.</summary>
        public event DNNDataGridCheckedColumnEventHandler CheckedChanged;

        /// <summary>
        /// Gets or sets a value indicating whether the column fires a postback when any check box is
        /// changed.
        /// </summary>
        /// <value>A Boolean.</value>
        public bool AutoPostBack { get; set; }

        /// <summary>Gets or sets a value indicating whether the checkbox is checked (unless DataBound).</summary>
        /// <value>A Boolean.</value>
        public bool Checked { get; set; }

        /// <summary>Gets or sets the Data Field that the column should bind to.</summary>
        /// <value>A String.</value>
        public string DataField { get; set; } = Null.NullString;

        /// <summary>Gets or sets a value indicating whether the Column is in Design Mode.</summary>
        /// <value>A Boolean.</value>
        public bool DesignMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the checkboxes are enabled (this is overridden if
        /// the EnabledField is set).
        /// </summary>
        /// <value>A Boolean.</value>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the Data Field that determines whether the checkbox is Enabled
        /// changed.
        /// </summary>
        /// <value>A String.</value>
        public string EnabledField { get; set; } = Null.NullString;

        /// <summary>
        /// Gets or sets a value indicating whether there is a checkbox in the Header that sets all
        /// the checkboxes.
        /// </summary>
        /// <value>A Boolean.</value>
        public bool HeaderCheckBox { get; set; } = true;

        /// <summary>Gets or sets the type of Template to Create.</summary>
        /// <value>A String.</value>
        public ListItemType ItemType { get; set; } = ListItemType.Item;

        /// <summary>Gets or sets the Text to display in a Header Template.</summary>
        /// <value>A String.</value>
        public string Text { get; set; } = string.Empty;

        /// <inheritdoc />
        public RenderFragment Render(DataGridCell cell)
        {
            ArgumentNullException.ThrowIfNull(cell);

            var state = this.BindState(cell);
            return builder =>
            {
                var sequence = 0;
                if (!string.IsNullOrEmpty(this.Text))
                {
                    builder.AddMarkupContent(sequence++, this.Text + "<br/>");
                }

                if (state == null)
                {
                    return;
                }

                builder.OpenElement(sequence++, "input");
                builder.AddAttribute(sequence++, "type", "checkbox");
                builder.AddAttribute(sequence++, "checked", state.Checked);
                if (!state.Enabled)
                {
                    builder.AddAttribute(sequence++, "disabled", true);
                }

                builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(
                    this,
                    args => this.OnCheckChanged(cell, state, args)));
                builder.CloseElement();
            };
        }

        /// <summary>Binds the state of this template's check box for the given cell.</summary>
        /// <param name="cell">The cell being rendered.</param>
        /// <returns>The state, or <see langword="null"/> when no check box renders in this cell.</returns>
        /// <remarks>Replaces the <c>Item_DataBinding</c> handler of the WebForms template.</remarks>
        private GridCheckBox BindState(DataGridCell cell)
        {
            if (this.ItemType == ListItemType.Header && !this.HeaderCheckBox)
            {
                return null;
            }

            var state = new GridCheckBox();
            var dataItem = cell.Item?.DataItem;
            if (!string.IsNullOrEmpty(this.DataField) && this.ItemType != ListItemType.Header)
            {
                state.Checked = this.DesignMode
                    ? false
                    : Convert.ToBoolean(GridDataBinder.Eval(dataItem, this.DataField), CultureInfo.InvariantCulture);
            }
            else
            {
                state.Checked = this.Checked;
            }

            if (!string.IsNullOrEmpty(this.EnabledField))
            {
                state.Enabled = this.DesignMode
                    ? false
                    : Convert.ToBoolean(GridDataBinder.Eval(dataItem, this.EnabledField), CultureInfo.InvariantCulture);
            }
            else
            {
                state.Enabled = this.Enabled;
            }

            cell.Controls.Clear();
            cell.Controls.Add(state);
            return state;
        }

        /// <summary>Centralised Event that is raised whenever a check box's state is modified.</summary>
        private void OnCheckChanged(DataGridCell cell, GridCheckBox state, ChangeEventArgs args)
        {
            state.Checked = args?.Value is bool isChecked
                ? isChecked
                : Convert.ToBoolean(args?.Value, CultureInfo.InvariantCulture);

            var isAll = cell.Item == null || cell.Item.ItemIndex == Null.NullInteger;
            var eventArgs = new DNNDataGridCheckChangedEventArgs(cell.Item, state.Checked, this.DataField, isAll);
            this.CheckedChanged?.Invoke(this, eventArgs);
        }
    }
}
