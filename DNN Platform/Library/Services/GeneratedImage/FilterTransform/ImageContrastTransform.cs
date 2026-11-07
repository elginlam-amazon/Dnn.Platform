// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage.FilterTransform
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;

    /// <summary>Constrast ImageTransform class.</summary>
    public class ImageContrastTransform : ImageTransform
    {
        /// <summary>Initializes a new instance of the <see cref="ImageContrastTransform"/> class.</summary>
        public ImageContrastTransform()
        {
            this.InterpolationMode = InterpolationMode.HighQualityBicubic;
            this.SmoothingMode = SmoothingMode.HighQuality;
            this.PixelOffsetMode = PixelOffsetMode.HighQuality;
            this.CompositingQuality = CompositingQuality.HighQuality;
            this.Contrast = 0;
        }

        /// <summary>Gets provides an Unique String for this class.</summary>
        public override string UniqueString => base.UniqueString + "-" + this.Contrast;

        /// <summary>Gets or sets the contrast value. Defaultvalue is 0. Range is -100 .. 100.</summary>
        public double Contrast { get; set; }

        /// <summary>Processes an input image applying a contrast image transformation.</summary>
        /// <param name="image">Input image.</param>
        /// <returns>Image result after image transformation.</returns>
        /// <remarks>
        /// The contrast formula, the clamping and the original's side effect of rewriting
        /// <see cref="Contrast"/> into its squared factor are all preserved; only the pixel access
        /// moved from <c>Bitmap.GetPixel</c>/<c>SetPixel</c> to ImageSharp's pixel rows.
        /// </remarks>
        public override Image ProcessImage(Image image)
        {
            var bmap = image.CloneAs<Rgba32>();
            if (this.Contrast < -100)
            {
                this.Contrast = -100;
            }

            if (this.Contrast > 100)
            {
                this.Contrast = 100;
            }

            this.Contrast = (100.0 + this.Contrast) / 100.0;
            this.Contrast *= this.Contrast;

            var contrast = this.Contrast;
            bmap.ProcessPixelRows(accessor =>
            {
                for (var j = 0; j < accessor.Height; j++)
                {
                    var row = accessor.GetRowSpan(j);
                    for (var i = 0; i < row.Length; i++)
                    {
                        var c = row[i];

                        double pR = c.R / 255.0;
                        pR -= 0.5;
                        pR *= contrast;
                        pR += 0.5;
                        pR *= 255;
                        if (pR < 0)
                        {
                            pR = 0;
                        }

                        if (pR > 255)
                        {
                            pR = 255;
                        }

                        double pG = c.G / 255.0;
                        pG -= 0.5;
                        pG *= contrast;
                        pG += 0.5;
                        pG *= 255;
                        if (pG < 0)
                        {
                            pG = 0;
                        }

                        if (pG > 255)
                        {
                            pG = 255;
                        }

                        double pB = c.B / 255.0;
                        pB -= 0.5;
                        pB *= contrast;
                        pB += 0.5;
                        pB *= 255;
                        if (pB < 0)
                        {
                            pB = 0;
                        }

                        if (pB > 255)
                        {
                            pB = 255;
                        }

                        row[i] = new Rgba32((byte)pR, (byte)pG, (byte)pB, byte.MaxValue);
                    }
                }
            });

            return bmap;
        }
    }
}
