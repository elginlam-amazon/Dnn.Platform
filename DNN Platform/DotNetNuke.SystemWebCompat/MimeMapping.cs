// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web
{
    using Microsoft.AspNetCore.StaticFiles;

    /// <summary>Replacement for the System.Web <c>MimeMapping</c> on .NET 10 (based on the ASP.NET Core content type mappings).</summary>
    public static class MimeMapping
    {
        private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new FileExtensionContentTypeProvider();

        /// <summary>Gets the MIME type of a file name.</summary>
        /// <param name="fileName">The file name.</param>
        /// <returns>The MIME type, <c>application/octet-stream</c> for unknown extensions.</returns>
        public static string GetMimeMapping(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            return ContentTypeProvider.TryGetContentType(fileName, out var contentType) ? contentType : "application/octet-stream";
        }
    }
}
