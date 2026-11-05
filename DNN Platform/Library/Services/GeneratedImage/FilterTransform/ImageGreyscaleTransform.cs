// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage.FilterTransform
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;

    /// <summary>Grey Scale ImageTransform class.</summary>
    public class ImageGreyScaleTransform : ImageTransform
    {
        /// <summary>Initializes a new instance of the <see cref="ImageGreyScaleTransform"/> class.</summary>
        public ImageGreyScaleTransform()
        {
            this.InterpolationMode = InterpolationMode.HighQualityBicubic;
            this.SmoothingMode = SmoothingMode.HighQuality;
            this.PixelOffsetMode = PixelOffsetMode.HighQuality;
            this.CompositingQuality = CompositingQuality.HighQuality;
        }

        /// <summary>Processes an input image applying a grey scale image transformation.</summary>
        /// <param name="image">Input image.</param>
        /// <returns>Image result after image transformation.</returns>
        /// <remarks>
        /// Keeps the original ITU-R BT.601 luma weights and the original truncating cast rather
        /// than delegating to ImageSharp's <c>Grayscale()</c> processor, which uses different
        /// coefficients and would change the output bytes.
        /// </remarks>
        public override Image ProcessImage(Image image)
        {
            var bmap = image.CloneAs<Rgba32>();
            bmap.ProcessPixelRows(accessor =>
            {
                for (var j = 0; j < accessor.Height; j++)
                {
                    var row = accessor.GetRowSpan(j);
                    for (var i = 0; i < row.Length; i++)
                    {
                        var c = row[i];
                        var gray = (byte)((.299 * c.R) + (.587 * c.G) + (.114 * c.B));
                        row[i] = new Rgba32(gray, gray, gray, byte.MaxValue);
                    }
                }
            });

            return bmap;
        }
    }
}
