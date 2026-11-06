// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;

    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A control that renders a checkbox with its label.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.CheckBox</c>.</remarks>
    public class DnnCheckBox : DnnComponentBase
    {
        /// <summary>Occurs when <see cref="Checked"/> changed.</summary>
        public event EventHandler CheckedChanged;

        /// <summary>Gets or sets the command argument.</summary>
        /// <remarks>This lived in view state; it is now a field of the component.</remarks>
        [Parameter]
        public string CommandArgument { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the checkbox is checked.</summary>
        [Parameter]
        public bool Checked { get; set; }

        /// <summary>Gets or sets the label rendered next to the checkbox.</summary>
        [Parameter]
        public string Text { get; set; }

        /// <summary>Gets or sets a value indicating whether the checkbox accepts input.</summary>
        [Parameter]
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the server is notified when the value changes.</summary>
        [Parameter]
        public bool AutoPostBack { get; set; }

        /// <summary>Gets or sets a callback raised in addition to the <see cref="CheckedChanged"/> event.</summary>
        [Parameter]
        public EventCallback<bool> CheckedChangedCallback { get; set; }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;
            builder.OpenElement(sequence++, "input");
            builder.AddAttribute(sequence++, "type", "checkbox");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
                builder.AddAttribute(sequence++, "name", this.UniqueId);
            }

            foreach (var attribute in this.Attributes)
            {
                builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
            }

            if (!this.Enabled)
            {
                builder.AddAttribute(sequence++, "disabled", true);
            }

            builder.AddAttribute(sequence++, "checked", this.Checked);
            builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnInputChanged));
            builder.CloseElement();

            if (!string.IsNullOrEmpty(this.Text))
            {
                builder.OpenElement(sequence++, "label");
                if (!string.IsNullOrEmpty(this.ID))
                {
                    builder.AddAttribute(sequence++, "for", this.ClientId);
                }

                builder.AddContent(sequence++, this.Text);
                builder.CloseElement();
            }
        }
        #pragma warning restore ASP0006

        /// <summary>Raises the <see cref="CheckedChanged"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnCheckedChanged(EventArgs e)
        {
            this.CheckedChanged?.Invoke(this, e);
            if (this.CheckedChangedCallback.HasDelegate)
            {
                this.CheckedChangedCallback.InvokeAsync(this.Checked);
            }
        }

        private void OnInputChanged(ChangeEventArgs args)
        {
            var isChecked = args?.Value is bool flag
                ? flag
                : bool.TryParse(args?.Value?.ToString(), out var parsed) && parsed;

            if (this.Checked == isChecked)
            {
                return;
            }

            this.Checked = isChecked;
            this.OnCheckedChanged(EventArgs.Empty);
        }
    }
}
