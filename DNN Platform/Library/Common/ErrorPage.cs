// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common
{
    using System;
    using System.Net;
    using System.Text;

    using DotNetNuke.Common.Options;

    /// <summary>
    /// Resolves the URL of the platform's friendly error page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The error page itself lives in the host web project, not in this library, so its path is a
    /// configuration value rather than a literal repeated at each call site. The default
    /// (<c>~/ErrorPage.aspx</c>) is the page the host still serves today; when the host project is
    /// migrated and the page becomes a routable component, only
    /// <c>CustomErrors:ErrorPagePath</c> in appsettings.json has to change — no code does.
    /// </para>
    /// <para>
    /// This replaces six hardcoded <c>~/ErrorPage.aspx</c> literals that would each have had to be
    /// found and edited, and which would otherwise have 404'd silently after the host converted.
    /// </para>
    /// </remarks>
    public static class ErrorPage
    {
        /// <summary>The error page path used when <c>CustomErrors:ErrorPagePath</c> is not configured.</summary>
        public const string DefaultPath = "~/ErrorPage.aspx";

        /// <summary>Gets the application-relative path of the friendly error page.</summary>
        /// <remarks>
        /// Reads <c>CustomErrors:ErrorPagePath</c>, falling back to <see cref="DefaultPath"/> so the
        /// platform always has a usable error page even before the setting is added.
        /// </remarks>
        public static string Path
        {
            get
            {
                if (!ConfigurationSource.IsRegistered)
                {
                    return DefaultPath;
                }

                var configured = ConfigurationSource.Current?
                    .GetSection(CustomErrorsOptions.SectionName)?[nameof(CustomErrorsOptions.ErrorPagePath)];

                return string.IsNullOrWhiteSpace(configured) ? DefaultPath : configured;
            }
        }

        /// <summary>Builds the error page URL, appending the status and message query parameters.</summary>
        /// <param name="status">The HTTP status code to report, or <see langword="null"/> to omit it.</param>
        /// <param name="error">The primary error message, or <see langword="null"/> to omit it.</param>
        /// <param name="error2">The secondary error message, or <see langword="null"/> to omit it.</param>
        /// <returns>The application-relative error page URL.</returns>
        public static string BuildUrl(int? status = null, string error = null, string error2 = null)
        {
            var builder = new StringBuilder(Path);
            var separator = '?';

            void Append(string name, string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return;
                }

                builder.Append(separator).Append(name).Append('=').Append(WebUtility.UrlEncode(value));
                separator = '&';
            }

            Append("status", status?.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append("error", error);
            Append("error2", error2);

            return builder.ToString();
        }
    }
}
