// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage.FilterTransform
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;

    /// <summary>Rotation ImageTransform class.</summary>
    public class ImageRotateFlipTransform : ImageTransform
    {
        /// <summary>Initializes a new instance of the <see cref="ImageRotateFlipTransform"/> class.</summary>
        public ImageRotateFlipTransform()
        {
            this.InterpolationMode = InterpolationMode.HighQualityBicubic;
            this.SmoothingMode = SmoothingMode.HighQuality;
            this.PixelOffsetMode = PixelOffsetMode.HighQuality;
            this.CompositingQuality = CompositingQuality.HighQuality;
            this.RotateFlip = RotateFlipType.RotateNoneFlipNone;
        }

        /// <summary>Gets provides an Unique String for this transformation.</summary>
        public override string UniqueString => base.UniqueString + "-" + this.RotateFlip;

        /// <summary>Gets or sets the type of rotation / flip . Defaultvalue is RotateNoneFlipNone.</summary>
        public RotateFlipType RotateFlip { get; set; }

        /// <summary>Processes an input image applying a rotation image transformation.</summary>
        /// <param name="image">Input image.</param>
        /// <returns>Image result after image transformation.</returns>
        /// <remarks>
        /// <c>Bitmap.RotateFlip</c> becomes ImageSharp's <c>Rotate</c> + <c>Flip</c> mutations. The
        /// GDI+ ordering is kept: the image is rotated clockwise first, then flipped.
        /// </remarks>
        public override Image ProcessImage(Image image)
        {
            var bmap = image.CloneAs<Rgba32>();

            // The low two bits of the GDI+ enum are the number of 90-degree clockwise rotations,
            // and the next bit selects a horizontal flip after the rotation.
            var value = (int)this.RotateFlip;
            var rotation = (RotateMode)(((value & 0x03) * 90) % 360);
            var flip = (value & 0x04) == 0 ? FlipMode.None : FlipMode.Horizontal;

            bmap.Mutate(x =>
            {
                if (rotation != RotateMode.None)
                {
                    x.Rotate(rotation);
                }

                if (flip != FlipMode.None)
                {
                    x.Flip(flip);
                }
            });

            return bmap;
        }
    }
}
