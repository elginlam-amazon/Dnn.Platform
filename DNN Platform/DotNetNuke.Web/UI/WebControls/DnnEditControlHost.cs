// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;

    using DotNetNuke.UI;
    using DotNetNuke.UI.WebControls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Hosts a property editor <see cref="EditControl"/> inside a DNN control tree.</summary>
    /// <remarks>
    /// The migrated <see cref="EditControl"/> hierarchy derives from
    /// <c>PropertyEditorComponentBase</c>, not from <see cref="DnnComponentBase"/>, so an edit
    /// control cannot be added to a <see cref="DnnControlCollection"/> directly. This adapter puts
    /// one in the tree and renders it through the <see cref="PropertyEditorComponentBase.Render"/>
    /// fragment the property editor exposes for exactly this purpose — which is what the Web Forms
    /// code achieved by adding the control to <c>container.Controls</c>.
    /// </remarks>
    public class DnnEditControlHost : DnnComponentBase
    {
        /// <summary>Gets or sets the hosted edit control.</summary>
        [Parameter]
        public EditControl Control { get; set; }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (!this.Visible || this.Control == null)
            {
                return;
            }

            builder.AddContent(0, this.Control.Render());
        }
    }
}
