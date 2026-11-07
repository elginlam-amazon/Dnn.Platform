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

    /// <summary>A control that renders a single line text input.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.TextBox</c>. The posted value arrives through the
    /// <c>change</c> event instead of a postback, and <see cref="Text"/> is the server side value
    /// exactly as before.
    /// </remarks>
    public class DnnTextBox : DnnComponentBase
    {
        /// <summary>Occurs when <see cref="Text"/> changed.</summary>
        public event EventHandler TextChanged;

        /// <summary>Gets or sets the text of the input.</summary>
        [Parameter]
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the type of the rendered input, e.g. <c>password</c>.</summary>
        [Parameter]
        public string TextMode { get; set; } = "text";

        /// <summary>Gets or sets a value indicating whether the input accepts input.</summary>
        [Parameter]
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets the maximum number of characters accepted.</summary>
        [Parameter]
        public int MaxLength { get; set; }

        /// <summary>Gets or sets the number of visible rows, when this is a multi line input.</summary>
        /// <remarks>Replaces <c>TextBox.Rows</c>.</remarks>
        [Parameter]
        public int Rows { get; set; }

        /// <summary>Gets or sets the number of visible columns, when this is a multi line input.</summary>
        /// <remarks>Replaces <c>TextBox.Columns</c>.</remarks>
        [Parameter]
        public int Columns { get; set; }

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>Gets or sets the CSS width of the input, e.g. <c>"300px"</c>.</summary>
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

        /// <summary>Gets or sets a callback raised in addition to the <see cref="TextChanged"/> event.</summary>
        [Parameter]
        public EventCallback<string> TextChangedCallback { get; set; }

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
            var multiLine = string.Equals(this.TextMode, "multiline", StringComparison.OrdinalIgnoreCase)
                || string.Equals(this.TextMode, "textarea", StringComparison.OrdinalIgnoreCase);

            builder.OpenElement(sequence++, multiLine ? "textarea" : "input");
            if (!multiLine)
            {
                builder.AddAttribute(sequence++, "type", this.TextMode);
            }

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

            if (this.MaxLength > 0)
            {
                builder.AddAttribute(sequence++, "maxlength", this.MaxLength.ToString(CultureInfo.InvariantCulture));
            }

            if (!this.Enabled)
            {
                builder.AddAttribute(sequence++, "disabled", true);
            }

            if (multiLine)
            {
                if (this.Rows > 0)
                {
                    builder.AddAttribute(sequence++, "rows", this.Rows.ToString(CultureInfo.InvariantCulture));
                }

                if (this.Columns > 0)
                {
                    builder.AddAttribute(sequence++, "cols", this.Columns.ToString(CultureInfo.InvariantCulture));
                }
            }

            builder.AddAttribute(sequence++, "value", this.Text);
            builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnInputChanged));
            if (multiLine)
            {
                builder.AddContent(sequence++, this.Text);
            }

            builder.CloseElement();
        }
        #pragma warning restore ASP0006

        /// <summary>Raises the <see cref="TextChanged"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnTextChanged(EventArgs e)
        {
            this.TextChanged?.Invoke(this, e);
            if (this.TextChangedCallback.HasDelegate)
            {
                this.TextChangedCallback.InvokeAsync(this.Text);
            }
        }

        private void OnInputChanged(ChangeEventArgs args)
        {
            var posted = args?.Value?.ToString() ?? string.Empty;
            if (string.Equals(posted, this.Text, StringComparison.Ordinal))
            {
                return;
            }

            this.Text = posted;
            this.OnTextChanged(EventArgs.Empty);
        }
    }
}
