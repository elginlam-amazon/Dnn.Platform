// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.IO;

    /// <summary>Renders a <c>span</c>. Replaces <c>System.Web.UI.WebControls.Label</c>.</summary>
    internal sealed class MarkupLabel : MarkupControl
    {
        /// <summary>Gets or sets the text rendered inside the span.</summary>
        internal string Text { get; set; }

        /// <inheritdoc/>
        protected override string TagName => "span";

        /// <inheritdoc/>
        protected override void RenderContents(TextWriter writer)
        {
            base.RenderContents(writer);
            if (!string.IsNullOrEmpty(this.Text))
            {
                writer.Write(this.Text);
            }
        }
    }
}
