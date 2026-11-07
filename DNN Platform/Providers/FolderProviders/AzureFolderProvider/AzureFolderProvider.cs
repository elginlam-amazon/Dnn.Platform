// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Providers.FolderProviders.AzureFolderProvider
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Amazon;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.S3.Model;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Security;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.Providers;
    using DotNetNuke.Providers.FolderProviders.Components;
    using DotNetNuke.Services.Cache;
    using DotNetNuke.Services.FileSystem;

    /// <summary>Amazon S3 Storage Folder Provider.</summary>
    /// <remarks>
    /// This provider stored its objects in Azure Blob Storage through the
    /// <c>WindowsAzure.Storage</c> SDK. It now stores them in Amazon S3 through
    /// <c>AWSSDK.S3</c>: the <c>Container</c> setting names an S3 bucket, the
    /// <c>AccountName</c>/<c>AccountKey</c> settings are the AWS access key id and secret access
    /// key, and a blob name is an object key. The provider name, the setting names and the public
    /// surface are unchanged so existing folder mappings keep working.
    /// </remarks>
    /// <inheritdoc />
    public class AzureFolderProvider : BaseRemoteStorageProvider
    {
        internal const string ProviderName = "AzureFolderProvider";
        internal const string ProviderType = "folder";

        private readonly ProviderConfiguration providerConfiguration = ProviderConfiguration.GetProviderConfiguration(ProviderType);
        private readonly ICryptographyProvider cryptographyProvider;
        private readonly string cacheControl = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="AzureFolderProvider"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public AzureFolderProvider()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="AzureFolderProvider"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="cryptographyProvider">The cryptography provider.</param>
        public AzureFolderProvider(IHostSettings hostSettings, ICryptographyProvider cryptographyProvider)
            : base(hostSettings)
        {
            // The obsolete parameterless overload has nothing injected, so the provider is resolved
            // from the platform's service locator. This replaces the HttpContext-bound lookup.
            this.cryptographyProvider = cryptographyProvider ?? ServiceLocator.GetRequiredService<ICryptographyProvider>();

            // Read the configuration specific information for this provider
            var objProvider = (Provider)this.providerConfiguration.Providers[ProviderName];

            if (!string.IsNullOrEmpty(objProvider.Attributes["cacheControl"]))
            {
                this.cacheControl = objProvider.Attributes["cacheControl"].ToLowerInvariant();
            }
        }

        /// <inheritdoc />
        protected override string FileNotFoundMessage => "Azure File Not Found";

        /// <inheritdoc />
        protected override string ObjectCacheKey => "Azure_Object_{0}_{1}";

        /// <inheritdoc />
        protected override string ListObjectsCacheKey => "Azure_ListObjects_{0}";

        /// <remarks>
        /// Object storage doesn't support folders, so we create a file in order for the folder to not be deleted during future synchronizations.
        /// The file has an extension not allowed by host. This way the file won't be added during synchronizations.
        /// </remarks>
        /// <inheritdoc />
        public override void AddFolder(string folderPath, FolderMappingInfo folderMapping, string mappedPath)
        {
            Requires.NotNull("folderPath", folderPath);
            Requires.NotNull("folderMapping", folderMapping);

            this.UpdateFileInternal(new MemoryStream(), folderMapping, mappedPath + Constants.PlaceHolderFileName);
        }

        /// <inheritdoc />
        public override string GetFileUrl(IFileInfo file)
        {
            Requires.NotNull("file", file);

            var folderMapping = FolderMappingController.Instance.GetFolderMapping(file.PortalId, file.FolderMappingID);
            var directLink = string.IsNullOrEmpty(GetSetting(folderMapping, Constants.DirectLink)) || GetSetting(folderMapping, Constants.DirectLink).ToLowerInvariant() == "true";

            if (directLink)
            {
                var folder = FolderManager.Instance.GetFolder(file.FolderId);
                var key = folder.MappedPath + file.FileName;

                var useHttps = GetBooleanSetting(folderMapping, Constants.UseHttps);

                string absuri;
                using (var s3Client = this.GetS3Client(folderMapping))
                {
                    absuri = GetObjectUrl(s3Client, GetBucketName(folderMapping), key, useHttps);
                }

                var customDomain = GetEncryptedSetting(this.cryptographyProvider, this.HostSettings, folderMapping.FolderMappingSettings, Constants.CustomDomain);

                // Rewriting the host of a pre-signed URL would break it, because an Amazon S3
                // signature version 4 signature covers the Host header (an Azure shared access
                // signature did not). The URL above is deliberately unsigned, which both preserves
                // the custom domain / CDN rewrite and keeps the link permanent, as the Azure
                // 100-year stored access policy was.
                if (!string.IsNullOrEmpty(customDomain))
                {
                    var customUri = new UriBuilder(customDomain).Uri;
                    absuri =
                        new UriBuilder(absuri) { Host = customUri.Host, Scheme = customUri.Scheme, Port = customUri.Port }
                            .Uri.AbsoluteUri;
                }

                return absuri;
            }

            return FileLinkClickController.Instance.GetFileLinkClick(file);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The settings user control of this provider is no longer the deployed file
        /// <c>Settings.ascx</c> but the Blazor component <c>Settings</c>
        /// (<c>AzureFolderProvider/Settings.razor</c>), which is compiled into this assembly. There
        /// is therefore no virtual path to return; the type-based hook
        /// <see cref="GetSettingsControlType"/> is used instead, and the host renders that
        /// component type.
        /// </remarks>
        public override string GetSettingsControlVirtualPath()
        {
            return string.Empty;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Returns the <c>Settings</c> Blazor component compiled into this assembly, which is what
        /// replaced this provider's <c>Settings.ascx</c>. This is the hook the host
        /// (<c>EditFolderMapping</c>) asks for before falling back to
        /// <see cref="GetSettingsControlVirtualPath"/>.
        /// </remarks>
        public override Type GetSettingsControlType()
        {
            return typeof(Settings);
        }

        /// <inheritdoc />
        public override string GetFolderProviderIconPath()
        {
            return Globals.ResolveUrl("~/Providers/FolderProviders/AzureFolderProvider/images/FolderAzure_32x32_Standard.png");
        }

        /// <summary>Gets the names of every bucket the configured credentials can see.</summary>
        /// <param name="folderMapping">The folder mapping holding the credentials.</param>
        /// <returns>The list of bucket names.</returns>
        public List<string> GetAllContainers(FolderMappingInfo folderMapping)
        {
            List<string> containers = [];

            using (var s3Client = this.GetS3Client(folderMapping))
            {
                var response = RunSync(() => s3Client.ListBucketsAsync());
                response.Buckets.ToList().ForEach(x => containers.Add(x.BucketName));
            }

            return containers;
        }

        /// <summary>Updates a file in the Amazon S3 folder provider.</summary>
        /// <remarks>
        /// Object keys are case sensitive. If you update dnninternals.pdf with DNNINTERNALS.pdf, to DNN it's the same
        /// so it just re-uploads it, causing both files to exist in the bucket. This provider specific method deletes the
        /// old existing duplicate that only differs in case as part of the update.
        /// </remarks>
        /// <inheritdoc />
        public override void UpdateFile(IFolderInfo folder, string fileName, Stream content)
        {
            IFileInfo originalFile = FileManager.Instance.GetFile(folder, fileName);

            base.UpdateFile(folder, fileName, content);

            if (originalFile != null && originalFile.FileName != fileName)
            {
                FolderMappingInfo folderMapping = FolderMappingController.Instance.GetFolderMapping(folder.FolderMappingID);
                this.DeleteFileInternal(folderMapping, folder.MappedPath + originalFile.FileName);
            }
        }

        /// <summary>Runs an asynchronous AWS SDK call from one of the synchronous
        /// <see cref="FolderProvider"/> members.</summary>
        /// <typeparam name="TResult">The result of the operation.</typeparam>
        /// <param name="operation">The operation to run.</param>
        /// <returns>The result of <paramref name="operation"/>.</returns>
        /// <remarks>
        /// The <see cref="FolderProvider"/> contract this provider implements is synchronous and is
        /// shared with every other folder provider, while AWSSDK.S3 exposes asynchronous operations
        /// only. The call is dispatched onto the thread pool so it never continues on - and
        /// therefore cannot deadlock against - the calling thread.
        /// </remarks>
        internal static TResult RunSync<TResult>(Func<Task<TResult>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
        }

        /// <summary>Runs an asynchronous AWS SDK call from one of the synchronous
        /// <see cref="FolderProvider"/> members.</summary>
        /// <param name="operation">The operation to run.</param>
        /// <remarks>See the generic overload for why this bridge is needed.</remarks>
        internal static void RunSync(Func<Task> operation)
        {
            Task.Run(operation).GetAwaiter().GetResult();
        }

        /// <summary>Creates an S3 client for the given credentials.</summary>
        /// <param name="accessKeyId">The AWS access key id (the <c>AccountName</c> setting).</param>
        /// <param name="secretAccessKey">The AWS secret access key (the <c>AccountKey</c> setting).</param>
        /// <param name="useHttps">Whether to talk to the service over HTTPS.</param>
        /// <returns>A new <see cref="IAmazonS3"/> which the caller must dispose.</returns>
        internal static IAmazonS3 CreateS3Client(string accessKeyId, string secretAccessKey, bool useHttps)
        {
            var config = new AmazonS3Config
            {
                UseHttp = !useHttps,

                // Replaces ServicePointManager.DefaultConnectionLimit = 100. The Nagle algorithm
                // and 100-continue behaviour the provider used to turn off are already disabled by
                // default on the HttpClient the SDK uses.
                MaxConnectionsPerServer = 100,
            };

            // Unlike an Azure storage account, an S3 client needs its region explicitly: without
            // one the SDK silently falls back to us-east-1 and requests against buckets in another
            // region fail with AuthorizationHeaderMalformed or a 301 redirect. It is read from the
            // AWS_REGION environment variable, falling back to the SDK's own resolution chain
            // (shared config profile, then EC2/ECS/Lambda instance metadata).
            var regionName = Environment.GetEnvironmentVariable("AWS_REGION");
            config.RegionEndpoint = string.IsNullOrEmpty(regionName)
                ? FallbackRegionFactory.GetRegionEndpoint()
                : RegionEndpoint.GetBySystemName(regionName);

            return new AmazonS3Client(new BasicAWSCredentials(accessKeyId, secretAccessKey), config);
        }

        /// <inheritdoc />
        protected override void CopyFileInternal(FolderMappingInfo folderMapping, string sourceUri, string newUri)
        {
            var bucketName = GetBucketName(folderMapping);

            using (var s3Client = this.GetS3Client(folderMapping))
            {
                RunSync(() => s3Client.CopyObjectAsync(new CopyObjectRequest
                {
                    SourceBucket = bucketName,
                    SourceKey = sourceUri,
                    DestinationBucket = bucketName,
                    DestinationKey = newUri,
                }));
            }

            this.ClearCache(folderMapping.FolderMappingID);
        }

        /// <inheritdoc />
        protected override void DeleteFileInternal(FolderMappingInfo folderMapping, string uri)
        {
            using (var s3Client = this.GetS3Client(folderMapping))
            {
                // DeleteObjectAsync is already idempotent on Amazon S3, so there is no
                // existence check to port from DeleteIfExists().
                RunSync(() => s3Client.DeleteObjectAsync(GetBucketName(folderMapping), uri));
            }

            this.ClearCache(folderMapping.FolderMappingID);
        }

        /// <inheritdoc />
        protected override void DeleteFolderInternal(FolderMappingInfo folderMapping, IFolderInfo folder)
        {
            this.DeleteFileInternal(folderMapping, folder.MappedPath + Constants.PlaceHolderFileName);
        }

        /// <inheritdoc />
        protected override Stream GetFileStreamInternal(FolderMappingInfo folderMapping, string uri)
        {
            var memoryStream = new MemoryStream();

            try
            {
                using (var s3Client = this.GetS3Client(folderMapping))
                {
                    RunSync(async () =>
                    {
                        using (var response = await s3Client.GetObjectAsync(GetBucketName(folderMapping), uri).ConfigureAwait(false))
                        using (var responseStream = response.ResponseStream)
                        {
                            await responseStream.CopyToAsync(memoryStream).ConfigureAwait(false);
                        }
                    });
                }
            }
            catch (AmazonS3Exception ex) when (IsNotFound(ex))
            {
                memoryStream.Dispose();
                throw new FileNotFoundException(this.FileNotFoundMessage, uri, ex);
            }
            catch
            {
                memoryStream.Dispose();
                throw;
            }

            memoryStream.Seek(0, SeekOrigin.Begin);

            return memoryStream;
        }

        /// <inheritdoc />
        protected override IList<IRemoteStorageItem> GetObjectList(FolderMappingInfo folderMapping)
        {
            var cacheKey = string.Format(this.ListObjectsCacheKey, folderMapping.FolderMappingID);

            return CBO.GetCachedObject<IList<IRemoteStorageItem>>(
                this.HostSettings,
                new CacheItemArgs(
                    cacheKey,
                    this.ListObjectsCacheTimeout,
                    CacheItemPriority.Default,
                    folderMapping.FolderMappingID),
                _ =>
                {
                    var synchBatchSize = GetIntegerSetting(
                        folderMapping,
                        Constants.SyncBatchSize,
                        Constants.DefaultSyncBatchSize);

                    var list = new List<IRemoteStorageItem>();

                    using (var s3Client = this.GetS3Client(folderMapping))
                    {
                        string continuationToken = null;

                        do
                        {
                            // MaxKeys controls the page size, exactly as the maxResults argument of
                            // the Azure ListBlobsSegmented overload used to.
                            var resultSegment = RunSync(() => s3Client.ListObjectsV2Async(new ListObjectsV2Request
                            {
                                BucketName = GetBucketName(folderMapping),
                                MaxKeys = synchBatchSize,
                                ContinuationToken = continuationToken,
                            }));

                            foreach (var s3Object in resultSegment.S3Objects)
                            {
                                list.Add(new AzureRemoteStorageItem { Blob = new AzureBlob(s3Object), });
                            }

                            // Get the continuation token.
                            continuationToken = resultSegment.IsTruncated ? resultSegment.NextContinuationToken : null;
                        }
                        while (continuationToken != null);
                    }

                    return list;
                });
        }

        /// <inheritdoc />
        protected override void MoveFileInternal(FolderMappingInfo folderMapping, string sourceUri, string newUri)
        {
            var bucketName = GetBucketName(folderMapping);

            using (var s3Client = this.GetS3Client(folderMapping))
            {
                RunSync(async () =>
                {
                    await s3Client.CopyObjectAsync(new CopyObjectRequest
                    {
                        SourceBucket = bucketName,
                        SourceKey = sourceUri,
                        DestinationBucket = bucketName,
                        DestinationKey = newUri,
                    }).ConfigureAwait(false);

                    await s3Client.DeleteObjectAsync(bucketName, sourceUri).ConfigureAwait(false);
                });
            }

            this.ClearCache(folderMapping.FolderMappingID);
        }

        /// <inheritdoc />
        protected override void MoveFolderInternal(FolderMappingInfo folderMapping, string sourceUri, string newUri)
        {
            var bucketName = GetBucketName(folderMapping);

            using (var s3Client = this.GetS3Client(folderMapping))
            {
                RunSync(async () =>
                {
                    // Object storage has no directories; the "folder" is the key prefix, so every
                    // object under it is copied to the new prefix and then deleted.
                    string continuationToken = null;

                    do
                    {
                        var resultSegment = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
                        {
                            BucketName = bucketName,
                            Prefix = sourceUri,
                            ContinuationToken = continuationToken,
                        }).ConfigureAwait(false);

                        foreach (var s3Object in resultSegment.S3Objects)
                        {
                            await s3Client.CopyObjectAsync(new CopyObjectRequest
                            {
                                SourceBucket = bucketName,
                                SourceKey = s3Object.Key,
                                DestinationBucket = bucketName,
                                DestinationKey = string.Concat(newUri, s3Object.Key.AsSpan(sourceUri.Length)),
                            }).ConfigureAwait(false);

                            await s3Client.DeleteObjectAsync(bucketName, s3Object.Key).ConfigureAwait(false);
                        }

                        continuationToken = resultSegment.IsTruncated ? resultSegment.NextContinuationToken : null;
                    }
                    while (continuationToken != null);
                });
            }

            this.ClearCache(folderMapping.FolderMappingID);
        }

        /// <inheritdoc />
        protected override void UpdateFileInternal(Stream stream, FolderMappingInfo folderMapping, string uri)
        {
            stream.Seek(0, SeekOrigin.Begin);

            var request = new PutObjectRequest
            {
                BucketName = GetBucketName(folderMapping),
                Key = uri,
                InputStream = stream,

                // The stream belongs to the caller (DNN's file manager), so the SDK must not
                // dispose it after the upload.
                AutoCloseStream = false,

                // The new SDK sets the object's headers during the upload instead of in a
                // separate SetProperties() round trip.
                ContentType = FileContentTypeManager.Instance.GetContentType(Path.GetExtension(uri)),
            };

            // Set cache control
            if (!string.IsNullOrEmpty(this.cacheControl))
            {
                request.Headers.CacheControl = this.cacheControl;
            }

            using (var s3Client = this.GetS3Client(folderMapping))
            {
                RunSync(() => s3Client.PutObjectAsync(request));
            }

            this.ClearCache(folderMapping.FolderMappingID);
        }

        private static void CheckSettings(FolderMappingInfo folderMapping)
        {
            var settings = folderMapping.FolderMappingSettings;

            if (string.IsNullOrEmpty((string)settings[Constants.AccountName]) ||
                string.IsNullOrEmpty((string)settings[Constants.AccountKey]) ||
                string.IsNullOrEmpty((string)settings[Constants.Container]) ||
                string.IsNullOrEmpty((string)settings[Constants.UseHttps]))
            {
                throw new Exception("Settings cannot be found.");
            }
        }

        /// <summary>Gets the name of the S3 bucket the folder mapping stores its objects in.</summary>
        /// <param name="folderMapping">The folder mapping.</param>
        /// <returns>The bucket name.</returns>
        private static string GetBucketName(FolderMappingInfo folderMapping)
        {
            return GetSetting(folderMapping, Constants.Container);
        }

        /// <summary>Gets the public URL of an object, the equivalent of the Azure <c>blob.Uri</c>.</summary>
        /// <param name="s3Client">The S3 client, which knows the endpoint of the configured region.</param>
        /// <param name="bucketName">The bucket holding the object.</param>
        /// <param name="key">The object key.</param>
        /// <param name="useHttps">Whether the URL should use HTTPS.</param>
        /// <returns>The unsigned URL of the object.</returns>
        /// <remarks>
        /// The URL is taken from a pre-signed request with its query string removed, so the host
        /// and path come from the SDK's own endpoint resolution (region, dual-stack, path style)
        /// instead of a hard-coded <c>s3.amazonaws.com</c> pattern, and nothing is signed.
        /// </remarks>
        private static string GetObjectUrl(IAmazonS3 s3Client, string bucketName, string key, bool useHttps)
        {
            var signedUrl = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(1),
                Protocol = useHttps ? Protocol.HTTPS : Protocol.HTTP,
            });

            return new UriBuilder(signedUrl) { Query = string.Empty }.Uri.AbsoluteUri;
        }

        /// <summary>Determines whether <paramref name="exception"/> reports a missing object or bucket.</summary>
        /// <param name="exception">The exception to inspect.</param>
        /// <returns><see langword="true"/> when the object or its bucket does not exist.</returns>
        private static bool IsNotFound(AmazonS3Exception exception)
        {
            return exception.StatusCode == System.Net.HttpStatusCode.NotFound
                   || exception.ErrorCode == "NoSuchKey"
                   || exception.ErrorCode == "NoSuchBucket";
        }

        /// <summary>Creates an S3 client for the credentials of <paramref name="folderMapping"/>.</summary>
        /// <param name="folderMapping">The folder mapping holding the credentials.</param>
        /// <returns>A new <see cref="IAmazonS3"/> which the caller must dispose.</returns>
        /// <remarks>
        /// Amazon S3 has no per-bucket client, so this replaces both
        /// <c>CloudStorageAccount.CreateCloudBlobClient()</c> and
        /// <c>GetContainerReference(container)</c>; the bucket is passed to each operation instead.
        /// </remarks>
        private IAmazonS3 GetS3Client(FolderMappingInfo folderMapping)
        {
            CheckSettings(folderMapping);

            var accountName = GetEncryptedSetting(this.cryptographyProvider, this.HostSettings, folderMapping.FolderMappingSettings, Constants.AccountName);
            var accountKey = GetEncryptedSetting(this.cryptographyProvider, this.HostSettings, folderMapping.FolderMappingSettings, Constants.AccountKey);
            var useHttps = GetBooleanSetting(folderMapping, Constants.UseHttps);

            return CreateS3Client(accountName, accountKey, useHttps);
        }
    }
}
