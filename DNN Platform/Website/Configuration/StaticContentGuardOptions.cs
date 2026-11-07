// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Website.Configuration
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The replacement for the three deny/cache <c>Web.config</c> files that used to sit inside the
    /// served content trees: <c>Portals/Web.config</c>, <c>DesktopModules/MVC/Web.config</c> and
    /// <c>Install/Web.config</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are SECURITY controls, not configuration tidy-up. On .NET Framework three separate
    /// mechanisms kept the content trees safe, and none of them exists on ASP.NET Core:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// the machine-level <c>HttpForbiddenHandler</c> mappings in <c>machine.config</c>/
    /// <c>applicationHost.config</c>, which refused <c>.cs</c>, <c>.ascx</c>, <c>.config</c>,
    /// <c>.resx</c>, <c>.resources</c> and friends for EVERY path in the application. This is the
    /// control that stopped source and data files under <c>DesktopModules/</c> and
    /// <c>Portals/</c> from being downloaded;
    /// </description></item>
    /// <item><description>
    /// DNN's own <c>SecureFolderProvider</c>, which stores protected uploads with a
    /// <c>.resources</c> suffix precisely because that extension was unfetchable;
    /// </description></item>
    /// <item><description>
    /// the <c>&lt;clientCache cacheControlMode="DisableCache"/&gt;</c> in
    /// <c>Install/Web.config</c>, which kept install/upgrade responses out of every cache.
    /// </description></item>
    /// </list>
    /// <para>
    /// ASP.NET Core's static-file middleware is deny-by-default for unknown content types, which
    /// covers much of (1) by accident — but "by accident" is not a security control: a single
    /// <c>ServeUnknownFileTypes</c> or a content-type mapping added later silently re-exposes the
    /// whole tree. The defaults below restate the rules explicitly so that they survive such a
    /// change, and they are bound from configuration so an operator can extend — never silently
    /// lose — them.
    /// </para>
    /// <para>
    /// The rules are PRESERVED exactly: nothing here denies a path that IIS served, and nothing
    /// here allows a path that IIS refused. In particular the <c>.aspx</c>/<c>.ashx</c>/
    /// <c>.asmx</c>/<c>.svc</c> extensions are deliberately NOT in the blocked list: those are no
    /// longer files on disk but routed endpoints (<c>Logoff.aspx</c>, <c>RSS.aspx</c>,
    /// <c>LinkClick.aspx</c>, <c>DnnImageHandler.ashx</c>, the PayPal IPN callback …), and blocking
    /// them would break the very URLs the migration preserves.
    /// </para>
    /// </remarks>
    public sealed class StaticContentGuardOptions
    {
        /// <summary>The configuration section these options bind to.</summary>
        public const string SectionName = "StaticContentGuards";

        /// <summary>
        /// Gets the file extensions that are never served over HTTP, from any path.
        /// </summary>
        /// <remarks>
        /// The ASP.NET <c>HttpForbiddenHandler</c> list, minus the handler extensions that are now
        /// routed endpoints. Source files, project files, database files, resource files and DNN's
        /// secure-upload/log extension.
        /// </remarks>
        public IList<string> BlockedExtensions { get; } = new List<string>
        {
            // Source and markup that must never be downloadable.
            ".cs", ".vb", ".ascx", ".master", ".asax", ".cshtml", ".vbhtml", ".razor",

            // Project and solution files.
            ".csproj", ".vbproj", ".sln", ".user", ".webinfo", ".licx",

            // Configuration and localization data.
            ".config", ".resx",

            // DNN's secure-folder storage AND its log files (Portals/_default/Logs/log.resources).
            ".resources",

            // Databases that live under App_Data.
            ".mdf", ".ldf", ".sdf",

            // DNN extension manifests.
            ".dnn", ".dnn5", ".dnn6", ".dnn7", ".dnn9",
        };

        /// <summary>
        /// Gets the path prefixes that are never served over HTTP, whatever the extension.
        /// </summary>
        /// <remarks>
        /// <c>App_Data</c> holds the portal database and install logs; <c>App_Code</c> held runtime
        /// compiled sources. Both were blocked wholesale by ASP.NET and neither may ever be served.
        /// </remarks>
        public IList<string> BlockedPathSegments { get; } = new List<string>
        {
            "App_Data", "App_Code", "App_GlobalResources", "App_LocalResources", "App_Browsers",
        };

        /// <summary>
        /// Gets the path prefixes whose responses must not be cached by any client or proxy.
        /// </summary>
        /// <remarks>
        /// The direct replacement for <c>Install/Web.config</c>'s
        /// <c>&lt;clientCache cacheControlMode="DisableCache"/&gt;</c>. A cached install or upgrade
        /// page is both a correctness problem (a stale wizard step) and an information-disclosure
        /// problem (the installation log is rendered into the page).
        /// </remarks>
        public IList<string> NoCachePathPrefixes { get; } = new List<string>
        {
            "/install",
        };
    }
}
