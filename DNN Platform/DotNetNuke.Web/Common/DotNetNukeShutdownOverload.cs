// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Common.Internal
{
    using System;
    using System.IO;
    using System.Threading;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Instrumentation;

    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>Watches <c>bin</c> folder and root files and shuts the host down proactively when they change.</summary>
    /// <remarks>
    /// <para>
    /// On .NET Framework this type reached into <c>HttpRuntime.FileChangesMonitor</c> and
    /// <c>System.Web.DirMonCompletion</c> by reflection to report the File Change Notification mode
    /// and the active directory-monitor count, and it recycled the application through
    /// <c>HttpRuntime.UnloadAppDomain()</c>. None of those exist on modern .NET: there is no app
    /// domain to unload and no <c>HostingEnvironment</c>.
    /// </para>
    /// <para>
    /// The behaviour is preserved against <see cref="IHostApplicationLifetime"/>: the configured FCN
    /// mode is reported from the <c>HttpRuntime:FcnMode</c> setting (the same
    /// <c>&lt;httpRuntime fcnMode="..."&gt;</c> value, now in <c>appsettings.json</c>), the root
    /// folder is still watched and its activity logged, a shutdown is still scheduled on a short
    /// delay, and the shutdown itself is <see cref="IHostApplicationLifetime.StopApplication"/>.
    /// The shutdown reason is reported from the <c>ApplicationStopping</c>/<c>ApplicationStopped</c>
    /// callbacks, which is the modern equivalent of <c>HostingEnvironment.ShutdownReason</c>.
    /// </para>
    /// </remarks>
    internal static partial class DotNetNukeShutdownOverload
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(DotNetNukeShutdownOverload));

        private static readonly object WatcherLock = new object();

        private static Timer shutDownDelayTimer;
        private static bool handleShutdowns;
        private static bool shutdownInprogress;
        private static FileSystemWatcher binOrRootWatcher;
        private static string binFolder = string.Empty;
        private static IHostApplicationLifetime applicationLifetime;

        /// <summary>
        /// Initializes the file-change monitoring and the shutdown reporting. Replaces
        /// <c>InitializeFcnSettings</c>, which read the FCN mode off the private
        /// <c>HttpRuntime.FileChangesMonitor</c>.
        /// </summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="lifetime">The host application lifetime.</param>
        /// <param name="fcnMode">The configured <c>HttpRuntime:FcnMode</c> value.</param>
        internal static void Initialize(IApplicationStatusInfo appStatus, IHostApplicationLifetime lifetime, string fcnMode)
        {
            ArgumentNullException.ThrowIfNull(appStatus);
            ArgumentNullException.ThrowIfNull(lifetime);

            // any error/message logged below should be informational only
            try
            {
                applicationLifetime = lifetime;

                // The replacement for reading HttpRuntime's private _FCNMode field: on modern .NET
                // the host does not watch content files at all, so the configured value is reported
                // for diagnostic parity and our own watcher below is what provides the monitoring.
                Logger.ShutdownOverloadFileChangeNotificationMode(fcnMode);

                lifetime.ApplicationStopping.Register(OnApplicationStopping);
                lifetime.ApplicationStopped.Register(OnApplicationStopped);

                // just monitor the root folder but don't interfere
                AddSiteFilesMonitoring(appStatus, false);
            }
            catch (Exception e)
            {
                Logger.ShutdownOverloadInitializeFcnSettingsException(e);
            }
        }

        private static void OnApplicationStopping()
        {
            Logger.ShutdownOverloadShutdownStarting(shutdownInprogress ? "File change in the bin folder" : "Host shutdown requested");
        }

        private static void OnApplicationStopped()
        {
            Logger.ShutdownOverloadShutdownCompleted();
        }

        private static void AddSiteFilesMonitoring(IApplicationStatusInfo appStatus, bool handleShutdowns)
        {
            if (binOrRootWatcher == null)
            {
                lock (WatcherLock)
                {
                    if (binOrRootWatcher == null)
                    {
                        try
                        {
                            DotNetNukeShutdownOverload.handleShutdowns = handleShutdowns;
                            if (DotNetNukeShutdownOverload.handleShutdowns)
                            {
                                shutDownDelayTimer = new Timer(InitiateShutdown);
                            }

                            binFolder = Path.Combine(appStatus.ApplicationMapPath, "bin").ToLowerInvariant();
                            binOrRootWatcher = new FileSystemWatcher
                            {
                                Filter = "*.*",
                                Path = handleShutdowns ? binFolder : appStatus.ApplicationMapPath,
                                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                                IncludeSubdirectories = true,
                            };

                            binOrRootWatcher.Created += WatcherOnCreated;
                            binOrRootWatcher.Deleted += WatcherOnDeleted;
                            binOrRootWatcher.Renamed += WatcherOnRenamed;
                            binOrRootWatcher.Changed += WatcherOnChanged;
                            binOrRootWatcher.Error += WatcherOnError;

                            // begin watching;
                            binOrRootWatcher.EnableRaisingEvents = true;
                            Logger.ShutdownOverloadAddedWatcherFor(binOrRootWatcher.Path, binOrRootWatcher.Filter);
                        }
                        catch (Exception ex)
                        {
                            Logger.ShutdownOverloadErrorAddingOurOwnFileMonitoringObject(ex);
                        }
                    }
                }
            }
        }

        private static void InitiateShutdown(object state)
        {
            if (!handleShutdowns)
            {
                return;
            }

            try
            {
                // The replacement for HttpRuntime.UnloadAppDomain(): there is no app domain, the
                // host process is stopped gracefully instead and the process manager restarts it.
                applicationLifetime?.StopApplication();
            }
            catch (Exception ex)
            {
                shutdownInprogress = false;
                Logger.ShutdownOverloadUnloadAppDomainException(ex);
            }
        }

        private static void ScheduleShutdown()
        {
            // no need for locking; worst case is timer extended a bit more
            if (handleShutdowns && !shutdownInprogress)
            {
                shutdownInprogress = true;

                // delay for a very short period
                shutDownDelayTimer.Change(1500, Timeout.Infinite);
            }
        }

        private static void WatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            if (!e.FullPath.EndsWith(".log.resources", StringComparison.OrdinalIgnoreCase))
            {
                Logger.ShutdownOverloadWatcherActivity(e.ChangeType, e.FullPath);
            }

            if (handleShutdowns && !shutdownInprogress && (e.FullPath ?? string.Empty).StartsWith(binFolder, StringComparison.OrdinalIgnoreCase))
            {
                ScheduleShutdown();
            }
        }

        private static void WatcherOnCreated(object sender, FileSystemEventArgs e)
        {
            if (!e.FullPath.EndsWith(".log.resources", StringComparison.OrdinalIgnoreCase))
            {
                Logger.ShutdownOverloadWatcherActivity(e.ChangeType, e.FullPath);
            }

            if (handleShutdowns && !shutdownInprogress && (e.FullPath ?? string.Empty).StartsWith(binFolder, StringComparison.OrdinalIgnoreCase))
            {
                ScheduleShutdown();
            }
        }

        private static void WatcherOnRenamed(object sender, RenamedEventArgs e)
        {
            if (!e.FullPath.EndsWith(".log.resources", StringComparison.OrdinalIgnoreCase))
            {
                Logger.ShutdownOverloadWatcherRenamedActivity(e.ChangeType, e.FullPath, e.OldFullPath);
            }

            if (handleShutdowns && !shutdownInprogress && (e.FullPath ?? string.Empty).StartsWith(binFolder, StringComparison.OrdinalIgnoreCase))
            {
                ScheduleShutdown();
            }
        }

        private static void WatcherOnDeleted(object sender, FileSystemEventArgs e)
        {
            if (!e.FullPath.EndsWith(".log.resources", StringComparison.OrdinalIgnoreCase))
            {
                Logger.ShutdownOverloadWatcherActivity(e.ChangeType, e.FullPath);
            }

            if (handleShutdowns && !shutdownInprogress && (e.FullPath ?? string.Empty).StartsWith(binFolder, StringComparison.OrdinalIgnoreCase))
            {
                ScheduleShutdown();
            }
        }

        private static void WatcherOnError(object sender, ErrorEventArgs e)
        {
            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.ShutdownOverloadWatcherError(e.GetException());
            }
        }
    }
}
