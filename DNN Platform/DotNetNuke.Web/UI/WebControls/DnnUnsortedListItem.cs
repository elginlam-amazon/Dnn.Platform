// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls
{
    using System;

    using DotNetNuke.UI;
    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Creates a control that render one item in a list (&lt;li&gt; control).</summary>
    /// <remarks>
    /// This was a <c>WebControl</c> whose <c>TagKey</c> was <c>li</c> and which wrote its
    /// <c>id</c>/<c>class</c>/<c>title</c> attributes through <c>AddAttributesToRender</c>. The
    /// element and its attributes are unchanged; they are emitted by
    /// <see cref="BuildRenderTree"/> now.
    /// </remarks>
    public class DnnUnsortedListItem : DnnComponentBase
    {
        /// <summary>Gets or sets the tooltip of the list item.</summary>
        [Parameter]
        public string ToolTip { get; set; }

        /// <inheritdoc />
        protected override string TagName => "li";

        /// <summary>Adds the controls.</summary>
        /// <param name="childControls">The controls to add.</param>
        public void AddControls(params IClientApiControl[] childControls)
        {
            if (childControls == null)
            {
                return;
            }

            foreach (var childControl in childControls)
            {
                if (childControl != null)
                {
                    this.Controls.Add(childControl);
                }
            }
        }

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
            builder.OpenElement(sequence++, "li");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            if (!string.IsNullOrEmpty(this.CssClass))
            {
                builder.AddAttribute(sequence++, "class", this.CssClass);
            }

            if (!string.IsNullOrEmpty(this.ToolTip))
            {
                builder.AddAttribute(sequence++, "title", this.ToolTip);
            }

            var inlineStyle = this.BuildStyleAttribute();
            if (!string.IsNullOrEmpty(inlineStyle))
            {
                builder.AddAttribute(sequence++, "style", inlineStyle);
            }

            this.RenderContents(builder, ref sequence);

            builder.CloseElement();
        }
        #pragma warning restore ASP0006
    }
}
