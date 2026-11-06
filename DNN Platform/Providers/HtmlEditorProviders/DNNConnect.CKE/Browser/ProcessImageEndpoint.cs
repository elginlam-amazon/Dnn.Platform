// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider.Browser;

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using DotNetNuke.Entities.Portals;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Services.FileSystem;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

/// <summary>
/// The image processing endpoint which replaces the <c>ProcessImage.ashx</c> <c>IHttpHandler</c>: it
/// resizes, rotates and crops the image selected in the file browser, either streaming the result
/// back as the crop preview or saving it as a new file.
/// </summary>
/// <remarks>
/// The URL is preserved exactly: <c>Browser/js/jquery.cropzoom.js</c> and
/// <c>Browser/js/jquery.ImageResizer.js</c> post the viewport/selector geometry to it. The
/// <c>System.Drawing</c> pipeline is ported onto ImageSharp, keeping the output format (the source
/// format when saving, JPEG when previewing) and the clipping behaviour of the original.
/// </remarks>
public static class ProcessImageEndpoint
{
    /// <summary>The preserved URL of the image processing handler.</summary>
    public const string ProcessImagePath = "/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/ProcessImage.ashx";

    /// <summary>Maps the image processing endpoint onto its original URL.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same <paramref name="endpoints"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    public static IEndpointRouteBuilder MapCKEditorProcessImageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // The handler was reached with both GET (the crop preview <img src>) and POST (the save).
        endpoints
            .MapMethods(ProcessImagePath, new[] { HttpMethods.Get, HttpMethods.Post }, HandleRequestAsync)
            .DisableAntiforgery();

        return endpoints;
    }

    /// <summary>Reads a request value from the form or the query string, as <c>Request[key]</c> did.</summary>
    /// <param name="request">The request.</param>
    /// <param name="key">The value name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    private static string GetRequestValue(HttpRequest request, string key)
    {
        if (request.HasFormContentType && request.Form.ContainsKey(key))
        {
            return request.Form[key];
        }

        return request.Query.ContainsKey(key) ? request.Query[key].ToString() : null;
    }

    private static float GetFloat(HttpRequest request, string key)
        => float.Parse(GetRequestValue(request, key), CultureInfo.CurrentCulture);

    /// <summary>Generates the New File Path.</summary>
    /// <param name="file">The Original Image.</param>
    /// <param name="sNewFileName">New File Name for the Image.</param>
    /// <returns>The generated name.</returns>
    private static string GenerateName(IFileInfo file, string sNewFileName)
    {
        var sNewFilePath = !string.IsNullOrEmpty(sNewFileName)
            ? $"{CleanName(sNewFileName)}.{file.Extension}"
            : $"{Path.GetFileNameWithoutExtension(sNewFileName)}_crop.{file.Extension}";

        var iCounter = 0;

        var folder = FolderManager.Instance.GetFolder(file.FolderId);
        while (FileManager.Instance.FileExists(folder, sNewFilePath))
        {
            iCounter++;

            var sFileNameNoExt = Path.GetFileNameWithoutExtension(sNewFilePath);
            sNewFilePath = $"{sFileNameNoExt}_{iCounter}.{file.Extension}";
        }

        return sNewFilePath;
    }

    /// <summary>
    /// The image copy: creates a new <paramref name="dstWidth"/> x <paramref name="dstHeight"/> image
    /// and copies the equally sized region at (<paramref name="srcX"/>, <paramref name="srcY"/>) of
    /// the source into it at (<paramref name="dstX"/>, <paramref name="dstY"/>).
    /// </summary>
    /// <param name="srcBitmap">The src bitmap.</param>
    /// <param name="dstX">The dst x.</param>
    /// <param name="dstY">The dst y.</param>
    /// <param name="srcX">The src x.</param>
    /// <param name="srcY">The src y.</param>
    /// <param name="dstWidth">The dst width.</param>
    /// <param name="dstHeight">The dst height.</param>
    /// <returns>Returns the copied image.</returns>
    private static Image<Rgba32> ImageCopy(
        Image srcBitmap, float dstX, float dstY, float srcX, float srcY, float dstWidth, float dstHeight)
    {
        // Source and destination rectangles have the same size, so drawing the source region at the
        // destination point is the same as drawing the whole source image at the delta of the two
        // origins and letting the canvas clip it - which is what Graphics.DrawImage did.
        var bmp = new Image<Rgba32>(Convert.ToInt32(dstWidth), Convert.ToInt32(dstHeight));

        var location = new Point(
            Convert.ToInt32(Math.Round(dstX - srcX)),
            Convert.ToInt32(Math.Round(dstY - srcY)));

        bmp.Mutate(context => context.DrawImage(srcBitmap, location, 1f));

        return bmp;
    }

    /// <summary>Method to rotate an image either clockwise or counter-clockwise.</summary>
    /// <param name="img">the image to be rotated.</param>
    /// <param name="rotationAngle">
    /// the angle (in degrees).
    ///   Positive values will rotate clockwise
    ///   negative values will rotate counter-clockwise.
    /// </param>
    /// <returns>Returns the Rotated Image.</returns>
    private static Image<Rgba32> RotateImage(Image img, double rotationAngle)
    {
        // The System.Drawing original drew the source - scaled into a height x width rectangle and
        // centred - onto a width x (height + 1) canvas rotated about its centre, clipping whatever
        // fell outside. ImageSharp's Rotate grows the canvas instead, so the result is cropped back
        // to the original canvas size, which reproduces that clipping.
        var canvasWidth = img.Width;
        var canvasHeight = img.Height + 1;

        using var scaled = img.Clone(context => context.Resize(new ResizeOptions
        {
            Size = new Size(img.Height, img.Width),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Bicubic,
        }));

        using var layer = new Image<Rgba32>(canvasWidth, canvasHeight);
        layer.Mutate(context => context.DrawImage(
            scaled,
            new Point((img.Width / 2) - (img.Height / 2), (img.Height / 2) - (img.Width / 2)),
            1f));

        var rotated = layer.Clone(context => context.Rotate((float)rotationAngle));

        var cropX = Math.Max(0, (rotated.Width - canvasWidth) / 2);
        var cropY = Math.Max(0, (rotated.Height - canvasHeight) / 2);
        rotated.Mutate(context => context.Crop(new Rectangle(
            cropX,
            cropY,
            Math.Min(canvasWidth, rotated.Width),
            Math.Min(canvasHeight, rotated.Height))));

        return rotated;
    }

    private static string CleanName(string name)
    {
        name = name.Replace(@"\", "/");
        if (name.Contains('/'))
        {
            name = name.Substring(name.LastIndexOf('/') + 1);
        }

        return name;
    }

    private static bool HasReadPermission(IFolderInfo folder)
    {
        return folder != null && FolderPermissionController.HasFolderPermission(folder.FolderPermissions, "READ");
    }

    private static bool HasWritePermission(IFolderInfo folder)
    {
        return folder != null && FolderPermissionController.HasFolderPermission(folder.FolderPermissions, "WRITE");
    }

    /// <summary>The resize image.</summary>
    /// <param name="img">The img.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>Returns the Resized image.</returns>
    private static Image<Rgba32> ResizeImage(Image img, int width, int height)
    {
        return img.CloneAs<Rgba32>()
            .Clone(context => context.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Bicubic,
            }));
    }

    private static async Task HandleRequestAsync(HttpContext context)
    {
        var request = context.Request;

        var imageH = GetFloat(request, "imageH");
        var imageW = GetFloat(request, "imageW");
        var angle = GetFloat(request, "imageRotate");
        var sourceImageId = int.Parse(GetRequestValue(request, "fileId"), CultureInfo.CurrentCulture);
        var imageX = GetFloat(request, "imageX");
        var imageY = GetFloat(request, "imageY");
        var selectorH = GetFloat(request, "selectorH");
        var selectorW = GetFloat(request, "selectorW");
        var selectorX = GetFloat(request, "selectorX");
        var selectorY = GetFloat(request, "selectorY");
        var viewPortH = GetFloat(request, "viewPortH");
        var viewPortW = GetFloat(request, "viewPortW");

        bool bSaveFile;
        try
        {
            bSaveFile = bool.Parse(GetRequestValue(request, "saveFile"));
        }
        catch (Exception)
        {
            bSaveFile = false;
        }

        string sNewFileName = null;
        var newFileNameValue = GetRequestValue(request, "newFileName");
        if (!string.IsNullOrEmpty(newFileNameValue))
        {
            sNewFileName = newFileNameValue;
        }

        var pWidth = imageW;
        var pHeight = imageH;

        var file = FileManager.Instance.GetFile(sourceImageId);
        if (file == null)
        {
            return;
        }

        var sourceFolder = FolderManager.Instance.GetFolder(file.FolderId);
        if (sourceFolder == null)
        {
            return;
        }

        if (PortalSettings.Current != null
            && !HasReadPermission(sourceFolder))
        {
            throw new SecurityException("You don't have read permission to view files under this folder.");
        }

        IImageFormat sourceFormat;
        Image img;
        using (var content = FileManager.Instance.GetFileContent(file))
        {
            img = Image.Load(content);
            sourceFormat = img.Metadata.DecodedImageFormat;
        }

        try
        {
            // Resize
            var imageP = ResizeImage(img, Convert.ToInt32(pWidth), Convert.ToInt32(pHeight));

            try
            {
                // Rotate if angle is not 0.00 or 360
                if (angle is > 0.0F and < 360.00F)
                {
                    var rotated = RotateImage(imageP, angle);
                    imageP.Dispose();
                    imageP = rotated;
                    pWidth = imageP.Width;
                    pHeight = imageP.Height;
                }

                // Calculate Coords of the Image into the ViewPort
                float srcX;
                float dstX;
                float srcY;
                float dstY;

                if (pWidth > viewPortW)
                {
                    srcX = Math.Abs(imageX - Math.Abs((imageW - pWidth) / 2));
                    dstX = 0;
                }
                else
                {
                    srcX = 0;
                    dstX = imageX + ((imageW - pWidth) / 2);
                }

                if (pHeight > viewPortH)
                {
                    srcY = Math.Abs(imageY - Math.Abs((imageH - pHeight) / 2));
                    dstY = 0;
                }
                else
                {
                    srcY = 0;
                    dstY = imageY + ((imageH - pHeight) / 2);
                }

                // Get Image viewed into the ViewPort
                var inViewPort = ImageCopy(imageP, dstX, dstY, srcX, srcY, viewPortW, viewPortH);
                imageP.Dispose();
                imageP = inViewPort;

                // Get Selector Portion
                var selection = ImageCopy(imageP, 0, 0, selectorX, selectorY, selectorW, selectorH);
                imageP.Dispose();
                imageP = selection;

                if (bSaveFile)
                {
                    context.Response.ContentType = "text/plain";

                    if (PortalSettings.Current != null
                        && !HasWritePermission(sourceFolder))
                    {
                        throw new SecurityException("You don't have write permission to save files under this folder.");
                    }

                    using var stream = new MemoryStream();
                    var fileName = GenerateName(file, sNewFileName);

                    // The format of the source image is preserved (was img.RawFormat).
                    if (sourceFormat is not null)
                    {
                        imageP.Save(stream, sourceFormat);
                    }
                    else
                    {
                        imageP.Save(stream, new JpegEncoder());
                    }

                    stream.Position = 0;

                    const bool alreadyCheckedPermissions = true;
                    FileManager.Instance.AddFile(sourceFolder, fileName, stream, false, !alreadyCheckedPermissions, FileContentTypeManager.Instance.GetContentType(Path.GetExtension(fileName)));
                }
                else
                {
                    context.Response.ContentType = "image/jpeg";
                    await imageP.SaveAsJpegAsync(context.Response.Body);
                }
            }
            finally
            {
                imageP.Dispose();
            }
        }
        finally
        {
            img.Dispose();
        }
    }
}
