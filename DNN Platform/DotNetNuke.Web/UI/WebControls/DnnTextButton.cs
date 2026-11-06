// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.JSInterop;

    /// <summary>A text button control.</summary>
    /// <remarks>
    /// This was a <c>LinkButton</c>: an anchor that posted back and raised <c>Click</c>. On
    /// ASP.NET Core it renders the same anchor, and the click is dispatched to the server as the
    /// <see cref="Click"/> event / <see cref="ClickCallback"/> instead of a postback. The
    /// <see cref="ConfirmMessage"/> that used to be emitted as an inline <c>onclick</c> confirm is
    /// now a <c>window.confirm</c> made through JS interop before the handler runs, so a cancelled
    /// confirmation still suppresses the action.
    /// </remarks>
    public class DnnTextButton : DnnComponentBase, ILocalizable
    {
        private bool localize = true;

        /// <summary>Occurs when the button is clicked.</summary>
        public event EventHandler Click;

        /// <summary>Gets or sets a confirmation message to display when the button is clicked.</summary>
        [Parameter]
        public string ConfirmMessage { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class to apply when the control is disabled.</summary>
        [Parameter]
        public string DisabledCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the text.</summary>
        [Parameter]
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the tooltip.</summary>
        [Parameter]
        public string ToolTip { get; set; }

        /// <summary>Gets or sets a value indicating whether the button is enabled.</summary>
        [Parameter]
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets a callback raised in addition to the <see cref="Click"/> event.</summary>
        [Parameter]
        public EventCallback<EventArgs> ClickCallback { get; set; }

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

        /// <summary>Gets or sets the JavaScript runtime used to show the confirmation dialog.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

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
                        this.ToolTip = Localization.GetString($"{this.Text}.ToolTip", this.LocalResourceFile);
                    }

                    if (string.IsNullOrEmpty(this.ToolTip))
                    {
                        this.ToolTip = this.Text;
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

            var cssClass = this.CssClass ?? string.Empty;
            if (!this.Enabled && !string.IsNullOrEmpty(this.DisabledCssClass))
            {
                cssClass = this.DisabledCssClass;
            }

            var sequence = 0;
            builder.OpenElement(sequence++, "a");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            builder.AddAttribute(sequence++, "class", cssClass.Trim());
            builder.AddAttribute(sequence++, "href", "javascript:void(0);");

            foreach (var attribute in this.Attributes)
            {
                if (string.Equals(attribute.Key, "class", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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

            if (this.Enabled)
            {
                builder.AddAttribute(sequence++, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, this.OnClickedAsync));
            }

            builder.AddContent(sequence++, this.Text);
            builder.CloseElement();
        }
        #pragma warning restore ASP0006

        /// <summary>Raises the <see cref="Click"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnClick(EventArgs e)
        {
            this.Click?.Invoke(this, e);
            if (this.ClickCallback.HasDelegate)
            {
                this.ClickCallback.InvokeAsync(e);
            }
        }

        private async Task OnClickedAsync(MouseEventArgs args)
        {
            if (!this.Enabled)
            {
                return;
            }

            if (!string.IsNullOrEmpty(this.ConfirmMessage) && this.JsRuntime != null)
            {
                var confirmed = await this.JsRuntime.InvokeAsync<bool>("confirm", this.ConfirmMessage);
                if (!confirmed)
                {
                    return;
                }
            }

            this.OnClick(EventArgs.Empty);
        }
    }
}
