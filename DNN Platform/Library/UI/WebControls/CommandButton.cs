// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;

    using DotNetNuke.Framework;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>The CommandButton Class provides an enhanced Button control for DotNetNuke.</summary>
    /// <remarks>
    /// Was a <c>WebControl</c> that composed an <c>ImageButton</c>, a literal separator and a
    /// <c>LinkButton</c> in <c>CreateChildControls</c>. It is now a <see cref="DnnComponentBase"/>
    /// (DNN's replacement for <c>System.Web.UI.Control</c>) that renders those three pieces itself,
    /// which is why the properties that used to forward to the child controls are plain properties
    /// here. Their names, and the <see cref="Click"/> and <see cref="Command"/> events, are
    /// unchanged.
    /// </remarks>
    public class CommandButton : DnnComponentBase
    {
        private string buttonSeparator = "&nbsp;";
        private string imageUrl;

        /// <summary>Occurs when either the icon or the link is clicked.</summary>
        public event EventHandler Click;

        /// <summary>Occurs when either the icon or the link raises its command.</summary>
        public event CommandEventHandler Command;

        /// <summary>Gets or sets the Separator between Buttons.</summary>
        /// <remarks>Defaults to 1 non-breaking spaces.</remarks>
        /// <value>A String.</value>
        [Parameter]
        public string ButtonSeparator
        {
            get => this.buttonSeparator;
            set => this.buttonSeparator = value;
        }

        /// <summary>Gets or sets a value indicating whether the control causes Validation to occur.</summary>
        /// <remarks>Defaults to True.</remarks>
        /// <value>A Boolean.</value>
        [Parameter]
        public bool CausesValidation { get; set; } = true;

        /// <summary>Gets or sets the command argument for this command button.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string CommandArgument { get; set; }

        /// <summary>Gets or sets the command name for this command button.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string CommandName { get; set; }

        /// <summary>Gets or sets a value indicating whether the link is displayed.</summary>
        /// <remarks>Defaults to True.</remarks>
        /// <value>A Boolean.</value>
        [Parameter]
        public bool DisplayLink { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the icon is displayed.</summary>
        /// <remarks>Defaults to True.</remarks>
        /// <value>A Boolean.</value>
        [Parameter]
        public bool DisplayIcon { get; set; } = true;

        /// <summary>Gets or sets the Image used for the Icon.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string ImageUrl
        {
            get
            {
                if (string.IsNullOrEmpty(this.imageUrl))
                {
                    this.imageUrl = Entities.Icons.IconController.IconURL(this.IconKey, this.IconSize, this.IconStyle);
                }

                return this.imageUrl;
            }

            set => this.imageUrl = value;
        }

        /// <summary>Gets or sets the Icon Key to obtain ImageURL.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string IconKey { get; set; }

        /// <summary>Gets or sets the Icon Siz to obtain ImageURL.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string IconSize { get; set; }

        /// <summary>Gets or sets the Icon Style to obtain ImageURL.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string IconStyle { get; set; }

        /// <summary>Gets or sets the "onClick" Attribute.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string OnClick { get; set; }

        /// <summary>  Gets or sets the "OnClientClick" Property.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string OnClientClick { get; set; }

        /// <summary>Gets or sets the Resource Key used for the Control.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string ResourceKey { get; set; }

        /// <summary>Gets or sets the Text used for the Control.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string Text { get; set; }

        /// <summary>Gets or sets the tooltip resource key used for the Control.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string ToolTipKey { get; set; }

        /// <summary>Gets or sets the Validation Group that this control "validates".</summary>
        /// <value>A String.</value>
        [Parameter]
        public string ValidationGroup { get; set; }

        /// <summary>Gets or sets the resource file used to localize this control.</summary>
        public string LocalResourceFile { get; set; }

        /// <summary>Gets or sets the tool tip of the control.</summary>
        /// <remarks>Replaces <c>WebControl.ToolTip</c>.</remarks>
        [Parameter]
        public string ToolTip { get; set; }

        /// <summary>Gets or sets the callback raised when the button is clicked.</summary>
        /// <remarks>The Blazor equivalent of the <see cref="Click"/> event, for markup consumers.</remarks>
        [Parameter]
        public EventCallback<EventArgs> OnButtonClicked { get; set; }

        /// <summary>Gets or sets the callback raised when the button raises its command.</summary>
        /// <remarks>The Blazor equivalent of the <see cref="Command"/> event, for markup consumers.</remarks>
        [Parameter]
        public EventCallback<CommandEventArgs> OnCommanded { get; set; }

        /// <inheritdoc />
        protected override string TagName => "span";

        /// <summary>Gets or sets the text of the tool tip, which is resolved before rendering.</summary>
        private string TooltipText { get; set; }

        /// <summary>Registers this control for a full post back.</summary>
        /// <remarks>
        /// Replaces the two <c>AJAX.RegisterPostBackControl</c> calls for the icon and the link;
        /// there are no child controls to register any more, so this control registers itself.
        /// </remarks>
        public void RegisterForPostback()
        {
            AJAX.RegisterPostBackControl(this);
        }

        /// <summary>OnButtonClick raises the CommandButton control's <see cref="Click"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnButtonClick(EventArgs e)
        {
            if (this.Click != null)
            {
                this.Click(this, e);
            }

            if (this.OnButtonClicked.HasDelegate)
            {
                this.OnButtonClicked.InvokeAsync(e);
            }
        }

        /// <summary>OnCommand raises the CommandButton control's <see cref="Command"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnCommand(CommandEventArgs e)
        {
            if (this.Command != null)
            {
                this.Command(this, e);
            }

            if (this.OnCommanded.HasDelegate)
            {
                this.OnCommanded.InvokeAsync(e);
            }
        }

        /// <summary>Resolves the CSS class, the local resource file and the tool tip text.</summary>
        /// <param name="e">The event arguments.</param>
        /// <remarks>
        /// This is the body of the WebForms <c>OnPreRender</c> override. It runs on
        /// <see cref="DnnComponentBase.OnLoad"/> instead, because it computes what the control
        /// renders and Blazor raises its pre-render hook after the first render.
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (string.IsNullOrEmpty(this.CssClass))
            {
                this.CssClass = "CommandButton";
            }

            this.LocalResourceFile = UIUtilities.GetLocalResourceFile(this);

            var tooltipText = string.Empty;
            if (!string.IsNullOrEmpty(this.ToolTipKey))
            {
                tooltipText = Localization.GetString(this.ToolTipKey, this.LocalResourceFile);
            }

            if (string.IsNullOrEmpty(tooltipText) && !string.IsNullOrEmpty(this.ToolTip))
            {
                tooltipText = this.ToolTip;
            }

            this.TooltipText = tooltipText;

            // The WebForms control set the "resourcekey" attribute on both child buttons; the
            // localization walker now reads it off this control's own attribute bag.
            if (string.IsNullOrEmpty(this.ResourceKey))
            {
                this.Attributes.Remove("resourcekey");
            }
            else
            {
                this.Attributes["resourcekey"] = this.ResourceKey;
            }
        }

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (this.DisplayIcon)
            {
                builder.OpenElement(3, "input");
                builder.AddAttribute(4, "type", "image");
                builder.AddAttribute(5, "src", this.ImageUrl);
                if (!string.IsNullOrEmpty(this.TooltipText))
                {
                    builder.AddAttribute(6, "title", this.TooltipText);
                    builder.AddAttribute(7, "alt", this.TooltipText);
                }

                this.AddClientClick(builder, 8);
                builder.AddAttribute(9, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, this.RaiseImageClick));
                builder.CloseElement();
            }

            if (this.DisplayLink && this.DisplayIcon && !string.IsNullOrEmpty(this.ButtonSeparator))
            {
                builder.AddMarkupContent(10, this.ButtonSeparator);
            }

            if (this.DisplayLink)
            {
                builder.OpenElement(11, "a");
                builder.AddAttribute(12, "href", "javascript:void(0);");
                if (!string.IsNullOrEmpty(this.CssClass))
                {
                    builder.AddAttribute(13, "class", this.CssClass);
                }

                if (!string.IsNullOrEmpty(this.TooltipText))
                {
                    builder.AddAttribute(14, "title", this.TooltipText);
                }

                this.AddClientClick(builder, 15);
                builder.AddAttribute(16, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, this.RaiseClick));
                builder.AddContent(17, this.Text);
                builder.CloseElement();
            }

            sequence += 18;
            base.RenderContents(builder, ref sequence);
        }

        /// <summary>RaiseImageClick runs when the Image button is clicked.</summary>
        /// <remarks>It raises a Click Event and a Command Event.</remarks>
        /// <param name="e">The click.</param>
        protected void RaiseImageClick(MouseEventArgs e)
        {
            this.OnButtonClick(new EventArgs());
            this.OnCommand(new CommandEventArgs(this.CommandName, this.CommandArgument));
        }

        /// <summary>Adds the client side click handler, which is the "onclick"/"OnClientClick" pair.</summary>
        /// <param name="builder">The render tree builder.</param>
        /// <param name="sequence">The sequence number to use.</param>
        /// <remarks>
        /// The WebForms control put <see cref="OnClick"/> into the element's <c>onclick</c>
        /// attribute and <see cref="OnClientClick"/> into the button's <c>OnClientClick</c>
        /// property, which also rendered into <c>onclick</c>. Both end up in the data attribute
        /// pair read by the DNN client script, because a Blazor element cannot carry both an inline
        /// and a component handler for <c>onclick</c>.
        /// </remarks>
        private void AddClientClick(RenderTreeBuilder builder, int sequence)
        {
            var script = string.Concat(this.OnClientClick, this.OnClientClick != null && this.OnClick != null ? ";" : null, this.OnClick);
            if (!string.IsNullOrEmpty(script))
            {
                builder.AddAttribute(sequence, "data-onclick", script);
            }
        }

        /// <summary>RaiseClick runs when one of the contained Link buttons is clciked.</summary>
        /// <remarks>It raises a Click Event and a Command Event.</remarks>
        /// <param name="e">The click.</param>
        private void RaiseClick(MouseEventArgs e)
        {
            this.OnButtonClick(EventArgs.Empty);
            this.OnCommand(new CommandEventArgs(this.CommandName, this.CommandArgument));
        }
    }
}
