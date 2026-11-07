// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Options
{
    /// <summary>How the application renders unhandled errors. Replaces <c>System.Web.Configuration.CustomErrorsMode</c>.</summary>
    public enum CustomErrorsMode
    {
        /// <summary>Custom (friendly) error pages are shown to every client.</summary>
        On = 0,

        /// <summary>Custom error pages are disabled; the detailed error is shown to every client.</summary>
        Off = 1,

        /// <summary>Custom error pages are shown to remote clients only; local requests see the detailed error.</summary>
        RemoteOnly = 2,
    }

    /// <summary>
    /// The replacement for the web.config <c>&lt;system.web&gt;&lt;customErrors&gt;</c> element
    /// (<c>System.Web.Configuration.CustomErrorsSection</c>, which does not exist on modern .NET).
    /// </summary>
    public class CustomErrorsOptions
    {
        /// <summary>The configuration section name.</summary>
        public const string SectionName = "CustomErrors";

        /// <summary>Gets or sets the custom error mode. Defaults to <see cref="CustomErrorsMode.RemoteOnly"/>, matching ASP.NET.</summary>
        public CustomErrorsMode Mode { get; set; } = CustomErrorsMode.RemoteOnly;

        /// <summary>Gets or sets the URL to redirect to when no status-code-specific page is configured.</summary>
        public string DefaultRedirect { get; set; }

        /// <summary>
        /// Gets or sets the application-relative path of the platform's friendly error page.
        /// </summary>
        /// <remarks>
        /// The page lives in the host web project, not in this library. Defaults to
        /// <c>~/ErrorPage.aspx</c>, which is what the host serves today; change this once the host
        /// is migrated and the error page becomes a routable component. See
        /// <see cref="DotNetNuke.Common.ErrorPage"/>, which every error redirect in the platform
        /// goes through.
        /// </remarks>
        public string ErrorPagePath { get; set; } = DotNetNuke.Common.ErrorPage.DefaultPath;
    }
}
