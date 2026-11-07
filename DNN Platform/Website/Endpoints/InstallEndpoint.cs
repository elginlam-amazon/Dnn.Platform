// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable once CheckNamespace
namespace DotNetNuke.Services.Install
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using System.Xml;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Data;
    using DotNetNuke.Entities;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Maintenance.Telerik;
    using DotNetNuke.Maintenance.Telerik.Removal;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.Installer.Blocker;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Scheduling;
    using DotNetNuke.Services.Upgrade.InternalController.Steps;
    using DotNetNuke.Services.Upgrade.Internals;
    using DotNetNuke.Services.Upgrade.Internals.Steps;
    using DotNetNuke.Website;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.Http.Timeouts;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The endpoint which installs or upgrades DNN. It replaces <c>Install/install.aspx</c> and its
    /// 691-line code-behind (<c>DotNetNuke.Services.Install.Install</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// STREAMED PROGRESS IS THE WHOLE POINT OF THIS PAGE, and it is why this is an endpoint rather
    /// than a component. The page never rendered a control tree: it disabled response buffering and
    /// wrote the installation log to the open response as each step completed, so that an operator
    /// watching a multi-minute database install saw lines appear. That behaviour is preserved
    /// exactly, because the helpers that produce it - <see cref="HtmlUtils.WriteHeader"/>,
    /// <see cref="HtmlUtils.WriteFeedback(HttpResponse, int, string)"/>,
    /// <see cref="HtmlUtils.WriteSuccessError"/>, <see cref="HtmlUtils.WriteFooter"/> and
    /// <see cref="HtmlUtils.WriteKeepAlive"/> - already take an ASP.NET Core
    /// <see cref="HttpResponse"/>, call <c>DisableBuffering()</c> on it and flush after every line.
    /// The upgrade/install engine (<c>Upgrade.InstallDNN</c>, <c>Upgrade.UpgradeDNN</c>, each
    /// installation step) writes its own progress through those very helpers, so nothing had to be
    /// rewritten for the incremental output to keep working. A Blazor component could not do this:
    /// its markup is only flushed when the render completes.
    /// </para>
    /// <para>
    /// ACCESS RESTRICTION. The page carried no authorization attribute and none is added here,
    /// because adding one would make a fresh installation - which by definition has no users -
    /// impossible. What restricts it is unchanged and is enforced below: the
    /// <see cref="IApplicationStatusInfo.Status"/> switch means that once the application is
    /// installed (<see cref="UpgradeStatus.None"/>) the URL does nothing at all unless an explicit
    /// maintenance <c>mode</c> is requested, <see cref="CheckPermissions"/> aborts the install when
    /// the file system is not writable, and <see cref="InstallBlocker"/> refuses a second
    /// concurrent install. <c>HtmlUtils.WriteKeepAlive</c> matches on the literal substring
    /// "install.aspx?", which is a second reason the legacy path is preserved verbatim.
    /// </para>
    /// </remarks>
    public static class InstallEndpoint
    {
        /// <summary>The preserved installer URL. Linked from NoUpgrade() output and documented in upgrade instructions.</summary>
        public const string InstallPath = "/Install/install.aspx";

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]

        // ReSharper disable once InconsistentNaming
        internal static string UpgradeWizardLocalResourceFile = "~/Install/App_LocalResources/UpgradeWizard.aspx.resx";

        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(InstallEndpoint));
        private static readonly object InstallLocker = new object();

        /// <summary>Maps the installer endpoint onto its original URL.</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        public static IEndpointRouteBuilder MapDnnInstaller(this IEndpointRouteBuilder endpoints)
        {
            // DisableRequestTimeout() is the route-level counterpart of the page's
            // Server.ScriptTimeout = int.MaxValue; the per-request DisableTimeout() below covers
            // the case where a timeout policy is applied by configuration.
            endpoints.Map(InstallPath, HandleInstallAsync).DisableRequestTimeout();
            return endpoints;
        }

        /// <summary>Replaces <c>OnInit</c>: updates Newtonsoft and restores a deleted install config.</summary>
        /// <param name="context">The request.</param>
        /// <returns><see langword="true"/> when a redirect was issued and the request must stop.</returns>
        private static bool OnInit(HttpContext context)
        {
            if (Upgrade.Upgrade.UpdateNewtonsoftVersion())
            {
                // Was this.Response.Redirect(this.Request.RawUrl, true): re-request the URL so the
                // application restarts with the updated assembly. `true` ended the response, which
                // is the `return true` the caller acts on.
                context.Response.Redirect(RawUrl(context));
                return true;
            }

            // if previous config deleted create new empty one
            string installConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Install", "DotNetNuke.install.config");
            if (!File.Exists(installConfig))
            {
                File.Copy(installConfig + ".resources", installConfig);
            }

            return false;
        }

        /// <summary>Replaces <c>OnLoad</c>: the install/upgrade/maintenance-mode switch.</summary>
        private static Task HandleInstallAsync(HttpContext context)
        {
            var appStatus = context.RequestServices.GetRequiredService<IApplicationStatusInfo>();
            var eventLogger = context.RequestServices.GetRequiredService<IEventLogger>();
            var response = context.Response;

            if (OnInit(context))
            {
                return Task.CompletedTask;
            }

            Config.AddFCNMode(appStatus, Config.FcnMode.Single);

            // Was: int scriptTimeOut = this.Server.ScriptTimeout; ... this.Server.ScriptTimeout =
            // int.MaxValue; ... restore. ASP.NET Core has no per-thread script timeout; the
            // equivalent is the request timeout feature. Disabling it is per request, so there is
            // nothing to restore afterwards - the next request starts with the configured policy
            // again, which is exactly what restoring the old value achieved.
            var timeoutFeature = context.Features.Get<IHttpRequestTimeoutFeature>();

            string mode = string.Empty;
            if (context.Request.Query.ContainsKey("mode"))
            {
                mode = context.Request.Query["mode"].ToString().ToLowerInvariant();
            }

            // Disable Client side caching
            // Was this.Response.Cache.SetCacheability(HttpCacheability.ServerAndNoCache).
            response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            response.Headers.Pragma = "no-cache";
            response.Headers.Expires = "0";
            response.ContentType = "text/html; charset=utf-8";

            // Check mode is not Nothing
            if (mode == "none")
            {
                NoUpgrade(response);
            }
            else
            {
                // Set Script timeout to MAX value
                timeoutFeature?.DisableTimeout();

                switch (appStatus.Status)
                {
                    case UpgradeStatus.Install:
                        if (InstallApplication(context, appStatus, eventLogger))
                        {
                            return Task.CompletedTask;
                        }

                        // Force an App Restart
                        Config.Touch(appStatus);
                        break;
                    case UpgradeStatus.Upgrade:
                        if (UpgradeApplication(context, appStatus))
                        {
                            return Task.CompletedTask;
                        }

                        // Force an App Restart
                        Config.Touch(appStatus);
                        break;
                    case UpgradeStatus.None:
                        // Check mode
                        switch (mode)
                        {
                            case "addportal":
                                AddPortal(response, appStatus, eventLogger);
                                break;
                            case "installresources":
                                InstallResources(response);
                                break;
                            case "executescripts":
                                ExecuteScripts(response);
                                break;
                        }

                        break;
                    case UpgradeStatus.Error:
                        NoUpgrade(response);
                        break;
                }
            }

            return Task.CompletedTask;
        }

        private static void RegisterInstallBegining()
        {
            InstallBlocker.Instance.RegisterInstallBegining();
        }

        private static void RegisterInstallEnd()
        {
            InstallBlocker.Instance.RegisterInstallEnd();
        }

        private static ITelerikUtils CreateTelerikUtils()
        {
            return Globals.GetCurrentServiceProvider().GetRequiredService<ITelerikUtils>();
        }

        private static void SetHostSetting(string key, string value, bool isSecure = false)
        {
            var setting = new ConfigurationSetting
            {
                IsSecure = isSecure,
                Key = key,
                Value = value,
            };

            Globals.GetCurrentServiceProvider()
                .GetRequiredService<IHostSettingsService>()
                .Update(setting);
        }

        private static void ExecuteScripts(HttpResponse response)
        {
            // Start Timer
            Upgrade.Upgrade.StartTimer();

            // Write out Header
            HtmlUtils.WriteHeader(response, "executeScripts");

            Write(response, "<h2>Execute Scripts Status Report</h2>");
            Flush(response);

            string strProviderPath = DataProvider.Instance().GetProviderPath();
            if (!strProviderPath.StartsWith("ERROR:"))
            {
                Upgrade.Upgrade.ExecuteScripts(strProviderPath);
            }

            Write(response, "<h2>Execution Complete</h2>");
            Flush(response);

            // Write out Footer
            HtmlUtils.WriteFooter(response);
        }

        /// <summary>Installs the application.</summary>
        /// <returns><see langword="true"/> when a redirect/transfer was issued and the request must stop.</returns>
        private static bool InstallApplication(HttpContext context, IApplicationStatusInfo appStatus, IEventLogger eventLogger)
        {
            var response = context.Response;

            // the application uses a two step installation process. The first step is used to update
            // the Web.config with any configuration settings - which forces an application restart.
            // The second step finishes the installation process and provisions the site.
            string installationDate = Config.GetSetting("InstallationDate");

            if (installationDate == null || string.IsNullOrEmpty(installationDate))
            {
                string strError = Config.UpdateMachineKey(appStatus);
                if (string.IsNullOrEmpty(strError))
                {
                    // send a new request to the application to initiate step 2
                    response.Redirect(RawUrl(context));
                    return true;
                }
                else
                {
                    // 403-3 Error - Redirect to ErrorPage
                    // 403.3 means directory permissions issue
                    // Was HttpContext.Current.Server.Transfer("~/ErrorPage.aspx?status=403_3&error=" + strError).
                    // Server.Transfer does not exist on ASP.NET Core; the error page is a routed
                    // component now, so the browser is sent to it. The URL comes from
                    // DotNetNuke.Common.ErrorPage.Path (the CustomErrors:ErrorPagePath key), so it
                    // stays in step with the error page's route; the two query parameters are the
                    // same ones ErrorPage reads.
                    var strUrl = $"{DotNetNuke.Common.ErrorPage.Path}?status=403_3&error={WebUtility.UrlEncode(strError)}";
                    response.Clear();
                    response.Redirect(Globals.ResolveUrl(strUrl));
                    return true;
                }
            }
            else
            {
                bool cleanupLocker = false;
                try
                {
                    var synchConnectionString = new SynchConnectionStringStep();
                    synchConnectionString.Execute();
                    if (synchConnectionString.Status == StepStatus.AppRestart)
                    {
                        // send a new request to the application to initiate step 2
                        response.Redirect(RawUrl(context));
                        return true;
                    }

                    // Start Timer
                    Upgrade.Upgrade.StartTimer();

                    // Write out Header
                    HtmlUtils.WriteHeader(response, "install");

                    // get path to script files
                    string strProviderPath = DataProvider.Instance().GetProviderPath();
                    if (!strProviderPath.StartsWith("ERROR:"))
                    {
                        if (!CheckPermissions(response))
                        {
                            return false;
                        }

                        // Add the install blocker logic
                        lock (InstallLocker)
                        {
                            if (InstallBlocker.Instance.IsInstallInProgress())
                            {
                                WriteInstallationHeader(response);
                                WriteInstallationInProgress(response);
                                return false;
                            }

                            cleanupLocker = true;
                            RegisterInstallBegining();
                        }

                        var installConfig = InstallController.Instance.GetInstallConfig();

                        // Create Folder Mappings config
                        if (!string.IsNullOrEmpty(installConfig.FolderMappingsSettings))
                        {
                            FolderMappingsConfigController.Instance.SaveConfig(installConfig.FolderMappingsSettings);
                        }

                        Upgrade.Upgrade.InstallDNN(strProviderPath);

                        // remove en-US from portal if installing in a different language
                        if (!installConfig.InstallCulture.Equals("en-us", StringComparison.InvariantCultureIgnoreCase))
                        {
                            var locale = LocaleController.Instance.GetLocale("en-US");
                            Localization.RemoveLanguageFromPortal(eventLogger, 0, locale.LanguageId, true);
                        }

                        var installVersion = DataProvider.Instance().GetInstallVersion();
                        string strError = Config.UpdateInstallVersion(appStatus, installVersion);

                        // Adding FCN mode to web.config
                        strError += Config.AddFCNMode(appStatus, Config.FcnMode.Single);
                        if (!string.IsNullOrEmpty(strError))
                        {
                            Logger.InstallAddFcnModeErrorMessage(strError);
                        }

                        Write(response, "<h2>Installation Complete</h2>");
                        Write(response, "<br><br><h2><a href='../Default.aspx'>Click Here To Access Your Site</a></h2><br><br>");
                        Flush(response);

                        // remove installwizard files
                        Upgrade.Upgrade.DeleteInstallerFiles();

                        // log APPLICATION_START event
                        Initialize.LogStart();

                        // Start Scheduler
                        Initialize.StartScheduler(true);
                    }
                    else
                    {
                        // upgrade error
                        Write(response, "<h2>Upgrade Error: " + strProviderPath + "</h2>");
                        Flush(response);
                    }

                    // Write out Footer
                    HtmlUtils.WriteFooter(response);
                }
                finally
                {
                    if (cleanupLocker)
                    {
                        RegisterInstallEnd();
                    }
                }
            }

            return false;
        }

        private static void WriteInstallationHeader(HttpResponse response)
        {
            Write(response, "<h2>Version: " + Globals.FormatVersion(DotNetNukeContext.Current.Application.Version) + "</h2>");
            Flush(response);

            Write(response, "<br><br>");
            Write(response, "<h2>Installation Status Report</h2>");
            Flush(response);
        }

        private static void WriteInstallationInProgress(HttpResponse response)
        {
            HtmlUtils.WriteFeedback(
                response,
                0,
                Localization.GetString("ThereIsAInstallationCurrentlyInProgress.Error", Localization.GlobalResourceFile) + "<br>");
            Flush(response);
        }

        /// <summary>Verifies that the file system is writable, and reports the outcome into the streamed log.</summary>
        /// <param name="response">The response the outcome is streamed to.</param>
        /// <returns><see langword="true"/> when all permissions verified.</returns>
        /// <remarks>
        /// SECURITY CONTROL - preserved exactly. Same single verifier over the application root,
        /// same <see cref="FileSystemPermissionVerifier.VerifyAll"/> call, same feedback line, and
        /// the same meaning for the return value: the caller aborts the installation when this is
        /// <see langword="false"/>. The only change is the path lookup:
        /// <c>this.Server.MapPath("~")</c> becomes <see cref="HttpContextSource.MapPath"/>, which
        /// resolves against the content root (TASK 101).
        /// </remarks>
        private static bool CheckPermissions(HttpResponse response)
        {
            bool verified = new FileSystemPermissionVerifier(HttpContextSource.MapPath("~")).VerifyAll();
            HtmlUtils.WriteFeedback(
                response,
                0,
                "Checking File and Folder permissions " + (verified ? "<font color='green'>Success</font>" : "<font color='red'>Error!</font>") + "<br>");
            Flush(response);

            return verified;
        }

        /// <summary>Upgrades the application.</summary>
        /// <returns><see langword="true"/> when a redirect was issued and the request must stop.</returns>
        private static bool UpgradeApplication(HttpContext context, IApplicationStatusInfo appStatus)
        {
            var response = context.Response;
            bool cleanupLocker = false;
            try
            {
                if (Upgrade.Upgrade.RemoveInvalidAntiForgeryCookie())
                {
                    response.Redirect(RawUrl(context));
                    return true;
                }

                var databaseVersion = DataProvider.Instance().GetVersion();

                // Start Timer
                Upgrade.Upgrade.StartTimer();

                // Write out Header
                HtmlUtils.WriteHeader(response, "upgrade");

                // There could be an installation in progress
                lock (InstallLocker)
                {
                    if (InstallBlocker.Instance.IsInstallInProgress())
                    {
                        WriteInstallationHeader(response);
                        WriteInstallationInProgress(response);
                        return false;
                    }

                    cleanupLocker = true;
                    RegisterInstallBegining();
                }

                Write(response, "<h2>Current Assembly Version: " + Globals.FormatVersion(DotNetNukeContext.Current.Application.Version) + "</h2>");
                Flush(response);

                // get path to script files
                string strProviderPath = DataProvider.Instance().GetProviderPath();
                if (!strProviderPath.StartsWith("ERROR:"))
                {
                    // get current database version
                    var strDatabaseVersion = Globals.FormatVersion(databaseVersion);

                    Write(response, "<h2>Current Database Version: " + strDatabaseVersion + "</h2>");
                    Flush(response);

                    Write(response, "<br><br>");
                    Write(response, "<h2>Upgrade Status Report</h2>");
                    Flush(response);

                    // stop scheduler
                    SchedulingProvider.Instance().Halt("Stopped by Upgrade Process");

                    Upgrade.Upgrade.UpgradeDNN(strProviderPath, databaseVersion);

                    // Install optional resources if present
                    var packages = Upgrade.Upgrade.GetInstallPackages();
                    foreach (var package in packages)
                    {
                        Upgrade.Upgrade.InstallPackage(package.Key, package.Value.PackageType, true);
                    }

                    // calling GetInstallVersion after SQL scripts execution to ensure sp GetDatabaseInstallVersion exists
                    var installVersion = DataProvider.Instance().GetInstallVersion();
                    string strError = Config.UpdateInstallVersion(appStatus, installVersion);

                    // Adding FCN mode to web.config
                    strError += Config.AddFCNMode(appStatus, Config.FcnMode.Single);
                    if (!string.IsNullOrEmpty(strError))
                    {
                        Logger.InstallAddFcnModeErrorMessage(strError);
                    }

                    HtmlUtils.WriteFeedback(response, 2, "Replacing Digital Assets Manager with the new Resource Manager: ");
                    Globals.GetCurrentServiceProvider().GetService<IDamUninstaller>().Execute();
                    HtmlUtils.WriteSuccessError(response, true);

                    Write(response, "<br>");
                    Write(response, "<h2>Checking Security Aspects</h2>");
                    var telerikUtils = CreateTelerikUtils();
                    if (!telerikUtils.TelerikIsInstalled())
                    {
                        Write(response, LocalizeString("TelerikNotInstalledInfo"));
                        Write(response, "<br>");
                    }
                    else
                    {
                        var version = telerikUtils.GetTelerikVersion();
                        Write(response, "<strong>");
                        Write(response, LocalizeString("TelerikInstalledHeading"));
                        Write(response, "</strong><br>");
                        Write(response, LocalizeString("TelerikInstalledDetected"));
                        Write(response, " ");
                        Write(response, version.ToString());
                        Write(response, "<br>");

                        if (!telerikUtils.IsTelerikVersionVulnerable(version))
                        {
                            Write(response, LocalizeString("TelerikVersionNotKnownToBeVulnerableInfo"));
                            Write(response, "<br>");
                        }
                        else
                        {
                            SetHostSetting(DotNetNuke.Maintenance.Constants.TelerikUninstallOptionSettingKey, DotNetNuke.Maintenance.Constants.TelerikUninstallYesValue);
                            var assemblies = telerikUtils.GetAssembliesThatDependOnTelerik()
                                .Select(a => Path.GetFileName(a));

                            Write(response, LocalizeString("TelerikInstalledBulletin"));
                            Write(response, "<br>");

                            if (!assemblies.Any())
                            {
                                Write(response, LocalizeString("TelerikInstalledButNotUsedInfo"));
                                Write(response, "<br>");
                            }
                            else
                            {
                                Write(response, LocalizeString("TelerikInstalledAndUsedInfo"));
                                Write(response, "<br>");
                                foreach (var a in assemblies)
                                {
                                    Write(response, $"{a}<br/>");
                                }

                                Write(response, "<br>");
                                Write(response, LocalizeString("TelerikInstalledAndUsedWarning"));
                                Write(response, "<br>");
                            }
                        }
                    }

                    Write(response, "<br>");
                    Write(response, "<h2>Upgrade Complete</h2>");
                    Write(response, "<br><br><h2><a href='../Default.aspx'>Click Here To Access Your Site</a></h2><br><br>");

                    // remove installwizard files
                    Upgrade.Upgrade.DeleteInstallerFiles();

                    Flush(response);
                }
                else
                {
                    Write(response, "<h2>Upgrade Error: " + strProviderPath + "</h2>");
                    Flush(response);
                }

                // Write out Footer
                HtmlUtils.WriteFooter(response);
            }
            finally
            {
                if (cleanupLocker)
                {
                    RegisterInstallEnd();
                }
            }

            return false;
        }

        private static void AddPortal(HttpResponse response, IApplicationStatusInfo appStatus, IEventLogger eventLogger)
        {
            // Start Timer
            Upgrade.Upgrade.StartTimer();

            // Write out Header
            HtmlUtils.WriteHeader(response, "addPortal");
            Write(response, "<h2>Add Site Status Report</h2>");
            Flush(response);

            // install new portal(s)
            string strNewFile = $@"{appStatus.ApplicationMapPath}\Install\Portal\Portals.resources";
            if (File.Exists(strNewFile))
            {
                var xmlDoc = new XmlDocument { XmlResolver = null, };
                using (var xmlReader = XmlReader.Create(strNewFile, new XmlReaderSettings { XmlResolver = null, }))
                {
                    xmlDoc.Load(xmlReader);
                }

                // parse portal(s) if available
                var nodes = xmlDoc.SelectNodes("//dotnetnuke/portals/portal");
                if (nodes != null)
                {
                    foreach (XmlNode node in nodes)
                    {
                        if (node != null)
                        {
                            Upgrade.Upgrade.AddPortal(eventLogger, appStatus, node, true, 0, null);
                        }
                    }
                }

                // delete the file
                try
                {
                    File.SetAttributes(strNewFile, FileAttributes.Normal);
                    File.Delete(strNewFile);
                }
                catch (Exception ex)
                {
                    // error removing the file
                    Logger.InstallDeletePortalResourcesFileException(ex);
                }

                Write(response, "<h2>Installation Complete</h2>");
                Write(response, "<br><br><h2><a href='../Default.aspx'>Click Here To Access Your Site</a></h2><br><br>");
                Flush(response);
            }

            // Write out Footer
            HtmlUtils.WriteFooter(response);
        }

        private static void InstallResources(HttpResponse response)
        {
            // Start Timer
            Upgrade.Upgrade.StartTimer();

            // Write out Header
            HtmlUtils.WriteHeader(response, "installResources");

            Write(response, "<h2>Install Resources Status Report</h2>");
            Flush(response);

            // install new resources(s)
            var packages = Upgrade.Upgrade.GetInstallPackages();
            foreach (var package in packages)
            {
                Upgrade.Upgrade.InstallPackage(package.Key, package.Value.PackageType, true);
            }

            Write(response, "<h2>Installation Complete</h2>");
            Write(response, "<br><br><h2><a href='../Default.aspx'>Click Here To Access Your Site</a></h2><br><br>");
            Flush(response);

            // Write out Footer
            HtmlUtils.WriteFooter(response);
        }

        private static void NoUpgrade(HttpResponse response)
        {
            // get path to script files
            string strProviderPath = DataProvider.Instance().GetProviderPath();
            if (!strProviderPath.StartsWith("ERROR:"))
            {
                // get current database version
                try
                {
                    using (var dr = DataProvider.Instance().GetDatabaseVersion())
                    {
                        if (dr.Read())
                        {
                            // Write out Header
                            HtmlUtils.WriteHeader(response, "none");
                            string currentAssembly = DotNetNukeContext.Current.Application.Version.ToString(3);
                            string currentDatabase = dr["Major"] + "." + dr["Minor"] + "." + dr["Build"];

                            // do not show versions if the same to stop information leakage
                            if (currentAssembly == currentDatabase)
                            {
                                Write(response, "<h2>Current Assembly Version && current Database Version are identical.</h2>");
                            }
                            else
                            {
                                Write(response, "<h2>Current Assembly Version: " + currentAssembly + "</h2>");

                                // Call Upgrade with the current DB Version to upgrade an
                                // existing DNN installation
                                var strDatabaseVersion = ((int)dr["Major"]).ToString("00") + "." + ((int)dr["Minor"]).ToString("00") + "." + ((int)dr["Build"]).ToString("00");
                                Write(response, "<h2>Current Database Version: " + strDatabaseVersion + "</h2>");
                            }

                            Write(response, "<br><br><a href='Install.aspx?mode=Install'>Click Here To Upgrade DotNetNuke</a>");
                            Flush(response);
                        }
                        else
                        {
                            // Write out Header
                            HtmlUtils.WriteHeader(response, "noDBVersion");
                            Write(response, "<h2>Current Assembly Version: " + DotNetNukeContext.Current.Application.Version.ToString(3) + "</h2>");

                            Write(response, "<h2>Current Database Version: N/A</h2>");
                            Write(response, "<br><br><h2><a href='Install.aspx?mode=Install'>Click Here To Install DotNetNuke</a></h2>");
                            Flush(response);
                        }

                        dr.Close();
                    }
                }
                catch (Exception ex)
                {
                    // Write out Header
                    Logger.InstallNoUpgradeException(ex);
                    HtmlUtils.WriteHeader(response, "error");
                    Write(response, "<h2>Current Assembly Version: " + DotNetNukeContext.Current.Application.Version.ToString(3) + "</h2>");

                    Write(response, "<h2>" + ex.Message + "</h2>");
                    Flush(response);
                }
            }
            else
            {
                // Write out Header
                HtmlUtils.WriteHeader(response, "error");
                Write(response, "<h2>Current Assembly Version: " + DotNetNukeContext.Current.Application.Version.ToString(3) + "</h2>");

                Write(response, "<h2>" + strProviderPath + "</h2>");
                Flush(response);
            }

            // Write out Footer
            HtmlUtils.WriteFooter(response);
        }

        private static string LocalizeString(string key)
        {
            return Localization.GetString(key, UpgradeWizardLocalResourceFile);
        }

        /// <summary>Replaces <c>HttpRequest.RawUrl</c>, which does not exist on ASP.NET Core.</summary>
        private static string RawUrl(HttpContext context) =>
            (context.Request.PathBase + context.Request.Path + context.Request.QueryString).ToString();

        /// <summary>Replaces <c>HttpResponse.Write</c>, keeping the output unbuffered and streamed.</summary>
        /// <remarks>
        /// Mirrors the private helper <see cref="HtmlUtils"/> uses for exactly the same purpose, so
        /// that lines this file writes and lines the install engine writes interleave in order.
        /// </remarks>
        private static void Write(HttpResponse response, string text)
        {
            response.WriteAsync(text).GetAwaiter().GetResult();
        }

        /// <summary>Replaces <c>HttpResponse.Flush</c>: pushes the buffered progress line to the client.</summary>
        private static void Flush(HttpResponse response)
        {
            response.Body.FlushAsync().GetAwaiter().GetResult();
        }
    }
}
