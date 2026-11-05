// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Common
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common.Internal;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Data;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Urls;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.EventQueue;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Installer.Blocker;
    using DotNetNuke.Services.Log.EventLog;
    using DotNetNuke.Services.Scheduling;
    using DotNetNuke.Services.Upgrade;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using SchedulerMode = DotNetNuke.Abstractions.Application.SchedulerMode;

    /// <summary>The Object to initialize application.</summary>
    /// <remarks>
    /// <para>
    /// On .NET Framework this type was driven by <c>Global.asax</c> and an <c>HttpModule</c>:
    /// <c>Application_Start</c> called <see cref="Init()"/> and <c>Application_End</c> called
    /// <see cref="LogEnd()"/>. On modern .NET the host drives it instead:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Call <see cref="InitializeApplicationAsync"/> once from startup (e.g. from an <c>IHostedService.StartAsync</c>) to run the application-start sequence.</description></item>
    /// <item><description>Call <see cref="Init(HttpContext)"/> from request middleware so the first request still gets the install/upgrade redirect, exactly as the old module did.</description></item>
    /// <item><description>Call <see cref="LogEnd(string)"/> from <c>IHostApplicationLifetime.ApplicationStopping</c>; there is no <c>HttpRuntime</c>/<c>HostingEnvironment.ShutdownReason</c> on modern .NET.</description></item>
    /// </list>
    /// </remarks>
    public partial class Initialize
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<Initialize>();
        private static readonly object InitializeLock = new object();
        private static bool alreadyInitialized;

        /// <summary>Runs the application-start sequence if it has not run yet.</summary>
        /// <remarks>
        /// Safe to call when there is no current request; this is the entry point startup code
        /// should use. Returns the virtual path the caller should redirect to when the site needs
        /// to be installed or upgraded, otherwise <see cref="string.Empty"/>.
        /// </remarks>
        /// <returns>A redirect virtual path, or an empty string.</returns>
        public static string Init()
        {
            return Init(HttpContextSource.Current);
        }

        /// <summary>Inits the app.</summary>
        /// <param name="context">The current request context, or <see langword="null"/> when initializing outside of a request.</param>
        /// <returns>The redirect virtual path that was applied, or an empty string when none was needed.</returns>
        public static string Init(HttpContext context)
        {
            string redirect;
            var status = Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>().Status;

            // Check if app is initialised
            if (alreadyInitialized && status == UpgradeStatus.None)
            {
                return string.Empty;
            }

            lock (InitializeLock)
            {
                // Double-Check if app was initialised by another request
                if (alreadyInitialized && status == UpgradeStatus.None)
                {
                    return string.Empty;
                }

                // Initialize ...
                redirect = InitializeApp(context, ref alreadyInitialized);
            }

            if (!string.IsNullOrEmpty(redirect) && context != null && !context.Response.HasStarted)
            {
                context.Response.Redirect(ResolveApplicationUrl(redirect));
            }

            return redirect ?? string.Empty;
        }

        /// <summary>Runs the application-start sequence from startup, off the startup thread.</summary>
        /// <returns>A task that completes when the application-start sequence has finished.</returns>
        /// <remarks>
        /// The sequence (version check, globals, server activity, scheduler start, APPLICATION_START
        /// event log, Application_Start event queue messages, service route registration) can block
        /// for a long time, so it must not run inline on the host's startup path. Any failure is
        /// logged rather than taking the host down, which matches the .NET Framework behaviour where
        /// an initialization failure produced an error page rather than a dead worker process.
        /// </remarks>
        public static Task InitializeApplicationAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    Init(null);
                }
                catch (Exception exc)
                {
                    Exceptions.LogException(exc);
                }
            });
        }

        /// <summary>LogStart logs the Application Start Event.</summary>
        public static void LogStart()
        {
            var log = new LogInfo
            {
                BypassBuffering = true,
                LogTypeKey = nameof(EventLogType.APPLICATION_START),
            };
            LogController.Instance.AddLog(log);
        }

        /// <summary>LogEnd logs the Application Shutting Down Event.</summary>
        /// <remarks>
        /// Call this from <c>IHostApplicationLifetime.ApplicationStopping</c>. Modern .NET has no
        /// <c>HostingEnvironment.ShutdownReason</c> and no <c>HttpRuntime</c>, so there is no
        /// shutdown reason to report; use <see cref="LogEnd(string)"/> when the caller knows one.
        /// </remarks>
        public static void LogEnd()
        {
            LogEnd("The application is shutting down (IHostApplicationLifetime.ApplicationStopping).");
        }

        /// <summary>LogEnd logs the Application Shutting Down Event.</summary>
        /// <param name="shutdownDetail">A human readable description of why the application is shutting down.</param>
        public static void LogEnd(string shutdownDetail)
        {
            if (string.IsNullOrEmpty(shutdownDetail))
            {
                shutdownDetail = "The application is shutting down.";
            }

            try
            {
                var log = new LogInfo
                {
                    BypassBuffering = true,
                    LogTypeKey = nameof(EventLogType.APPLICATION_SHUTTING_DOWN),
                };
                log.AddProperty("Shutdown Details", shutdownDetail);
                LogController.Instance.AddLog(log);

                Logger.InitializeApplicationShuttingDown(shutdownDetail);
            }
            catch (Exception exc)
            {
                Exceptions.LogException(exc);
            }

            var status = Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>().Status;
            if (status != UpgradeStatus.Install)
            {
                // purge log buffer
                LoggingProvider.Instance().PurgeLogBuffer();
            }
        }

        /// <summary>Tests whether this request should be processed in the DNN request pipeline.</summary>
        /// <param name="request">The Http request.</param>
        /// <param name="allowUnknownExtensions">When false only .aspx, .asmx, .ashx, .svg are checked for Http Module processing.</param>
        /// <param name="checkOmitFromRewriteProcessing">A value indicating whether to check for "omit from rewrite" processing.</param>
        /// <returns>A value indicating whether the request should be processed in the DNN request pipeline.</returns>
        public static bool ProcessHttpModule(HttpRequest request, bool allowUnknownExtensions, bool checkOmitFromRewriteProcessing)
        {
            var localPath = GetLocalPath(request);
            if (localPath.EndsWith("webresource.axd", StringComparison.OrdinalIgnoreCase)
                    || localPath.EndsWith("scriptresource.axd", StringComparison.OrdinalIgnoreCase)
                    || localPath.EndsWith("captcha.aspx", StringComparison.OrdinalIgnoreCase)
                    || localPath.Contains("upgradewizard.aspx", StringComparison.OrdinalIgnoreCase)
                    || localPath.Contains("installwizard.aspx", StringComparison.OrdinalIgnoreCase)
                    || localPath.EndsWith("install.aspx", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!allowUnknownExtensions
                    && !localPath.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)
                    && !localPath.EndsWith(".asmx", StringComparison.OrdinalIgnoreCase)
                    && !localPath.EndsWith(".ashx", StringComparison.OrdinalIgnoreCase)
                    && !localPath.EndsWith(".svc", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !checkOmitFromRewriteProcessing || !RewriterUtils.OmitFromRewriteProcessing(localPath);
        }

        /// <summary>Attempts to run scheduled tasks when "Request Method" is used in the scheduler.</summary>
        /// <param name="request">The http request.</param>
        public static void RunSchedule(HttpRequest request)
        {
            if (!IsUpgradeOrInstallRequest(request))
            {
                try
                {
                    using var scope = Globals.GetOrCreateServiceScope();
                    var hostSettings = scope.ServiceProvider.GetRequiredService<IHostSettings>();

                    if (hostSettings.SchedulerMode == SchedulerMode.RequestMethod && SchedulingProvider.ReadyForPoll)
                    {
                        Logger.InitializeRunningSchedule(hostSettings.SchedulerMode);
                        var scheduler = SchedulingProvider.Instance();

                        // The .NET Framework implementation started a background Thread per poll.
                        // Task.Run queues the poll on the thread pool instead; ExecuteTasks is a
                        // no-op when the SchedulerHostedService dispatch loop is already running,
                        // so a request poll can never create a second, competing loop.
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                scheduler.ExecuteTasks();
                            }
                            catch (Exception exc)
                            {
                                Exceptions.LogException(exc);
                            }
                        });

                        SchedulingProvider.ScheduleLastPolled = DateTime.Now;
                    }
                }
                catch (Exception exc)
                {
                    Exceptions.LogException(exc);
                }
            }
        }

        /// <summary>StartScheduler starts the Scheduler.</summary>
        /// <param name="resetAppStartElapseTime">Whether reset app start elapse time before running schedule tasks.</param>
        public static void StartScheduler(bool resetAppStartElapseTime = false)
        {
            if (resetAppStartElapseTime)
            {
                Globals.ResetAppStartElapseTime();
            }

            var scheduler = SchedulingProvider.Instance();
            scheduler.RunEventSchedule(EventName.APPLICATION_START);

            using var scope = Globals.GetOrCreateServiceScope();
            var hostSettings = scope.ServiceProvider.GetRequiredService<IHostSettings>();

            // instantiate APPLICATION_START scheduled jobs
            if (hostSettings.SchedulerMode == SchedulerMode.TimerMethod)
            {
                Logger.InitializeRunningSchedule(hostSettings.SchedulerMode);

                // The .NET Framework implementation created a named background "Scheduler Thread"
                // here. On modern .NET SchedulerHostedService owns the dispatch loop; this call
                // starts it immediately if the hosted service has not picked it up yet, and is a
                // no-op when a loop is already active.
                _ = Task.Run(() =>
                {
                    try
                    {
                        scheduler.Start();
                    }
                    catch (Exception exc)
                    {
                        Exceptions.LogException(exc);
                    }
                });
            }
        }

        /// <summary>StopScheduler stops the Scheduler.</summary>
        public static void StopScheduler()
        {
            // stop scheduled jobs
            SchedulingProvider.Instance().Halt("Stopped by application shutdown");
        }

        private static string GetLocalPath(HttpRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            return request.Path.HasValue ? request.Path.Value : string.Empty;
        }

        /// <summary>Expands a DNN application-relative ("~/") virtual path into a rooted one.</summary>
        /// <param name="virtualPath">The virtual path.</param>
        /// <returns>A rooted virtual path.</returns>
        private static string ResolveApplicationUrl(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath) || !virtualPath.StartsWith("~/", StringComparison.Ordinal))
            {
                return virtualPath;
            }

            var applicationPath = HttpContextSource.ApplicationVirtualPath ?? string.Empty;
            applicationPath = applicationPath.TrimEnd('/');

            return applicationPath + virtualPath.Substring(1);
        }

        private static string CheckVersion(HttpContext context)
        {
            bool autoUpgrade = Config.GetSetting("AutoUpgrade") == null || bool.Parse(Config.GetSetting("AutoUpgrade"));
            bool useWizard = Config.GetSetting("UseInstallWizard") == null || bool.Parse(Config.GetSetting("UseInstallWizard"));

            // Determine the Upgrade status and redirect as necessary to InstallWizard.aspx
            string retValue = Null.NullString;
            var applicationStatusInfo = Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            switch (applicationStatusInfo.Status)
            {
                case UpgradeStatus.Install:
                    if (autoUpgrade || useWizard)
                    {
                        retValue = useWizard ? "~/Install/InstallWizard.aspx" : "~/Install/Install.aspx?mode=install";
                    }
                    else
                    {
                        CreateUnderConstructionPage();
                        retValue = "~/Install/UnderConstruction.htm";
                        LoggerMessages.InitializeUnderConstructionPageShownBecauseInstallationNeeded(Logger);
                    }

                    break;
                case UpgradeStatus.Upgrade:
                    if (autoUpgrade || useWizard)
                    {
                        retValue = useWizard ? "~/Install/UpgradeWizard.aspx" : "~/Install/Install.aspx?mode=upgrade";
                    }
                    else
                    {
                        CreateUnderConstructionPage();
                        retValue = "~/Install/UnderConstruction.htm";
                        LoggerMessages.InitializeUnderConstructionPageShownBecauseUpgradeNeeded(Logger);
                    }

                    break;
                case UpgradeStatus.Error:
                    // here we need to check if the application is already installed
                    // or is in pre-install state.
                    // see http://support.dotnetnuke.com/project/DNN/2/item/26053 for scenarios
                    bool isInstalled = applicationStatusInfo.IsInstalled();

                    if (!isInstalled && (useWizard || autoUpgrade))
                    {
                        // app has never been installed, and either Wizard or Autoupgrade is configured
                        CreateUnderConstructionPage();
                        retValue = "~/Install/UnderConstruction.htm";
                        Logger.InitializeUnderConstructionPageShownBecauseNoWorkingDatabaseConnection();
                    }
                    else if (context != null)
                    {
                        // 500 Error - Redirect to ErrorPage
                        if (!isInstalled)
                        {
                            Logger.InitializeConnectionToTheDatabaseHasFailedTheApplicationIsNotInstalledYetA500ErrorPageWillBeShown();
                        }
                        else
                        {
                            Logger.InitializeConnectionToTheDatabaseHasFailedHoweverTheApplicationIsAlreadyCompletelyInstalledA500ErrorPageWillBeShown();
                        }

                        // ASP.NET Core has no Server.Transfer; a redirect to the error page is the
                        // equivalent observable behaviour for the browser.
                        retValue = DotNetNuke.Common.ErrorPage.BuildUrl(500, "Site Unavailable", "Connection To The Database Failed");
                    }

                    break;
            }

            return retValue;
        }

        private static void CreateUnderConstructionPage()
        {
            // create an UnderConstruction page if it does not exist already
            var target = HttpContextSource.MapPath("~/Install/UnderConstruction.htm");
            var template = HttpContextSource.MapPath("~/Install/UnderConstruction.template.htm");
            if (!File.Exists(target) && File.Exists(template))
            {
                File.Copy(template, target);
            }
        }

        private static Version GetDatabaseEngineVersion()
        {
            return DataProvider.Instance().GetDatabaseEngineVersion();
        }

        /// <summary>Gets the version of the .NET runtime the application is executing on.</summary>
        /// <returns>The runtime version.</returns>
        /// <remarks>
        /// The .NET Framework implementation read the <c>NDP\v4\Full</c> "Release" DWORD out of the
        /// Windows registry because <see cref="Environment.Version"/> always reported 4.0.x. On
        /// modern .NET <see cref="Environment.Version"/> reports the real runtime version on every
        /// platform, so the registry probe (which is Windows-only and would throw or silently
        /// mis-report on Linux) is gone.
        /// </remarks>
        private static Version GetNETFrameworkVersion()
        {
            var version = Environment.Version;
            if (version.Major > 0)
            {
                return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
            }

            // Environment.Version should never be unset; fall back to parsing the framework
            // description (e.g. ".NET 10.0.0") rather than returning a bogus version.
            var description = RuntimeInformation.FrameworkDescription ?? string.Empty;
            foreach (var token in description.Split(' '))
            {
                if (Version.TryParse(token, out var parsed))
                {
                    return parsed;
                }
            }

            throw new InvalidOperationException($"Unable to determine the .NET runtime version from '{description}'.");
        }

        private static string InitializeApp(HttpContext context, ref bool initialized)
        {
            var request = context?.Request;

            Logger.InitializeRequest(GetLocalPath(request));

            // Don't process some of the AppStart methods if we are installing
            if (request != null && IsUpgradeOrInstallRequest(request))
            {
                // NET runtime version is needed by Upgrade
                Globals.NETFrameworkVersion = GetNETFrameworkVersion();
                Globals.IISAppName = GetApplicationName();
                Globals.OperatingSystemVersion = Environment.OSVersion.Version;

                return Null.NullString;
            }

            // Check whether the current App Version is the same as the DB Version
            var redirect = CheckVersion(context);
            if (!string.IsNullOrEmpty(redirect) || InstallBlocker.Instance.IsInstallInProgress())
            {
                return redirect;
            }

            LoggerMessages.InitializeApplicationInitializing(Logger);

            // Set globals
            Globals.IISAppName = GetApplicationName();
            Globals.OperatingSystemVersion = Environment.OSVersion.Version;
            Globals.NETFrameworkVersion = GetNETFrameworkVersion();
            Globals.DatabaseEngineVersion = GetDatabaseEngineVersion();

            Upgrade.CheckFipsCompilanceAssemblies();

            // Log Server information
            ServerController.UpdateServerActivity(new ServerInfo());

            // Start Scheduler
            StartScheduler();

            // Log Application Start
            LogStart();

            // Process any messages in the EventQueue for the Application_Start event
            using (var scope = Globals.DependencyProvider.CreateScope())
            {
                EventQueueController.ProcessMessages(scope.ServiceProvider, "Application_Start");
            }

            ServicesRoutingManager.RegisterServiceRoutes();

            // Set Flag so we can determine the first Page Request after Application Start
            if (context != null)
            {
                context.Items["FirstRequest"] = true;
            }

            LoggerMessages.InitializeApplicationInitialized(Logger);

            initialized = true;

            return redirect;
        }

        /// <summary>
        /// Gets the name that identifies this application within a web farm, previously read from
        /// the <c>APPL_MD_PATH</c> server variable (which modern .NET does not expose).
        /// </summary>
        /// <returns>The application's virtual path, which is what <see cref="Entities.Host.ServerController"/> uses it for.</returns>
        private static string GetApplicationName()
        {
            var applicationPath = HttpContextSource.ApplicationVirtualPath;
            return string.IsNullOrEmpty(applicationPath) ? "/" : applicationPath;
        }

        private static bool IsUpgradeOrInstallRequest(HttpRequest request)
        {
            var url = GetLocalPath(request);

            return url.EndsWith("/install.aspx", StringComparison.OrdinalIgnoreCase)
                || url.Contains("/upgradewizard.aspx", StringComparison.OrdinalIgnoreCase)
                || url.Contains("/installwizard.aspx", StringComparison.OrdinalIgnoreCase);
        }
    }
}
