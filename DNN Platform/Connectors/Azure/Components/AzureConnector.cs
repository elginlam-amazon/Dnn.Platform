// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.AzureConnector.Components
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using Amazon;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.S3.Model;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Collections;
    using DotNetNuke.Common;
    using DotNetNuke.Data;
    using DotNetNuke.Entities.Controllers;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Services.Connections;
    using DotNetNuke.Services.Cryptography;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.FileSystem.Internal;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Log.EventLog;

    using ICryptographyProvider = DotNetNuke.Abstractions.Security.ICryptographyProvider;

    /// <inheritdoc />
    /// <remarks>
    /// This connector now stores through Amazon S3 (AWSSDK.S3) instead of Azure Blob Storage, matching
    /// the <c>AzureFolderProvider</c> it configures. The semantic mapping is:
    /// the <c>Container</c> setting names an S3 bucket, and <c>AccountName</c>/<c>AccountKey</c> are the
    /// AWS access key id and secret access key. The connector name ("Azure"), the display name,
    /// <see cref="Constants.FolderProviderType"/>, every setting key, the icon and plugin folder URLs,
    /// <c>AzureConnector.dnn</c>, <c>connector.htm</c>, <c>connector.css</c> and <c>Scripts/connector.js</c>
    /// are all deliberately unchanged, so existing folder mappings and installed packages keep working.
    /// </remarks>
    public class AzureConnector : IConnector
    {
        private const string DefaultDisplayName = "Azure Storage";
        private static readonly DataProvider DataProvider = DataProvider.Instance();

        private readonly IFolderMappingController folderMappingController;
        private readonly ICryptographyProvider cryptographyProvider;
        private readonly IHostSettings hostSettings;
        private string displayName;

        /// <summary>Initializes a new instance of the <see cref="AzureConnector"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.0. Please use overload with IFolderMappingController. Scheduled removal in v12.0.0.")]
        public AzureConnector()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="AzureConnector"/> class.</summary>
        /// <param name="folderMappingController">The folder mapping controller.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Use overload with ICryptographyProvider. Scheduled for removal in v12.0.0.")]
        public AzureConnector(IFolderMappingController folderMappingController)
            : this(folderMappingController, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="AzureConnector"/> class.</summary>
        /// <param name="folderMappingController">The folder mapping controller.</param>
        /// <param name="cryptographyProvider">The cryptography provider.</param>
        /// <param name="hostSettings">The host settings.</param>
        public AzureConnector(IFolderMappingController folderMappingController, ICryptographyProvider cryptographyProvider, IHostSettings hostSettings)
        {
            this.folderMappingController = folderMappingController ?? FolderMappingController.Instance;
#pragma warning disable CS0618 // Type or member is obsolete
            this.cryptographyProvider = cryptographyProvider ?? (ICryptographyProvider)CryptographyProvider.Instance();
#pragma warning restore CS0618 // Type or member is obsolete
            this.hostSettings = hostSettings ??
                                new HostSettings(
                                    new HostController(
#pragma warning disable CS0618 // Type or member is obsolete
                                        new EventLogController(),
#pragma warning restore CS0618 // Type or member is obsolete
                                        new Lazy<IPortalController>(() => PortalController.Instance)));
        }

        /// <inheritdoc />
        public string Name => "Azure";

        /// <inheritdoc />
        public string IconUrl => "~/DesktopModules/Connectors/Azure/Images/Azure.png";

        /// <inheritdoc />
        public string PluginFolder => "~/DesktopModules/Connectors/Azure/";

        /// <inheritdoc />
        public bool IsEngageConnector => false;

        /// <inheritdoc />
        public ConnectorCategories Type => ConnectorCategories.FileSystem;

        /// <inheritdoc />
        public bool SupportsMultiple => true;

        /// <inheritdoc />
        public string DisplayName
        {
            get => string.IsNullOrEmpty(this.displayName) ? DefaultDisplayName : this.displayName;
            set => this.displayName = value;
        }

        /// <inheritdoc />
        public string Id { get; set; }

        /// <inheritdoc />
        public IEnumerable<IConnector> GetConnectors(int portalId)
        {
            var connectors = this.FindAzureFolderMappings(portalId);
            if (connectors != null && connectors.Count != 0)
            {
                connectors.ForEach(x => { this.Id = x.FolderMappingID.ToString(CultureInfo.InvariantCulture); });
                var finalCon = connectors.Select(x => (IConnector)Activator.CreateInstance(this.GetType())).ToList();
                finalCon.ForEach(x =>
                {
                    x.Id = connectors[finalCon.IndexOf(x)].FolderMappingID.ToString(CultureInfo.InvariantCulture);
                    x.DisplayName = connectors[finalCon.IndexOf(x)].MappingName;
                });
                return finalCon;
            }

            return new List<IConnector> { this };
        }

        /// <inheritdoc />
        public void DeleteConnector(int portalId)
        {
            if (!string.IsNullOrEmpty(this.Id))
            {
                int mappingId;
                if (int.TryParse(this.Id, out mappingId))
                {
                    this.DeleteAzureFolders(portalId, mappingId);
                    this.DeleteAzureFolderMapping(portalId, mappingId);
                }
            }
        }

        /// <inheritdoc />
        public bool HasConfig(int portalId)
        {
            var folderMapping = this.FindAzureFolderMapping(portalId, false, true);
            this.Id = Convert.ToString(folderMapping?.FolderMappingID, CultureInfo.InvariantCulture);
            return this.GetConfig(portalId)["Connected"] == "true";
        }

        /// <inheritdoc />
        public IDictionary<string, string> GetConfig(int portalId)
        {
            var configs = new Dictionary<string, string>();

            var folderMapping = this.FindAzureFolderMapping(portalId, false);

            var settings = folderMapping != null ? folderMapping.FolderMappingSettings : new Hashtable();

            configs.Add("AccountName", this.GetSetting(settings, Constants.AzureAccountName, true));
            configs.Add("AccountKey", this.GetSetting(settings, Constants.AzureAccountKey, true));
            configs.Add("Container", this.GetSetting(settings, Constants.AzureContainerName));
            configs.Add("Connected", !string.IsNullOrEmpty(this.GetSetting(settings, Constants.AzureAccountName)) && !string.IsNullOrEmpty(this.GetSetting(settings, Constants.AzureContainerName)) ? "true" : "false");

            // This setting will improve the UI to set password-type inputs on secure settings
            configs.Add("SecureSettings", "AccountKey");
            configs.Add("Id", Convert.ToString(folderMapping?.FolderMappingID, CultureInfo.InvariantCulture));
            return configs;
        }

        /// <inheritdoc />
        public bool SaveConfig(int portalId, IDictionary<string, string> values, ref bool validated, out string customErrorMessage)
        {
            customErrorMessage = string.Empty;
            var azureAccountName = values[Constants.AzureAccountName];
            var azureAccountKey = values[Constants.AzureAccountKey];
            var azureContainerName = values.TryGetValue(Constants.AzureContainerName, out var containerName) ? containerName : string.Empty;

            var emptyFields = string.IsNullOrEmpty(azureAccountKey) && string.IsNullOrEmpty(azureAccountName);

            validated = true;
            if (emptyFields)
            {
                if (this.SupportsMultiple)
                {
                    throw new AzureConnectorException(Localization.GetString("ErrorRequiredFields", Constants.LocalResourceFile));
                }

                this.DeleteAzureFolderMapping(portalId);
                return true;
            }

            if (!Validation(azureAccountName, azureAccountKey, azureContainerName))
            {
                validated = false;
                return true;
            }

            if (this.FolderMappingNameExists(portalId, this.DisplayName, Convert.ToInt32(!string.IsNullOrEmpty(this.Id) ? this.Id : null, CultureInfo.InvariantCulture)))
            {
                throw new AzureConnectorException(Localization.GetString("ErrorMappingNameExists", Constants.LocalResourceFile));
            }

            try
            {
                var folderMappings = this.FindAzureFolderMappings(portalId);
                FolderMappingInfo folderMapping;
                if (this.SupportsMultiple && !string.IsNullOrEmpty(this.Id))
                {
                    folderMapping = folderMappings.FirstOrDefault(x => x.FolderMappingID.ToString(CultureInfo.InvariantCulture) == this.Id);
                }
                else
                {
                    folderMapping = this.FindAzureFolderMapping(portalId);
                }

                var settings = folderMapping.FolderMappingSettings;

                var savedAccount = this.GetSetting(settings, Constants.AzureAccountName, true);
                var savedKey = this.GetSetting(settings, Constants.AzureAccountKey, true);
                var savedContainer = this.GetSetting(settings, Constants.AzureContainerName);

                var accountChanged = savedAccount != azureAccountName || savedKey != azureAccountKey;

                if (accountChanged)
                {
                    this.DeleteAzureFolderMapping(portalId, folderMapping.FolderMappingID);
                    folderMapping = this.FindAzureFolderMapping(portalId);
                    settings = folderMapping.FolderMappingSettings;
                }
                else if (savedContainer != azureContainerName)
                {
                    this.DeleteAzureFolders(portalId, folderMapping.FolderMappingID);
                }

                var (encryptedAccountName, accountNameAlgorithm, accountNameIv) = FolderProvider.EncryptValue(this.cryptographyProvider, this.hostSettings, azureAccountName);
                settings[Constants.AzureAccountName] = encryptedAccountName;
                settings[FolderProvider.GetAlgorithmSettingKey(Constants.AzureAccountName)] = accountNameAlgorithm;
                settings[FolderProvider.GetInitializationVectorSettingKey(Constants.AzureAccountName)] = accountNameIv;

                var (encryptedAccountKey, accountKeyAlgorithm, accountKeyIv) = FolderProvider.EncryptValue(this.cryptographyProvider, this.hostSettings, azureAccountKey);
                settings[Constants.AzureAccountKey] = encryptedAccountKey;
                settings[FolderProvider.GetAlgorithmSettingKey(Constants.AzureAccountKey)] = accountKeyAlgorithm;
                settings[FolderProvider.GetInitializationVectorSettingKey(Constants.AzureAccountKey)] = accountKeyIv;

                if (values.TryGetValue(Constants.AzureContainerName, out var container) && !string.IsNullOrEmpty(container))
                {
                    settings[Constants.AzureContainerName] = container;
                }
                else
                {
                    folderMapping.FolderMappingSettings[Constants.AzureContainerName] = string.Empty;
                }

                if (!folderMapping.FolderMappingSettings.ContainsKey(Constants.DirectLink))
                {
                    folderMapping.FolderMappingSettings[Constants.DirectLink] = "True";
                }

                if (!folderMapping.FolderMappingSettings.ContainsKey(Constants.UseHttps))
                {
                    folderMapping.FolderMappingSettings[Constants.UseHttps] = "True";
                }

                if (folderMapping.MappingName != this.DisplayName && !string.IsNullOrEmpty(this.DisplayName) &&
                   this.DisplayName != DefaultDisplayName)
                {
                    folderMapping.MappingName = this.DisplayName;
                }

                if (!folderMapping.FolderMappingSettings.ContainsKey(Constants.SyncBatchSize))
                {
                    folderMapping.FolderMappingSettings[Constants.SyncBatchSize] = Constants.DefaultSyncBatchSize.ToString(CultureInfo.InvariantCulture);
                }

                this.folderMappingController.UpdateFolderMapping(folderMapping);

                return true;
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return false;
            }
        }

        /// <summary>Find the folder mapping.</summary>
        /// <param name="folderMappingController">The folder mapping controller.</param>
        /// <param name="portalId">The portal ID.</param>
        /// <param name="folderMappingId">The folder mapping ID.</param>
        /// <param name="autoCreate">Whether to auto-create the folder mapping.</param>
        /// <returns>A <see cref="FolderMappingInfo"/> instance.</returns>
        internal static FolderMappingInfo FindAzureFolderMappingStatic(IFolderMappingController folderMappingController, int portalId, int? folderMappingId = null, bool autoCreate = true)
        {
            var folderMappings = folderMappingController.GetFolderMappings(portalId)
                .Where(f => f.FolderProviderType == Constants.FolderProviderType);

            if (folderMappingId != null)
            {
                return folderMappings.FirstOrDefault(x => x.FolderMappingID == folderMappingId);
            }

            if (!folderMappings.Any() && autoCreate)
            {
                return CreateAzureFolderMappingStatic(folderMappingController, portalId);
            }

            return folderMappings.FirstOrDefault();
        }

        private static FolderMappingInfo CreateAzureFolderMappingStatic(IFolderMappingController folderMappingController, int portalId, string mappingName = "")
        {
            var folderMapping = new FolderMappingInfo
            {
                PortalID = portalId,
                MappingName =
                    string.IsNullOrEmpty(mappingName)
                        ? $"{DefaultDisplayName}_{DateTime.Now.Ticks}"
                        : mappingName,
                FolderProviderType = Constants.FolderProviderType,
            };
            folderMapping.FolderMappingID = folderMappingController.AddFolderMapping(folderMapping);
            return folderMapping;
        }

        /// <summary>Creates the S3 client used to validate the configured credentials.</summary>
        /// <param name="accessKeyId">The AWS access key id (the <c>AccountName</c> setting).</param>
        /// <param name="secretAccessKey">The AWS secret access key (the <c>AccountKey</c> setting).</param>
        /// <returns>A new <see cref="AmazonS3Client"/> which the caller must dispose.</returns>
        /// <remarks>
        /// <c>AzureFolderProvider.CreateS3Client</c> is <c>internal</c> to its own assembly, so the
        /// client is constructed locally here with the same decisions: an explicit region (an S3 client
        /// needs one, otherwise the SDK silently falls back to us-east-1 and requests against buckets in
        /// another region fail with <c>AuthorizationHeaderMalformed</c> or a 301 redirect) and no retries,
        /// which is what the Azure <c>NoRetry</c> policy gave this validation.
        /// </remarks>
        private static AmazonS3Client CreateValidationS3Client(string accessKeyId, string secretAccessKey)
        {
            var config = new AmazonS3Config
            {
                // Preserves the Azure NoRetry policy: validating credentials must fail fast.
                MaxErrorRetry = 0,
            };

            var regionName = Environment.GetEnvironmentVariable("AWS_REGION");
            config.RegionEndpoint = string.IsNullOrEmpty(regionName)
                ? FallbackRegionFactory.GetRegionEndpoint()
                : RegionEndpoint.GetBySystemName(regionName);

            return new AmazonS3Client(new BasicAWSCredentials(accessKeyId, secretAccessKey), config);
        }

        /// <summary>Runs an asynchronous AWS SDK call from the synchronous <see cref="SaveConfig"/> path.</summary>
        /// <typeparam name="TResult">The result of the operation.</typeparam>
        /// <param name="operation">The operation to run.</param>
        /// <returns>The result of <paramref name="operation"/>.</returns>
        /// <remarks>
        /// The <see cref="IConnector"/> contract is synchronous while AWSSDK.S3 exposes asynchronous
        /// operations only. The call is dispatched onto the thread pool so it never continues on - and
        /// therefore cannot deadlock against - the calling thread. Same bridge as
        /// <c>AzureFolderProvider.RunSync</c>.
        /// </remarks>
        private static TResult RunSync<TResult>(Func<Task<TResult>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
        }

        private static bool Validation(string azureAccountName, string azureAccountKey, string azureContainerName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(azureAccountName))
                {
                    throw new ConnectorArgumentException(Localization.GetString(
                        "AccountNameCannotBeEmpty.ErrorMessage",
                        Constants.LocalResourceFile));
                }

                if (string.IsNullOrWhiteSpace(azureAccountKey))
                {
                    throw new ConnectorArgumentException(Localization.GetString(
                        "AccountKeyCannotBeEmpty.ErrorMessage",
                        Constants.LocalResourceFile));
                }

                // The Container setting now names an S3 bucket and AccountName/AccountKey are the AWS
                // access key id / secret access key. MaxErrorRetry = 0 preserves the Azure NoRetry
                // policy this validation used, so bad credentials fail fast instead of being retried.
                using (var s3Client = CreateValidationS3Client(azureAccountName, azureAccountKey))
                {
                    var response = RunSync(() => s3Client.ListBucketsAsync());
                    var buckets = response.Buckets;
                    if (buckets != null && buckets.Count != 0)
                    {
                        if (!string.IsNullOrEmpty(azureContainerName))
                        {
                            if (!buckets.Any(bucket => bucket.BucketName == azureContainerName))
                            {
                                throw new AzureConnectorException(Localization.GetString("ErrorInvalidContainerName", Constants.LocalResourceFile));
                            }
                            else
                            {
                                return true;
                            }
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        throw new AzureConnectorException(Localization.GetString("AccountNotFound.ErrorMessage", Constants.LocalResourceFile));
                    }
                }
            }
            catch (AmazonS3Exception ex)
            {
                // The Azure extended error codes map one-for-one onto S3 error codes so the same
                // localized messages still surface. S3 has no equivalent of
                // RequestInformation.HttpStatusMessage, so the error code and status are used instead.
                if (ex.ErrorCode == "InvalidAccessKeyId" || ex.ErrorCode == "NoSuchBucket")
                {
                    throw new AzureConnectorException(Localization.GetString("AccountNotFound.ErrorMessage", Constants.LocalResourceFile), ex);
                }
                else if (ex.ErrorCode == "AccessDenied")
                {
                    throw new AzureConnectorException(Localization.GetString("AccessDenied.ErrorMessage", Constants.LocalResourceFile), ex);
                }
                else if (ex.ErrorCode == "SignatureDoesNotMatch")
                {
                    throw new AzureConnectorException(Localization.GetString("InvalidAccountKey.ErrorMessage", Constants.LocalResourceFile), ex);
                }

                throw new AzureConnectorException(
                    string.IsNullOrEmpty(ex.ErrorCode)
                        ? ex.Message
                        : string.Format(CultureInfo.CurrentCulture, "{0} ({1})", ex.Message, ex.ErrorCode),
                    ex);
            }
            catch (FormatException ex)
            {
                if (ex.GetType() == typeof(UriFormatException))
                {
                    throw new ConnectorArgumentException(Localization.GetString("InvalidAccountName.ErrorMessage", Constants.LocalResourceFile));
                }

                throw new ConnectorArgumentException(Localization.GetString("InvalidAccountKey.ErrorMessage", Constants.LocalResourceFile));
            }
        }

        private string GetSetting(Hashtable settings, string name, bool encrypt = false)
        {
            if (!settings.ContainsKey(name))
            {
                return string.Empty;
            }

            if (encrypt)
            {
                return FolderProvider.GetEncryptedSetting(this.cryptographyProvider, this.hostSettings, settings, name);
            }

            return settings[name].ToString();
        }

        private void DeleteAzureFolderMapping(int portalId, int folderMappingId)
        {
            this.folderMappingController.DeleteFolderMapping(portalId, folderMappingId);
        }

        private void DeleteAzureFolderMapping(int portalId)
        {
            var folderMapping = this.folderMappingController.GetFolderMappings(portalId)
                .FirstOrDefault(f => f.FolderProviderType == Constants.FolderProviderType);

            if (folderMapping != null)
            {
                this.folderMappingController.DeleteFolderMapping(portalId, folderMapping.FolderMappingID);
            }
        }

        private void DeleteAzureFolders(int portalId, int folderMappingId)
        {
            var folderManager = FolderManager.Instance;
            var folders = folderManager.GetFolders(portalId);

            var folderMappingFolders = folders.Where(f => f.FolderMappingID == folderMappingId);

            if (folderMappingFolders.Any())
            {
                // Delete files in folders with the provided mapping (only in the database)
                foreach (
                    var file in
                    folderMappingFolders.Select<IFolderInfo, IEnumerable<IFileInfo>>(folderManager.GetFiles)
                        .SelectMany(files => files))
                {
                    DataProvider.DeleteFile(portalId, file.FileName, file.FolderId);
                }

                // Remove the folders with the provided mapping that doesn't have child folders with other mapping (only in the database and filesystem)
                var folders1 = folders; // copy the variable to not access a modified closure
                var removableFolders =
                    folders.Where(
                        f => f.FolderMappingID == folderMappingId && !folders1.Any(f2 => f2.FolderID != f.FolderID &&
                                                                                         f2.FolderPath.StartsWith(f.FolderPath, StringComparison.OrdinalIgnoreCase) &&
                                                                                         f2.FolderMappingID != folderMappingId));

                if (removableFolders.Any())
                {
                    foreach (var removableFolder in removableFolders.OrderByDescending(rf => rf.FolderPath))
                    {
                        DirectoryWrapper.Instance.Delete(removableFolder.PhysicalPath, false);
                        DataProvider.DeleteFolder(portalId, removableFolder.FolderPath);
                    }
                }

                // Update the rest of folders with the provided mapping to use the standard mapping
                folders = folderManager.GetFolders(portalId, false); // re-fetch the folders

                folderMappingFolders = folders.Where(f => f.FolderMappingID == folderMappingId);

                if (folderMappingFolders.Any())
                {
                    var defaultFolderMapping = this.folderMappingController.GetDefaultFolderMapping(portalId);

                    foreach (var folderMappingFolder in folderMappingFolders)
                    {
                        folderMappingFolder.FolderMappingID = defaultFolderMapping.FolderMappingID;
                        folderManager.UpdateFolder(folderMappingFolder);
                    }
                }
            }
        }

        private FolderMappingInfo FindAzureFolderMapping(int portalId, bool autoCreate = true, bool checkId = false)
        {
            var folderMappings = this.folderMappingController.GetFolderMappings(portalId)
                .Where(f => f.FolderProviderType == Constants.FolderProviderType).ToList();

            // Create new mapping if none is found.
            if (folderMappings.Count == 0 && autoCreate)
            {
                return this.CreateAzureFolderMapping(portalId, DefaultDisplayName);
            }

            if ((checkId && string.IsNullOrEmpty(this.Id)) || !this.SupportsMultiple)
            {
                return folderMappings.FirstOrDefault();
            }

            var folderMapping = folderMappings.FirstOrDefault(x => x.FolderMappingID.ToString(CultureInfo.InvariantCulture) == this.Id);

            if (folderMapping == null && autoCreate)
            {
                folderMapping = this.CreateAzureFolderMapping(portalId);
            }

            return folderMapping;
        }

        private List<FolderMappingInfo> FindAzureFolderMappings(int portalId)
        {
            return this.folderMappingController.GetFolderMappings(portalId)
                .Where(f => f.FolderProviderType == Constants.FolderProviderType).ToList();
        }

        private bool FolderMappingNameExists(int portalId, string mappingName, int? exceptMappingId)
        {
            return this.folderMappingController.GetFolderMappings(portalId)
                .Any(
                    f =>
                        f.MappingName.Equals(mappingName, StringComparison.OrdinalIgnoreCase) &&
                        (f.FolderMappingID != exceptMappingId));
        }

        private FolderMappingInfo CreateAzureFolderMapping(int portalId, string mappingName = "")
        {
            var folderMapping = CreateAzureFolderMappingStatic(this.folderMappingController, portalId, mappingName);
            this.Id = folderMapping.FolderMappingID.ToString(CultureInfo.InvariantCulture);
            return folderMapping;
        }
    }
}
