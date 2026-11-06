// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;

    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A radio button control.</summary>
    /// <remarks>
    /// This was a <c>System.Web.UI.WebControls.RadioButton</c>: an <c>input type=radio</c> with a
    /// label. The auto-postback <c>CheckedChanged</c> event is now the <see cref="CheckedChanged"/>
    /// event plus the <see cref="CheckedChangedCallback"/> parameter.
    /// </remarks>
    public class DnnRadioButton : DnnComponentBase, ILocalizable
    {
        private bool localize = true;

        /// <summary>Initializes a new instance of the <see cref="DnnRadioButton"/> class.</summary>
        public DnnRadioButton()
        {
            this.CssClass = "SubHead dnnLabel";
        }

        /// <summary>Occurs when <see cref="Checked"/> changed.</summary>
        public event EventHandler CheckedChanged;

        /// <summary>Gets or sets a value indicating whether the button is selected.</summary>
        [Parameter]
        public bool Checked { get; set; }

        /// <summary>Gets or sets the group the button belongs to.</summary>
        [Parameter]
        public string GroupName { get; set; }

        /// <summary>Gets or sets the value submitted for this button.</summary>
        [Parameter]
        public string Value { get; set; }

        /// <summary>Gets or sets the label text of the button.</summary>
        [Parameter]
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the tooltip of the button.</summary>
        [Parameter]
        public string ToolTip { get; set; }

        /// <summary>Gets or sets a value indicating whether the button accepts input.</summary>
        [Parameter]
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether a change is raised to the server.</summary>
        /// <remarks>Replaces the Web Forms auto-postback.</remarks>
        [Parameter]
        public bool AutoPostBack { get; set; }

        /// <summary>Gets or sets a callback raised in addition to the <see cref="CheckedChanged"/> event.</summary>
        [Parameter]
        public EventCallback<EventArgs> CheckedChangedCallback { get; set; }

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <inheritdoc />
        [Parameter]
        public bool Localize
        {
            get
            {
                return this.localize;
            }

            set
            {
                this.localize = value;
            }
        }
        #pragma warning restore BL0007

        /// <inheritdoc />
        [Parameter]
        public string LocalResourceFile { get; set; }

        /// <inheritdoc />
        public virtual void LocalizeStrings()
        {
            if (this.Localize)
            {
                if (!string.IsNullOrEmpty(this.ToolTip))
                {
                    this.ToolTip = Localization.GetString(this.ToolTip, this.LocalResourceFile);
                }

                if (!string.IsNullOrEmpty(this.Text))
                {
                    this.Text = Localization.GetString(this.Text, this.LocalResourceFile);

                    if (string.IsNullOrEmpty(this.ToolTip))
                    {
                        this.ToolTip = Localization.GetString(this.Text + ".ToolTip", this.LocalResourceFile);
                    }
                }
            }
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            this.LocalResourceFile = Utilities.GetLocalResourceFile(this);
        }

        /// <summary>Raises the <see cref="CheckedChanged"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnCheckedChanged(EventArgs e)
        {
            this.CheckedChanged?.Invoke(this, e);
            if (this.CheckedChangedCallback.HasDelegate)
            {
                this.CheckedChangedCallback.InvokeAsync(e);
            }
        }

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

            this.LocalizeStrings();

            var sequence = 0;
            builder.OpenElement(sequence++, "span");
            foreach (var attribute in this.Attributes)
            {
                builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
            }

            var inlineStyle = this.BuildStyleAttribute();
            if (!string.IsNullOrEmpty(inlineStyle))
            {
                builder.AddAttribute(sequence++, "style", inlineStyle);
            }

            if (!string.IsNullOrEmpty(this.ToolTip))
            {
                builder.AddAttribute(sequence++, "title", this.ToolTip);
            }

            builder.OpenElement(sequence++, "input");
            builder.AddAttribute(sequence++, "type", "radio");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            builder.AddAttribute(sequence++, "name", string.IsNullOrEmpty(this.GroupName) ? this.UniqueId : this.GroupName);
            if (!string.IsNullOrEmpty(this.Value))
            {
                builder.AddAttribute(sequence++, "value", this.Value);
            }

            builder.AddAttribute(sequence++, "checked", this.Checked);
            if (!this.Enabled)
            {
                builder.AddAttribute(sequence++, "disabled", true);
            }

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

            builder.CloseElement();
        }
        #pragma warning restore ASP0006

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
