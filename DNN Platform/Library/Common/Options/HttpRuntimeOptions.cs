// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Options
{
    /// <summary>
    /// The replacement for the web.config <c>&lt;system.web&gt;&lt;httpRuntime&gt;</c> element
    /// (<c>System.Web.Configuration.HttpRuntimeSection</c>, which does not exist on modern .NET).
    /// </summary>
    /// <remarks>
    /// On ASP.NET Core the request-size limits these values used to drive live on
    /// <c>FormOptions</c> and on the server's own limits (Kestrel / IIS);
    /// <see cref="DotNetNuke.Common.DnnConfigurationServiceCollectionExtensions.AddDnnPlatformConfiguration"/>
    /// applies <see cref="MaxRequestLength"/> to both so the platform keeps a single source of
    /// truth for "how large an upload may be".
    /// </remarks>
    public class HttpRuntimeOptions
    {
        /// <summary>The configuration section name.</summary>
        public const string SectionName = "HttpRuntime";

        /// <summary>
        /// Gets or sets the file-change-notification mode. Purely informational on ASP.NET Core
        /// (there is no <c>FileChangesMonitor</c>), but preserved because the platform records and
        /// reports it during installation and upgrade.
        /// </summary>
        public string FcnMode { get; set; } = DotNetNuke.Common.Utilities.Config.FcnMode.NotSet.ToString();

        /// <summary>Gets or sets the maximum request size, in kilobytes (the legacy <c>maxRequestLength</c> unit).</summary>
        public long MaxRequestLength { get; set; } = 28672;

        /// <summary>Gets or sets the request size, in kilobytes, above which the body is buffered to disk.</summary>
        public long RequestLengthDiskThreshold { get; set; } = 80;

        /// <summary>Gets or sets the request execution timeout, in seconds.</summary>
        public int ExecutionTimeout { get; set; } = 1200;

        /// <summary>Gets or sets the maximum URL length, in characters.</summary>
        public int MaxUrlLength { get; set; } = 2048;

        /// <summary>Gets the maximum request size in bytes, for the ASP.NET Core server limits.</summary>
        public long MaxRequestLengthBytes => this.MaxRequestLength * 1024L;
    }
}
