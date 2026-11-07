// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage.StartTransform
{
    using System;
    using System.Linq;

    using SixLabors.Fonts;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Drawing;
    using SixLabors.ImageSharp.Drawing.Processing;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;

    /// <summary>Placeholder ImageTransform class.</summary>
    public class PlaceholderTransform : ImageTransform
    {
        /// <summary>
        /// The font families the placeholder text is drawn with, in order of preference. The
        /// original asked GDI+ for "Arial"; the rest are the common substitutes that ship with
        /// Linux font packages.
        /// </summary>
        private static readonly string[] FontFamilies =
        {
            "Arial",
            "Liberation Sans",
            "DejaVu Sans",
            "FreeSans",
            "Helvetica",
        };

        /// <summary>Initializes a new instance of the <see cref="PlaceholderTransform"/> class.</summary>
        public PlaceholderTransform()
        {
            this.InterpolationMode = InterpolationMode.HighQualityBicubic;
            this.SmoothingMode = SmoothingMode.HighQuality;
            this.PixelOffsetMode = PixelOffsetMode.HighQuality;
            this.CompositingQuality = CompositingQuality.HighQuality;
            this.BackColor = Color.LightGray;
            this.Color = Color.LightSlateGray;
            this.Width = 0;
            this.Height = 0;
            this.Text = string.Empty;
        }

        /// <summary>Gets provides an Unique String for the image transformation.</summary>
        public override string UniqueString => base.UniqueString + this.Width + "-" + this.Height + "-" + this.Color + "-" + this.BackColor + "-" + this.Text;

        /// <summary>Gets or sets the width of the placeholder image.</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the Height of the placeholder image.</summary>
        public int Height { get; set; }

        /// <summary>Gets or sets the Color of the border and text element.</summary>
        public Color Color { get; set; }

        /// <summary>Gets or sets the backcolor of the placeholder element.</summary>
        public Color BackColor { get; set; }

        /// <summary>Gets or sets the text of the placeholder image. if blank dimension will be used.</summary>
        public string Text { get; set; }

        /// <summary>Processes an input image returning a placeholder image.</summary>
        /// <param name="image">Input image.</param>
        /// <returns>Image result after image transformation.</returns>
        /// <remarks>
        /// The GDI+ <c>Graphics</c> calls become a single ImageSharp mutation: <c>Clear</c> +
        /// <c>FillRectangle</c> become <c>Fill</c>, <c>DrawRectangle</c> becomes
        /// <c>Draw</c> of a <see cref="RectangularPolygon"/> with a 2px pen, and
        /// <c>DrawString</c> with a centered <c>StringFormat</c> becomes <c>DrawText</c> with
        /// <see cref="HorizontalAlignment.Center"/>/<see cref="VerticalAlignment.Center"/> and a
        /// wrapping width. The font-size ladder is unchanged.
        /// </remarks>
        public override Image ProcessImage(Image image)
        {
            // Check dimensions
            if (this.Width == 0 && this.Height > 0)
            {
                this.Width = this.Height;
            }

            if (this.Width > 0 && this.Height == 0)
            {
                this.Height = this.Width;
            }

            var bitmap = new Image<Rgba32>(Math.Max(this.Width, 1), Math.Max(this.Height, 1));
            var text = string.IsNullOrEmpty(this.Text) ? $"{this.Width}x{this.Height}" : this.Text;

            // Determine fontsize
            int fontSize;
            if (this.Width < 101)
            {
                fontSize = 8;
            }
            else if (this.Width < 151)
            {
                fontSize = 10;
            }
            else if (this.Width < 201)
            {
                fontSize = 12;
            }
            else if (this.Width < 301)
            {
                fontSize = 14;
            }
            else
            {
                fontSize = 24;
            }

            var font = GetFontFamily().CreateFont(fontSize, FontStyle.Bold);

            bitmap.Mutate(ctx =>
            {
                // Initialize graphics: the original cleared to white before filling with the
                // backcolor, which matters where the backcolor is translucent.
                ctx.Fill(Color.White);
                ctx.Fill(this.BackColor, new RectangularPolygon(0, 0, this.Width, this.Height));

                // Draw border
                ctx.Draw(this.Color, 2, new RectangularPolygon(1, 1, this.Width - 3, this.Height - 3));

                // Draw text on image, aligned to the center of the inner rectangle.
                var textOptions = new RichTextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    WrappingLength = Math.Max(this.Width - 10, 1),
                    Origin = new PointF(5 + ((this.Width - 10) / 2f), 5 + ((this.Height - 10) / 2f)),
                };

                ctx.DrawText(textOptions, text, this.Color);
            });

            return bitmap;
        }

        /// <summary>Resolves the font family to draw the placeholder text with.</summary>
        /// <remarks>
        /// Unlike GDI+, ImageSharp ships no fonts of its own — it can only use what is installed on
        /// the machine. The preferred families are tried first and any installed family is used as a
        /// fallback, because a placeholder without its dimensions label is useless. When the machine
        /// has no fonts at all this throws with an actionable message instead of rendering nothing.
        /// </remarks>
        private static FontFamily GetFontFamily()
        {
            foreach (var name in FontFamilies)
            {
                if (SystemFonts.Collection.TryGet(name, out var preferred))
                {
                    return preferred;
                }
            }

            var installed = SystemFonts.Families.ToList();
            if (installed.Count == 0)
            {
                throw new InvalidOperationException(
                    "No font is installed on this machine, so the placeholder image text cannot be drawn. Install a font package (for example 'dejavu-fonts' or 'mscorefonts') on the server.");
            }

            return installed[0];
        }
    }
}
