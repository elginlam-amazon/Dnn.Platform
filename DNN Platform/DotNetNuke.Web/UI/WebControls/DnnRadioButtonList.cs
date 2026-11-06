// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Globalization;

    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A control that renders a list of radio buttons.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.RadioButtonList</c>. It derives from
    /// <see cref="DnnListControl"/> so that the imperative <c>Items</c>/<c>SelectedItem</c>/
    /// <c>ClearSelection</c>/<c>SelectedIndexChanged</c> object model DNN uses is preserved; only
    /// the rendering differs from the <c>select</c> of the base class.
    /// </remarks>
    public class DnnRadioButtonList : DnnListControl
    {
        /// <summary>Gets or sets the layout direction of the buttons.</summary>
        [Parameter]
        public string RepeatDirection { get; set; } = "Vertical";

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
            builder.OpenElement(sequence++, "div");
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

            var groupName = string.IsNullOrEmpty(this.ID) ? "DnnRadioButtonList" : this.UniqueId;
            var index = 0;
            foreach (var item in this.Items)
            {
                var currentItem = item;
                var itemId = this.ClientId + "_" + index.ToString(CultureInfo.InvariantCulture);

                builder.OpenElement(sequence, "span");
                builder.OpenElement(sequence + 1, "input");
                builder.AddAttribute(sequence + 2, "type", "radio");
                builder.AddAttribute(sequence + 3, "id", itemId);
                builder.AddAttribute(sequence + 4, "name", groupName);
                builder.AddAttribute(sequence + 5, "value", currentItem.Value);
                builder.AddAttribute(sequence + 6, "checked", currentItem.Selected);
                if (!this.Enabled)
                {
                    builder.AddAttribute(sequence + 7, "disabled", true);
                }

                builder.AddAttribute(
                    sequence + 8,
                    "onchange",
                    EventCallback.Factory.Create<ChangeEventArgs>(this, _ => this.OnItemSelected(currentItem)));
                builder.CloseElement();

                builder.OpenElement(sequence + 9, "label");
                builder.AddAttribute(sequence + 10, "for", itemId);
                builder.AddContent(sequence + 11, currentItem.Text);
                builder.CloseElement();
                builder.CloseElement();

                if (string.Equals(this.RepeatDirection, "Vertical", StringComparison.OrdinalIgnoreCase))
                {
                    builder.OpenElement(sequence + 12, "br");
                    builder.CloseElement();
                }

                index++;
            }

            builder.CloseElement();
        }
        #pragma warning restore ASP0006

        private void OnItemSelected(DnnListItem item)
        {
            this.ClearSelection();
            item.Selected = true;
            this.OnSelectedIndexChanged(EventArgs.Empty);
        }
    }
}
