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

    /// <summary>A label control.</summary>
    /// <remarks>
    /// This was a <c>System.Web.UI.WebControls.Label</c>. On ASP.NET Core it renders the same
    /// element — a <c>label</c> when it is associated with an input, a <c>span</c> otherwise — and
    /// keeps the <see cref="ILocalizable"/> behaviour that resolved <see cref="Text"/> and
    /// <see cref="ToolTip"/> through DNN's localization engine right before rendering.
    /// </remarks>
    public class DnnLabel : DnnComponentBase, ILocalizable
    {
        private bool localize = true;

        /// <summary>Initializes a new instance of the <see cref="DnnLabel"/> class.</summary>
        public DnnLabel()
        {
            this.CssClass = "dnnFormLabel";
        }

        /// <summary>Gets or sets the text (markup) of the label.</summary>
        [Parameter]
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the tooltip of the label.</summary>
        [Parameter]
        public string ToolTip { get; set; }

        /// <summary>Gets or sets the identifier of the control this label describes.</summary>
        /// <remarks>Replaces <c>Label.AssociatedControlID</c>.</remarks>
        [Parameter]
        public string AssociatedControlID { get; set; }

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
                    var unLocalized = this.Text;

                    this.Text = Localization.GetString(unLocalized, this.LocalResourceFile);

                    if (string.IsNullOrEmpty(this.ToolTip))
                    {
                        this.ToolTip = Localization.GetString(unLocalized + ".ToolTip", this.LocalResourceFile);
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

            var sequence = 0;
            builder.OpenElement(sequence++, string.IsNullOrEmpty(this.AssociatedControlID) ? "span" : "label");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            if (!string.IsNullOrEmpty(this.AssociatedControlID))
            {
                builder.AddAttribute(sequence++, "for", this.AssociatedControlID);
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

            if (!string.IsNullOrEmpty(this.ToolTip))
            {
                builder.AddAttribute(sequence++, "title", this.ToolTip);
            }

            if (!string.IsNullOrEmpty(this.Text))
            {
                builder.AddMarkupContent(sequence++, this.Text);
            }

            if (this.ChildContent != null)
            {
                builder.AddContent(sequence++, this.ChildContent);
            }

            this.RenderChildControls(builder, ref sequence);

            builder.CloseElement();
        }
        #pragma warning restore ASP0006
    }
}
