// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider.Browser;

using System;
using System.Collections.Generic;
using System.IO;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Image = SixLabors.ImageSharp.Image;

/// <summary>
/// The file type lists and the imaging pipeline of the file browser.
/// </summary>
/// <remarks>
/// These were <c>private static</c> members of <c>Browser.aspx.cs</c>
/// (<c>AllowedFlashExtensions</c>/<c>AllowedImageExtensions</c> and the <c>System.Drawing</c>
/// resize helpers, ported onto <c>SixLabors.ImageSharp</c>). They live here because both the
/// interactive browser (<see cref="Browser"/>) and the CKEditor quick upload endpoint
/// (<see cref="QuickUploadEndpoint"/>) use them, and the bodies are unchanged.
/// </remarks>
internal static class BrowserMedia
{
    /// <summary>The JPEG quality the upload side resize used (<c>new EncoderParameter(Encoder.Quality, 80L)</c>).</summary>
    public const int UploadJpegQuality = 80;

    /// <summary>The allowed flash extensions.</summary>
    public static readonly ISet<string> AllowedFlashExtensions = new HashSet<string>(["swf", "flv", "mp3"], StringComparer.OrdinalIgnoreCase);

    /// <summary>The allowed image extensions.</summary>
    public static readonly ISet<string> AllowedImageExtensions = new HashSet<string>(["bmp", "gif", "jpeg", "jpg", "png", "svg"], StringComparer.OrdinalIgnoreCase);

    /// <summary>The resize image.</summary>
    /// <param name="uplImage">The image to resize.</param>
    /// <param name="newWidth">The new width.</param>
    /// <param name="newHeight">The new height.</param>
    /// <returns>A rewound stream carrying the resized image.</returns>
    /// <remarks>
    /// Was <c>Image.GetThumbnailImage(newWidth, newHeight, null, IntPtr.Zero)</c> followed by
    /// <c>Save(stream, uplImage.RawFormat)</c>: the format of the source image is preserved, and a
    /// JPEG source still goes through the compressing path below.
    /// </remarks>
    public static Stream ResizeImage(Image uplImage, int newWidth, int newHeight)
    {
        // Add Compression to Jpeg Images
        if (uplImage.Metadata.DecodedImageFormat is JpegFormat)
        {
            return ResizeJpegImage(uplImage, newWidth, newHeight);
        }

        // Finally Create a new Resized Image
        using var newImage = Resize(uplImage, newWidth, newHeight);
        var imageFormat = uplImage.Metadata.DecodedImageFormat;
        var fileContents = new MemoryStream();
        if (imageFormat is not null)
        {
            newImage.Save(fileContents, imageFormat);
        }
        else
        {
            // REVIEW: behavior inferred from usage - System.Drawing always had a RawFormat.
            newImage.Save(fileContents, new JpegEncoder());
        }

        fileContents.Position = 0;
        return fileContents;
    }

    /// <summary>Resizes a JPEG image, adding the quality 80 compression the original applied.</summary>
    /// <param name="uplImage">The image to resize.</param>
    /// <param name="newWidth">The new width.</param>
    /// <param name="newHeight">The new height.</param>
    /// <returns>A rewound stream carrying the resized image.</returns>
    /// <remarks>
    /// Was a <c>Bitmap</c> drawn onto with <c>SmoothingMode.AntiAlias</c> +
    /// <c>InterpolationMode.HighQualityBicubic</c> and saved through the JPEG
    /// <c>ImageCodecInfo</c> with <c>Encoder.Quality</c> = 80.
    /// </remarks>
    public static Stream ResizeJpegImage(Image uplImage, int newWidth, int newHeight)
    {
        using var dst = Resize(uplImage, newWidth, newHeight);

        var fileContents = new MemoryStream();
        dst.Save(fileContents, new JpegEncoder { Quality = UploadJpegQuality });
        fileContents.Position = 0;
        return fileContents;
    }

    /// <summary>Determines the dimensions an over sized image is resized to.</summary>
    /// <param name="uplImage">The uploaded image.</param>
    /// <param name="maxWidth">The configured maximum width.</param>
    /// <param name="maxHeight">The configured maximum height.</param>
    /// <returns>The new width and height.</returns>
    public static (int NewWidth, int NewHeight) DetermineNewDimensions(Image uplImage, int maxWidth, int maxHeight)
    {
        // it's too big: we need to resize
        int newWidth, newHeight;

        // which determines the max: height or width?
        double ratioWidth = (double)maxWidth / (double)uplImage.Width;
        double ratioHeight = (double)maxHeight / (double)uplImage.Height;
        if (ratioWidth < ratioHeight)
        {
            // max width needs to be used
            newWidth = maxWidth;
            newHeight = (int)Math.Round(uplImage.Height * ratioWidth);
        }
        else
        {
            // max height needs to be used
            newHeight = maxHeight;
            newWidth = (int)Math.Round(uplImage.Width * ratioHeight);
        }

        return (newWidth, newHeight);
    }

    /// <summary>Gets the encoder the image is saved through.</summary>
    /// <param name="format">The format of the source image, or <see langword="null"/> when it is unknown.</param>
    /// <param name="quality">The JPEG quality to apply, when the format is JPEG.</param>
    /// <returns>The encoder.</returns>
    /// <remarks>
    /// Replaces <c>GetEncoder(ImageFormat)</c>, which looked the <c>ImageCodecInfo</c> of the
    /// source format up among the installed GDI+ codecs. ImageSharp resolves the encoder off the
    /// format itself, so the format of the source image is preserved here as well; only the JPEG
    /// encoder carries a quality, which is what <c>Encoder.Quality</c> was used for.
    /// </remarks>
    public static IImageEncoder GetEncoder(IImageFormat format, int quality)
    {
        if (format is JpegFormat or null)
        {
            return new JpegEncoder { Quality = quality };
        }

        return Configuration.Default.ImageFormatsManager.GetEncoder(format);
    }

    /// <summary>The bicubic stretch which replaces <c>Image.GetThumbnailImage</c>/<c>Graphics.DrawImage</c>.</summary>
    /// <param name="source">The source image.</param>
    /// <param name="newWidth">The new width.</param>
    /// <param name="newHeight">The new height.</param>
    /// <returns>The resized image.</returns>
    public static Image<Rgba32> Resize(Image source, int newWidth, int newHeight)
    {
        return source.CloneAs<Rgba32>()
            .Clone(context => context.Resize(new ResizeOptions
            {
                Size = new Size(newWidth, newHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Bicubic,
            }));
    }
}
