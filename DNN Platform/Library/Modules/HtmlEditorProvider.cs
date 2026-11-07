// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.HTMLEditorProvider
{
    using System.Collections;

    using DotNetNuke.ComponentModel;
    using DotNetNuke.Framework;
    using DotNetNuke.UI.WebControls;

    using Microsoft.AspNetCore.Components;

    /// <summary>Base class for the DNN rich text editor providers.</summary>
    /// <remarks>
    /// <c>HtmlEditorControl</c> was a <c>System.Web.UI.Control</c> that the consumer added to its own
    /// control collection. There is no server control tree on ASP.NET Core, so a provider now exposes
    /// the markup of its editor as a <see cref="RenderFragment"/>, which the consumer places in its
    /// own render tree. <c>Width</c>/<c>Height</c> use
    /// <see cref="DotNetNuke.UI.WebControls.Unit"/>, the replacement for
    /// <c>System.Web.UI.WebControls.Unit</c>.
    /// </remarks>
    public abstract class HtmlEditorProvider : UserControlBase
    {
        /// <summary>Gets the markup of the editor this provider renders.</summary>
        public abstract RenderFragment HtmlEditorControl { get; }

        /// <summary>Gets or sets the additional toolbars of the editor.</summary>
        public abstract ArrayList AdditionalToolbars { get; set; }

        /// <summary>Gets or sets the identifier of the editor control.</summary>
        public abstract string ControlID { get; set; }

        /// <summary>Gets or sets the root directory the image browser starts in.</summary>
        public abstract string RootImageDirectory { get; set; }

        /// <summary>Gets or sets the html being edited.</summary>
        public abstract string Text { get; set; }

        /// <summary>Gets or sets the width of the editor.</summary>
        public abstract Unit Width { get; set; }

        /// <summary>Gets or sets the height of the editor.</summary>
        public abstract Unit Height { get; set; }

        /// <summary>Gets the configured provider.</summary>
        /// <returns>The <see cref="HtmlEditorProvider"/> instance.</returns>
        public static HtmlEditorProvider Instance()
        {
            return ComponentFactory.GetComponent<HtmlEditorProvider>();
        }

        /// <summary>Adds the configured toolbars to the editor.</summary>
        public abstract void AddToolbar();

        /// <summary>Initializes the editor.</summary>
        public abstract void Initialize();
    }
}
