// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    /// <summary>Renders an image. Replaces <c>System.Web.UI.WebControls.Image</c>.</summary>
    internal sealed class MarkupImage : MarkupControl
    {
        /// <summary>Gets or sets the value rendered as the <c>src</c> attribute.</summary>
        internal string ImageUrl { get; set; }

        /// <summary>Gets or sets the value rendered as the <c>alt</c> attribute.</summary>
        internal string AlternateText { get; set; }

        /// <inheritdoc/>
        protected override string TagName => "img";

        /// <inheritdoc/>
        protected override bool IsSelfClosing => true;

        /// <inheritdoc/>
        protected override void AddAttributesToRender(HtmlMarkupWriter writer)
        {
            if (this.ImageUrl is not null)
            {
                writer.AddAttribute("src", this.ImageUrl);
            }

            if (this.AlternateText is not null)
            {
                writer.AddAttribute("alt", this.AlternateText);
            }

            base.AddAttributesToRender(writer);
        }
    }
}
