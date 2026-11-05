// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.GeneratedImage
{
    using System.IO;

    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Processing.Processors.Transforms;

    /// <summary>An abstract ImageTransform class.</summary>
    /// <remarks>
    /// <para>
    /// <b>Breaking change:</b> <see cref="ProcessImage"/> takes and returns a
    /// <see cref="SixLabors.ImageSharp.Image"/> instead of a <c>System.Drawing.Image</c>, because
    /// <c>System.Drawing</c> is Windows-only and is not available on modern .NET. Derived
    /// transforms keep their names, their constructors and the four quality knobs below; only the
    /// image type changed.
    /// </para>
    /// <para>
    /// The quality knobs are now the source-compatible DNN enumerations
    /// <c>InterpolationMode</c>, <c>SmoothingMode</c>,
    /// <c>PixelOffsetMode</c> and
    /// <c>CompositingQuality</c>, which carry the same member names the GDI+
    /// enumerations had. A third-party transform only needs to drop its
    /// <c>using System.Drawing.Drawing2D;</c> directive.
    /// </para>
    /// </remarks>
    public abstract class ImageTransform
    {
        /// <summary>Initializes a new instance of the <see cref="ImageTransform"/> class.</summary>
        protected ImageTransform()
        {
            this.InterpolationMode = InterpolationMode.HighQualityBicubic;
            this.SmoothingMode = SmoothingMode.HighQuality;
            this.PixelOffsetMode = PixelOffsetMode.HighQuality;
            this.CompositingQuality = CompositingQuality.HighQuality;
        }

        // REVIEW: should this property be abstract?

        /// <summary>Gets provides an Unique String for the image transformation.</summary>
        public virtual string UniqueString => this.GetType().FullName;

        /// <summary>Gets or sets the interpolation mode used for resizing images. The default is <c>InterpolationMode.HighQualityBicubic</c>.</summary>
        public InterpolationMode InterpolationMode { get; set; }

        /// <summary>Gets or sets the smoothing mode used for resizing images. The default is <c>SmoothingMode.HighQuality</c>.</summary>
        public SmoothingMode SmoothingMode { get; set; }

        /// <summary>Gets or sets the pixel offset mode used for resizing images. The default is <c>PixelOffsetMode.HighQuality</c>.</summary>
        public PixelOffsetMode PixelOffsetMode { get; set; }

        /// <summary>Gets or sets the compositing quality used for resizing images. The default is <c>CompositingQuality.HighQuality</c>.</summary>
        public CompositingQuality CompositingQuality { get; set; }

        /// <summary>Process an input image applying the image transformation.</summary>
        /// <param name="image">Input image.</param>
        /// <returns>Image processed.</returns>
        public abstract Image ProcessImage(Image image);

        /// <summary>Creates a new image from stream. The created image is independent of the stream.</summary>
        /// <param name="imgStream">A stream with the contents of the image to copy.</param>
        /// <returns>Image object.</returns>
        /// <remarks>
        /// Replaces the GDI+ "decode then re-draw into a new 32bpp bitmap" dance. ImageSharp's
        /// <see cref="Image.Load(Stream)"/> already copies the pixel data out of the stream, so the
        /// returned image stays valid after the stream is closed — which was the whole point of the
        /// original copy. The format the image was decoded from is kept in
        /// <c>Image.Metadata.DecodedImageFormat</c> so later stages can round-trip it.
        /// </remarks>
        public virtual Image<Rgba32> CopyImage(Stream imgStream)
        {
            return Image.Load<Rgba32>(imgStream);
        }

        /// <summary>Maps <see cref="InterpolationMode"/> onto the ImageSharp resampler to use.</summary>
        /// <returns>The resampler matching the configured interpolation mode.</returns>
        protected IResampler GetResampler()
        {
            switch (this.InterpolationMode)
            {
                case InterpolationMode.Low:
                case InterpolationMode.NearestNeighbor:
                    return KnownResamplers.NearestNeighbor;
                case InterpolationMode.Bilinear:
                case InterpolationMode.HighQualityBilinear:
                    return KnownResamplers.Triangle;
                case InterpolationMode.High:
                case InterpolationMode.Bicubic:
                case InterpolationMode.HighQualityBicubic:
                    return KnownResamplers.Bicubic;
                default:
                    return KnownResamplers.Bicubic;
            }
        }
    }
}
