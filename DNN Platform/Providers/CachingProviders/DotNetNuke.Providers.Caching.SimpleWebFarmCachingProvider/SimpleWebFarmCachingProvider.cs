// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Providers.Caching.SimpleWebFarmCachingProvider
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Controllers;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Framework;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Cache;
    using DotNetNuke.Services.Cryptography;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Log.EventLog;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using ICryptographyProvider = DotNetNuke.Abstractions.Security.ICryptographyProvider;

    /// <inheritdoc />
    public class SimpleWebFarmCachingProvider : CachingProvider
    {
        /// <summary>
        /// The name of the <see cref="HttpClient"/> registration (see <see cref="IHttpClientFactory"/>)
        /// used to notify the other web-farm servers. It is registered by <see cref="Startup"/>.
        /// </summary>
        public const string SyncHttpClientName = "DnnSimpleWebFarmCacheSync";

        private static readonly ILogger Logger = DnnLoggingController.GetLogger<SimpleWebFarmCachingProvider>();
        private readonly IHostSettingsService hostSettingsService;
        private readonly ICryptographyProvider cryptographyProvider;
        private readonly IApplicationStatusInfo appStatus;

        private readonly int executionTimeout = 5000; // Limit timeout to 5 seconds as cache operations should be quick

        /// <summary>Initializes a new instance of the <see cref="SimpleWebFarmCachingProvider"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public SimpleWebFarmCachingProvider()
            : this(null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="SimpleWebFarmCachingProvider"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo. Scheduled removal in v12.0.0.")]
        public SimpleWebFarmCachingProvider(IHostSettings hostSettings)
            : this(hostSettings, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="SimpleWebFarmCachingProvider"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="cryptographyProvider">The cryptography provider.</param>
        public SimpleWebFarmCachingProvider(IHostSettings hostSettings, IApplicationStatusInfo appStatus, IHostSettingsService hostSettingsService, ICryptographyProvider cryptographyProvider)
            : base(hostSettings)
        {
            this.appStatus = appStatus ?? new ApplicationStatusInfo(new Application());
            this.hostSettingsService = hostSettingsService ??
                                       new HostController(
#pragma warning disable CS0618 // Type or member is obsolete
                                           new EventLogController(),
#pragma warning restore CS0618 // Type or member is obsolete
                                           new Lazy<IPortalController>(() => PortalController.Instance));
#pragma warning disable CS0618 // Type or member is obsolete
            this.cryptographyProvider = cryptographyProvider ?? CryptographyProvider.Instance() as ICryptographyProvider;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <inheritdoc />
        public override void Clear(string type, string data)
        {
            // Clear the local cache
            this.ClearCacheInternal(type, data, true);

            // Per API implementation standards only notify others if expiration has not been disabled
            if (CacheExpirationDisable)
            {
                return;
            }

            // Notify other servers
            this.NotifyOtherServers("Clear~" + type, data);
        }

        /// <inheritdoc />
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", Justification = "Breaking change")]
        public override void Remove(string key)
        {
            // Remove from local cache
            this.RemoveInternal(key);

            // Per API implementation standards only notify others if expiration has not been disabled
            if (CacheExpirationDisable)
            {
                return;
            }

            // Notify Other Servers
            this.NotifyOtherServers("Remove", key);
        }

        /// <summary>This method responds to an incoming request to process synchronization from an additional server.</summary>
        /// <remarks>This is internal as it should only be called from <see cref="SimpleWebFarmSynchronizationHandler"/>.</remarks>
        /// <param name="command">The command to process, currently supported Remove and Clear~{Type}.</param>
        /// <param name="detail">Additional detail to pass to the caching sub-system.</param>
        internal void ProcessSynchronizationRequest(string command, string detail)
        {
            // Handle basic removal
            if (command.StartsWith("remove", StringComparison.OrdinalIgnoreCase))
            {
                this.RemoveInternal(detail);
                return;
            }

            // A clear method will have additional type information included, split using the ~ character
            if (command.StartsWith("clear~", StringComparison.InvariantCultureIgnoreCase))
            {
                var commandParts = command.Split('~');
                this.ClearCacheInternal(commandParts[1], detail, true);
            }
        }

        private static void LogNotificationFailure(string url, HttpStatusCode statusCode)
        {
            Exceptions.LogException(new SyncException(
                $"Error sending cache server notification.  Url: {url} with a status code {statusCode}"));
        }

        private void NotifyOtherServers(string command, string detail)
        {
            // Do not send notifications to other servers if currently upgrading
            if (this.appStatus.Status != UpgradeStatus.None)
            {
                return;
            }

            // Get all servers currently in the database that could be used for synchronization, excluding this one
            // But focus on only servers that could be used for this application and notifications
            // including activity within 60 minutes
            var lastActivityDate = DateTime.Now.AddHours(-1);
            var additionalServers = ServerController.GetEnabledServers()
                .Where(s => !string.IsNullOrWhiteSpace(s.Url)
                            && s.LastActivityDate >= lastActivityDate
                            && s.ServerName != Globals.ServerName)
                .ToList();

            // If we have no additional servers do nothing
            if (additionalServers.Count == 0)
            {
                return;
            }

            // The named client carries the default credentials and the cookie container that the
            // HttpWebRequest used to set per request (see Startup).
            var httpClientFactory = ServiceLocator.ServiceProvider?.GetService<IHttpClientFactory>();
            if (httpClientFactory is null)
            {
                // No container yet (the application is still starting): there is nothing to notify with.
                Logger.DependencyInjectionNotInitialized();
                return;
            }

            // Otherwise notify each server
            foreach (var server in additionalServers)
            {
                // Setup parameters for sending
                var commandParameter = this.HostSettings.DebugMode ? command : UrlUtils.EncryptParameter(this.cryptographyProvider, command, this.HostSettings.Guid);
                var detailParameter = this.HostSettings.DebugMode ? detail : UrlUtils.EncryptParameter(this.cryptographyProvider, detail, this.HostSettings.Guid);
                var protocol = this.hostSettingsService.GetBoolean("UseSSLForCacheSync", false) ? "https://" : "http://";
                var notificationUrl =
                    $"{protocol}{server.Url}/SimpleWebFarmSync.axd?command={commandParameter}&detail={detailParameter}";

                // Start the request without waiting for it: cache operations must not block on the
                // other servers (this is what BeginGetResponse did). Every failure is logged inside
                // SendNotificationAsync, so nothing is thrown back to the caller.
                _ = this.SendNotificationAsync(httpClientFactory, notificationUrl);
            }
        }

        private async Task SendNotificationAsync(IHttpClientFactory httpClientFactory, string url)
        {
            // Replaces ThreadPool.RegisterWaitForSingleObject + HttpWebRequest.Abort: the same
            // 5 second budget, enforced by cancellation.
            using var timeout = new CancellationTokenSource(this.executionTimeout);
            try
            {
                var client = httpClientFactory.CreateClient(SyncHttpClientName);
                using var response = await client.GetAsync(url, timeout.Token).ConfigureAwait(false);

                // If status code is ok do nothing
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }

                // Otherwise log the failure
                LogNotificationFailure(url, response.StatusCode);
            }
            catch (OperationCanceledException)
            {
                // The request was cancelled (timed out). This is the WebExceptionStatus.RequestCanceled
                // case, which was deliberately not logged.
                Logger.CacheServerNotificationTimedOut(url);
            }
            catch (HttpRequestException e)
            {
                Exceptions.LogException(new SyncException("Synchronization Error in Request: " + url, e));
            }
        }
    }
}
