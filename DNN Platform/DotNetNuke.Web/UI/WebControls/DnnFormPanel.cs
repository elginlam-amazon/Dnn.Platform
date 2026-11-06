// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls
{
    using System;

    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A form panel control.</summary>
    /// <remarks>
    /// This was a <c>WebControl</c> rendering through <c>HtmlTextWriter</c>; the markup it emits
    /// (<c>h2 &gt; a</c> followed by a <c>fieldset</c> holding the children) is unchanged.
    /// </remarks>
    public class DnnFormPanel : DnnComponentBase
    {
        /// <summary>Gets or sets a value indicating whether the panel is expanded.</summary>
        [Parameter]
        public bool Expanded { get; set; }

        /// <summary>Gets or sets the text.</summary>
        [Parameter]
        public string Text { get; set; }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.EnsureChildControls();

            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;

            builder.OpenElement(sequence++, "h2");
            builder.AddAttribute(sequence++, "class", this.CssClass);

            builder.OpenElement(sequence++, "a");
            if (this.Expanded)
            {
                builder.AddAttribute(sequence++, "class", "dnnSectionExpanded");
            }

            builder.AddContent(sequence++, this.Text);
            builder.CloseElement();

            builder.CloseElement();

            builder.OpenElement(sequence++, "fieldset");
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
