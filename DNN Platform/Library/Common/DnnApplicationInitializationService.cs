// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Runs the DNN application-start sequence once, and the application-end sequence on shutdown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces <c>Global.asax</c>'s <c>Application_Start</c> / <c>Application_End</c>, which
    /// do not exist on ASP.NET Core. <see cref="Initialize.InitializeApplicationAsync"/> carries the
    /// whole legacy start sequence (version check, cache priming, portal initialization, scheduler
    /// start, <c>LogStart</c>); <see cref="Initialize.StopScheduler"/> and
    /// <see cref="Initialize.LogEnd()"/> carry <c>Application_End</c>.
    /// </para>
    /// <para>
    /// It is registered as the FIRST hosted service so that the start sequence has run before the
    /// scheduler supervisor (<c>SchedulerHostedService</c>) and the image-store purge service tick:
    /// <see cref="IHostedService"/> instances are started in registration order.
    /// </para>
    /// </remarks>
    public sealed class DnnApplicationInitializationService : IHostedService
    {
        private readonly IHostApplicationLifetime lifetime;
        private readonly ILogger<DnnApplicationInitializationService> logger;
        private IDisposable stoppingRegistration;

        /// <summary>Initializes a new instance of the <see cref="DnnApplicationInitializationService"/> class.</summary>
        /// <param name="lifetime">The host application lifetime, used to hook application shutdown.</param>
        /// <param name="logger">The logger.</param>
        public DnnApplicationInitializationService(IHostApplicationLifetime lifetime, ILogger<DnnApplicationInitializationService> logger)
        {
            this.lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            this.stoppingRegistration = this.lifetime.ApplicationStopping.Register(this.OnApplicationStopping);

            // Initialize.InitializeApplicationAsync is idempotent (it guards on the
            // ApplicationStatus/initialization flag) but is only ever called from here.
            await Initialize.InitializeApplicationAsync().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            this.stoppingRegistration?.Dispose();
            this.stoppingRegistration = null;
            return Task.CompletedTask;
        }

        private void OnApplicationStopping()
        {
            // Application_End parity: halt the scheduler and write the SHUTDOWN event-log entry.
            // Exceptions are logged rather than thrown: throwing from a shutdown callback would
            // abort the remaining shutdown work (including the scheduler's own drain).
            try
            {
                Initialize.StopScheduler();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Stopping the DNN scheduler during application shutdown failed.");
            }

            try
            {
                Initialize.LogEnd();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Writing the DNN application-end log entry failed.");
            }
        }
    }
}
