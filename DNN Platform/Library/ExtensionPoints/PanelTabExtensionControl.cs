// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.ExtensionPoints
{
    using System;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Wraps a tab extension point's control in the edit-page tab content panel.</summary>
    /// <remarks>
    /// Was a <c>WebControl</c> whose <c>RenderBeginTag</c>/<c>RenderEndTag</c> were suppressed and
    /// whose <c>RenderContents</c> emitted the <c>ehccContent</c> wrapper around its children. The
    /// emitted markup is unchanged; the children now come from <see cref="ChildContent"/> instead of
    /// the <c>Controls</c> collection.
    /// </remarks>
    public class PanelTabExtensionControl : ComponentBase
    {
        /// <summary>Gets or sets the DOM id of the panel.</summary>
        [Parameter]
        public string PanelId { get; set; }

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
            builder.AddAttribute(1, "class", "ehccContent dnnClear");
            builder.AddAttribute(2, "id", this.PanelId);
            builder.AddContent(3, this.ChildContent);
            builder.CloseElement();
        }
    }
}
