// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.ExtensionPoints
{
    using System;
    using System.ComponentModel;
    using System.Globalization;
    using System.Text;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.ExtensionPoints.Filters;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Renders the tool bar button extension points registered for a module/group.</summary>
    /// <remarks>
    /// Was a <c>WebControl</c> that built its HTML into a <c>StringBuilder</c> and wrote it from
    /// <c>RenderContents</c>/<c>Render</c>. The same HTML is now emitted as pre-rendered markup from
    /// <see cref="BuildRenderTree"/>. The authentication state that selects the filter was read from
    /// <c>HttpContext.Current.Request.IsAuthenticated</c>; it is now read from the request captured
    /// by <see cref="DotNetNuke.Framework.UserControlBase"/> during initialization, because
    /// <c>HttpContext</c> is <see langword="null"/> once a Blazor circuit is interactive.
    /// </remarks>
    public class ToolBarButtonExtensionControl : DefaultExtensionControl
    {
        private IExtensionControlRenderer btnRenderer;
        private string content = string.Empty;

        /// <summary>Gets or sets a value indicating whether the buttons are rendered on a host page.</summary>
        [Parameter]
        [Bindable(true)]
        [DefaultValue(false)]
        public bool IsHost { get; set; }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            var extensionPointManager = new ExtensionPointManager();

            var str = new StringBuilder();

            var filter = new CompositeFilter()
                .And(new FilterByHostMenu(this.IsHost))
                .And(new FilterByUnauthenticated(this.Request.IsAuthenticated()));

            foreach (var extension in extensionPointManager.GetToolBarButtonExtensionPoints(this.Module, this.Group, filter))
            {
                if (extension is IToolBarMenuButtonExtensionPoint)
                {
                    this.btnRenderer = new ToolBarMenuButtonRenderer();
                }
                else
                {
                    extension.ModuleContext = this.ModuleContext;
                    this.btnRenderer = new ToolBarButtonRenderer();
                }

                str.AppendFormat(CultureInfo.InvariantCulture, this.btnRenderer.GetOutput(extension));
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
