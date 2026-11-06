// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>A control that renders a list of selectable items.</summary>
    /// <remarks>
    /// <para>
    /// This replaces <c>System.Web.UI.WebControls.ListControl</c> (and the <c>DropDownList</c> /
    /// <c>CheckBoxList</c> derivations in this project). It keeps the imperative object model DNN
    /// and third-party modules are written against — <see cref="Items"/>, <see cref="DataSource"/>,
    /// <see cref="DataTextField"/>, <see cref="DataValueField"/>, <see cref="DataBind()"/>,
    /// <see cref="SelectedIndex"/>, <see cref="SelectedValue"/> — while rendering through
    /// <see cref="BuildRenderTree"/>.
    /// </para>
    /// <para>
    /// The auto-postback <c>SelectedIndexChanged</c> event became a plain .NET event plus an
    /// <see cref="EventCallback"/> raised from the <c>change</c> handler of the rendered element.
    /// </para>
    /// </remarks>
    public class DnnListControl : DnnComponentBase
    {
        private readonly DnnListItemCollection items = new DnnListItemCollection();
        private object dataSource;

        /// <summary>Occurs when the selected item changed.</summary>
        public event EventHandler SelectedIndexChanged;

        /// <summary>Gets the items of this list.</summary>
        public DnnListItemCollection Items => this.items;

        /// <summary>Gets or sets the data source the items are built from.</summary>
        public object DataSource
        {
            get => this.dataSource;
            set
            {
                this.dataSource = value;
                this.RequiresDataBinding = true;
            }
        }

        /// <summary>Gets or sets the name of the data source property providing the item text.</summary>
        [Parameter]
        public string DataTextField { get; set; }

        /// <summary>Gets or sets the name of the data source property providing the item value.</summary>
        [Parameter]
        public string DataValueField { get; set; }

        /// <summary>Gets or sets the format applied to the item text.</summary>
        [Parameter]
        public string DataTextFormatString { get; set; }

        /// <summary>Gets or sets a value indicating whether this list still has to be bound.</summary>
        public bool RequiresDataBinding { get; set; }

        /// <summary>Gets or sets a value indicating whether this list accepts input.</summary>
        [Parameter]
        public virtual bool Enabled { get; set; } = true;

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>Gets or sets the CSS width of this list, e.g. <c>"300px"</c>.</summary>
        /// <remarks>Replaces <c>WebControl.Width</c>, which was a <c>System.Web.UI.WebControls.Unit</c>.</remarks>
        [Parameter]
        public string Width
        {
            get => this.Style.TryGetValue("width", out var width) ? width : null;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    this.Style.Remove("width");
                }
                else
                {
                    this.Style["width"] = value;
                }
            }
        }
        #pragma warning restore BL0007

        /// <summary>Gets or sets a value indicating whether a selection change is raised to the server.</summary>
        /// <remarks>Replaces the Web Forms auto-postback.</remarks>
        [Parameter]
        public bool AutoPostBack { get; set; }

        /// <summary>Gets or sets a callback raised in addition to the <see cref="SelectedIndexChanged"/> event.</summary>
        [Parameter]
        public EventCallback<EventArgs> SelectedIndexChangedCallback { get; set; }

        /// <summary>Gets or sets the index of the selected item, or <c>-1</c>.</summary>
        public virtual int SelectedIndex
        {
            get
            {
                for (var index = 0; index < this.items.Count; index++)
                {
                    if (this.items[index].Selected)
                    {
                        return index;
                    }
                }

                return -1;
            }

            set
            {
                this.ClearSelection();
                if (value >= 0 && value < this.items.Count)
                {
                    this.items[value].Selected = true;
                }
            }
        }

        /// <summary>Gets the selected item, or <see langword="null"/>.</summary>
        public virtual DnnListItem SelectedItem
        {
            get
            {
                var index = this.SelectedIndex;
                return index < 0 ? null : this.items[index];
            }
        }

        /// <summary>Gets or sets the value of the selected item.</summary>
        public virtual string SelectedValue
        {
            get => this.SelectedItem?.Value ?? string.Empty;
            set
            {
                this.ClearSelection();
                var item = this.items.FindByValue(value);
                if (item != null)
                {
                    item.Selected = true;
                }
            }
        }

        /// <summary>Gets the values of all the selected items.</summary>
        public IEnumerable<string> SelectedValues => this.items.Where(item => item.Selected).Select(item => item.Value);

        /// <summary>Gets or sets a value indicating whether more than one item can be selected.</summary>
        [Parameter]
        public virtual bool MultipleSelect { get; set; }

        /// <inheritdoc />
        protected override string TagName => null;

        /// <summary>Removes the selection from every item.</summary>
        public void ClearSelection()
        {
            foreach (var item in this.items)
            {
                item.Selected = false;
            }
        }

        /// <summary>Rebuilds <see cref="Items"/> from <see cref="DataSource"/>.</summary>
        /// <remarks>Replaces <c>ListControl.DataBind()</c>, including its reflection over
        /// <see cref="DataTextField"/>/<see cref="DataValueField"/>.</remarks>
        public virtual void DataBind()
        {
            this.RequiresDataBinding = false;

            if (this.dataSource is not IEnumerable enumerable)
            {
                this.NotifyTreeChanged();
                return;
            }

            var selected = this.SelectedValues.ToList();
            this.items.Clear();

            this.PerformDataBinding(enumerable, selected);

            this.NotifyTreeChanged();
        }

        /// <summary>Builds <see cref="Items"/> from the enumerated data source.</summary>
        /// <param name="dataSource">The enumerated data source.</param>
        /// <param name="previouslySelectedValues">The values that were selected before the bind.</param>
        /// <remarks>Replaces <c>ListControl.PerformDataBinding(IEnumerable)</c>.</remarks>
        protected virtual void PerformDataBinding(IEnumerable dataSource, IList<string> previouslySelectedValues)
        {
            ArgumentNullException.ThrowIfNull(dataSource);

            foreach (var row in dataSource)
            {
                if (row == null)
                {
                    continue;
                }

                var text = ReadField(row, this.DataTextField);
                var value = ReadField(row, this.DataValueField) ?? text;

                if (!string.IsNullOrEmpty(this.DataTextFormatString))
                {
                    text = string.Format(CultureInfo.CurrentCulture, this.DataTextFormatString, text);
                }

                this.items.Add(new DnnListItem(text, value) { Selected = previouslySelectedValues?.Contains(value) == true, });
            }
        }

        /// <summary>Binds this list if it has a pending data source.</summary>
        /// <remarks>Replaces <c>ListControl.EnsureDataBound()</c>.</remarks>
        protected void EnsureDataBound()
        {
            if (this.RequiresDataBinding && this.dataSource != null)
            {
                this.DataBind();
            }
        }

        /// <summary>Raises the <see cref="SelectedIndexChanged"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnSelectedIndexChanged(EventArgs e)
        {
            this.SelectedIndexChanged?.Invoke(this, e);
            if (this.SelectedIndexChangedCallback.HasDelegate)
            {
                this.SelectedIndexChangedCallback.InvokeAsync(e);
            }
        }

        /// <summary>Applies a selection made on the client.</summary>
        /// <param name="args">The change event arguments.</param>
        protected void OnSelectionChanged(ChangeEventArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);

            var posted = args.Value?.ToString() ?? string.Empty;
            if (this.MultipleSelect)
            {
                var values = posted.Split(',', StringSplitOptions.RemoveEmptyEntries);
                this.ClearSelection();
                foreach (var value in values)
                {
                    var item = this.items.FindByValue(value);
                    if (item != null)
                    {
                        item.Selected = true;
                    }
                }
            }
            else
            {
                this.SelectedValue = posted;
            }

            this.OnSelectedIndexChanged(EventArgs.Empty);
        }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.EnsureDataBound();

            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;
            builder.OpenElement(sequence++, "select");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
                builder.AddAttribute(sequence++, "name", this.UniqueId);
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

            if (!this.Enabled)
            {
                builder.AddAttribute(sequence++, "disabled", true);
            }

            if (this.MultipleSelect)
            {
                builder.AddAttribute(sequence++, "multiple", true);
            }

            builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnSelectionChanged));

            this.RenderOptions(builder, ref sequence);

            builder.CloseElement();
        }
        #pragma warning restore ASP0006

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <summary>Renders the <c>option</c> elements of this list.</summary>
        /// <param name="builder">The render tree builder.</param>
        /// <param name="sequence">The current render tree sequence number.</param>
        protected void RenderOptions(RenderTreeBuilder builder, ref int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            foreach (var item in this.items)
            {
                builder.OpenElement(sequence, "option");
                builder.AddAttribute(sequence + 1, "value", item.Value);
                if (item.Selected)
                {
                    builder.AddAttribute(sequence + 2, "selected", true);
                }

                builder.AddContent(sequence + 3, item.Text);
                builder.CloseElement();
            }

            sequence += 4;
        }
        #pragma warning restore ASP0006

        private static string ReadField(object row, string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                return Convert.ToString(row, CultureInfo.CurrentCulture);
            }

            if (row is IDictionary dictionary && dictionary.Contains(fieldName))
            {
                return Convert.ToString(dictionary[fieldName], CultureInfo.CurrentCulture);
            }

            var property = row.GetType().GetProperty(fieldName);
            return property == null
                ? Convert.ToString(row, CultureInfo.CurrentCulture)
                : Convert.ToString(property.GetValue(row, null), CultureInfo.CurrentCulture);
        }
    }
}
