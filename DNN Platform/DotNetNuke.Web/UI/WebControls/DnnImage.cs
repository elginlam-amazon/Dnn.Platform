// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Globalization;

    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A control that renders an <c>img</c> element.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.Image</c>, whose <c>Width</c>/<c>Height</c> were
    /// <c>Unit</c> values rendered as pixel sizes. They are kept as pixel integers here because
    /// that is how DNN set them (thumbnail sizing in <c>Utilities.CreateThumbnail</c>).
    /// </remarks>
    public class DnnImage : DnnComponentBase
    {
        /// <summary>Gets or sets the URL of the image.</summary>
        [Parameter]
        public string ImageUrl { get; set; }

        /// <summary>Gets or sets the alternate text of the image.</summary>
        [Parameter]
        public string AlternateText { get; set; }

        /// <summary>Gets or sets the rendered width of the image, in pixels.</summary>
        [Parameter]
        public int Width { get; set; }

        /// <summary>Gets or sets the rendered height of the image, in pixels.</summary>
        [Parameter]
        public int Height { get; set; }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;
            builder.OpenElement(sequence++, "img");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            if (!string.IsNullOrEmpty(this.ImageUrl))
            {
                builder.AddAttribute(sequence++, "src", this.ImageUrl);
            }

            builder.AddAttribute(sequence++, "alt", this.AlternateText ?? string.Empty);

            if (this.Width > 0)
            {
                builder.AddAttribute(sequence++, "width", this.Width.ToString(CultureInfo.InvariantCulture));
            }

            if (this.Height > 0)
            {
                builder.AddAttribute(sequence++, "height", this.Height.ToString(CultureInfo.InvariantCulture));
            }

            foreach (var attribute in this.Attributes)
            {
                builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
            }

            var inlineStyle = this.BuildStyleAttribute();
            if (!string.IsNullOrEmpty(inlineStyle))
            {
                builder.AddAttribute(sequence++, "style", inlineStyle);
            }

            builder.CloseElement();
        }
        #pragma warning restore ASP0006
    }
}
