// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Scheduling
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Collections.Internal;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Log.EventLog;

    using Microsoft.Extensions.Logging;
    using Microsoft.VisualBasic;

    /// <summary>The scheduler.</summary>
    internal static partial class Scheduler
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(Scheduler));

        /// <summary>The scheduler implementation.</summary>
        internal static partial class CoreScheduler
        {
            // If KeepRunning gets switched to false,
            // the scheduler stops running.
            [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
            [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
            public static bool KeepThreadAlive = true;

            [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
            [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
            public static bool KeepRunning = true;

            private static readonly SharedList<ScheduleItem> ScheduleQueue;
            private static readonly SharedList<ScheduleHistoryItem> ScheduleInProgress;
            private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(45);
            private static readonly ReaderWriterLockSlim StatusLock = new ReaderWriterLockSlim();
            private static readonly char[] ServerSeparator = [',',];

            /// <summary>
            /// Guards <see cref="FireEventsAsync"/>. Replaces the <c>lock (ScheduleQueue)</c> the
            /// .NET Framework implementation used, because a <c>lock</c> cannot be held across an
            /// <c>await</c> and the dispatch loop now awaits instead of calling <c>Thread.Sleep</c>.
            /// </summary>
            private static readonly SemaphoreSlim FireEventsLock = new SemaphoreSlim(1, 1);

            /// <summary>
            /// 1 while a scheduler dispatch loop (<see cref="RunSchedulerLoopAsync"/>) is active, 0
            /// otherwise. The loop is normally owned by <see cref="SchedulerHostedService"/>; this
            /// flag keeps a later call to <see cref="Start"/> from starting a second, competing loop.
            /// </summary>
            private static int loopActive;

            // This is the heart of the scheduler mechanism.
            // This class manages running new events according
            // to the schedule.
            //
            // This class can also react to the three
            // scheduler events (Started, Progressing and Completed)
            private static bool threadPoolInitialized;

            // The MaxThreadCount establishes the maximum
            // threads you want running simultaneously
            // for spawning SchedulerClient processes
            private static int maxThreadCount;
            private static int activeThreadCount;

            // If KeepRunning gets switched to false,
            // the scheduler stops running.
            private static bool forceReloadSchedule;
            private static bool debug;

            private static int numberOfProcessGroups;

            // This is our array that holds the process group
            // where our threads will be kicked off.
            private static ProcessGroup[] processGroup;

            // A ReaderWriterLockSlim will protect our objects
            // in memory from being corrupted by simultaneous
            // thread operations.  This block of code below
            // establishes variables to help keep track
            // of the ReaderWriter locks.
            private static int readerTimeouts;
            private static int writerTimeouts;
            private static ScheduleStatus status = ScheduleStatus.STOPPED;

            static CoreScheduler()
            {
                var lockStrategy = new ReaderWriterLockStrategy(LockRecursionPolicy.SupportsRecursion);

                ScheduleQueue = new SharedList<ScheduleItem>(lockStrategy);
                ScheduleInProgress = new SharedList<ScheduleHistoryItem>(lockStrategy);
            }

            /// <summary>Gets tracks how many threads we have free to work with at any given time.</summary>
            public static int FreeThreads
            {
                get
                {
                    return maxThreadCount - activeThreadCount;
                }
            }

            public static ScheduleHistoryItem AddScheduleHistory(ScheduleHistoryItem scheduleHistoryItem)
            {
                try
                {
                    int scheduleHistoryID = SchedulingController.AddScheduleHistory(scheduleHistoryItem);
                    scheduleHistoryItem.ScheduleHistoryID = scheduleHistoryID;
                }
                catch (Exception exc)
                {
                    Exceptions.Exceptions.ProcessSchedulerException(exc);
                }

                return scheduleHistoryItem;
            }

            /// <summary>
            /// Checks the scheduled item if it has servers specified to run on. If it does, then it checks to see if the server is active.
            /// </summary>
            /// <param name="scheduleItem">The schedule to check.</param>
            /// <returns>True if nothing happens to the scheduled item. Returns false if the scheduled item was updated.</returns>
            /// <remarks>
            /// If the server specified is not active then the scheduled item will be assigned to the most recent active server.
            /// This assumes that all schedules will run on a single server if none of the servers specified are active.
            /// </remarks>
            public static bool ValidateServersAreActiveForScheduledItem(ScheduleItem scheduleItem)
            {
                // If this scheduled items runs on all servers, continue.
                if (string.IsNullOrEmpty(scheduleItem.Servers))
                {
                    return true;
                }

                // Get the individual server names from the scheduled item
                var servers = scheduleItem.Servers.ToLowerInvariant().Split(ServerSeparator, StringSplitOptions.RemoveEmptyEntries);

                // Get the enabled servers with recent activity.
                var enabledServers = ServerController.GetEnabledServersWithActivity();

                // Validate that any server in the scheduled item is in the enabled server list with activity in the last 10 minutes.
                bool serverHasRecentActivity = enabledServers.Any(i => servers.Contains(i.ServerName.ToLowerInvariant()));

                // If no servers in the scheduled item had recent activity, assign this scheduled item to the server with the most recent activity
                if (!serverHasRecentActivity)
                {
                    // Assign a new server to this task then run it
                    scheduleItem.Servers = $",{enabledServers.First().ServerName.ToLowerInvariant()},";

                    // Save the scheduled item back to the database.
                    SchedulingController.UpdateSchedule(scheduleItem);
                    return false;
                }

                return true;
            }

            /// <summary>Adds an item to the collection of schedule items in queue.</summary>
            /// <param name="scheduleHistoryItem">The schedule history item.</param>
            /// <remarks>Thread Safe.</remarks>
            public static void AddToScheduleQueue(ScheduleHistoryItem scheduleHistoryItem)
            {
                if (!ScheduleQueueContains(scheduleHistoryItem))
                {
                    try
                    {
                        ////objQueueReadWriteLock.EnterWriteLock(WriteTimeout)
                        using (ScheduleQueue.GetWriteLock(LockTimeout))
                        {
                            // Do a second check just in case
                            if (!ScheduleQueueContains(scheduleHistoryItem) &&
                                !IsInProgress(scheduleHistoryItem) &&
                                ValidateServersAreActiveForScheduledItem(scheduleHistoryItem))
                            {
                                // It is safe for this thread to read or write
                                // from the shared resource.
                                ScheduleQueue.Add(scheduleHistoryItem);
                            }
                        }
                    }
                    catch (ApplicationException ex)
                    {
                        // The writer lock request timed out.
                        Interlocked.Increment(ref writerTimeouts);
                        Exceptions.Exceptions.LogException(ex);
                    }
                }
            }

            /// <summary>Dispatches every due item in the schedule queue to a process group.</summary>
            /// <remarks>
            /// Synchronous entry point kept for the (internal) call sites that are not themselves
            /// async, e.g. <see cref="RunEventSchedule"/>'s synchronous wrapper. Prefer
            /// <see cref="FireEventsAsync"/>.
            /// </remarks>
            public static void FireEvents()
            {
                FireEventsAsync(CancellationToken.None).GetAwaiter().GetResult();
            }

            /// <summary>Dispatches every due item in the schedule queue to a process group.</summary>
            /// <param name="cancellationToken">Cancelled when the host is shutting down.</param>
            /// <returns>A task that completes once the whole queue snapshot has been considered.</returns>
            public static async Task FireEventsAsync(CancellationToken cancellationToken)
            {
                // This method uses the thread pool (via ProcessGroup) to
                // call the SchedulerClient methods that need
                // to be called.

                // For each item in the queue that there
                // is an open thread for, set the object
                // in the array to a new ProcessGroup object.
                // Pass in the ScheduleItem to the ProcessGroup
                // so the ProcessGroup can pass it around for
                // logging and notifications.
                await FireEventsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var scheduleList = new List<ScheduleItem>();
                    using (ScheduleQueue.GetReadLock(LockTimeout))
                    {
                        foreach (ScheduleItem scheduleItem in ScheduleQueue)
                        {
                            scheduleList.Add(scheduleItem);
                        }
                    }

                    int numToRun = scheduleList.Count;
                    int numRun = 0;

                    foreach (ScheduleItem scheduleItem in scheduleList)
                    {
                        if (!KeepRunning || cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        int processGroup = GetProcessGroup();

                        if (scheduleItem.NextStart <= DateTime.Now &&
                            scheduleItem.Enabled &&
                            !IsInProgress(scheduleItem) &&
                            !HasDependenciesConflict(scheduleItem) &&
                            numRun < numToRun)
                        {
                            scheduleItem.ProcessGroup = processGroup;
                            if (scheduleItem.ScheduleSource == ScheduleSource.NOT_SET)
                            {
                                if (SchedulingProvider.SchedulerMode == SchedulerMode.TIMER_METHOD)
                                {
                                    scheduleItem.ScheduleSource = ScheduleSource.STARTED_FROM_TIMER;
                                }
                                else if (SchedulingProvider.SchedulerMode == SchedulerMode.REQUEST_METHOD)
                                {
                                    scheduleItem.ScheduleSource = ScheduleSource.STARTED_FROM_BEGIN_REQUEST;
                                }
                            }

                            var scheduleHistoryItem = new ScheduleHistoryItem(scheduleItem);
                            scheduleHistoryItem.StartDate = DateTime.Now;

                            // The .NET Framework implementation used Delegate.BeginInvoke here, which
                            // modern .NET does not support. Task.Run reproduces the same semantics:
                            // the "in progress" bookkeeping happens on a thread-pool thread while
                            // this method goes on to queue the work item one second later.
                            _ = Task.Run(() => AddToScheduleInProgress(scheduleHistoryItem), CancellationToken.None);
                            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);

                            CoreScheduler.processGroup[processGroup].AddQueueUserWorkItem(scheduleItem);

                            LogEventAddedToProcessGroup(scheduleItem);
                            numRun += 1;
                        }
                        else
                        {
                            LogWhyTaskNotRun(scheduleItem);
                        }
                    }
                }
                finally
                {
                    FireEventsLock.Release();
                }
            }

            public static int GetActiveThreadCount()
            {
                return activeThreadCount;
            }

            public static int GetFreeThreadCount()
            {
                return FreeThreads;
            }

            public static int GetMaxThreadCount()
            {
                return maxThreadCount;
            }

            /// <summary>Gets a copy of the collection of schedule items in progress.</summary>
            /// <returns>Copy of the schedule items currently in progress.</returns>
            /// <remarks>This is a snapshot of the collection scheduled items could start or complete at any time.</remarks>
            public static Collection GetScheduleInProgress()
            {
                var c = new Collection();
                try
                {
                    using (ScheduleInProgress.GetReadLock(LockTimeout))
                    {
                        foreach (ScheduleHistoryItem item in ScheduleInProgress)
                        {
                            c.Add(item, item.ScheduleID.ToString(CultureInfo.InvariantCulture), null, null);
                        }
                    }
                }
                catch (ApplicationException ex)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                    Exceptions.Exceptions.LogException(ex);
                }

                return c;
            }

            /// <summary>Gets the number of items in the collection of schedule items in progress.</summary>
            /// <returns>Number of items in progress.</returns>
            /// <remarks>Thread Safe
            /// This count is a snapshot and may change at any time.
            /// </remarks>
            public static int GetScheduleInProgressCount()
            {
                try
                {
                    using (ScheduleInProgress.GetReadLock(LockTimeout))
                    {
                        return ScheduleInProgress.Count;
                    }
                }
                catch (ApplicationException ex)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                    Logger.SchedulerReaderLockRequestTimeout(ex);

                    return 0;
                }
            }

            /// <summary>Gets a copy of collection of all schedule items in queue.</summary>
            /// <returns>A copy of the ScheduleQueue.</returns>
            /// <remarks>Thread Safe
            /// The returned collection is a snapshot in time the real ScheduleQueue may change at any time.
            /// </remarks>
            public static Collection GetScheduleQueue()
            {
                var c = new Collection();
                try
                {
                    using (ScheduleQueue.GetReadLock(LockTimeout))
                    {
                        foreach (ScheduleItem item in ScheduleQueue)
                        {
                            c.Add(item, item.ScheduleID.ToString(CultureInfo.InvariantCulture), null, null);
                        }
                    }

                    return c;
                }
                catch (ApplicationException ex)
                {
                    Interlocked.Increment(ref readerTimeouts);
                    Exceptions.Exceptions.LogException(ex);
                }

                return c;
            }

            /// <summary>Gets the number of items in the collection of schedule items in progress.</summary>
            /// <returns>Number of items in progress.</returns>
            /// <remarks>Thread Safe
            /// This count is a snapshot and may change at any time.
            /// </remarks>
            public static int GetScheduleQueueCount()
            {
                try
                {
                    using (ScheduleQueue.GetReadLock(LockTimeout))
                    {
                        return ScheduleQueue.Count;
                    }
                }
                catch (ApplicationException)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                    return 0;
                }
            }

            public static ScheduleStatus GetScheduleStatus()
            {
                try
                {
                    if (StatusLock.TryEnterReadLock(LockTimeout))
                    {
                        try
                        {
                            // ScheduleStatus is a value type a copy is returned (enumeration)
                            return status;
                        }
                        finally
                        {
                            StatusLock.ExitReadLock();
                        }
                    }
                }
                catch (ApplicationException)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                }

                return ScheduleStatus.NOT_SET;
            }

            /// <summary>Halt the Scheduler.</summary>
            /// <param name="sourceOfHalt">Initiator of Halt.</param>
            public static void Halt(string sourceOfHalt)
            {
                HaltAsync(sourceOfHalt, CancellationToken.None).GetAwaiter().GetResult();
            }

            /// <summary>Halt the Scheduler, waiting for the dispatch loop to drain.</summary>
            /// <param name="sourceOfHalt">Initiator of Halt.</param>
            /// <param name="cancellationToken">Cancelled when the host is shutting down.</param>
            /// <returns>A task that completes once the scheduler reports <see cref="ScheduleStatus.STOPPED"/> or the 120 second grace period elapses.</returns>
            public static async Task HaltAsync(string sourceOfHalt, CancellationToken cancellationToken)
            {
                // should do nothing if the scheduler havn't start yet.
                var currentStatus = GetScheduleStatus();
                if (currentStatus == ScheduleStatus.NOT_SET || currentStatus == ScheduleStatus.STOPPED)
                {
                    return;
                }

                SetScheduleStatus(ScheduleStatus.SHUTTING_DOWN);
                var log = new LogInfo { LogTypeKey = "SCHEDULER_SHUTTING_DOWN" };
                log.AddProperty("Initiator", sourceOfHalt);
                LogController.Instance.AddLog(log);

                KeepRunning = false;

                // wait for up to 120 seconds for the dispatch loop
                // to shut down
                for (int i = 0; i <= 120; i++)
                {
                    if (GetScheduleStatus() == ScheduleStatus.STOPPED)
                    {
                        return;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }

                activeThreadCount = 0;
            }

            public static bool HasDependenciesConflict(ScheduleItem scheduleItem)
            {
                try
                {
                    using (ScheduleInProgress.GetReadLock(LockTimeout))
                    {
                        if (scheduleItem.ObjectDependencies.Length != 0)
                        {
                            foreach (ScheduleHistoryItem item in ScheduleInProgress.Where(si => si.ObjectDependencies.Length != 0))
                            {
                                if (item.HasObjectDependencies(scheduleItem.ObjectDependencies))
                                {
                                    return true;
                                }
                            }
                        }
                    }

                    return false;
                }
                catch (ApplicationException ex)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                    Logger.SchedulerReaderLockRequestTimeout(ex);

                    return false;
                }
            }

            public static void LoadQueueFromEvent(EventName eventName)
            {
                var executingServer = ServerController.GetExecutingServerName();
                List<ScheduleItem> schedule = SchedulingController.GetScheduleByEvent(eventName.ToString(), executingServer);
                Logger.SchedulerLoadQueue(executingServer);

                var thisServer = GetServer(executingServer);
                if (thisServer == null)
                {
                    return;
                }

                bool runningInAGroup = !string.IsNullOrEmpty(thisServer.ServerGroup);

                var serverGroupServers = ServerGroupServers(thisServer);

                foreach (ScheduleItem scheduleItem in schedule)
                {
                    if (runningInAGroup && string.IsNullOrEmpty(scheduleItem.Servers))
                    {
                        scheduleItem.Servers = serverGroupServers;
                    }

                    var historyItem = new ScheduleHistoryItem(scheduleItem);

                    if (!IsInQueue(historyItem) &&
                        !IsInProgress(historyItem) &&
                        !HasDependenciesConflict(historyItem) &&
                        historyItem.Enabled)
                    {
                        historyItem.ScheduleSource = ScheduleSource.STARTED_FROM_EVENT;
                        AddToScheduleQueue(historyItem);
                    }
                }
            }

            public static void LoadQueueFromTimer()
            {
                forceReloadSchedule = false;
                var executingServer = ServerController.GetExecutingServerName();
                List<ScheduleItem> schedule = SchedulingController.GetSchedule(executingServer);
                Logger.SchedulerLoadQueueFromTimer(executingServer);

                var thisServer = GetServer(executingServer);
                if (thisServer == null)
                {
                    return;
                }

                bool runningInAGroup = !string.IsNullOrEmpty(thisServer.ServerGroup);

                var serverGroupServers = ServerGroupServers(thisServer);

                foreach (ScheduleItem scheduleItem in schedule)
                {
                    if (runningInAGroup && string.IsNullOrEmpty(scheduleItem.Servers))
                    {
                        scheduleItem.Servers = serverGroupServers;
                    }

                    var historyItem = new ScheduleHistoryItem(scheduleItem);

                    if (!IsInQueue(historyItem) &&
                        historyItem.TimeLapse != Null.NullInteger &&
                        historyItem.TimeLapseMeasurement != Null.NullString &&
                        historyItem.Enabled)
                    {
                        if (SchedulingProvider.SchedulerMode == SchedulerMode.TIMER_METHOD)
                        {
                            historyItem.ScheduleSource = ScheduleSource.STARTED_FROM_TIMER;
                        }
                        else if (SchedulingProvider.SchedulerMode == SchedulerMode.REQUEST_METHOD)
                        {
                            historyItem.ScheduleSource = ScheduleSource.STARTED_FROM_BEGIN_REQUEST;
                        }

                        AddToScheduleQueue(historyItem);
                    }
                }
            }

            public static void PurgeScheduleHistory()
            {
                SchedulingController.PurgeScheduleHistory();
            }

            public static void ReloadSchedule()
            {
                forceReloadSchedule = true;
            }

            /// <summary>Removes an item from the collection of schedule items in queue.</summary>
            /// <param name="scheduleItem">Item to remove.</param>
            public static void RemoveFromScheduleQueue(ScheduleItem scheduleItem)
            {
                try
                {
                    using (ScheduleQueue.GetWriteLock(LockTimeout))
                    {
                        // the scheduleitem instances may not be equal even though the scheduleids are equal
                        var item = ScheduleQueue.FirstOrDefault(si => si.ScheduleID == scheduleItem.ScheduleID);
                        if (item != null)
                        {
                            ScheduleQueue.Remove(item);
                        }
                    }
                }
                catch (ApplicationException ex)
                {
                    // The writer lock request timed out.
                    Interlocked.Increment(ref writerTimeouts);
                    Exceptions.Exceptions.LogException(ex);
                }
            }

            public static void RunEventSchedule(EventName eventName)
            {
                RunEventScheduleAsync(eventName, CancellationToken.None).GetAwaiter().GetResult();
            }

            /// <summary>Runs every schedule item attached to <paramref name="eventName"/>.</summary>
            /// <param name="eventName">The event that fired.</param>
            /// <param name="cancellationToken">Cancelled when the host is shutting down.</param>
            /// <returns>A task that completes when the event-triggered queue has drained.</returns>
            public static async Task RunEventScheduleAsync(EventName eventName, CancellationToken cancellationToken)
            {
                try
                {
                    var log = new LogInfo { LogTypeKey = "SCHEDULE_FIRED_FROM_EVENT" };
                    log.AddProperty("EVENT", eventName.ToString());
                    LogController.Instance.AddLog(log);

                    // We allow for three threads to run simultaneously.
                    // As long as we have an open thread, continue.

                    // Load the queue to determine which schedule
                    // items need to be run.
                    LoadQueueFromEvent(eventName);

                    while (GetScheduleQueueCount() > 0)
                    {
                        SetScheduleStatus(ScheduleStatus.RUNNING_EVENT_SCHEDULE);

                        // Fire off the events that need running.
                        if (GetScheduleQueueCount() > 0)
                        {
                            await FireEventsAsync(cancellationToken).ConfigureAwait(false);
                        }

                        if (writerTimeouts > 20 || readerTimeouts > 20)
                        {
                            // Wait for 10 minutes so we don't fill up the logs
                            await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            // Wait for 10 seconds to avoid cpu overutilization
                            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                        }

                        if (GetScheduleQueueCount() == 0)
                        {
                            return;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // The host is shutting down; let the caller (the hosted service) observe it.
                    throw;
                }
                catch (Exception exc)
                {
                    Exceptions.Exceptions.ProcessSchedulerException(exc);
                }
            }

            public static void SetScheduleStatus(ScheduleStatus newStatus)
            {
                try
                {
                    // note:locking inside this method is highly misleading
                    // as there is no lock in place between when the caller
                    // decides to call this method and when the lock is acquired
                    // the value could easily change in that time
                    if (StatusLock.TryEnterWriteLock(LockTimeout))
                    {
                        try
                        {
                            // It is safe for this thread to read or write
                            // from the shared resource.
                            status = newStatus;
                        }
                        finally
                        {
                            // Ensure that the lock is released.
                            StatusLock.ExitWriteLock();
                        }
                    }
                }
                catch (ApplicationException ex)
                {
                    // The writer lock request timed out.
                    Interlocked.Increment(ref writerTimeouts);
                    Exceptions.Exceptions.LogException(ex);
                }
            }

            /// <summary>Runs the scheduler dispatch loop on the calling thread until <see cref="KeepRunning"/> is cleared.</summary>
            /// <remarks>
            /// Synchronous entry point preserved for <see cref="DNNScheduler.Start"/> /
            /// <see cref="DNNScheduler.ExecuteTasks"/>. The loop is normally owned by
            /// <see cref="SchedulerHostedService"/>; if that loop is already active this call
            /// returns immediately rather than starting a second, competing loop.
            /// </remarks>
            public static void Start()
            {
                RunSchedulerLoopAsync(CancellationToken.None).GetAwaiter().GetResult();
            }

            /// <summary>The scheduler dispatch loop.</summary>
            /// <param name="stoppingToken">The host's shutdown token.</param>
            /// <returns>A task that completes when the loop exits.</returns>
            /// <remarks>
            /// This is the migrated form of the .NET Framework <c>Start()</c> loop that ran on a
            /// dedicated <see cref="Thread"/>: every <c>Thread.Sleep</c> became
            /// <see cref="Task.Delay(TimeSpan, CancellationToken)"/> and the shutdown signal is the
            /// host's <paramref name="stoppingToken"/> in addition to the legacy
            /// <see cref="KeepRunning"/> flag. The <see cref="ScheduleStatus"/> state machine and
            /// the N-process-group concurrency model are unchanged.
            /// </remarks>
            public static async Task RunSchedulerLoopAsync(CancellationToken stoppingToken)
            {
                // Only one dispatch loop may be active. The hosted service owns it; a later
                // SchedulingProvider.Start()/ExecuteTasks() call must not spawn a rival loop.
                if (Interlocked.CompareExchange(ref loopActive, 1, 0) != 0)
                {
                    return;
                }

                try
                {
                    activeThreadCount = 0;

                    // This is where the action begins.
                    ////Loop until KeepRunning = false
                    if (SchedulingProvider.SchedulerMode != SchedulerMode.REQUEST_METHOD || debug)
                    {
                        var log = new LogInfo();
                        log.LogTypeKey = "SCHEDULER_STARTED";
                        LogController.Instance.AddLog(log);
                    }

                    while (KeepRunning && !stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            if (Common.Globals.ElapsedSinceAppStart.TotalSeconds < SchedulingProvider.DelayAtAppStart)
                            {
                                if (!KeepThreadAlive)
                                {
                                    return;
                                }

                                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                                continue;
                            }

                            if (SchedulingProvider.SchedulerMode == SchedulerMode.TIMER_METHOD)
                            {
                                SetScheduleStatus(ScheduleStatus.RUNNING_TIMER_SCHEDULE);
                            }
                            else
                            {
                                SetScheduleStatus(ScheduleStatus.RUNNING_REQUEST_SCHEDULE);
                            }

                            // Load the queue to determine which schedule
                            // items need to be run.
                            LoadQueueFromTimer();

                            // Keep track of when the queue was last refreshed
                            // so we can perform a refresh periodically
                            DateTime lastQueueRefresh = DateTime.Now;
                            bool refreshQueueSchedule = false;

                            // We allow for [MaxThreadCount] threads to run
                            // simultaneously.  As long as we have an open thread
                            // and we don't have to refresh the queue, continue
                            // to loop.
                            // refreshQueueSchedule can get set to true near bottom of loop
                            while (FreeThreads > 0 && !refreshQueueSchedule && KeepRunning && !forceReloadSchedule && !stoppingToken.IsCancellationRequested)
                            {
                                // Fire off the events that need running.
                                if (SchedulingProvider.SchedulerMode == SchedulerMode.TIMER_METHOD)
                                {
                                    SetScheduleStatus(ScheduleStatus.RUNNING_TIMER_SCHEDULE);
                                }
                                else
                                {
                                    SetScheduleStatus(ScheduleStatus.RUNNING_REQUEST_SCHEDULE);
                                }

                                // It is safe for this thread to read from
                                // the shared resource.
                                if (GetScheduleQueueCount() > 0)
                                {
                                    await FireEventsAsync(stoppingToken).ConfigureAwait(false);
                                }

                                if (KeepThreadAlive == false)
                                {
                                    return;
                                }

                                if (writerTimeouts > 20 || readerTimeouts > 20)
                                {
                                    // Some kind of deadlock on a resource.
                                    // Wait for 10 minutes so we don't fill up the logs
                                    if (KeepRunning)
                                    {
                                        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        return;
                                    }
                                }
                                else
                                {
                                    // Wait for 10 seconds to avoid cpu overutilization
                                    if (KeepRunning)
                                    {
                                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        return;
                                    }

                                    // Refresh queue from database every 10 minutes
                                    // if there are no items currently in progress
                                    if ((lastQueueRefresh.AddMinutes(10) <= DateTime.Now || forceReloadSchedule) && FreeThreads == maxThreadCount)
                                    {
                                        refreshQueueSchedule = true;
                                        break;
                                    }
                                }
                            }

                            // There are no available threads, all threads are being
                            // used.  Wait 10 seconds until one is available
                            if (KeepRunning && !stoppingToken.IsCancellationRequested)
                            {
                                if (refreshQueueSchedule == false)
                                {
                                    SetScheduleStatus(ScheduleStatus.WAITING_FOR_OPEN_THREAD);
                                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                return;
                            }
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            // Shutdown. Rethrow so the hosted service reports a clean stop; the
                            // finally block below still records the STOPPED status and log entry.
                            throw;
                        }
                        catch (Exception exc)
                        {
                            Exceptions.Exceptions.ProcessSchedulerException(exc);

                            // sleep for 10 minutes
                            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref loopActive, 0);

                    if (SchedulingProvider.SchedulerMode == SchedulerMode.TIMER_METHOD || SchedulingProvider.SchedulerMode == SchedulerMode.DISABLED)
                    {
                        SetScheduleStatus(ScheduleStatus.STOPPED);
                    }
                    else
                    {
                        SetScheduleStatus(ScheduleStatus.WAITING_FOR_REQUEST);
                    }

                    if (SchedulingProvider.SchedulerMode != SchedulerMode.REQUEST_METHOD || debug)
                    {
                        var log = new LogInfo { LogTypeKey = "SCHEDULER_STOPPED" };
                        LogController.Instance.AddLog(log);
                    }
                }
            }

            public static void UpdateScheduleHistory(ScheduleHistoryItem scheduleHistoryItem)
            {
                try
                {
                    SchedulingController.UpdateScheduleHistory(scheduleHistoryItem);
                }
                catch (Exception exc)
                {
                    Exceptions.Exceptions.ProcessSchedulerException(exc);
                }
            }

            public static void WorkCompleted(SchedulerClient schedulerClient)
            {
                try
                {
                    ScheduleHistoryItem scheduleHistoryItem = schedulerClient.ScheduleHistoryItem;

                    // Remove the object in the ScheduleInProgress collection
                    RemoveFromScheduleInProgress(scheduleHistoryItem);

                    // A SchedulerClient is notifying us that their
                    // process has completed.  Decrease our ActiveThreadCount
                    Interlocked.Decrement(ref activeThreadCount);

                    // Update the schedule item object property
                    // to note the end time and next start
                    scheduleHistoryItem.EndDate = DateTime.Now;

                    if (scheduleHistoryItem.ScheduleSource == ScheduleSource.STARTED_FROM_EVENT)
                    {
                        scheduleHistoryItem.NextStart = Null.NullDate;
                    }
                    else
                    {
                        if (scheduleHistoryItem.CatchUpEnabled)
                        {
                            switch (scheduleHistoryItem.TimeLapseMeasurement)
                            {
                                case "s":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.NextStart.AddSeconds(scheduleHistoryItem.TimeLapse);
                                    break;
                                case "m":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.NextStart.AddMinutes(scheduleHistoryItem.TimeLapse);
                                    break;
                                case "h":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.NextStart.AddHours(scheduleHistoryItem.TimeLapse);
                                    break;
                                case "d":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.NextStart.AddDays(scheduleHistoryItem.TimeLapse);
                                    break;
                                case "w":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.StartDate.AddDays(scheduleHistoryItem.TimeLapse * 7);
                                    break;
                                case "mo":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.StartDate.AddMonths(scheduleHistoryItem.TimeLapse);
                                    break;
                                case "y":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.StartDate.AddYears(scheduleHistoryItem.TimeLapse);
                                    break;
                            }
                        }
                        else
                        {
                            switch (scheduleHistoryItem.TimeLapseMeasurement)
                            {
                                case "s":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.NextStart.AddSeconds(scheduleHistoryItem.TimeLapse);
                                    break;
                                case "m":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.NextStart.AddMinutes(scheduleHistoryItem.TimeLapse);
                                    break;
                                case "h":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.NextStart.AddHours(scheduleHistoryItem.TimeLapse);
                                    break;
                                case "d":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.NextStart.AddDays(scheduleHistoryItem.TimeLapse);
                                    break;
                                case "w":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.NextStart.AddDays(scheduleHistoryItem.TimeLapse * 7);
                                    break;
                                case "mo":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.NextStart.AddMonths(scheduleHistoryItem.TimeLapse);
                                    break;
                                case "y":
                                    scheduleHistoryItem.NextStart = scheduleHistoryItem.NextStart.AddYears(scheduleHistoryItem.TimeLapse);
                                    break;
                            }
                        }
                    }

                    // Update the ScheduleHistory in the database
                    UpdateScheduleHistory(scheduleHistoryItem);

                    if (scheduleHistoryItem.NextStart != Null.NullDate)
                    {
                        // Put the object back into the ScheduleQueue
                        // collection with the new NextStart date.
                        scheduleHistoryItem.StartDate = Null.NullDate;
                        scheduleHistoryItem.EndDate = Null.NullDate;
                        scheduleHistoryItem.LogNotes = string.Empty;
                        scheduleHistoryItem.ProcessGroup = -1;
                        AddToScheduleQueue(scheduleHistoryItem);
                    }

                    if (schedulerClient.ScheduleHistoryItem.RetainHistoryNum > 0)
                    {
                        var log = new LogInfo { LogTypeKey = "SCHEDULER_EVENT_COMPLETED" };
                        log.AddProperty("TYPE", schedulerClient.GetType().FullName);
                        log.AddProperty("THREAD ID", Thread.CurrentThread.GetHashCode().ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("NEXT START", Convert.ToString(scheduleHistoryItem.NextStart, CultureInfo.InvariantCulture));
                        log.AddProperty("SOURCE", schedulerClient.ScheduleHistoryItem.ScheduleSource.ToString());
                        log.AddProperty("ACTIVE THREADS", activeThreadCount.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("FREE THREADS", FreeThreads.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("READER TIMEOUTS", readerTimeouts.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("WRITER TIMEOUTS", writerTimeouts.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("IN PROGRESS", GetScheduleInProgressCount().ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("IN QUEUE", GetScheduleQueueCount().ToString(CultureInfo.InvariantCulture));
                        LogController.Instance.AddLog(log);
                    }
                }
                catch (Exception exc)
                {
                    Exceptions.Exceptions.ProcessSchedulerException(exc);
                }
            }

            public static void WorkErrored(SchedulerClient schedulerClient, Exception exception)
            {
                WorkErrored(schedulerClient.ScheduleHistoryItem, exception);
            }

            public static void WorkErrored(ScheduleHistoryItem scheduleHistoryItem, Exception exception)
            {
                try
                {
                    // Remove the object in the ScheduleInProgress collection
                    RemoveFromScheduleInProgress(scheduleHistoryItem);

                    // A SchedulerClient is notifying us that their
                    // process has errored.  Decrease our ActiveThreadCount
                    Interlocked.Decrement(ref activeThreadCount);

                    Exceptions.Exceptions.ProcessSchedulerException(exception);

                    // Update the schedule item object property
                    // to note the end time and next start
                    scheduleHistoryItem.EndDate = DateTime.Now;
                    if (scheduleHistoryItem.ScheduleSource == ScheduleSource.STARTED_FROM_EVENT)
                    {
                        scheduleHistoryItem.NextStart = Null.NullDate;
                    }
                    else if (scheduleHistoryItem.RetryTimeLapse != Null.NullInteger)
                    {
                        switch (scheduleHistoryItem.RetryTimeLapseMeasurement)
                        {
                            case "s":
                                scheduleHistoryItem.NextStart = scheduleHistoryItem.StartDate.AddSeconds(scheduleHistoryItem.RetryTimeLapse);
                                break;
                            case "m":
                                scheduleHistoryItem.NextStart = scheduleHistoryItem.StartDate.AddMinutes(scheduleHistoryItem.RetryTimeLapse);
                                break;
                            case "h":
                                scheduleHistoryItem.NextStart = scheduleHistoryItem.StartDate.AddHours(scheduleHistoryItem.RetryTimeLapse);
                                break;
                            case "d":
                                scheduleHistoryItem.NextStart = scheduleHistoryItem.StartDate.AddDays(scheduleHistoryItem.RetryTimeLapse);
                                break;
                            case "w":
                                scheduleHistoryItem.NextStart = scheduleHistoryItem.StartDate.AddDays(scheduleHistoryItem.RetryTimeLapse * 7);
                                break;
                            case "mo":
                                scheduleHistoryItem.NextStart = scheduleHistoryItem.StartDate.AddMonths(scheduleHistoryItem.RetryTimeLapse);
                                break;
                            case "y":
                                scheduleHistoryItem.NextStart = scheduleHistoryItem.StartDate.AddYears(scheduleHistoryItem.RetryTimeLapse);
                                break;
                        }
                    }

                    // Update the ScheduleHistory in the database
                    UpdateScheduleHistory(scheduleHistoryItem);

                    if (scheduleHistoryItem.NextStart != Null.NullDate && scheduleHistoryItem.RetryTimeLapse != Null.NullInteger)
                    {
                        // Put the object back into the ScheduleQueue
                        // collection with the new NextStart date.
                        scheduleHistoryItem.StartDate = Null.NullDate;
                        scheduleHistoryItem.EndDate = Null.NullDate;
                        scheduleHistoryItem.LogNotes = string.Empty;
                        scheduleHistoryItem.ProcessGroup = -1;
                        AddToScheduleQueue(scheduleHistoryItem);
                    }

                    if (scheduleHistoryItem.RetainHistoryNum > 0)
                    {
                        // Write out the log entry for this event
                        var log = new LogInfo { LogTypeKey = "SCHEDULER_EVENT_FAILURE" };
                        log.AddProperty("THREAD ID", Thread.CurrentThread.GetHashCode().ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("TYPE", scheduleHistoryItem.TypeFullName);
                        if (exception != null)
                        {
                            log.AddProperty("EXCEPTION", exception.Message);
                        }

                        log.AddProperty("RESCHEDULED FOR", Convert.ToString(scheduleHistoryItem.NextStart, CultureInfo.InvariantCulture));
                        log.AddProperty("SOURCE", scheduleHistoryItem.ScheduleSource.ToString());
                        log.AddProperty("ACTIVE THREADS", activeThreadCount.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("FREE THREADS", FreeThreads.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("READER TIMEOUTS", readerTimeouts.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("WRITER TIMEOUTS", writerTimeouts.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("IN PROGRESS", GetScheduleInProgressCount().ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("IN QUEUE", GetScheduleQueueCount().ToString(CultureInfo.InvariantCulture));
                        LogController.Instance.AddLog(log);
                    }
                }
                catch (Exception exc)
                {
                    Exceptions.Exceptions.ProcessSchedulerException(exc);
                }
            }

            public static void WorkProgressing(SchedulerClient schedulerClient)
            {
                try
                {
                    // A SchedulerClient is notifying us that their
                    // process is in progress.  Informational only.
                    if (schedulerClient.ScheduleHistoryItem.RetainHistoryNum > 0)
                    {
                        // Write out the log entry for this event
                        var log = new LogInfo { LogTypeKey = "SCHEDULER_EVENT_PROGRESSING" };
                        log.AddProperty("THREAD ID", Thread.CurrentThread.GetHashCode().ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("TYPE", schedulerClient.GetType().FullName);
                        log.AddProperty("SOURCE", schedulerClient.ScheduleHistoryItem.ScheduleSource.ToString());
                        log.AddProperty("ACTIVE THREADS", activeThreadCount.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("FREE THREADS", FreeThreads.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("READER TIMEOUTS", readerTimeouts.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("WRITER TIMEOUTS", writerTimeouts.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("IN PROGRESS", GetScheduleInProgressCount().ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("IN QUEUE", GetScheduleQueueCount().ToString(CultureInfo.InvariantCulture));
                        LogController.Instance.AddLog(log);
                    }
                }
                catch (Exception exc)
                {
                    Exceptions.Exceptions.ProcessSchedulerException(exc);
                }
            }

            public static void WorkStarted(SchedulerClient schedulerClient)
            {
                WorkStarted(schedulerClient.ScheduleHistoryItem);
            }

            public static void WorkStarted(ScheduleHistoryItem scheduleHistoryItem)
            {
                bool activeThreadCountIncremented = false;
                try
                {
                    scheduleHistoryItem.ThreadID = Thread.CurrentThread.GetHashCode();

                    // Put the object in the ScheduleInProgress collection
                    // and remove it from the ScheduleQueue
                    RemoveFromScheduleQueue(scheduleHistoryItem);
                    AddToScheduleInProgress(scheduleHistoryItem);

                    // A SchedulerClient is notifying us that their
                    // process has started.  Increase our ActiveThreadCount
                    Interlocked.Increment(ref activeThreadCount);
                    activeThreadCountIncremented = true;

                    // Update the schedule item
                    // object property to note the start time.
                    scheduleHistoryItem.StartDate = DateTime.Now;
                    AddScheduleHistory(scheduleHistoryItem);

                    if (scheduleHistoryItem.RetainHistoryNum > 0)
                    {
                        // Write out the log entry for this event
                        var log = new LogInfo { LogTypeKey = "SCHEDULER_EVENT_STARTED" };
                        log.AddProperty("THREAD ID", Thread.CurrentThread.GetHashCode().ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("TYPE", scheduleHistoryItem.TypeFullName);
                        log.AddProperty("SOURCE", scheduleHistoryItem.ScheduleSource.ToString());
                        log.AddProperty("ACTIVE THREADS", activeThreadCount.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("FREE THREADS", FreeThreads.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("READER TIMEOUTS", readerTimeouts.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("WRITER TIMEOUTS", writerTimeouts.ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("IN PROGRESS", GetScheduleInProgressCount().ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("IN QUEUE", GetScheduleQueueCount().ToString(CultureInfo.InvariantCulture));
                        LogController.Instance.AddLog(log);
                    }
                }
                catch (Exception exc)
                {
                    // Decrement the ActiveThreadCount because
                    // otherwise the number of active threads
                    // will appear to be climbing when in fact
                    // no tasks are being executed.
                    if (activeThreadCountIncremented)
                    {
                        Interlocked.Decrement(ref activeThreadCount);
                    }

                    Exceptions.Exceptions.ProcessSchedulerException(exc);
                }
            }

            // DNN-5001
            public static void StopScheduleInProgress(ScheduleItem scheduleItem, ScheduleHistoryItem runningscheduleHistoryItem)
            {
                try
                {
                    // attempt to stop task only if it is still in progress
                    if (GetScheduleItemFromScheduleInProgress(scheduleItem) != null)
                    {
                        var scheduleHistoryItem = GetScheduleItemFromScheduleInProgress(scheduleItem);
                        scheduleHistoryItem.ScheduleHistoryID = runningscheduleHistoryItem.ScheduleHistoryID;
                        scheduleHistoryItem.StartDate = runningscheduleHistoryItem.StartDate;

                        // Remove the object in the ScheduleInProgress collection
                        RemoveFromScheduleInProgress(scheduleHistoryItem);

                        // A SchedulerClient is notifying us that their
                        // process has completed.  Decrease our ActiveThreadCount
                        Interlocked.Decrement(ref activeThreadCount);

                        // Update the schedule item object property
                        // to note the end time and next start
                        scheduleHistoryItem.EndDate = DateTime.Now;

                        if (scheduleHistoryItem.ScheduleSource == ScheduleSource.STARTED_FROM_EVENT)
                        {
                            scheduleHistoryItem.NextStart = Null.NullDate;
                        }
                        else
                        {
                            if (scheduleHistoryItem.CatchUpEnabled)
                            {
                                switch (scheduleHistoryItem.TimeLapseMeasurement)
                                {
                                    case "s":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.NextStart.AddSeconds(scheduleHistoryItem.TimeLapse);
                                        break;
                                    case "m":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.NextStart.AddMinutes(scheduleHistoryItem.TimeLapse);
                                        break;
                                    case "h":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.NextStart.AddHours(scheduleHistoryItem.TimeLapse);
                                        break;
                                    case "d":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.NextStart.AddDays(scheduleHistoryItem.TimeLapse);
                                        break;
                                    case "w":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.StartDate.AddDays(scheduleHistoryItem.TimeLapse * 7);
                                        break;
                                    case "mo":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.StartDate.AddMonths(scheduleHistoryItem.TimeLapse);
                                        break;
                                    case "y":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.StartDate.AddYears(scheduleHistoryItem.TimeLapse);
                                        break;
                                }
                            }
                            else
                            {
                                switch (scheduleHistoryItem.TimeLapseMeasurement)
                                {
                                    case "s":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.NextStart.AddSeconds(scheduleHistoryItem.TimeLapse);
                                        break;
                                    case "m":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.NextStart.AddMinutes(scheduleHistoryItem.TimeLapse);
                                        break;
                                    case "h":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.NextStart.AddHours(scheduleHistoryItem.TimeLapse);
                                        break;
                                    case "d":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.NextStart.AddDays(scheduleHistoryItem.TimeLapse);
                                        break;
                                    case "w":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.NextStart.AddDays(scheduleHistoryItem.TimeLapse * 7);
                                        break;
                                    case "mo":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.NextStart.AddMonths(scheduleHistoryItem.TimeLapse);
                                        break;
                                    case "y":
                                        scheduleHistoryItem.NextStart =
                                            scheduleHistoryItem.NextStart.AddYears(scheduleHistoryItem.TimeLapse);
                                        break;
                                }
                            }
                        }

                        // Update the ScheduleHistory in the database
                        UpdateScheduleHistory(scheduleHistoryItem);

                        if (scheduleHistoryItem.NextStart != Null.NullDate)
                        {
                            // Put the object back into the ScheduleQueue
                            // collection with the new NextStart date.
                            scheduleHistoryItem.StartDate = Null.NullDate;
                            scheduleHistoryItem.EndDate = Null.NullDate;
                            scheduleHistoryItem.LogNotes = string.Empty;
                            scheduleHistoryItem.ProcessGroup = -1;
                            AddToScheduleQueue(scheduleHistoryItem);
                        }

                        // Write out the log entry for this event
                        var log = new LogInfo { LogTypeKey = "SCHEDULER_EVENT_COMPLETED" };
                        log.AddProperty("REASON", "Scheduler task has been stopped manually");
                        log.AddProperty("TYPE", scheduleHistoryItem.TypeFullName);
                        log.AddProperty("THREAD ID", Thread.CurrentThread.GetHashCode().ToString(CultureInfo.InvariantCulture));
                        log.AddProperty("NEXT START", Convert.ToString(scheduleHistoryItem.NextStart, CultureInfo.InvariantCulture));
                        LogController.Instance.AddLog(log);
                    }
                }
                catch (Exception exc)
                {
                    Exceptions.Exceptions.ProcessSchedulerException(exc);
                }
            }

            internal static bool IsInQueue(ScheduleItem scheduleItem)
            {
                try
                {
                    using (ScheduleQueue.GetReadLock(LockTimeout))
                    {
                        return ScheduleQueue.Any(si => si.ScheduleID == scheduleItem.ScheduleID);
                    }
                }
                catch (ApplicationException)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                    return false;
                }
            }

            internal static ServerInfo GetServer(string executingServer)
            {
                try
                {
                    return ServerController.GetServers().FirstOrDefault(
                        s => ServerController.GetServerName(s).Equals(executingServer, StringComparison.OrdinalIgnoreCase) && s.Enabled);
                }
                catch (Exception)
                {
                    // catches edge-case where schedule runs before webserver registration
                    return null;
                }
            }

            internal static void InitializeThreadPool(bool boolDebug, int maxThreads)
            {
                debug = boolDebug;
                lock (typeof(CoreScheduler))
                {
                    if (!threadPoolInitialized)
                    {
                        threadPoolInitialized = true;
                        if (maxThreads == -1)
                        {
                            maxThreads = 1;
                        }

                        numberOfProcessGroups = maxThreads;
                        maxThreadCount = maxThreads;
                        for (int i = 0; i < numberOfProcessGroups; i++)
                        {
                            Array.Resize(ref processGroup, i + 1);
                            processGroup[i] = new ProcessGroup();
                        }
                    }
                }
            }

            /// <summary>adds an item to the collection of schedule items in progress.</summary>
            /// <param name="scheduleHistoryItem">Item to add.</param>
            /// <remarks>Thread Safe.</remarks>
            private static void AddToScheduleInProgress(ScheduleHistoryItem scheduleHistoryItem)
            {
                if (!ScheduleInProgressContains(scheduleHistoryItem))
                {
                    try
                    {
                        using (ScheduleInProgress.GetWriteLock(LockTimeout))
                        {
                            if (!ScheduleInProgressContains(scheduleHistoryItem))
                            {
                                ScheduleInProgress.Add(scheduleHistoryItem);
                            }
                        }
                    }
                    catch (ApplicationException ex)
                    {
                        // The writer lock request timed out.
                        Interlocked.Increment(ref writerTimeouts);
                        Exceptions.Exceptions.LogException(ex);
                    }
                }
            }

            private static int GetProcessGroup()
            {
                // return a random process group
                var r = new Random();
                return r.Next(0, numberOfProcessGroups - 1);
            }

            private static bool IsInProgress(ScheduleItem scheduleItem)
            {
                try
                {
                    using (ScheduleInProgress.GetReadLock(LockTimeout))
                    {
                        return ScheduleInProgress.Any(si => si.ScheduleID == scheduleItem.ScheduleID);
                    }
                }
                catch (ApplicationException ex)
                {
                    // The reader lock request timed out.
                    Interlocked.Increment(ref readerTimeouts);
                    Logger.SchedulerReaderLockRequestTimeout(ex);

                    return false;
                }
            }

            /// <summary>Removes an item from the collection of schedule items in progress.</summary>
            /// <param name="scheduleItem">The schedule item.</param>
            /// <remarks>Thread Safe.</remarks>
            private static void RemoveFromScheduleInProgress(ScheduleItem scheduleItem)
            {
                try
                {
                    using (ScheduleInProgress.GetWriteLock(LockTimeout))
                    {
                        var item = ScheduleInProgress.FirstOrDefault(si => si.ScheduleID == scheduleItem.ScheduleID);
                        if (item != null)
                        {
                            ScheduleInProgress.Remove(item);
                        }
                    }
                }
                catch (ApplicationException ex)
                {
                    // The writer lock request timed out.
                    Interlocked.Increment(ref writerTimeouts);
                    Exceptions.Exceptions.LogException(ex);
                }
            }

            /// <summary>Gets a schedulehistory item from the collection of schedule items in progress.</summary>
            /// <param name="scheduleItem">The schedule item.</param>
            /// <remarks>Thread Safe.</remarks>
            private static ScheduleHistoryItem GetScheduleItemFromScheduleInProgress(ScheduleItem scheduleItem)
            {
                try
                {
                    using (ScheduleInProgress.GetWriteLock(LockTimeout))
                    {
                        var item = ScheduleInProgress.FirstOrDefault(si => si.ScheduleID == scheduleItem.ScheduleID);
                        return item;
                    }
                }
                catch (ApplicationException ex)
                {
                    // The writer lock request timed out.
                    Interlocked.Increment(ref writerTimeouts);
                    Exceptions.Exceptions.LogException(ex);
                }

                return null;
            }

            private static bool ScheduleInProgressContains(ScheduleHistoryItem scheduleHistoryItem)
            {
                try
                {
                    using (ScheduleInProgress.GetReadLock(LockTimeout))
                    {
                        return ScheduleInProgress.Any(si => si.ScheduleID == scheduleHistoryItem.ScheduleID);
                    }
                }
                catch (ApplicationException ex)
                {
                    Interlocked.Increment(ref readerTimeouts);
                    Exceptions.Exceptions.LogException(ex);
                    return false;
                }
            }

            private static bool ScheduleQueueContains(ScheduleItem objScheduleItem)
            {
                try
                {
                    using (ScheduleQueue.GetReadLock(LockTimeout))
                    {
                        return ScheduleQueue.Any(si => si.ScheduleID == objScheduleItem.ScheduleID);
                    }
                }
                catch (ApplicationException ex)
                {
                    Interlocked.Increment(ref readerTimeouts);
                    Exceptions.Exceptions.LogException(ex);
                    return false;
                }
            }

            private static void LogWhyTaskNotRun(ScheduleItem scheduleItem)
            {
                if (debug)
                {
                    bool appended = false;
                    var strDebug = new StringBuilder("Task not run because ");
                    if (!(scheduleItem.NextStart <= DateTime.Now))
                    {
                        strDebug.Append(" task is scheduled for " + scheduleItem.NextStart);
                        appended = true;
                    }

                    if (!scheduleItem.Enabled)
                    {
                        if (appended)
                        {
                            strDebug.Append(" and");
                        }

                        strDebug.Append(" task is not enabled");
                        appended = true;
                    }

                    if (IsInProgress(scheduleItem))
                    {
                        if (appended)
                        {
                            strDebug.Append(" and");
                        }

                        strDebug.Append(" task is already in progress");
                        appended = true;
                    }

                    if (HasDependenciesConflict(scheduleItem))
                    {
                        if (appended)
                        {
                            strDebug.Append(" and");
                        }

                        strDebug.Append(" task has conflicting dependency");
                    }

                    var log = new LogInfo();
                    log.AddProperty("EVENT NOT RUN REASON", strDebug.ToString());
                    log.AddProperty("SCHEDULE ID", scheduleItem.ScheduleID.ToString(CultureInfo.InvariantCulture));
                    log.AddProperty("TYPE FULL NAME", scheduleItem.TypeFullName);
                    log.LogTypeKey = "DEBUG";
                    LogController.Instance.AddLog(log);
                }
            }

            private static void LogEventAddedToProcessGroup(ScheduleItem scheduleItem)
            {
                if (debug)
                {
                    var log = new LogInfo();
                    log.AddProperty("EVENT ADDED TO PROCESS GROUP " + scheduleItem.ProcessGroup, scheduleItem.TypeFullName);
                    log.AddProperty("SCHEDULE ID", scheduleItem.ScheduleID.ToString(CultureInfo.InvariantCulture));
                    log.LogTypeKey = "DEBUG";
                    LogController.Instance.AddLog(log);
                }
            }

            private static string ServerGroupServers(ServerInfo thisServer)
            {
                // Get the servers
                var servers = ServerController.GetEnabledServers().Where(s => s.ServerGroup == thisServer.ServerGroup);
                return servers.Aggregate(string.Empty, (current, serverInfo) => current + ServerController.GetServerName(serverInfo) + ",");
            }
        }
    }
}
