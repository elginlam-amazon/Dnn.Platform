// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Common.Utilities
{
    using System;
    using System.IO;

    using DotNetNuke.Services.FileSystem.Internal;

    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Formats;
    using SixLabors.ImageSharp.Formats.Bmp;
    using SixLabors.ImageSharp.Formats.Gif;
    using SixLabors.ImageSharp.Formats.Jpeg;
    using SixLabors.ImageSharp.Formats.Png;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;

    /// <summary>Image helpers.</summary>
    /// <remarks>
    /// Ported from <c>System.Drawing</c> to ImageSharp. <see cref="Size"/> is now
    /// <see cref="SixLabors.ImageSharp.Size"/>, and <see cref="CreateJPG"/> takes an ImageSharp
    /// image. <c>GetEncoderInfo</c> (which returned a GDI+ <c>ImageCodecInfo</c>) is replaced by
    /// <see cref="GetEncoder"/>, which returns the ImageSharp encoder for a MIME type.
    /// </remarks>
    public class ImageUtils
    {
        /// <summary>return height of image.</summary>
        /// <param name="sPath">file path of image.</param>
        /// <returns>The image size in pixels.</returns>
        public static Size GetSize(string sPath)
        {
            var info = Image.Identify(sPath);
            return new Size(info.Width, info.Height);
        }

        /// <summary>return height of image.</summary>
        /// <param name="sPath">file path of image.</param>
        /// <returns>The image height in pixels.</returns>
        public static int GetHeight(string sPath)
        {
            return Image.Identify(sPath).Height;
        }

        /// <summary>return width of image.</summary>
        /// <param name="sPath">file path of image.</param>
        /// <returns>The image width in pixels.</returns>
        public static int GetWidth(string sPath)
        {
            return Image.Identify(sPath).Width;
        }

        /// <summary>return height of image.</summary>
        /// <param name="sFile">Stream of image.</param>
        /// <returns>The image height in pixels.</returns>
        public static int GetHeightFromStream(Stream sFile)
        {
            return Image.Identify(sFile).Height;
        }

        /// <summary>width of image.</summary>
        /// <param name="sFile">Steam of image.</param>
        /// <returns>The image width in pixels.</returns>
        public static int GetWidthFromStream(Stream sFile)
        {
            return Image.Identify(sFile).Width;
        }

        /// <summary>create an image.</summary>
        /// <param name="sFile">path of load image file - will be resized according to height and width set.</param>
        /// <returns>The path of the created image.</returns>
        public static string CreateImage(string sFile)
        {
            var info = Image.Identify(sFile);
            return CreateImage(sFile, info.Height, info.Width);
        }

        /// <summary>create an image.</summary>
        /// <param name="sFile">path of image file.</param>
        /// <param name="intHeight">height.</param>
        /// <param name="intWidth">width.</param>
        /// <returns>The path of the created image.</returns>
        public static string CreateImage(string sFile, int intHeight, int intWidth)
        {
            var fi = new FileInfo(sFile);
            string tmp = fi.FullName.Replace(fi.Extension, "_TEMP" + fi.Extension);
            if (FileWrapper.Instance.Exists(tmp))
            {
                FileWrapper.Instance.SetAttributes(tmp, FileAttributes.Normal);
                FileWrapper.Instance.Delete(tmp);
            }

            File.Copy(sFile, tmp);

            using (var fileContent = File.OpenRead(tmp))
            using (var content = CreateImage(fileContent, intHeight, intWidth, fi.Extension))
            {
                string sFileExt = fi.Extension;
                string sFileNoExtension = Path.GetFileNameWithoutExtension(sFile);

                sFile += sFileNoExtension + sFileExt;
                if (FileWrapper.Instance.Exists(sFile))
                {
                    FileWrapper.Instance.SetAttributes(sFile, FileAttributes.Normal);
                    FileWrapper.Instance.Delete(sFile);
                }

                var arrData = new byte[2048];
                using (Stream outStream = FileWrapper.Instance.Create(sFile))
                {
                    long originalPosition = content.Position;
                    content.Position = 0;

                    try
                    {
                        int intLength = content.Read(arrData, 0, arrData.Length);

                        while (intLength > 0)
                        {
                            outStream.Write(arrData, 0, intLength);
                            intLength = content.Read(arrData, 0, arrData.Length);
                        }
                    }
                    finally
                    {
                        content.Position = originalPosition;
                    }
                }
            }

            if (FileWrapper.Instance.Exists(tmp))
            {
                FileWrapper.Instance.SetAttributes(tmp, FileAttributes.Normal);
                FileWrapper.Instance.Delete(tmp);
            }

            return sFile;
        }

        /// <summary>Scales an image down to fit the given bounds and returns it as a stream.</summary>
        /// <param name="stream">The source image.</param>
        /// <param name="intHeight">The maximum height.</param>
        /// <param name="intWidth">The maximum width.</param>
        /// <param name="extension">The file extension, which selects the output format.</param>
        /// <returns>A stream holding the encoded image. The caller owns it.</returns>
        /// <remarks>
        /// The GDI+ "create a canvas, white-fill it for non-PNG, draw the source scaled into it"
        /// sequence becomes a single ImageSharp mutation. The output format is still chosen from the
        /// extension, so a PNG stays a PNG (and keeps its alpha) and a GIF stays a GIF. The original
        /// 24bpp substitution for indexed source formats is no longer needed: ImageSharp decodes
        /// into RGBA regardless of how the file stored its pixels.
        /// </remarks>
        public static Stream CreateImage(Stream stream, int intHeight, int intWidth, string extension)
        {
            using (var original = Image.Load<Rgba32>(stream))
            {
                int imgHeight, imgWidth;

                int newHeight = intHeight;
                int newWidth = intWidth;
                Size imgSize;
                if (original.Width > newWidth || original.Height > newHeight)
                {
                    imgSize = NewImageSize(original.Width, original.Height, newWidth, newHeight);
                    imgHeight = imgSize.Height;
                    imgWidth = imgSize.Width;
                }
                else
                {
                    imgHeight = original.Height;
                    imgWidth = original.Width;
                }

                if (imgWidth < 1)
                {
                    imgWidth = 1;
                }

                if (imgHeight < 1)
                {
                    imgHeight = 1;
                }

                imgSize = new Size(imgWidth, imgHeight);

                var isPng = extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
                using (var newImg = new Image<Rgba32>(imgSize.Width, imgSize.Height))
                {
                    // Replaces newImg.SetResolution(original.Horizontal/VerticalResolution).
                    newImg.Metadata.HorizontalResolution = original.Metadata.HorizontalResolution;
                    newImg.Metadata.VerticalResolution = original.Metadata.VerticalResolution;
                    newImg.Metadata.ResolutionUnits = original.Metadata.ResolutionUnits;

                    using (var scaled = original.Clone(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size = imgSize,
                        Mode = ResizeMode.Stretch,
                        Sampler = KnownResamplers.Bicubic,
                    })))
                    {
                        newImg.Mutate(ctx =>
                        {
                            // Everything but PNG gets a white background, as the original did,
                            // because those formats cannot carry transparency.
                            if (!isPng)
                            {
                                ctx.BackgroundColor(Color.White);
                            }

                            ctx.DrawImage(scaled, new Point(0, 0), 1f);
                        });
                    }

                    var content = new MemoryStream();
                    newImg.Save(content, GetEncoderFromExtension(extension));
                    content.Position = 0;
                    return content;
                }
            }
        }

        /// <summary>create a JPG image.</summary>
        /// <param name="sFile">name of image.</param>
        /// <param name="img">the image.</param>
        /// <param name="compressionLevel">image quality.</param>
        /// <returns>The path of the created image.</returns>
        /// <remarks>
        /// The <c>img</c> parameter is now an ImageSharp <see cref="Image"/> rather than a
        /// <c>System.Drawing.Bitmap</c>. The original's redundant "draw the bitmap onto itself"
        /// step is dropped because it was a no-op; the JPEG quality parameter is honored through
        /// <see cref="JpegEncoder.Quality"/> instead of an <c>EncoderParameter</c>. As before, the
        /// image is disposed and unexpected save failures are suppressed.
        /// </remarks>
        public static string CreateJPG(string sFile, Image img, int compressionLevel)
        {
            if (img == null)
            {
                throw new ArgumentNullException(nameof(img));
            }

            if (File.Exists(sFile))
            {
                File.Delete(sFile);
            }

            try
            {
                img.Save(sFile, new JpegEncoder { Quality = Math.Clamp(compressionLevel, 1, 100) });
            }
            catch (Exception)
            {
                // suppress unexpected exceptions
            }

            img.Dispose();
            return sFile;
        }

        /// <summary>create an image based on a stream (read from a database).</summary>
        /// <param name="sFile">image name.</param>
        /// <param name="intHeight">height.</param>
        /// <param name="intWidth">width.</param>
        /// <returns>steam.</returns>
        public static MemoryStream CreateImageForDB(Stream sFile, int intHeight, int intWidth)
        {
            var newStream = new MemoryStream();
            using (var g = Image.Load<Rgba32>(sFile))
            {
                int imgHeight, imgWidth;

                if (intHeight > 0 & intWidth > 0)
                {
                    int newHeight = intHeight;
                    int newWidth = intWidth;
                    if (g.Width > newWidth | g.Height > newHeight)
                    {
                        Size imgSize = NewImageSize(g.Width, g.Height, newWidth, newHeight);
                        imgHeight = imgSize.Height;
                        imgWidth = imgSize.Width;
                    }
                    else
                    {
                        imgHeight = g.Height;
                        imgWidth = g.Width;
                    }
                }
                else
                {
                    imgWidth = g.Width;
                    imgHeight = g.Height;
                }

                using (var imgOutput1 = g.Clone(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new Size(imgWidth, imgHeight),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Bicubic,
                })))
                {
                    // This method is explicitly "for DB" storage as JPEG at quality 90, which is
                    // what the original EncoderParameter asked GDI+ for.
                    imgOutput1.Save(newStream, new JpegEncoder { Quality = 90 });
                }
            }

            return newStream;
        }

        /// <summary>return the appropriate encoder for the mime-type of the image being created.</summary>
        /// <param name="myMimeType">mime type (e.g <c>image/jpeg</c>).</param>
        /// <returns>The <see cref="IImageEncoder"/> or <see langword="null"/> when the MIME type is unknown.</returns>
        /// <remarks>
        /// Replaces <c>GetEncoderInfo</c>, which looked a GDI+ <c>ImageCodecInfo</c> up by MIME
        /// type. The lookup is now against ImageSharp's format manager, so it covers exactly the
        /// formats ImageSharp can actually write.
        /// </remarks>
        public static IImageEncoder GetEncoder(string myMimeType)
        {
            try
            {
                var configuration = SixLabors.ImageSharp.Configuration.Default;
                foreach (var format in configuration.ImageFormats)
                {
                    foreach (var mimeType in format.MimeTypes)
                    {
                        if (string.Equals(mimeType, myMimeType, StringComparison.OrdinalIgnoreCase))
                        {
                            return configuration.ImageFormatsManager.GetEncoder(format);
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>scale an image based on existing dimensions and updated requirement.</summary>
        /// <param name="currentWidth">current width.</param>
        /// <param name="currentHeight">current height.</param>
        /// <param name="newWidth">new width.</param>
        /// <param name="newHeight">new height.</param>
        /// <returns>updated calculated height/width dimensions.</returns>
        public static Size NewImageSize(int currentWidth, int currentHeight, int newWidth, int newHeight)
        {
            decimal decScale = ((decimal)currentWidth / (decimal)newWidth) > ((decimal)currentHeight / (decimal)newHeight) ? Convert.ToDecimal((decimal)currentWidth / (decimal)newWidth) : Convert.ToDecimal((decimal)currentHeight / (decimal)newHeight);
            newWidth = Convert.ToInt32(Math.Floor((decimal)currentWidth / decScale));
            newHeight = Convert.ToInt32(Math.Floor((decimal)currentHeight / decScale));

            var newSize = new Size(newWidth, newHeight);

            return newSize;
        }

        /// <summary>Selects the encoder matching a file extension, replacing the <c>ImageFormat</c> switch.</summary>
        private static IImageEncoder GetEncoderFromExtension(string extension)
        {
            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                return new PngEncoder();
            }

            if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                return new GifEncoder();
            }

            if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return new JpegEncoder();
            }

            // The original defaulted to ImageFormat.Bmp for anything else.
            return new BmpEncoder();
        }
    }
}
