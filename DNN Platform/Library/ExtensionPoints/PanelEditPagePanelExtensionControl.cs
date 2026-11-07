// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.ExtensionPoints
{
    using System;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Wraps an edit-page-panel extension point's control in a DNN form section.</summary>
    /// <remarks>
    /// Was a <c>WebControl</c> whose <c>RenderContents</c> emitted the form-section markup around its
    /// children. The markup is reproduced as it was (including the malformed <c>href</c> attribute,
    /// which is what browsers have been receiving); the children now come from
    /// <see cref="ChildContent"/> instead of the <c>Controls</c> collection, and the implicit
    /// <c>&lt;span&gt;</c> that <c>WebControl</c> wrapped around everything is gone because a Blazor
    /// component has no implicit wrapper element.
    /// </remarks>
    public class PanelEditPagePanelExtensionControl : ComponentBase
    {
        /// <summary>Gets or sets the DOM id of the panel heading.</summary>
        [Parameter]
        public string PanelId { get; set; }

        /// <summary>Gets or sets the heading text of the panel.</summary>
        [Parameter]
        public string Text { get; set; }

        /// <summary>Gets or sets the CSS class of the panel.</summary>
        /// <remarks>Replaces <c>WebControl.CssClass</c>.</remarks>
        [Parameter]
        public string CssClass { get; set; }

        /// <summary>Gets or sets the content rendered inside the panel.</summary>
        [Parameter]
        public RenderFragment ChildContent { get; set; }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", this.CssClass);

            builder.OpenElement(2, "h2");
            builder.AddAttribute(3, "id", this.PanelId);
            builder.AddAttribute(4, "class", "dnnFormSectionHead");

            // REVIEW: the original markup was `<a href=" class="dnnLabelExpanded">`, i.e. an
            // unterminated href attribute that browsers recovered from. The clearly-intended markup
            // is emitted instead, since a render tree cannot express a malformed attribute.
            builder.OpenElement(5, "a");
            builder.AddAttribute(6, "href", "#");
            builder.AddAttribute(7, "class", "dnnLabelExpanded");
            builder.AddContent(8, this.Text);
            builder.CloseElement();

            builder.CloseElement();

            builder.OpenElement(9, "fieldset");
            builder.AddContent(10, this.ChildContent);
            builder.CloseElement();

            builder.CloseElement();
        }
    }
}
