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
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>An image button control.</summary>
    /// <remarks>
    /// This was a <c>System.Web.UI.WebControls.ImageButton</c>: an <c>input type=image</c> that
    /// posted back. The DNN addition is the icon resolution — when no <see cref="ImageUrl"/> was
    /// assigned, the URL is resolved from the icon key/size/style through
    /// <see cref="DotNetNuke.Entities.Icons.IconController"/>, which ran in <c>OnPreRender</c> and
    /// now runs right before the element is rendered.
    /// </remarks>
    public class DnnImageButton : DnnComponentBase
    {
        /// <summary>Occurs when the button is clicked.</summary>
        public event EventHandler Click;

        /// <summary>Gets or sets the icon key.</summary>
        [Parameter]
        public string IconKey { get; set; }

        /// <summary>Gets or sets the icon size.</summary>
        [Parameter]
        public string IconSize { get; set; }

        /// <summary>Gets or sets the icon style.</summary>
        [Parameter]
        public string IconStyle { get; set; }

        /// <summary>Gets or sets the URL of the image.</summary>
        [Parameter]
        public string ImageUrl { get; set; }

        /// <summary>Gets or sets the alternate text of the image.</summary>
        [Parameter]
        public string AlternateText { get; set; }

        /// <summary>Gets or sets the tooltip of the button.</summary>
        [Parameter]
        public string ToolTip { get; set; }

        /// <summary>Gets or sets the argument passed to the click handlers.</summary>
        [Parameter]
        public string CommandArgument { get; set; }

        /// <summary>Gets or sets a value indicating whether the button accepts input.</summary>
        [Parameter]
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets the rendered width of the image, in pixels.</summary>
        [Parameter]
        public int Width { get; set; }

        /// <summary>Gets or sets the rendered height of the image, in pixels.</summary>
        [Parameter]
        public int Height { get; set; }

        /// <summary>Gets or sets a callback raised in addition to the <see cref="Click"/> event.</summary>
        [Parameter]
        public EventCallback<EventArgs> ClickCallback { get; set; }

        /// <summary>Resolves <see cref="ImageUrl"/> from the icon key when it was not assigned.</summary>
        /// <remarks>Replaces the <c>OnPreRender</c> override of the original control.</remarks>
        protected void EnsureImageUrl()
        {
            if (string.IsNullOrEmpty(this.ImageUrl))
            {
                this.ImageUrl = DotNetNuke.Entities.Icons.IconController.IconURL(this.IconKey, this.IconSize, this.IconStyle);
            }
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            this.EnsureImageUrl();
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

            this.EnsureImageUrl();

            var sequence = 0;
            builder.OpenElement(sequence++, "input");
            builder.AddAttribute(sequence++, "type", "image");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
                builder.AddAttribute(sequence++, "name", this.UniqueId);
            }

            if (!string.IsNullOrEmpty(this.ImageUrl))
            {
                builder.AddAttribute(sequence++, "src", this.ImageUrl);
            }

            builder.AddAttribute(sequence++, "alt", this.AlternateText ?? string.Empty);

            if (!string.IsNullOrEmpty(this.ToolTip))
            {
                builder.AddAttribute(sequence++, "title", this.ToolTip);
            }

            if (this.Width > 0)
            {
                builder.AddAttribute(sequence++, "width", this.Width.ToString(CultureInfo.InvariantCulture));
            }

            if (this.Height > 0)
            {
                builder.AddAttribute(sequence++, "height", this.Height.ToString(CultureInfo.InvariantCulture));
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

            if (this.Enabled)
            {
                builder.AddAttribute(sequence++, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, _ => this.OnClick(EventArgs.Empty)));
            }
            else
            {
                builder.AddAttribute(sequence++, "disabled", true);
            }

            builder.CloseElement();
        }
        #pragma warning restore ASP0006
    }
}
