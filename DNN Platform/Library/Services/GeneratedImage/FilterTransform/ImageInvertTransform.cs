// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage.FilterTransform
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;

    /// <summary>Invert ImageTransform class.</summary>
    public class ImageInvertTransform : ImageTransform
    {
        /// <summary>Initializes a new instance of the <see cref="ImageInvertTransform"/> class.</summary>
        public ImageInvertTransform()
        {
            this.InterpolationMode = InterpolationMode.HighQualityBicubic;
            this.SmoothingMode = SmoothingMode.HighQuality;
            this.PixelOffsetMode = PixelOffsetMode.HighQuality;
            this.CompositingQuality = CompositingQuality.HighQuality;
        }

        /// <summary>Processes an input image applying an invert image transformation.</summary>
        /// <param name="image">Input image.</param>
        /// <returns>Image result after image transformation.</returns>
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
                        row[i] = new Rgba32((byte)(255 - c.R), (byte)(255 - c.G), (byte)(255 - c.B), byte.MaxValue);
                    }
                }
            });

            return bmap;
        }
    }
}
