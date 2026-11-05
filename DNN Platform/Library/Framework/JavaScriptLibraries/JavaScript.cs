// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework.JavaScriptLibraries
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Abstractions.Pages;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Installer.Packages;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Skins;
    using DotNetNuke.UI.Skins.Controls;
    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.Client.ClientResourceManagement;
    using DotNetNuke.Web.Client.ResourceManager;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>Utility methods to requesting JavaScript libraries to be added to a page.</summary>
    public partial class JavaScript : IJavaScriptLibraryHelper
    {
        private const string ScriptPrefix = "JSL.";
        private const string LegacyPrefix = "LEGACY.";

        private readonly IHostSettings hostSettings;
        private readonly IHostSettingsService hostSettingsService;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;
        private readonly IPortalController portalController;
        private readonly IJavaScriptLibraryController javaScriptLibraryController;

        /// <summary>Initializes a new instance of the <see cref="JavaScript"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="javaScriptLibraryController">The JavaScript library controller.</param>
        public JavaScript(IHostSettings hostSettings, IHostSettingsService hostSettingsService, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IPortalController portalController, IJavaScriptLibraryController javaScriptLibraryController)
        {
            this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            this.hostSettingsService = hostSettingsService ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettingsService>();
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            this.portalController = portalController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>();
            this.javaScriptLibraryController = javaScriptLibraryController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IJavaScriptLibraryController>();
        }

        /// <summary>Initializes a new instance of the <see cref="JavaScript"/> class.</summary>
        protected JavaScript()
            : this(null, null, null, null, null, null)
        {
        }

        /// <summary>checks whether the script file is a known javascript library.</summary>
        /// <param name="jsname">script identifier.</param>
        /// <returns><see langword="true"/> if a library with the given name is installed, otherwise <see langword="false"/>.</returns>
        [DnnDeprecated(10, 0, 2, "Use overload taking IJavaScriptLibraryController")]
        public static partial bool IsInstalled(string jsname)
            => IsInstalled(null, jsname);

        /// <summary>checks whether the script file is a known javascript library.</summary>
        /// <param name="javaScriptLibraryController">The JavaScript library controller.</param>
        /// <param name="jsname">script identifier.</param>
        /// <returns><see langword="true"/> if a library with the given name is installed, otherwise <see langword="false"/>.</returns>
        public static bool IsInstalled(IJavaScriptLibraryController javaScriptLibraryController, string jsname)
        {
            javaScriptLibraryController ??= Globals.GetCurrentServiceProvider().GetRequiredService<IJavaScriptLibraryController>();
            var library = javaScriptLibraryController.GetLibrary(l => l.LibraryName.Equals(jsname, StringComparison.OrdinalIgnoreCase));
            return library != null;
        }

        /// <summary>determine whether to use the debug script for a file.</summary>
        /// <returns>whether to use the debug script.</returns>
        [DnnDeprecated(10, 0, 2, "Use overload taking IHostSettings")]
        public static partial bool UseDebugScript()
            => UseDebugScript(null);

        /// <summary>determine whether to use the debug script for a file.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <returns>whether to use the debug script.</returns>
        public static bool UseDebugScript(IApplicationStatusInfo appStatus)
            => UseDebugScript(appStatus, null);

        /// <summary>determine whether to use the debug script for a file.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <returns>whether to use the debug script.</returns>
        /// <remarks>
        /// Replaces <c>HttpContext.IsDebuggingEnabled</c> (the <c>&lt;compilation debug="true"&gt;</c>
        /// switch, which does not exist on ASP.NET Core) with <see cref="IHostSettings.DebugMode"/>,
        /// which is the same decision DNN's client-resource providers now make.
        /// </remarks>
        public static bool UseDebugScript(IApplicationStatusInfo appStatus, IHostSettings hostSettings)
        {
            appStatus ??= Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            if (appStatus.Status != UpgradeStatus.None)
            {
                return false;
            }

            hostSettings ??= Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            return hostSettings.DebugMode;
        }

        /// <summary>Returns the version of a javascript library from the database.</summary>
        /// <param name="jsname">the library name.</param>
        /// <returns>The highest version number of the library or <see cref="string.Empty"/>.</returns>
        [DnnDeprecated(10, 0, 2, "Use overload taking IApplicationStatusInfo")]
        public static partial string Version(string jsname)
            => Version(null, jsname);

        /// <summary>Returns the version of a javascript library from the database.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="jsname">the library name.</param>
        /// <returns>The highest version number of the library or <see cref="string.Empty"/>.</returns>
        public static string Version(IApplicationStatusInfo appStatus, string jsname)
        {
            appStatus ??= Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();

            var library = GetHighestVersionLibrary(appStatus, jsname);
            return library != null ? Convert.ToString(library.Version, CultureInfo.InvariantCulture) : string.Empty;
        }

        /// <summary>Requests a script to be added to the page.</summary>
        /// <param name="jsname">the library name.</param>
        [DnnDeprecated(10, 0, 2, "Use overload taking IApplicationStatusInfo")]
        public static partial void RequestRegistration(string jsname)
            => RequestRegistration(null, null, null, jsname);

        /// <summary>Requests a script to be added to the page.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="portalSettings">The portal settings.</param>
        /// <param name="jsname">the library name.</param>
        public static void RequestRegistration(IApplicationStatusInfo appStatus, IEventLogger eventLogger, IPortalSettings portalSettings, string jsname)
        {
            appStatus ??= Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            eventLogger ??= Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            portalSettings ??= Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>().GetCurrentSettings();

            // handle case where script has no JavaScript library
            switch (jsname)
            {
                case CommonJs.jQuery:
                    RequestRegistration(appStatus, eventLogger, portalSettings, CommonJs.jQueryMigrate);
                    break;
            }

            RequestRegistration(appStatus, eventLogger, portalSettings, jsname, null, SpecificVersion.Latest);
        }

        /// <summary>Requests a script to be added to the page.</summary>
        /// <param name="jsname">the library name.</param>
        /// <param name="version">the library's version.</param>
        [DnnDeprecated(10, 0, 2, "Use overload taking IApplicationStatusInfo")]
        public static partial void RequestRegistration(string jsname, Version version)
            => RequestRegistration(null, null, null, jsname, version);

        /// <summary>Requests a script to be added to the page.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="portalSettings">The portal settings.</param>
        /// <param name="jsname">the library name.</param>
        /// <param name="version">the library's version.</param>
        public static void RequestRegistration(IApplicationStatusInfo appStatus, IEventLogger eventLogger, IPortalSettings portalSettings, string jsname, Version version)
        {
            appStatus ??= Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            eventLogger ??= Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            portalSettings ??= Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>().GetCurrentSettings();
            RequestRegistration(appStatus, eventLogger, portalSettings, jsname, version, SpecificVersion.Exact);
        }

        /// <summary>Requests a script to be added to the page.</summary>
        /// <param name="jsname">the library name.</param>
        /// <param name="version">the library's version.</param>
        /// <param name="specific">
        /// how much of the <paramref name="version"/> to pay attention to.
        /// When <see cref="SpecificVersion.Latest"/> is passed, ignore the <paramref name="version"/>.
        /// When <see cref="SpecificVersion.LatestMajor"/> is passed, match the major version.
        /// When <see cref="SpecificVersion.LatestMinor"/> is passed, match the major and minor versions.
        /// When <see cref="SpecificVersion.Exact"/> is passed, match all parts of the version.
        /// </param>
        [DnnDeprecated(10, 0, 2, "Use overload taking IApplicationStatusInfo")]
        public static partial void RequestRegistration(string jsname, Version version, SpecificVersion specific)
            => RequestRegistration(null, null, null, jsname, version, specific);

        /// <summary>Requests a script to be added to the page.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="portalSettings">The portal settings.</param>
        /// <param name="jsname">the library name.</param>
        /// <param name="version">the library's version.</param>
        /// <param name="specific">
        /// how much of the <paramref name="version"/> to pay attention to.
        /// When <see cref="SpecificVersion.Latest"/> is passed, ignore the <paramref name="version"/>.
        /// When <see cref="SpecificVersion.LatestMajor"/> is passed, match the major version.
        /// When <see cref="SpecificVersion.LatestMinor"/> is passed, match the major and minor versions.
        /// When <see cref="SpecificVersion.Exact"/> is passed, match all parts of the version.
        /// </param>
        public static void RequestRegistration(IApplicationStatusInfo appStatus, IEventLogger eventLogger, IPortalSettings portalSettings, string jsname, Version version, SpecificVersion specific)
        {
            appStatus ??= Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            eventLogger ??= Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            portalSettings ??= Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>().GetCurrentSettings();

            switch (specific)
            {
                case SpecificVersion.Latest:
                    RequestHighestVersionLibraryRegistration(appStatus, jsname);
                    return;
                case SpecificVersion.LatestMajor:
                case SpecificVersion.LatestMinor:
                    if (RequestLooseVersionLibraryRegistration(appStatus, eventLogger, portalSettings, jsname, version, specific))
                    {
                        return;
                    }

                    break;
                case SpecificVersion.Exact:
                    RequestSpecificVersionLibraryRegistration(eventLogger, portalSettings, jsname, version);
                    return;
            }

            // this should only occur if packages are incorrect or a RequestRegistration call has a typo
            LogCollision(eventLogger, portalSettings, $"Missing specific version library - {jsname},{version},{specific}");
        }

        /// <summary>method is called once per page event cycle and will load all scripts requested during that page processing cycle.</summary>
        /// <param name="context">The HTTP context of the current request.</param>
        [DnnDeprecated(10, 0, 2, "Use overload taking IHostSettings")]
        public static partial void Register(HttpContext context)
            => Register(null, null, null, null, null, context);

        /// <summary>method is called once per page event cycle and will load all scripts requested during that page processing cycle.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="portalSettings">The portal settings.</param>
        /// <param name="context">The HTTP context of the current request.</param>
        public static void Register(IHostSettings hostSettings, IHostSettingsService hostSettingsService, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IPortalSettings portalSettings, HttpContext context)
        {
            hostSettings ??= Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            hostSettingsService ??= Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettingsService>();
            appStatus ??= Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            eventLogger ??= Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            portalSettings ??= Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>().GetCurrentSettings();

            var scripts = GetScriptVersions(appStatus);
            var finalScripts = ResolveVersionConflicts(eventLogger, portalSettings, scripts);
            foreach (var jsl in finalScripts)
            {
                RegisterScript(hostSettings, hostSettingsService, context, jsl);
            }
        }

        /// <summary>Gets the script path to the jQuery UI library.</summary>
        /// <param name="getMinFile">Whether to request the minified file.</param>
        /// <returns>The path or <see langword="null"/>.</returns>
        [DnnDeprecated(10, 0, 2, "Use overload taking IHostSettings")]
        public static partial string JQueryUIFile(bool getMinFile)
            => JQueryUIFile(null, null, getMinFile);

        /// <summary>Gets the script path to the jQuery UI library.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="getMinFile">Whether to request the minified file.</param>
        /// <returns>The path or <see langword="null"/>.</returns>
        public static string JQueryUIFile(IHostSettings hostSettings, IHostSettingsService hostSettingsService, bool getMinFile)
        {
            hostSettings ??= Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            hostSettingsService ??= Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettingsService>();
            return GetScriptPath(hostSettings, hostSettingsService, CommonJs.jQueryUI);
        }

        /// <summary>Gets the script path to the jQuery library.</summary>
        /// <returns>The path or <see langword="null"/>.</returns>
        [DnnDeprecated(10, 0, 2, "Use overload taking IHostSettings")]
        public static partial string GetJQueryScriptReference()
            => GetJQueryScriptReference(null, null);

        /// <summary>Gets the script path to the jQuery library.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <returns>The path or <see langword="null"/>.</returns>
        public static string GetJQueryScriptReference(IHostSettings hostSettings, IHostSettingsService hostSettingsService)
        {
            hostSettings ??= Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            hostSettingsService ??= Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettingsService>();
            return GetScriptPath(hostSettings, hostSettingsService, CommonJs.jQuery);
        }

        /// <summary>Gets the path for a JS library's primary script.</summary>
        /// <param name="libraryName">The name of the JS library.</param>
        /// <returns>The path or <see langword="null"/>.</returns>
        [DnnDeprecated(10, 0, 2, "Use overload taking IHostSettings")]
        public static partial string GetScriptPath(string libraryName)
            => GetScriptPath(null, null, libraryName);

        /// <summary>Gets the path for a JS library's primary script.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="libraryName">The name of the JS library.</param>
        /// <returns>The path or <see langword="null"/>.</returns>
        public static string GetScriptPath(IHostSettings hostSettings, IHostSettingsService hostSettingsService, string libraryName)
        {
            hostSettings ??= Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            hostSettingsService ??= Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettingsService>();

            var library = JavaScriptLibraryController.Instance.GetLibrary(jsl => jsl.LibraryName.Equals(libraryName, StringComparison.OrdinalIgnoreCase));
            if (library == null)
            {
                return null;
            }

            return GetScriptPath(hostSettings, hostSettingsService, library, HttpContextSource.Current?.Request);
        }

        /// <summary>Request one of the <see cref="ClientAPI.ClientNamespaceReferences"/> be added to the current page.</summary>
        /// <param name="context">The HTTP context of the current request.</param>
        /// <param name="reference">The reference to add.</param>
        /// <remarks>
        /// Was <c>RegisterClientReference(System.Web.UI.Page, …)</c>. The script registration goes
        /// through the client-resource controller (keyed off the request) and the page-scoped client
        /// variables/script blocks go through the request's <see cref="IClientApiPage"/>.
        /// </remarks>
        public static void RegisterClientReference(HttpContext context, ClientAPI.ClientNamespaceReferences reference)
        {
            if (context == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(RegisterClientReference)} requires a request in flight: client references are registered per request.");
            }

            switch (reference)
            {
                case ClientAPI.ClientNamespaceReferences.dnn:
                case ClientAPI.ClientNamespaceReferences.dnn_dom:
                    if (context.Items.ContainsKey(LegacyPrefix + "dnn.js"))
                    {
                        break;
                    }

                    ClientResourceManager.RegisterScript(context, ClientAPI.ScriptPath + "dnn.js", 12);
                    context.Items[LegacyPrefix + "dnn.js"] = true;

                    var page = ClientApiPageAccessor.GetPage(context, nameof(RegisterClientReference));
                    if (page != null)
                    {
                        page.RegisterClientScriptBlock(page.GetType(), "dnn.js", string.Empty);

                        if (!ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.SingleCharDelimiters))
                        {
                            ClientAPI.RegisterClientVariable(page, "__scdoff", "1", true);
                        }

                        if (!ClientAPI.UseExternalScripts)
                        {
                            ClientAPI.RegisterEmbeddedResource(page, "dnn.scripts.js", typeof(ClientAPI));
                        }
                    }

                    break;
                case ClientAPI.ClientNamespaceReferences.dnn_dom_positioning:
                    RegisterClientReference(context, ClientAPI.ClientNamespaceReferences.dnn);
                    ClientResourceManager.RegisterScript(context, ClientAPI.ScriptPath + "dnn.dom.positioning.js", 13);
                    break;
            }
        }

        /// <inheritdoc />
        bool IJavaScriptLibraryHelper.UseDebugScript()
            => UseDebugScript(this.appStatus);

        /// <inheritdoc />
        string IJavaScriptLibraryHelper.Version(string jsname)
            => Version(this.appStatus, jsname);

        /// <inheritdoc />
        void IJavaScriptLibraryHelper.RequestRegistration(string jsname)
            => RequestRegistration(this.appStatus, this.eventLogger, this.portalController.GetCurrentSettings(), jsname);

        /// <inheritdoc />
        void IJavaScriptLibraryHelper.RequestRegistration(string jsname, Version version)
            => RequestRegistration(this.appStatus, this.eventLogger, this.portalController.GetCurrentSettings(), jsname, version);

        /// <inheritdoc />
        void IJavaScriptLibraryHelper.RequestRegistration(string jsname, Version version, SpecificVersion specific)
            => RequestRegistration(this.appStatus, this.eventLogger, this.portalController.GetCurrentSettings(), jsname, version, specific);

        /// <inheritdoc />
        string IJavaScriptLibraryHelper.GetScriptPath(string libraryName)
            => GetScriptPath(this.hostSettings, this.hostSettingsService, libraryName);

        /// <inheritdoc />
        bool IJavaScriptLibraryHelper.IsInstalled(string jsname)
            => IsInstalled(this.javaScriptLibraryController, jsname);

        private static void RequestHighestVersionLibraryRegistration(IApplicationStatusInfo appStatus, string jsname)
        {
            var library = GetHighestVersionLibrary(appStatus, jsname);
            if (library != null)
            {
                AddItemRequest(library, false);
            }
            else
            {
                // covers case where upgrading to 7.2.0 and JSL's are not installed
                AddPreInstallOrLegacyItemRequest(jsname);
            }
        }

        private static bool RequestLooseVersionLibraryRegistration(IApplicationStatusInfo appStatus, IEventLogger eventLogger, IPortalSettings portalSettings, string jsname, Version version, SpecificVersion specific)
        {
            Func<JavaScriptLibrary, bool> isValidLibrary = specific == SpecificVersion.LatestMajor
                ? l => l.Version.Major == version.Major && l.Version.Minor >= version.Minor
                : l => l.Version.Major == version.Major && l.Version.Minor == version.Minor && l.Version.Build >= version.Build;
            var library = JavaScriptLibraryController.Instance.GetLibraries(l => l.LibraryName.Equals(jsname, StringComparison.OrdinalIgnoreCase))
                                                              .OrderByDescending(l => l.Version)
                                                              .FirstOrDefault(isValidLibrary);
            if (library != null)
            {
                AddItemRequest(library, false);
                return true;
            }

            // unable to find a higher major version
            library = GetHighestVersionLibrary(appStatus, jsname);
            if (library != null)
            {
                AddItemRequest(library, false);
                LogCollision(eventLogger, portalSettings, $"Requested:{jsname}:{version}:{specific}.Resolved:{library.Version}");
                return true;
            }

            return false;
        }

        private static void RequestSpecificVersionLibraryRegistration(IEventLogger eventLogger, IPortalSettings portalSettings, string jsname, Version version)
        {
            var library = JavaScriptLibraryController.Instance.GetLibrary(l => l.LibraryName.Equals(jsname, StringComparison.OrdinalIgnoreCase) && l.Version == version);
            if (library != null)
            {
                AddItemRequest(library, true);
            }
            else
            {
                // this will only occur if a specific library is requested and not available
                LogCollision(eventLogger, portalSettings, $"Missing Library request - {jsname} : {version}");
            }
        }

        private static void AddItemRequest(JavaScriptLibrary library, bool forceVersion)
        {
            HttpContextSource.Current.Items[ScriptPrefix + library.JavaScriptLibraryID] = true;
            var controller = GetClientResourcesController();
            controller.CreateScript(GetScriptPath(null, null, library.LibraryName))
                .SetNameAndVersion(library.LibraryName, library.Version.ToString(), forceVersion)
                .SetProvider(GetScriptLocation(library))
                .SetPriority(GetFileOrder(library))
                .Register();
            var dependencies = GetAllDependencies(Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>(), library);
            foreach (var dependency in dependencies)
            {
                if (HttpContextSource.Current.Items[ScriptPrefix + dependency.JavaScriptLibraryID] == null)
                {
                    controller.CreateScript(GetScriptPath(null, null, dependency.LibraryName))
                        .SetNameAndVersion(dependency.LibraryName, dependency.Version.ToString(), false)
                        .SetProvider(GetScriptLocation(dependency))
                        .SetPriority(GetFileOrder(dependency))
                        .Register();
                }
            }
        }

        private static void AddPreInstallOrLegacyItemRequest(string jsl)
        {
            HttpContextSource.Current.Items[LegacyPrefix + jsl] = true;
        }

        private static List<JavaScriptLibrary> ResolveVersionConflicts(IEventLogger eventLogger, IPortalSettings portalSettings, IEnumerable<string> scripts)
        {
            var finalScripts = new List<JavaScriptLibrary>();
            foreach (var libraryId in scripts)
            {
                var processingLibrary = JavaScriptLibraryController.Instance.GetLibrary(l => l.JavaScriptLibraryID.ToString(CultureInfo.InvariantCulture) == libraryId);

                var existingLatestLibrary = finalScripts.FindAll(lib => lib.LibraryName.Equals(processingLibrary.LibraryName, StringComparison.OrdinalIgnoreCase))
                                                        .OrderByDescending(l => l.Version)
                                                        .SingleOrDefault();
                if (existingLatestLibrary != null)
                {
                    // determine previous registration for same JSL
                    if (existingLatestLibrary.Version > processingLibrary.Version)
                    {
                        // skip new library & log
                        var collisionText = string.Format(
                            CultureInfo.CurrentCulture,
                            "{0}-{1} -> {2}-{3}",
                            existingLatestLibrary.LibraryName,
                            existingLatestLibrary.Version,
                            processingLibrary.LibraryName,
                            processingLibrary.Version);
                        LogCollision(eventLogger, portalSettings, collisionText);
                    }
                    else if (existingLatestLibrary.Version != processingLibrary.Version)
                    {
                        finalScripts.Remove(existingLatestLibrary);
                        finalScripts.Add(processingLibrary);
                    }
                }
                else
                {
                    finalScripts.Add(processingLibrary);
                }
            }

            return finalScripts;
        }

        private static JavaScriptLibrary GetHighestVersionLibrary(IApplicationStatusInfo appStatus, string jsname)
        {
            if (appStatus.Status == UpgradeStatus.Install)
            {
                // if in install process, then do not use JSL but all use the legacy versions.
                return null;
            }

            try
            {
                return JavaScriptLibraryController.Instance.GetLibraries(l => l.LibraryName.Equals(jsname, StringComparison.OrdinalIgnoreCase))
                                                           .OrderByDescending(l => l.Version)
                                                           .FirstOrDefault();
            }
            catch (Exception)
            {
                // no library found (install or upgrade)
                return null;
            }
        }

        private static string GetScriptPath(IHostSettings hostSettings, IHostSettingsService hostSettingsService, JavaScriptLibrary js, HttpRequest request)
        {
            if (hostSettings.CdnEnabled)
            {
                // load custom CDN path setting
                var customCdn = hostSettingsService.GetString("CustomCDN_" + js.LibraryName);
                if (!string.IsNullOrEmpty(customCdn))
                {
                    return customCdn;
                }

                // cdn enabled but jsl does not have one defined
                if (!string.IsNullOrEmpty(js.CDNPath))
                {
                    var cdnPath = js.CDNPath;
                    if (cdnPath.StartsWith("//", StringComparison.Ordinal))
                    {
                        var useSecurePath = request == null || UrlUtils.IsSecureConnectionOrSslOffload(request);
                        cdnPath = $"{(useSecurePath ? "https" : "http")}:{cdnPath}";
                    }

                    return cdnPath;
                }
            }

            var formatVersion = Globals.FormatVersion(js.Version, "00", 3, "_");
            return $"~/Resources/libraries/{js.LibraryName}/{formatVersion}/{js.FileName}";
        }

        private static string GetScriptLocation(JavaScriptLibrary js)
        {
            switch (js.PreferredScriptLocation)
            {
                case ScriptLocation.PageHead:
                    return Abstractions.ClientResources.ClientResourceProviders.DnnPageHeaderProvider;
                case ScriptLocation.BodyBottom:
                    return Abstractions.ClientResources.ClientResourceProviders.DnnFormBottomProvider;
                case ScriptLocation.BodyTop:
                    return Abstractions.ClientResources.ClientResourceProviders.DnnBodyProvider;
            }

            return string.Empty;
        }

        private static List<string> GetScriptVersions(IApplicationStatusInfo appStatus)
        {
            var orderedScripts = (
                from object item in HttpContextSource.Current.Items.Keys
                where item.ToString().StartsWith(ScriptPrefix, StringComparison.Ordinal)
                select item.ToString().Substring(4))
                .ToList();
            orderedScripts.Sort();
            var finalScripts = orderedScripts.ToList();
            foreach (var libraryId in orderedScripts)
            {
                // find dependencies
                var library = JavaScriptLibraryController.Instance.GetLibrary(l => l.JavaScriptLibraryID.ToString(CultureInfo.InvariantCulture) == libraryId);
                if (library == null)
                {
                    continue;
                }

                foreach (var dependencyLibrary in GetAllDependencies(appStatus, library).Distinct())
                {
                    if (HttpContextSource.Current.Items[ScriptPrefix + "." + dependencyLibrary.JavaScriptLibraryID] == null)
                    {
                        finalScripts.Add(dependencyLibrary.JavaScriptLibraryID.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }

            return finalScripts;
        }

        private static IEnumerable<JavaScriptLibrary> GetAllDependencies(IApplicationStatusInfo appStatus, JavaScriptLibrary library)
        {
            var package = PackageController.Instance.GetExtensionPackage(Null.NullInteger, p => p.PackageID == library.PackageID);
            foreach (var dependency in package.Dependencies)
            {
                var dependencyLibrary = GetHighestVersionLibrary(appStatus, dependency.PackageName);
                yield return dependencyLibrary;

                foreach (var childDependency in GetAllDependencies(appStatus, dependencyLibrary))
                {
                    yield return childDependency;
                }
            }
        }

        private static void LogCollision(IEventLogger eventLogger, IPortalSettings portalSettings, string collisionText)
        {
            // need to log an event
            eventLogger.AddLog(
                "Javascript Libraries",
                collisionText,
                portalSettings,
                UserController.Instance.GetCurrentUserInfo().UserID,
                EventLogType.SCRIPT_COLLISION);
            var strMessage = Localization.GetString("ScriptCollision", Localization.SharedResourceFile);

            if (HttpContextSource.Current != null)
            {
                var pageService = Globals.GetCurrentServiceProvider().GetRequiredService<IPageService>();
                pageService.AddMessage(new PageMessage(string.Empty, strMessage, PageMessageType.Warning, string.Empty, PagePriority.Default));
            }
        }

        private static void RegisterScript(IHostSettings hostSettings, IHostSettingsService hostSettingsService, HttpContext context, JavaScriptLibrary jsl)
        {
            if (string.IsNullOrEmpty(jsl.FileName))
            {
                return;
            }

            ClientResourceManager.RegisterScript(context, GetScriptPath(hostSettings, hostSettingsService, jsl, context?.Request), GetFileOrder(jsl), GetScriptLocation(jsl), jsl.LibraryName, jsl.Version.ToString(3));

            if (hostSettings.CdnEnabled && !string.IsNullOrEmpty(jsl.ObjectName))
            {
                // The CDN fallback used to be a DnnJsIncludeFallback control inserted right after the
                // script loader control for the library's preferred location. On ASP.NET Core the
                // equivalent control is DnnJsIncludeFallbackTagHelper, which cannot be inserted from
                // code, so the identical markup it renders is handed to IPageService, which emits the
                // page's head. The priority keeps the fallback after the script it falls back for.
                var localPath = Globals.ResolveUrl("~/Resources/libraries/" + jsl.LibraryName + "/" + Globals.FormatVersion(jsl.Version, "00", 3, "_") + "/" + jsl.FileName);
                var pageService = (context?.RequestServices ?? Globals.GetCurrentServiceProvider()).GetRequiredService<IPageService>();
                pageService.AddToHead(new PageTag(BuildJsIncludeFallback(jsl.ObjectName, localPath), GetFileOrder(jsl) + 1));
            }
        }

        /// <summary>Builds the CDN fallback script block for a library.</summary>
        /// <param name="objectName">The JS object/function name that proves the library loaded.</param>
        /// <param name="fileName">The locally served path to fall back to.</param>
        /// <returns>The script markup.</returns>
        /// <remarks>Identical output to <see cref="DnnJsIncludeFallbackTagHelper"/>, which is the tag-helper form of the same control.</remarks>
        private static string BuildJsIncludeFallback(string objectName, string fileName)
        {
            var script = new StringBuilder();
            script.Append("<script type=\"text/javascript\">");
            script.Append(
                objectName.Contains('.')
                    ? $"if (typeof {objectName} != 'function') {{"
                    : $"if (typeof {objectName} == 'undefined') {{");
            script.Append(CultureInfo.InvariantCulture, $"document.write('<script src=\"{fileName}\" type=\"text/javascript\"></' + 'script>');");
            script.Append('}');
            script.Append("</script>");
            return script.ToString();
        }

        private static int GetFileOrder(JavaScriptLibrary jsl)
        {
            switch (jsl.LibraryName)
            {
                case CommonJs.jQuery:
                    return (int)Abstractions.ClientResources.FileOrder.Js.jQuery;
                case CommonJs.jQueryMigrate:
                    return (int)Abstractions.ClientResources.FileOrder.Js.jQueryMigrate;
                case CommonJs.jQueryUI:
                    return (int)Abstractions.ClientResources.FileOrder.Js.jQueryUI;
                case CommonJs.HoverIntent:
                    return (int)Abstractions.ClientResources.FileOrder.Js.HoverIntent;
                default:
                    return jsl.PackageID + (int)Abstractions.ClientResources.FileOrder.Js.DefaultPriority;
            }
        }

        private static IClientResourceController GetClientResourcesController()
        {
            var serviceProvider = Globals.GetCurrentServiceProvider();
            return serviceProvider.GetRequiredService<IClientResourceController>();
        }
    }
}
