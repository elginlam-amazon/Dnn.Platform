// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Common.Internal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Application;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Log.EventLog;
    using DotNetNuke.Services.Mail;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Watches the application folder for files with a dangerous extension and alerts the host when
    /// one appears.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>System.Web.Security</c>/<c>System.Web</c> surface this type used is gone: the
    /// application path now comes from <see cref="IApplicationStatusInfo"/> (formerly
    /// <c>Globals.ApplicationMapPath</c>), the upgrade status from the same service (formerly
    /// <c>Globals.Status</c>), the host setting from <see cref="IHostSettingsService"/> (formerly
    /// <c>HostController.Instance</c>), the event log from <see cref="IEventLogger"/> and the
    /// shutdown hook from the caller instead of <c>AppDomain.DomainUnload</c>.
    /// </para>
    /// <para>
    /// Behaviour is otherwise preserved exactly, including the fact that it is NOT started: on
    /// .NET Framework the only call site was the commented-out
    /// <c>////DotNetNukeSecurity.Initialize();</c> line of <c>Application_Start</c>, so the watcher
    /// never ran. It is kept as a service (rather than deleted) because the host alert it
    /// implements, its resource keys and its blocked-extension list are the feature; a host that
    /// wants it only has to resolve this service and call <see cref="Initialize"/>.
    /// </para>
    /// </remarks>
    internal sealed class DotNetNukeSecurity : IDisposable
    {
        private const int CacheTimeOut = 5; // obtain the setting and do calculations once every 5 minutes at most, plus no need for locking
        private const int SlidingDelay = 30 * 1000; // milliseconds

        private static readonly ILogger Logger = DnnLoggingController.GetLogger<DotNetNukeSecurity>();

        // Source: Configuring Blocked File Extensions
        // https://msdn.microsoft.com/en-us/library/cc767397.aspx
        private static readonly IEnumerable<string> DefaultRestrictExtensions =
            (
                ".ade,.adp,.app,.ashx,.asmx,.asp,.aspx,.bas,.bat,.chm,.class,.cmd,.com,.cpl,.crt,.dll,.exe," +
                ".fxp,.hlp,.hta,.ins,.isp,.jse,.lnk,.mda,.mdb,.mde,.mdt,.mdw,.mdz,.msc,.msi,.msp,.mst,.ops,.pcd,.php," +
                ".pif,.prf,.prg,.py,.reg,.scf,.scr,.sct,.shb,.shs,.url,.vb,.vbe,.vbs,.wsc,.wsf,.wsh")
            .ToLowerInvariant()
            .Split(',')
            .Where(e => !string.IsNullOrEmpty(e))
            .Select(e => e.Trim())
            .ToList();

        private readonly object initializeLock = new object();
        private readonly IApplicationStatusInfo applicationStatusInfo;
        private readonly IHostSettingsService hostSettingsService;
        private readonly IHostSettings hostSettings;
        private readonly IEventLogger eventLogger;

        private FileSystemWatcher fileWatcher;
        private DateTime lastRead;
        private UpgradeStatus appStatus = UpgradeStatus.None;
        private IEnumerable<string> settingsRestrictExtensions = [];
        private Queue<string> filesQueue;
        private Timer queueTimer;

        // used to indicate already send first real-time email within last SlidingDelay period
        private int notificationSent;

        /// <summary>Initializes a new instance of the <see cref="DotNetNukeSecurity"/> class.</summary>
        /// <param name="applicationStatusInfo">The application status.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="hostSettings">The host settings, replacing the obsolete static <c>Host</c> facade.</param>
        /// <param name="eventLogger">The event logger.</param>
        public DotNetNukeSecurity(
            IApplicationStatusInfo applicationStatusInfo,
            IHostSettingsService hostSettingsService,
            IHostSettings hostSettings,
            IEventLogger eventLogger)
        {
            this.applicationStatusInfo = applicationStatusInfo ?? throw new ArgumentNullException(nameof(applicationStatusInfo));
            this.hostSettingsService = hostSettingsService ?? throw new ArgumentNullException(nameof(hostSettingsService));
            this.hostSettings = hostSettings ?? throw new ArgumentNullException(nameof(hostSettings));
            this.eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // The replacement for AppDomain.CurrentDomain.DomainUnload: the owner disposes this
            // service when the host shuts down, which flushes the pending notifications.
            this.fileWatcher?.Dispose();
            this.fileWatcher = null;
            this.QueueTimerCallBack(null);
            this.queueTimer?.Dispose();
            this.queueTimer = null;
        }

        /// <summary>Starts watching the application folder.</summary>
        internal void Initialize()
        {
            if (this.fileWatcher == null)
            {
                lock (this.initializeLock)
                {
                    if (this.fileWatcher == null)
                    {
                        try
                        {
                            this.InitializeFileWatcher();
                        }
                        catch (Exception e)
                        {
                            Logger.DotNetNukeSecurityInitializeFailed(e);
                        }
                    }
                }
            }
        }

        private static void LogException(Exception ex)
        {
            Logger.DotNetNukeSecurityWatcherActivityError(ex, ex?.Message);
        }

        private void InitializeFileWatcher()
        {
            this.fileWatcher = new FileSystemWatcher
            {
                Filter = "*.*",
                Path = this.applicationStatusInfo.ApplicationMapPath,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
            };

            this.fileWatcher.Created += this.WatcherOnCreated;
            this.fileWatcher.Renamed += this.WatcherOnRenamed;
            this.fileWatcher.Error += this.WatcherOnError;

            this.filesQueue = new Queue<string>();
            this.queueTimer = new Timer(this.QueueTimerCallBack);

            this.fileWatcher.EnableRaisingEvents = true;
        }

        private void QueueTimerCallBack(object obj)
        {
            try
            {
                if (this.filesQueue == null)
                {
                    return;
                }

                string[] items;
                lock (this.filesQueue)
                {
                    while (!this.queueTimer.Change(Timeout.Infinite, Timeout.Infinite))
                    {
                    }

                    Interlocked.Exchange(ref this.notificationSent, 0);
                    items = this.filesQueue.ToArray();
                    while (this.filesQueue.Count > 0)
                    {
                        this.filesQueue.Dequeue();
                    }
                }

                if (items.Length > 0)
                {
                    this.NotifyManager(items);
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        private void WatcherOnRenamed(object sender, RenamedEventArgs e)
        {
            this.CheckFile(e.FullPath);
        }

        private void WatcherOnCreated(object sender, FileSystemEventArgs e)
        {
            this.CheckFile(e.FullPath);
        }

        private void WatcherOnError(object sender, ErrorEventArgs e)
        {
            LogException(e.GetException());
        }

        private void CheckFile(string path)
        {
            try
            {
                if (this.IsRestrictedExtension(path))
                {
                    if (this.appStatus != UpgradeStatus.Install && this.appStatus != UpgradeStatus.Upgrade)
                    {
                        UpgradeStatus status;

                        try
                        {
                            status = this.applicationStatusInfo.Status;
                        }
                        catch (NullReferenceException)
                        {
                            status = UpgradeStatus.None;
                        }

                        // make status sticky; once set to install/upgrade, it stays so until finishing & appl restarts
                        if (status == UpgradeStatus.Install || status == UpgradeStatus.Upgrade)
                        {
                            this.appStatus = status;
                        }
                        else
                        {
                            ThreadPool.QueueUserWorkItem(_ => this.AddEventLog(path));
                            var val = Interlocked.Increment(ref this.notificationSent);
                            if (val <= 1)
                            {
                                // first notification goes immediately
                                ThreadPool.QueueUserWorkItem(_ => this.NotifyManager([path]));
                            }
                            else
                            {
                                lock (this.filesQueue)
                                {
                                    while (!this.queueTimer.Change(val >= 100 ? 1 : SlidingDelay, Timeout.Infinite))
                                    {
                                    }

                                    this.filesQueue.Enqueue(path);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        private bool IsRestrictedExtension(string path)
        {
            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            return !string.IsNullOrEmpty(extension) &&
                this.GetRestrictExtensions().Contains(extension);
        }

        private IEnumerable<string> GetRestrictExtensions()
        {
            // obtain the setting and do calculations once every 5 minutes at most, plus no need for locking
            if ((DateTime.Now - this.lastRead).TotalMinutes > CacheTimeOut)
            {
                this.lastRead = DateTime.Now;
                var settings = this.hostSettingsService.GetString("SA_RestrictExtensions", string.Empty);
                this.settingsRestrictExtensions = string.IsNullOrEmpty(settings)
                    ? DefaultRestrictExtensions
                    : settings.ToLowerInvariant()
                        .Split(',')
                        .Where(e => !string.IsNullOrEmpty(e))
                        .Select(e => e.Trim())
                        .Concat(DefaultRestrictExtensions)
                        .ToList();
            }

            return this.settingsRestrictExtensions ?? DefaultRestrictExtensions;
        }

        private void AddEventLog(string path)
        {
            try
            {
                var log = new LogInfo
                {
                    LogTypeKey = nameof(EventLogType.HOST_ALERT),
                };
                log.AddProperty("Summary", Localization.GetString("PotentialDangerousFile.Text"));
                log.AddProperty("File Name", path);

                this.eventLogger.AddLog(log);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        private void NotifyManager(string[] paths)
        {
            try
            {
                var pathNames = string.Join("<br/>", paths);
                var subject = Localization.GetString("RestrictFileMail_Subject.Text");
                var body = Localization.GetString("RestrictFileMail_Body.Text")
                    .Replace("[Path]", pathNames)
                    .Replace("[AppName]", this.hostSettings.HostTitle)
                    .Replace("[AppVersion]", DotNetNukeContext.Current.Application.CurrentVersion.ToString());

                Mail.SendEmail(this.hostSettings.HostEmail, this.hostSettings.HostEmail, subject, body);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }
    }
}
