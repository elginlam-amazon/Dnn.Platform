// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.ExtensionPoints
{
    using System;
    using System.Text;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Renders the context menu item extension points registered for a module/group.</summary>
    /// <remarks>
    /// Was a <c>WebControl</c> that built its HTML in <c>OnInit</c> and wrote it from
    /// <c>RenderContents</c>. The HTML is unchanged; it is now emitted as pre-rendered markup from
    /// <see cref="BuildRenderTree"/>.
    /// </remarks>
    public class ContextMenuItemExtensionControl : DefaultExtensionControl
    {
        private string content = string.Empty;

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            var extensionPointManager = new ExtensionPointManager();

            StringBuilder str = new StringBuilder();

            foreach (var extension in extensionPointManager.GetContextMenuItemExtensionPoints(this.Module, this.Group))
            {
                var icon = extension.Icon;
                if (icon.StartsWith("~/", StringComparison.Ordinal))
                {
                    icon = Globals.ResolveUrl(icon);
                }

                str.Append(
                    $"""
                     <li id="{extension.CtxMenuItemId}" class="{extension.CssClass}">
                         <a id="{extension.CtxMenuItemId}_link" href="#" onclick="{extension.Action}" >
                             <img id="{extension.CtxMenuItemId}_icon" alt="{extension.AltText}" src="{icon}" title="{extension.AltText}">
                             <span id="{extension.CtxMenuItemId}_text">{extension.Text}</span>
                         </a>
                     </li>
                     """);
            }

            this.content = str.ToString();
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddContent(0, new MarkupString(this.content));
        }
    }
}
