// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Scheduling
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>
    /// Hosts the DNN scheduler. This is the modern .NET replacement for the dedicated background
    /// <see cref="Thread"/> that <c>Global.asax</c>'s <c>Application_Start</c> used to spin up (and
    /// for the <c>BeginRequest</c> poll used by <see cref="SchedulerMode.REQUEST_METHOD"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Register it with <c>services.AddHostedService&lt;SchedulerHostedService&gt;()</c>. Because a
    /// hosted service is a singleton it takes an <see cref="IServiceScopeFactory"/> rather than any
    /// scoped dependency, and creates a scope per unit of work. (The per-task scope for each
    /// <see cref="SchedulerClient"/> is created by <see cref="ProcessGroup"/>.)
    /// </para>
    /// <para>
    /// This type is a supervisor, not the scheduler itself: the dispatch loop lives in
    /// <c>Scheduler.CoreScheduler.RunSchedulerLoopAsync</c> so that the existing
    /// <see cref="SchedulingProvider"/> / <see cref="DNNScheduler"/> public API keeps working
    /// unchanged. The supervisor ticks every <see cref="SupervisorInterval"/> and (re)starts the
    /// dispatch loop whenever the scheduler should be running but is not — which is also how the
    /// scheduler comes back after <see cref="SchedulingProvider.ReStart"/>.
    /// </para>
    /// <para>
    /// <c>Scheduler.CoreScheduler.KeepRunning</c> is the authoritative "the scheduler should be
    /// running" flag, exactly as it was on .NET Framework. <see cref="SchedulingProvider.Halt"/>
    /// clears it, so a halted scheduler is <em>not</em> restarted by the next supervisor tick.
    /// </para>
    /// </remarks>
    public class SchedulerHostedService : BackgroundService
    {
        /// <summary>How often the supervisor re-checks whether the dispatch loop should be running.</summary>
        private static readonly TimeSpan SupervisorInterval = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<SchedulerHostedService> logger;

        /// <summary>Initializes a new instance of the <see cref="SchedulerHostedService"/> class.</summary>
        /// <param name="scopeFactory">Creates the service scope each supervisor tick runs in.</param>
        /// <param name="logger">The logger.</param>
        public SchedulerHostedService(IServiceScopeFactory scopeFactory, ILogger<SchedulerHostedService> logger)
        {
            this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            // Mirror the .NET Framework IRegisteredObject.Stop(immediate: false) behaviour: ask the
            // dispatch loop to wind down and give in-progress tasks the chance to finish. Halting is
            // also what writes the SCHEDULER_SHUTTING_DOWN event-log entry.
            try
            {
                await Scheduler.CoreScheduler.HaltAsync("Application Shutdown", cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The host's shutdown timeout elapsed while draining; nothing more we can do here.
                this.logger.LogWarning("The DNN scheduler did not drain before the host shutdown timeout elapsed.");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Halting the DNN scheduler during shutdown failed.");
            }

            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(SupervisorInterval);

            // Let OperationCanceledException propagate on shutdown: that is how a BackgroundService
            // reports "I stopped because I was asked to", and swallowing it hides shutdown bugs.
            do
            {
                try
                {
                    await this.RunOneSupervisorTickAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // A failure must not take the hosted service (and with it the host) down;
                    // the next tick retries.
                    this.logger.LogError(ex, "The DNN scheduler host encountered an error; it will retry on the next tick.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }

        /// <summary>Starts (or re-starts) the dispatch loop if the scheduler should be running.</summary>
        /// <param name="stoppingToken">The host's shutdown token.</param>
        /// <returns>A task that completes when the dispatch loop exits, or immediately when the scheduler must not run yet.</returns>
        private async Task RunOneSupervisorTickAsync(CancellationToken stoppingToken)
        {
            // Never run scheduled tasks while the site is being installed or upgraded: the database
            // schema the SchedulerClients depend on may not exist yet.
            var status = Globals.Status;
            if (status != Globals.UpgradeStatus.None)
            {
                this.logger.LogDebug("The DNN scheduler is idle because the application status is {Status}.", status);
                return;
            }

            if (!SchedulingProvider.Enabled)
            {
                return;
            }

            // Halt() clears KeepRunning; honour that rather than immediately restarting.
            if (!Scheduler.CoreScheduler.KeepRunning)
            {
                return;
            }

            var scheduleStatus = Scheduler.CoreScheduler.GetScheduleStatus();
            if (scheduleStatus != ScheduleStatus.NOT_SET &&
                scheduleStatus != ScheduleStatus.STOPPED &&
                scheduleStatus != ScheduleStatus.WAITING_FOR_REQUEST)
            {
                // A dispatch loop is already running.
                return;
            }

            // The scheduling provider is resolved (and its settings read) inside a scope, because
            // this singleton must not capture scoped services.
            using var scope = this.scopeFactory.CreateScope();

            var mode = SchedulingProvider.SchedulerMode;
            Scheduler.CoreScheduler.InitializeThreadPool(SchedulingProvider.Debug, SchedulingProvider.MaxThreads);

            if (mode == SchedulerMode.TIMER_METHOD)
            {
                // Long-running: this awaits until shutdown or until the scheduler is halted.
                Scheduler.CoreScheduler.KeepThreadAlive = true;
                this.logger.LogInformation("Starting the DNN scheduler dispatch loop in TIMER_METHOD mode.");
                await Scheduler.CoreScheduler.RunSchedulerLoopAsync(stoppingToken).ConfigureAwait(false);
                return;
            }

            // REQUEST_METHOD used to be polled from BeginRequest. A Blazor Server app serves very
            // few top-level requests, so polling it from here instead keeps the scheduled tasks
            // actually running; ReadyForPoll/ScheduleLastPolled preserve the original throttle.
            if (!SchedulingProvider.ReadyForPoll)
            {
                return;
            }

            SchedulingProvider.ScheduleLastPolled = DateTime.Now;
            Scheduler.CoreScheduler.KeepThreadAlive = false;
            await Scheduler.CoreScheduler.RunSchedulerLoopAsync(stoppingToken).ConfigureAwait(false);
        }
    }
}
