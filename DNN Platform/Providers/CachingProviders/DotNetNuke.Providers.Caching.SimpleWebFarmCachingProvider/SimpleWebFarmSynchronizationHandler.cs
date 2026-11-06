// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Providers.Caching.SimpleWebFarmCachingProvider
{
    using System;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Controllers;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Services.Cache;
    using DotNetNuke.Services.Cryptography;
    using DotNetNuke.Services.Log.EventLog;

    using Microsoft.AspNetCore.Http;

    using ICryptographyProvider = DotNetNuke.Abstractions.Security.ICryptographyProvider;

    /// <summary>
    ///     This synchronization handler receives requests from other servers and passes them to the cache system for
    ///     processing.  Error handling is purposefully allowed to bubble up from here to ensure the caller is notified.
    /// </summary>
    /// <remarks>
    ///     This is the ASP.NET Core replacement for the <c>IHttpHandler</c> which was registered in
    ///     <c>web.config</c> for <c>SimpleWebFarmSync.axd</c>. The URL is preserved by
    ///     <see cref="SimpleWebFarmSyncEndpoint"/>, so notifications from servers running the old
    ///     build still resolve.
    /// </remarks>
    public class SimpleWebFarmSynchronizationHandler
    {
        private readonly IHostSettings hostSettings;
        private readonly ICryptographyProvider cryptographyProvider;

        /// <summary>Initializes a new instance of the <see cref="SimpleWebFarmSynchronizationHandler"/> class.</summary>
        public SimpleWebFarmSynchronizationHandler()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="SimpleWebFarmSynchronizationHandler"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="cryptographyProvider">The cryptography provider.</param>
        public SimpleWebFarmSynchronizationHandler(IHostSettings hostSettings, ICryptographyProvider cryptographyProvider)
        {
            this.hostSettings = hostSettings ??
                                new HostSettings(
                                    new HostController(
#pragma warning disable CS0618 // Type or member is obsolete
                                        new EventLogController(),
#pragma warning restore CS0618 // Type or member is obsolete
                                        new Lazy<IPortalController>(() => PortalController.Instance)));
#pragma warning disable CS0618 // Type or member is obsolete
            this.cryptographyProvider = cryptographyProvider ?? CryptographyProvider.Instance() as ICryptographyProvider;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>Processes an incoming cache synchronization request from another web-farm server.</summary>
        /// <param name="context">The request context.</param>
        public void ProcessRequest(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // Validate the request for required inputs, return if no action possible
            if (string.IsNullOrWhiteSpace(context.Request.Query["command"]))
            {
                return; // No command we cannot process
            }

            if (string.IsNullOrWhiteSpace(context.Request.Query["detail"]))
            {
                return; // No action we cannot return
            }

            // Only continue if our provider is current
            if (CachingProvider.Instance() is not SimpleWebFarmCachingProvider provider)
            {
                return;
            }

            var commandValue = context.Request.Query["command"].ToString();
            var detailValue = context.Request.Query["detail"].ToString();

            // Get the values, noting that if in debug we are not encrypted
            var command = this.hostSettings.DebugMode
                ? commandValue
                : UrlUtils.DecryptParameter(this.cryptographyProvider, commandValue, this.hostSettings.Guid);

            var detail = this.hostSettings.DebugMode
                ? detailValue
                : UrlUtils.DecryptParameter(this.cryptographyProvider, detailValue, this.hostSettings.Guid);

            // Pass the action on, if the current caching provider is ours
            provider.ProcessSynchronizationRequest(command, detail);
        }
    }
}
