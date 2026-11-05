// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.IO;

    /// <summary>Renders an anchor. Replaces <c>System.Web.UI.WebControls.HyperLink</c>.</summary>
    internal sealed class MarkupHyperLink : MarkupControl
    {
        /// <summary>Gets or sets the text rendered inside the anchor.</summary>
        internal string Text { get; set; }

        /// <summary>Gets or sets the value rendered as the <c>href</c> attribute.</summary>
        internal string NavigateUrl { get; set; }

        /// <summary>Gets or sets the value rendered as the <c>target</c> attribute.</summary>
        internal string Target { get; set; }

        /// <inheritdoc/>
        protected override string TagName => "a";

        /// <inheritdoc/>
        protected override void AddAttributesToRender(HtmlMarkupWriter writer)
        {
            if (!string.IsNullOrEmpty(this.NavigateUrl))
            {
                writer.AddAttribute("href", this.NavigateUrl);
            }

            if (!string.IsNullOrEmpty(this.Target))
            {
                writer.AddAttribute("target", this.Target);
            }

            base.AddAttributesToRender(writer);
        }

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
