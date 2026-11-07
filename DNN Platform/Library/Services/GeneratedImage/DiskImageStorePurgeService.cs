// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.GeneratedImage
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Periodically deletes stale files from the <see cref="DiskImageStore"/> server-side image
    /// cache.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces the fire-and-forget <c>ThreadPool.QueueUserWorkItem</c> purge that
    /// <see cref="DiskImageStore"/> used to queue from inside an image request. Register it with
    /// <c>services.AddHostedService&lt;DiskImageStorePurgeService&gt;()</c>.
    /// </para>
    /// <para>
    /// The service is a singleton, so it takes an <see cref="IServiceScopeFactory"/> rather than any
    /// scoped dependency, and creates a scope per tick for the work it does.
    /// </para>
    /// </remarks>
    public class DiskImageStorePurgeService : BackgroundService
    {
        /// <summary>The shortest interval the timer is allowed to run at, so a mis-configured <c>PurgeInterval</c> of zero cannot spin.</summary>
        private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<DiskImageStorePurgeService> logger;

        /// <summary>Initializes a new instance of the <see cref="DiskImageStorePurgeService"/> class.</summary>
        /// <param name="scopeFactory">Creates the service scope each purge runs in.</param>
        /// <param name="logger">The logger.</param>
        public DiskImageStorePurgeService(IServiceScopeFactory scopeFactory, ILogger<DiskImageStorePurgeService> logger)
        {
            this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = DiskImageStore.PurgeInterval;
            if (interval < MinimumInterval)
            {
                interval = MinimumInterval;
            }

            using var timer = new PeriodicTimer(interval);

            // Let OperationCanceledException propagate on shutdown: that is how a BackgroundService
            // reports "I stopped because I was asked to", and swallowing it hides shutdown bugs.
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                if (!DiskImageStore.EnableAutoPurge)
                {
                    continue;
                }

                using var scope = this.scopeFactory.CreateScope();
                try
                {
                    await DiskImageStore.PurgeAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // A failed purge must not take the hosted service (and with it the host) down;
                    // the next tick retries.
                    this.logger.LogError(ex, "Purging the generated-image disk cache failed.");
                }
            }
        }
    }
}
