// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Servers.Components.WebServer
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Net;
    using System.Reflection;
    using System.Security.Principal;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Http;

    public class ServerInfo
    {
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string Framework => Environment.Version.ToString();

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string NetFrameworkVersion => Globals.FormattedNetFrameworkVersion;

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string HostName => Dns.GetHostName();

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string Identity => GetProcessIdentity();

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string IISVersion => GetServerSoftware();

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string OSVersion => Environment.OSVersion.ToString();

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string PhysicalPath => Globals.ApplicationMapPath;

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string Url => GetUrl();

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string RelativePath => string.IsNullOrEmpty(Globals.ApplicationPath) ? "/" : Globals.ApplicationPath;

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string ServerTime => DateTime.Now.ToString(CultureInfo.CurrentCulture);

        /// <summary>
        /// Gets the identity the worker process runs as. The legacy implementation used
        /// <see cref="WindowsIdentity.GetCurrent()"/>, which only exists on Windows; the Windows
        /// value is kept identical and other platforms report the OS user of the process.
        /// </summary>
        /// <returns>The process identity name.</returns>
        private static string GetProcessIdentity()
        {
            if (OperatingSystem.IsWindows())
            {
                return WindowsIdentity.GetCurrent().Name;
            }

            return Environment.UserName;
        }

        /// <summary>
        /// Gets the web server software description. The legacy implementation read the IIS
        /// <c>SERVER_SOFTWARE</c> server variable, which has no ASP.NET Core equivalent: the
        /// value is taken from the <c>Server</c> response header when the host emits one, from
        /// the <c>SERVER_SOFTWARE</c> environment variable when the process is started by a
        /// front-end that publishes it, from the ASP.NET Core Module variables when hosted
        /// behind IIS, and otherwise from the in-process (Kestrel) server and its version.
        /// </summary>
        /// <returns>The server software description. Never <see langword="null"/>; never throws when there is no ambient request.</returns>
        private static string GetServerSoftware()
        {
            // HttpContextSource.Current is null outside of a request (e.g. the scheduler), which is a supported case here.
            var context = HttpContextSource.Current;
            var serverHeader = context?.Response?.Headers["Server"].ToString();
            if (!string.IsNullOrEmpty(serverHeader))
            {
                return serverHeader;
            }

            var fromEnvironment = Environment.GetEnvironmentVariable("SERVER_SOFTWARE");
            if (!string.IsNullOrEmpty(fromEnvironment))
            {
                return fromEnvironment;
            }

            var aspNetCoreVersion = typeof(HttpContext).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(HttpContext).Assembly.GetName().Version?.ToString();

            var hostedByIis = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_IIS_PHYSICAL_PATH"))
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_IIS_HTTPAUTH"));

            var server = hostedByIis ? "Microsoft-IIS (ASP.NET Core Module)" : "Kestrel";
            return string.IsNullOrEmpty(aspNetCoreVersion) ? server : $"{server} (ASP.NET Core {aspNetCoreVersion})";
        }

        /// <summary>
        /// Gets the domain name the server is reached by. Uses the current request when one is in
        /// flight, exactly as the legacy implementation did; when there is none (for example when
        /// this information is gathered from the scheduler) it reports the machine's DNS host
        /// name instead of throwing.
        /// </summary>
        /// <returns>The domain or host name.</returns>
        private static string GetUrl()
        {
            var request = HttpContextSource.Current?.Request;
            return request != null ? Globals.GetDomainName(request) : Dns.GetHostName();
        }
    }
}
