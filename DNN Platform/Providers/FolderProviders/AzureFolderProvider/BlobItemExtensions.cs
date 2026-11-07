// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Providers.FolderProviders.AzureFolderProvider
{
    using Amazon.S3.Model;

    /// <summary>Extension methods to resolve the storage-relative path of an S3 object.</summary>
    internal static class BlobItemExtensions
    {
        /// <summary>Gets the path of <paramref name="item"/> relative to the root of its bucket.</summary>
        /// <param name="item">The S3 object.</param>
        /// <returns>The object key, which is already relative to the bucket root.</returns>
        /// <remarks>
        /// The Azure implementation had to strip the container URI off the absolute blob URI to get
        /// at the blob name. An S3 object key is already that relative name, so there is nothing to
        /// strip; this overload keeps the call sites unchanged.
        /// </remarks>
        public static string RelativePath(this S3Object item)
        {
            return item?.Key ?? string.Empty;
        }
    }
}
