// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.IO;

    /// <summary>Renders literal markup. Replaces <c>System.Web.UI.LiteralControl</c>.</summary>
    internal sealed class MarkupLiteral : MarkupControl
    {
        /// <summary>Initializes a new instance of the <see cref="MarkupLiteral"/> class.</summary>
        /// <param name="text">The literal markup to emit.</param>
        internal MarkupLiteral(string text)
        {
            this.Text = text;
        }

        /// <summary>Gets or sets the literal markup to emit.</summary>
        internal string Text { get; set; }

        /// <inheritdoc/>
        protected override string TagName => string.Empty;

        /// <summary>Writes the literal text, without any surrounding tag.</summary>
        /// <param name="writer">The writer to render to.</param>
        internal new void RenderControl(TextWriter writer)
        {
            writer.Write(this.Text);
        }

        /// <inheritdoc/>
        protected override void RenderContents(TextWriter writer)
        {
            writer.Write(this.Text);
        }
    }
}
