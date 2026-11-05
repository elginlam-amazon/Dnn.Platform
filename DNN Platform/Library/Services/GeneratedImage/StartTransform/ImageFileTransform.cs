// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage.StartTransform
{
    using System;
    using System.IO;
    using System.Net.Http;

    using SixLabors.ImageSharp;

    /// <summary>Image File ImageTransform class.</summary>
    public class ImageFileTransform : ImageTransform
    {
        /// <summary>
        /// The client used to download <see cref="ImageUrl"/>. Replaces the
        /// <c>HttpWebRequest</c>/<c>HttpWebResponse</c> pair, which is obsolete on modern .NET.
        /// </summary>
        /// <remarks>
        /// A single static instance is used deliberately: it is the pattern that avoids socket
        /// exhaustion, and it mirrors the stateless use the original made of <c>WebRequest</c>.
        /// </remarks>
        private static readonly HttpClient HttpClient = new HttpClient();

        /// <summary>Initializes a new instance of the <see cref="ImageFileTransform"/> class.</summary>
        public ImageFileTransform()
        {
            this.InterpolationMode = InterpolationMode.HighQualityBicubic;
            this.SmoothingMode = SmoothingMode.HighQuality;
            this.PixelOffsetMode = PixelOffsetMode.HighQuality;
            this.CompositingQuality = CompositingQuality.HighQuality;
        }

        /// <summary>Gets provides an Unique String for the image transformation.</summary>
        public override string UniqueString => base.UniqueString + "-" + this.ImageFilePath + this.ImageUrl;

        /// <summary>Gets or sets file path of the image.</summary>
        public string ImageFilePath { get; set; }

        /// <summary>Gets or sets url of the image.</summary>
        public string ImageUrl { get; set; }

        /// <summary>Gets or sets the Image to return if no image or error.</summary>
        public Image EmptyImage { get; set; }

        /// <summary>
        /// Processes an input image applying a file image transformation.
        /// This will return an image after read the stream from the File Path  <see cref="ImageFilePath"/> or Url <see cref="ImageUrl"/>.
        /// </summary>
        /// <param name="image">Input image.</param>
        /// <returns>Image result after file image transformation.</returns>
        public override Image ProcessImage(Image image)
        {
            return !string.IsNullOrEmpty(this.ImageUrl) ?
                this.ProcessImageFromUrl() :
                this.ProcessImageFilePath();
        }

        private Image ProcessImageFilePath()
        {
            try
            {
                using (var stream = new FileStream(this.ImageFilePath, FileMode.Open, FileAccess.Read))
                {
                    return this.CopyImage(stream);
                }
            }
            catch (Exception ex)
            {
                Exceptions.Exceptions.LogException(ex);
                return this.EmptyImage;
            }
        }

        private Image ProcessImageFromUrl()
        {
            try
            {
                using (var response = HttpClient.GetAsync(this.ImageUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                {
                    response.EnsureSuccessStatusCode();
                    using (var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    {
                        return this.CopyImage(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                Exceptions.Exceptions.LogException(ex);
                return this.EmptyImage;
            }
        }
    }
}
