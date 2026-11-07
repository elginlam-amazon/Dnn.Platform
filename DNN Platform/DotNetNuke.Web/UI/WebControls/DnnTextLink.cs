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

    /// <summary>A text link control.</summary>
    /// <remarks>
    /// This was a <c>WebControl("span")</c> wrapping a <c>HyperLink</c>. On ASP.NET Core it renders
    /// the same markup — a <c>span</c> with the control's css class around an <c>a</c> carrying the
    /// text, tooltip, href and target — directly from <see cref="BuildRenderTree"/>, and keeps the
    /// <see cref="ILocalizable"/> contract including the <c>.ToolTip</c> resource key fallback.
    /// </remarks>
    public class DnnTextLink : DnnComponentBase, ILocalizable
    {
        private bool localize = true;

        /// <summary>Initializes a new instance of the <see cref="DnnTextLink"/> class.</summary>
        public DnnTextLink()
        {
            this.CssClass = "dnnTextLink";
            this.DisabledCssClass = "dnnTextLink disabled";
        }

        /// <summary>Gets or sets the text.</summary>
        [Parameter]
        public string Text { get; set; }

        /// <summary>Gets or sets the tooltip.</summary>
        [Parameter]
        public string ToolTip { get; set; }

        /// <summary>Gets or sets the URL.</summary>
        [Parameter]
        public string NavigateUrl { get; set; }

        /// <summary>Gets or sets the window target of the link.</summary>
        [Parameter]
        public string Target { get; set; }

        /// <summary>Gets or sets a value indicating whether the link is enabled.</summary>
        [Parameter]
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets the CSS class to apply when the control is disabled.</summary>
        [Parameter]
        public string DisabledCssClass { get; set; } = string.Empty;

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

            var cssClass = this.CssClass;
            var href = this.NavigateUrl;
            if (!this.Enabled)
            {
                if (!string.IsNullOrEmpty(this.DisabledCssClass))
                {
                    cssClass = this.DisabledCssClass;
                }

                href = "javascript:void(0);";
            }

            var sequence = 0;
            builder.OpenElement(sequence++, "span");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            if (!string.IsNullOrEmpty(cssClass))
            {
                builder.AddAttribute(sequence++, "class", cssClass);
            }

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

            builder.OpenElement(sequence++, "a");
            if (!string.IsNullOrEmpty(href))
            {
                builder.AddAttribute(sequence++, "href", href);
            }

            if (!string.IsNullOrEmpty(this.Target))
            {
                builder.AddAttribute(sequence++, "target", this.Target);
            }

            if (!string.IsNullOrEmpty(this.ToolTip))
            {
                builder.AddAttribute(sequence++, "title", this.ToolTip);
            }

            builder.AddContent(sequence++, this.Text);
            builder.CloseElement();

            builder.CloseElement();
        }
        #pragma warning restore ASP0006
    }
}
