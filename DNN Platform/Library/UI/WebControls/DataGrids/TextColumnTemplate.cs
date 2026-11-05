// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;

    using DotNetNuke.Common.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>The TextColumnTemplate provides a Template for the TextColumn.</summary>
    /// <remarks>
    /// Was an <c>ITemplate</c> that instantiated a <c>Label</c> (or, while editing, a
    /// <c>TextBox</c>) into the cell. It now renders a <c>span</c> or an <c>input</c> itself, and
    /// registers the edited text as a <see cref="GridTextBox"/> in the cell so that it can be read
    /// back the way the WebForms code read it off the <c>TextBox</c>.
    /// </remarks>
    public class TextColumnTemplate : IGridTemplate
    {
        private ListItemType mItemType = ListItemType.Item;

        /// <summary>Initializes a new instance of the <see cref="TextColumnTemplate"/> class.</summary>
        public TextColumnTemplate()
            : this(ListItemType.Item)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="TextColumnTemplate"/> class.</summary>
        /// <param name="itemType">The list item type.</param>
        public TextColumnTemplate(ListItemType itemType)
        {
            this.ItemType = itemType;
        }

        /// <summary>Gets or sets the Data Field is the field that binds to the Text Column.</summary>
        /// <value>A String.</value>
        public string DataField { get; set; }

        /// <summary>Gets or sets a value indicating whether the Column is in design mode.</summary>
        /// <value>A Boolean.</value>
        public bool DesignMode { get; set; }

        /// <summary>Gets or sets the type of Template to Create.</summary>
        /// <value>A String.</value>
        public ListItemType ItemType
        {
            get
            {
                return this.mItemType;
            }

            set
            {
                this.mItemType = value;
            }
        }

        /// <summary>Gets or sets the Text (for Header/Footer Templates).</summary>
        /// <value>A String.</value>
        public string Text { get; set; }

        /// <summary>Gets or sets the Width of the Column.</summary>
        /// <value>A Unit.</value>
        public Unit Width { get; set; }

        /// <inheritdoc />
        public RenderFragment Render(DataGridCell cell)
        {
            ArgumentNullException.ThrowIfNull(cell);

            switch (this.ItemType)
            {
                case ListItemType.Item:
                case ListItemType.AlternatingItem:
                case ListItemType.SelectedItem:
                    cell.Text = this.GetValue(cell.Item);
                    return this.RenderLabel(cell.Text);
                case ListItemType.EditItem:
                    var state = new GridTextBox { Text = this.GetValue(cell.Item) };
                    cell.Text = state.Text;
                    cell.Controls.Clear();
                    cell.Controls.Add(state);
                    return this.RenderTextBox(state);
                case ListItemType.Footer:
                case ListItemType.Header:
                    cell.Text = this.Text;
                    var text = this.Text;
                    return builder => builder.AddMarkupContent(0, text);
                default:
                    return static builder => { };
            }
        }

        /// <summary>Gets the value of the Data Field.</summary>
        /// <param name="container">The parent container (DataGridItem).</param>
        private string GetValue(DataGridItem container)
        {
            string itemValue = Null.NullString;
            if (!string.IsNullOrEmpty(this.DataField))
            {
                if (this.DesignMode)
                {
                    itemValue = "DataBound to " + this.DataField;
                }
                else
                {
                    if (container?.DataItem != null)
                    {
                        object evaluation = GridDataBinder.Eval(container.DataItem, this.DataField);
                        if (evaluation != null)
                        {
                            itemValue = evaluation.ToString();
                        }
                    }
                }
            }

            return itemValue;
        }

        private RenderFragment RenderLabel(string text)
        {
            var width = this.Width;
            return builder =>
            {
                builder.OpenElement(0, "span");
                if (!width.IsEmpty)
                {
                    builder.AddAttribute(1, "style", "width:" + width);
                }

                builder.AddContent(2, text);
                builder.CloseElement();
            };
        }

        private RenderFragment RenderTextBox(GridTextBox state)
        {
            var width = this.Width;
            return builder =>
            {
                builder.OpenElement(0, "input");
                builder.AddAttribute(1, "type", "text");
                if (!width.IsEmpty)
                {
                    builder.AddAttribute(2, "style", "width:" + width);
                }

                builder.AddAttribute(3, "value", state.Text);
                builder.AddAttribute(4, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(
                    this,
                    args => state.Text = args?.Value?.ToString() ?? string.Empty));
                builder.CloseElement();
            };
        }
    }
}
