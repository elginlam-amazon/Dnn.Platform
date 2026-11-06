// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Providers.FolderProviders.AzureFolderProvider
{
    using System;

    using Amazon.S3.Model;

    /// <summary>A serializable snapshot of a remote storage object.</summary>
    /// <remarks>
    /// This was built from an Azure <c>CloudBlob</c>; it is now built from an Amazon S3
    /// <see cref="S3Object"/>. The public property surface is unchanged so that the cached
    /// <see cref="AzureRemoteStorageItem"/> instances keep their shape.
    /// </remarks>
    [Serializable]
    public class AzureBlob
    {
        private readonly string relativePath = string.Empty;
        private readonly DateTime lastModifiedUtc = DateTime.MinValue;
        private readonly long length = -1;
        private readonly string etag = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="AzureBlob"/> class.</summary>
        /// <param name="blob">The S3 object to snapshot, or <see langword="null"/>.</param>
        public AzureBlob(S3Object blob)
        {
            if (blob == null)
            {
                return;
            }

            this.relativePath = blob.RelativePath();

            // S3Object.LastModified does not reliably carry DateTimeKind.Utc; treating an
            // Unspecified value as local time would shift the timestamp by the server's offset and
            // make BaseRemoteStorageProvider's change detection re-synchronize unchanged files.
            this.lastModifiedUtc = blob.LastModified.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(blob.LastModified, DateTimeKind.Utc)
                : blob.LastModified.ToUniversalTime();
            this.length = blob.Size;
            this.etag = blob.ETag;
        }

        /// <summary>Gets the path of the object relative to the root of its bucket.</summary>
        public string RelativePath => this.relativePath;

        /// <summary>Gets the UTC time the object was last modified.</summary>
        public DateTime LastModifiedUtc => this.lastModifiedUtc;

        /// <summary>Gets the size of the object in bytes.</summary>
        public long Length => this.length;

        /// <summary>Gets the entity tag of the object.</summary>
        public string ETag => this.etag;
    }
}
