// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    /// <summary>
    /// The settings of the DNN Web Utility library, bound to the <c>DnnWebUtility</c> configuration section.
    /// </summary>
    /// <remarks>
    /// Replaces the <c>ClientAPI</c> app setting and the <c>Browser Caps/ClientAPICaps.config</c> file
    /// location lookups the WebForms implementation performed through
    /// <c>ConfigurationManager.AppSettings</c> and <c>Server.MapPath</c>.
    /// </remarks>
    public class DnnWebUtilityOptions
    {
        /// <summary>The name of the configuration section these options are bound to.</summary>
        public const string SectionName = "DnnWebUtility";

        /// <summary>
        /// Gets or sets a value indicating whether the Client API is disabled.
        /// </summary>
        /// <remarks>
        /// Equivalent to the legacy <c>ClientAPI</c> app setting being <c>"0"</c>.
        /// </remarks>
        public bool ClientApiDisabled { get; set; }

        /// <summary>
        /// Gets or sets the path the client side js files are served from.
        /// </summary>
        /// <remarks>
        /// When empty the path is composed from the request's path base, as the WebForms
        /// implementation composed it from <c>Request.ApplicationPath</c>.
        /// </remarks>
        public string ScriptPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the scripts are referenced as external files
        /// instead of being emitted from the assembly's embedded resources.
        /// </summary>
        public bool UseExternalScripts { get; set; }

        /// <summary>
        /// Gets or sets the path of a browser capabilities file overriding the embedded
        /// <c>ClientAPICaps.config</c> fallback.
        /// </summary>
        public string BrowserCapsPath { get; set; }
    }
}
