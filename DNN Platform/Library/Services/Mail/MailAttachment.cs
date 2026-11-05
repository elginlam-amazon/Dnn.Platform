// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.Mail
{
    using System.IO;

    using Microsoft.AspNetCore.StaticFiles;

    public class MailAttachment
    {
        private const string DefaultContentType = "application/octet-stream";

        /// <summary>Replaces <c>System.Web.MimeMapping</c>, which does not exist on modern .NET.</summary>
        private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new FileExtensionContentTypeProvider();

        /// <summary>Initializes a new instance of the <see cref="MailAttachment"/> class.</summary>
        /// <param name="filePath">The path to the file to attach.</param>
        public MailAttachment(string filePath)
        {
            var content = File.ReadAllBytes(filePath);
            var filename = Path.GetFileName(filePath);
            var contentType = GetMimeMapping(filename);

            this.MailAttachmentInternal(filename, content, contentType);
        }

        /// <summary>Initializes a new instance of the <see cref="MailAttachment"/> class.</summary>
        /// <param name="filename">The name of the attachment.</param>
        /// <param name="content">The contents of the attachment.</param>
        public MailAttachment(string filename, byte[] content)
        {
            this.MailAttachmentInternal(filename, content, GetMimeMapping(filename));
        }

        /// <summary>Initializes a new instance of the <see cref="MailAttachment"/> class.</summary>
        /// <param name="filename">The name of the attachment.</param>
        /// <param name="content">The contents of the attachment.</param>
        /// <param name="contentType">The MIME content type of the attachment.</param>
        public MailAttachment(string filename, byte[] content, string contentType)
        {
            this.MailAttachmentInternal(filename, content, contentType);
        }

        public byte[] Content { get; set; }

        public string Filename { get; set; }

        public string ContentType { get; set; }

        private static string GetMimeMapping(string filename)
        {
            return ContentTypeProvider.TryGetContentType(filename, out var contentType) ? contentType : DefaultContentType;
        }

        private void MailAttachmentInternal(string filename, byte[] content, string contentType)
        {
            this.Filename = filename;
            this.Content = content;
            this.ContentType = contentType;
        }
    }
}
