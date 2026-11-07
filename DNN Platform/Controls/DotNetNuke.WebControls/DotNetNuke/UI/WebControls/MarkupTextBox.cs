// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    /// <summary>Renders a single line text input. Replaces <c>System.Web.UI.WebControls.TextBox</c>.</summary>
    internal sealed class MarkupTextBox : MarkupControl
    {
        /// <summary>Gets or sets the value rendered as the <c>value</c> attribute.</summary>
        internal string Text { get; set; }

        /// <inheritdoc/>
        protected override string TagName => "input";

        /// <inheritdoc/>
        protected override bool IsSelfClosing => true;

        /// <inheritdoc/>
        protected override void AddAttributesToRender(HtmlMarkupWriter writer)
        {
            writer.AddAttribute("type", "text");
            if (this.Text is not null)
            {
                writer.AddAttribute("value", this.Text);
            }

            base.AddAttributesToRender(writer);
        }
    }
}
