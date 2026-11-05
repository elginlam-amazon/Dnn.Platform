// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage.FilterTransform
{
    using System;

    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;

    /// <summary>Gamma ImageTransform class.</summary>
    public class ImageGammaTransform : ImageTransform
    {
        /// <summary>Initializes a new instance of the <see cref="ImageGammaTransform"/> class.</summary>
        public ImageGammaTransform()
        {
            this.InterpolationMode = InterpolationMode.HighQualityBicubic;
            this.SmoothingMode = SmoothingMode.HighQuality;
            this.PixelOffsetMode = PixelOffsetMode.HighQuality;
            this.CompositingQuality = CompositingQuality.HighQuality;
            this.Gamma = 1;
        }

        /// <summary>Gets provides an Unique String for this class.</summary>
        public override string UniqueString => base.UniqueString + "-" + this.Gamma;

        /// <summary>Gets or sets the gamma value. Defaultvalue is 0. Range is 0.2 .. 5.</summary>
        public double Gamma { get; set; }

        /// <summary>Processes an input image applying a gamma image transformation.</summary>
        /// <param name="image">Input image.</param>
        /// <returns>Image result after image transformation.</returns>
        /// <remarks>
        /// The 256-entry gamma lookup table and its exact rounding are preserved; only the pixel
        /// access moved from <c>Bitmap.GetPixel</c>/<c>SetPixel</c> to ImageSharp's pixel rows.
        /// </remarks>
        public override Image ProcessImage(Image image)
        {
            var bmap = image.CloneAs<Rgba32>();
            var gammaArray = new byte[256];
            for (var i = 0; i < 256; ++i)
            {
                gammaArray[i] = (byte)Math.Min(255, (int)((255.0 * Math.Pow(i / 255.0, 1.0 / this.Gamma)) + 0.5));
            }

            bmap.ProcessPixelRows(accessor =>
            {
                for (var j = 0; j < accessor.Height; j++)
                {
                    var row = accessor.GetRowSpan(j);
                    for (var i = 0; i < row.Length; i++)
                    {
                        var c = row[i];
                        row[i] = new Rgba32(gammaArray[c.R], gammaArray[c.G], gammaArray[c.B], byte.MaxValue);
                    }
                }
            });

            return bmap;
        }
    }
}
