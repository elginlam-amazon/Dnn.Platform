// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage.FilterTransform
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;

    /// <summary>Brightness ImageTransform class.</summary>
    public class ImageBrightnessTransform : ImageTransform
    {
        /// <summary>Initializes a new instance of the <see cref="ImageBrightnessTransform"/> class.</summary>
        public ImageBrightnessTransform()
        {
            this.InterpolationMode = InterpolationMode.HighQualityBicubic;
            this.SmoothingMode = SmoothingMode.HighQuality;
            this.PixelOffsetMode = PixelOffsetMode.HighQuality;
            this.CompositingQuality = CompositingQuality.HighQuality;
            this.Brightness = 0;
        }

        /// <summary>Gets provides an Unique String for this class.</summary>
        public override string UniqueString => base.UniqueString + "-" + this.Brightness;

        /// <summary>Gets or sets the brightness value. Defaultvalue is 0. Range is -255 .. 255.</summary>
        public int Brightness { get; set; }

        /// <summary>Processes an input image applying a brightness image transformation.</summary>
        /// <param name="image">Input image.</param>
        /// <returns>Image result after image transformation.</returns>
        /// <remarks>
        /// The per-channel arithmetic — including the original's quirk of clamping a negative
        /// channel to 1 rather than 0 — is preserved exactly; only the pixel access moved from
        /// <c>Bitmap.GetPixel</c>/<c>SetPixel</c> to ImageSharp's pixel rows.
        /// </remarks>
        public override Image ProcessImage(Image image)
        {
            var bmap = image.CloneAs<Rgba32>();
            if (this.Brightness < -255)
            {
                this.Brightness = -255;
            }

            if (this.Brightness > 255)
            {
                this.Brightness = 255;
            }

            var brightness = this.Brightness;
            bmap.ProcessPixelRows(accessor =>
            {
                for (var j = 0; j < accessor.Height; j++)
                {
                    var row = accessor.GetRowSpan(j);
                    for (var i = 0; i < row.Length; i++)
                    {
                        var c = row[i];
                        int cR = c.R + brightness;
                        int cG = c.G + brightness;
                        int cB = c.B + brightness;

                        if (cR < 0)
                        {
                            cR = 1;
                        }

                        if (cR > 255)
                        {
                            cR = 255;
                        }

                        if (cG < 0)
                        {
                            cG = 1;
                        }

                        if (cG > 255)
                        {
                            cG = 255;
                        }

                        if (cB < 0)
                        {
                            cB = 1;
                        }

                        if (cB > 255)
                        {
                            cB = 255;
                        }

                        row[i] = new Rgba32((byte)cR, (byte)cG, (byte)cB, byte.MaxValue);
                    }
                }
            });

            return bmap;
        }
    }
}
