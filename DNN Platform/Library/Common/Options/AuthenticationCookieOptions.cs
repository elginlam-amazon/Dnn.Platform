// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Options
{
    /// <summary>
    /// The replacement for the <c>web.config</c>
    /// <c>&lt;system.web&gt;&lt;authentication mode="Forms"&gt;&lt;forms …&gt;</c> element, which does
    /// not exist on modern .NET. These values configure the platform's cookie authentication.
    /// </summary>
    /// <remarks>
    /// The defaults are the values DNN shipped in its own <c>web.config</c>
    /// (<c>name=".DOTNETNUKE" protection="All" timeout="60"</c>), so an existing installation
    /// that does not add an <c>Authentication:Cookie</c> section keeps its cookie name and
    /// therefore its existing sessions.
    /// </remarks>
    public class AuthenticationCookieOptions
    {
        /// <summary>The configuration section name.</summary>
        public const string SectionName = "Authentication:Cookie";

        /// <summary>Gets or sets the authentication cookie name (legacy <c>forms/@name</c>).</summary>
        public string Name { get; set; } = ".DOTNETNUKE";

        /// <summary>Gets or sets the cookie domain, or empty for the current host (legacy <c>forms/@domain</c>).</summary>
        public string Domain { get; set; } = string.Empty;

        /// <summary>Gets or sets the cookie path (legacy <c>forms/@path</c>).</summary>
        public string Path { get; set; } = "/";

        /// <summary>Gets or sets a value indicating whether the cookie is only sent over HTTPS (legacy <c>forms/@requireSSL</c>).</summary>
        public bool RequireSsl { get; set; }

        /// <summary>Gets or sets the authentication timeout in minutes (legacy <c>forms/@timeout</c>).</summary>
        public int TimeoutMinutes { get; set; } = 60;

        /// <summary>Gets or sets a value indicating whether the expiration is renewed on activity (legacy <c>forms/@slidingExpiration</c>).</summary>
        public bool SlidingExpiration { get; set; } = true;

        /// <summary>Gets or sets the login page URL (legacy <c>forms/@loginUrl</c>).</summary>
        public string LoginUrl { get; set; } = "~/Login";
    }
}
