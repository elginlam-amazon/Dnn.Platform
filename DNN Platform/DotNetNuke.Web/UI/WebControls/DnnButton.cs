// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    /// <summary>A button control.</summary>
    /// <remarks>
    /// <para>
    /// This was a <c>System.Web.UI.WebControls.Button</c>: an <c>input type=submit</c> that posted
    /// back and raised <c>Click</c>. It renders a <c>button</c> here and the click is dispatched to
    /// the server as the <see cref="Click"/> event / <see cref="ClickCallback"/>.
    /// </para>
    /// <para>
    /// <see cref="ConfirmMessage"/> and <see cref="DisabledCssClass"/> lived in <c>ViewState</c>;
    /// they are plain parameters now, which live for the lifetime of the component. The confirmation
    /// was emitted as an inline <c>onclick</c> script built by
    /// <see cref="Utilities.GetOnClientClickConfirm(IClientResourceController, DotNetNuke.UI.Utilities.IClientApiControl, string)"/>;
    /// it is now a dialog raised through JS interop before the server handler runs, so cancelling it
    /// still suppresses the action. An <see cref="OnClientClick"/> script assigned by a consumer is
    /// still honoured: it is evaluated client side first and a <see langword="false"/> result
    /// cancels the click, exactly as the Web Forms <c>OnClientClick</c> did.
    /// </para>
    /// </remarks>
    public class DnnButton : DnnComponentBase, ILocalizable
    {
        private readonly IClientResourceController clientResourceController;
        private bool localize = true;

        /// <summary>Initializes a new instance of the <see cref="DnnButton"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public DnnButton()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnButton"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        public DnnButton(IClientResourceController clientResourceController)
        {
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();

            this.CssClass = "CommandButton";
            this.DisabledCssClass = "CommandButtonDisabled";
        }

        /// <summary>Occurs when the button is clicked.</summary>
        public event EventHandler Click;

        /// <summary>Gets or sets a message to display upon clicking the button.</summary>
        [Parameter]
        public string ConfirmMessage { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class to use when the control is disabled.</summary>
        [Parameter]
        public string DisabledCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the caption of the button.</summary>
        [Parameter]
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the tooltip of the button.</summary>
        [Parameter]
        public string ToolTip { get; set; }

        /// <summary>Gets or sets a value indicating whether the button accepts input.</summary>
        [Parameter]
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets a client side script evaluated before the server handler runs.</summary>
        [Parameter]
        public string OnClientClick { get; set; }

        /// <summary>Gets or sets the argument passed to the click handlers.</summary>
        [Parameter]
        public string CommandArgument { get; set; }

        /// <summary>Gets or sets a callback raised in addition to the <see cref="Click"/> event.</summary>
        [Parameter]
        public EventCallback<EventArgs> ClickCallback { get; set; }

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <inheritdoc />
        [Parameter]
        public bool Localize
        {
            get => this.localize;
            set => this.localize = value;
        }
        #pragma warning restore BL0007

        /// <inheritdoc />
        [Parameter]
        public string LocalResourceFile { get; set; }

        /// <summary>Gets or sets the JavaScript runtime used to raise the confirmation dialog.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <inheritdoc />
        public virtual void LocalizeStrings()
        {
            if (this.Localize)
            {
                if (!string.IsNullOrEmpty(this.ToolTip))
                {
                    this.ToolTip = Utilities.GetLocalizedStringFromParent(this.ToolTip, this);
                }

                if (!string.IsNullOrEmpty(this.Text))
                {
                    string unlocalizedText = this.Text;
                    this.Text = Utilities.GetLocalizedStringFromParent(unlocalizedText, this);
                    if (string.IsNullOrEmpty(this.Text))
                    {
                        this.Text = unlocalizedText;
                    }

                    if (string.IsNullOrEmpty(this.ToolTip))
                    {
                        this.ToolTip = Utilities.GetLocalizedStringFromParent(unlocalizedText + ".ToolTip", this);
                        if (string.IsNullOrEmpty(this.ToolTip))
                        {
                            this.ToolTip = unlocalizedText;
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (!this.Enabled)
            {
                this.CssClass = this.DisabledCssClass;
            }

            if (!string.IsNullOrEmpty(this.ConfirmMessage))
            {
                // the confirmation script's resource file is still registered, because the
                // dnnAlert/postBackConfirm styling it brings in is what the dialog is themed with.
                this.clientResourceController?.RegisterScript(Utilities.ToAbsolute("~/js/dnn.postbackconfirm.js"));
            }
        }

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
            builder.OpenElement(sequence++, "button");
            builder.AddAttribute(sequence++, "type", "button");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
                builder.AddAttribute(sequence++, "name", this.UniqueId);
            }

            builder.AddAttribute(sequence++, "class", cssClass.Trim());

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
            else
            {
                builder.AddAttribute(sequence++, "disabled", true);
            }

            builder.AddContent(sequence++, this.Text);
            builder.CloseElement();
        }
        #pragma warning restore ASP0006

        private async Task OnClickedAsync(MouseEventArgs args)
        {
            if (!this.Enabled)
            {
                return;
            }

            if (this.JsRuntime != null && !string.IsNullOrEmpty(this.OnClientClick))
            {
                var proceed = await this.JsRuntime.InvokeAsync<bool>("dnn.runClientClick", this.OnClientClick);
                if (!proceed)
                {
                    return;
                }
            }

            if (!string.IsNullOrEmpty(this.ConfirmMessage) && this.JsRuntime != null)
            {
                var message = this.Localize
                    ? Utilities.GetLocalizedStringFromParent(this.ConfirmMessage, this)
                    : this.ConfirmMessage;

                if (string.IsNullOrEmpty(message))
                {
                    message = this.ConfirmMessage;
                }

                var confirmed = await this.JsRuntime.InvokeAsync<bool>("confirm", message);
                if (!confirmed)
                {
                    return;
                }
            }

            this.OnClick(EventArgs.Empty);
        }
    }
}
