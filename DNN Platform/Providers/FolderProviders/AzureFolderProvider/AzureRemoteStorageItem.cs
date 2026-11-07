// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Providers.FolderProviders.AzureFolderProvider
{
    using System;

    using DotNetNuke.Providers.FolderProviders.Components;

    /// <summary>A remote storage item backed by an <see cref="AzureBlob"/>.</summary>
    [Serializable]
    public class AzureRemoteStorageItem : IRemoteStorageItem
    {
        /// <inheritdoc />
        /// <remarks>
        /// The Azure implementation derived the relative path from the blob's percent-encoded
        /// <c>Uri.AbsoluteUri</c> and therefore had to URL-decode it (pre-escaping "+" so a literal
        /// plus survived). An Amazon S3 object key is returned already decoded, so decoding it again
        /// would corrupt any key containing a literal "%" or "+" and make synchronization treat the
        /// remote file as missing.
        /// </remarks>
        public string Key => this.Blob.RelativePath;

        /// <inheritdoc />
        public DateTime LastModified => this.Blob.LastModifiedUtc;

        /// <inheritdoc />
        public long Size => this.Blob.Length;

        /// <inheritdoc />
        public string HashCode => this.Blob.ETag;

        public AzureBlob Blob { get; set; }
    }
}
