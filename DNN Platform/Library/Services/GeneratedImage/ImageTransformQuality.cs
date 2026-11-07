// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage
{
    /// <summary>
    /// Specifies the algorithm used when images are scaled or rotated. Replaces
    /// <c>System.Drawing.Drawing2D.InterpolationMode</c>, which does not exist on modern .NET.
    /// </summary>
    /// <remarks>
    /// The member names are identical to the GDI+ enumeration they replace, so an existing
    /// <c>this.InterpolationMode = InterpolationMode.HighQualityBicubic;</c> assignment keeps
    /// compiling once the <c>using System.Drawing.Drawing2D;</c> directive is dropped. Each value
    /// maps onto an ImageSharp <c>SixLabors.ImageSharp.Processing.Processors.Transforms.IResampler</c> through
    /// <see cref="ImageTransform.GetResampler"/>.
    /// </remarks>
    public enum InterpolationMode
    {
        /// <summary>Equivalent to <see cref="Default"/>.</summary>
        Invalid = -1,

        /// <summary>Specifies the default mode (bilinear/triangle).</summary>
        Default = 0,

        /// <summary>Specifies low quality interpolation (nearest neighbor).</summary>
        Low = 1,

        /// <summary>Specifies high quality interpolation (bicubic).</summary>
        High = 2,

        /// <summary>Specifies bilinear interpolation.</summary>
        Bilinear = 3,

        /// <summary>Specifies bicubic interpolation.</summary>
        Bicubic = 4,

        /// <summary>Specifies nearest-neighbor interpolation.</summary>
        NearestNeighbor = 5,

        /// <summary>Specifies high quality bilinear interpolation.</summary>
        HighQualityBilinear = 6,

        /// <summary>Specifies high quality bicubic interpolation.</summary>
        HighQualityBicubic = 7,
    }

    /// <summary>
    /// Specifies whether smoothing (antialiasing) is applied. Replaces
    /// <c>System.Drawing.Drawing2D.SmoothingMode</c>.
    /// </summary>
    public enum SmoothingMode
    {
        /// <summary>Specifies an invalid mode.</summary>
        Invalid = -1,

        /// <summary>Specifies no antialiasing.</summary>
        Default = 0,

        /// <summary>Specifies no antialiasing.</summary>
        HighSpeed = 1,

        /// <summary>Specifies antialiased rendering.</summary>
        HighQuality = 2,

        /// <summary>Specifies no antialiasing.</summary>
        None = 3,

        /// <summary>Specifies antialiased rendering.</summary>
        AntiAlias = 4,
    }

    /// <summary>
    /// Specifies how pixels are offset during rendering. Replaces
    /// <c>System.Drawing.Drawing2D.PixelOffsetMode</c>.
    /// </summary>
    /// <remarks>
    /// ImageSharp has no equivalent knob — it always renders with pixel-center sampling, which is
    /// what <see cref="HighQuality"/> requested from GDI+. The value is preserved so that it keeps
    /// participating in <see cref="ImageTransform.UniqueString"/>-based cache keys and so that
    /// derived transforms keep compiling.
    /// </remarks>
    public enum PixelOffsetMode
    {
        /// <summary>Specifies an invalid mode.</summary>
        Invalid = -1,

        /// <summary>Specifies the default mode.</summary>
        Default = 0,

        /// <summary>Specifies high speed, low quality rendering.</summary>
        HighSpeed = 1,

        /// <summary>Specifies high quality, low speed rendering.</summary>
        HighQuality = 2,

        /// <summary>Specifies no pixel offset.</summary>
        None = 3,

        /// <summary>Specifies that pixels are offset by -.5 units for high speed antialiasing.</summary>
        Half = 4,
    }

    /// <summary>
    /// Specifies whether gamma correction is applied when images are composited. Replaces
    /// <c>System.Drawing.Drawing2D.CompositingQuality</c>.
    /// </summary>
    public enum CompositingQuality
    {
        /// <summary>Specifies an invalid value.</summary>
        Invalid = -1,

        /// <summary>Specifies the default quality.</summary>
        Default = 0,

        /// <summary>Specifies high speed, low quality compositing.</summary>
        HighSpeed = 1,

        /// <summary>Specifies high quality, low speed compositing.</summary>
        HighQuality = 2,

        /// <summary>Specifies gamma corrected compositing.</summary>
        GammaCorrected = 3,

        /// <summary>Specifies compositing that assumes linear values.</summary>
        AssumeLinear = 4,
    }

    /// <summary>
    /// Specifies the amount of rotation and the axis used to flip an image. Replaces
    /// <c>System.Drawing.RotateFlipType</c>.
    /// </summary>
    /// <remarks>
    /// The member names (and numeric values) are identical to the GDI+ enumeration, which matters
    /// because <c>DnnImageHandler</c> parses the <c>RotateFlip</c> query-string parameter by name:
    /// every existing <c>DnnImageHandler.ashx?...&amp;RotateFlip=Rotate90FlipNone</c> URL keeps
    /// producing the same image.
    /// </remarks>
    public enum RotateFlipType
    {
        /// <summary>Specifies no clockwise rotation and no flipping.</summary>
        RotateNoneFlipNone = 0,

        /// <summary>Specifies a 90-degree clockwise rotation without flipping.</summary>
        Rotate90FlipNone = 1,

        /// <summary>Specifies a 180-degree clockwise rotation without flipping.</summary>
        Rotate180FlipNone = 2,

        /// <summary>Specifies a 270-degree clockwise rotation without flipping.</summary>
        Rotate270FlipNone = 3,

        /// <summary>Specifies no clockwise rotation followed by a horizontal flip.</summary>
        RotateNoneFlipX = 4,

        /// <summary>Specifies a 90-degree clockwise rotation followed by a horizontal flip.</summary>
        Rotate90FlipX = 5,

        /// <summary>Specifies a 180-degree clockwise rotation followed by a horizontal flip.</summary>
        Rotate180FlipX = 6,

        /// <summary>Specifies a 270-degree clockwise rotation followed by a horizontal flip.</summary>
        Rotate270FlipX = 7,

        /// <summary>Specifies no clockwise rotation followed by a vertical flip.</summary>
        RotateNoneFlipY = Rotate180FlipX,

        /// <summary>Specifies a 90-degree clockwise rotation followed by a vertical flip.</summary>
        Rotate90FlipY = Rotate270FlipX,

        /// <summary>Specifies a 180-degree clockwise rotation followed by a vertical flip.</summary>
        Rotate180FlipY = RotateNoneFlipX,

        /// <summary>Specifies a 270-degree clockwise rotation followed by a vertical flip.</summary>
        Rotate270FlipY = Rotate90FlipX,

        /// <summary>Specifies no clockwise rotation followed by a horizontal and vertical flip.</summary>
        RotateNoneFlipXY = Rotate180FlipNone,

        /// <summary>Specifies a 90-degree clockwise rotation followed by a horizontal and vertical flip.</summary>
        Rotate90FlipXY = Rotate270FlipNone,

        /// <summary>Specifies a 180-degree clockwise rotation followed by a horizontal and vertical flip.</summary>
        Rotate180FlipXY = RotateNoneFlipNone,

        /// <summary>Specifies a 270-degree clockwise rotation followed by a horizontal and vertical flip.</summary>
        Rotate270FlipXY = Rotate90FlipNone,
    }
}
