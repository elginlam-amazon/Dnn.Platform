// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Providers.AspNetClientCapabilityProvider
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Describes the capabilities of the browser which issued a request.
    /// </summary>
    /// <remarks>
    /// This type replaces the .NET Framework <c>System.Web.HttpBrowserCapabilities</c> /
    /// <c>System.Web.Configuration.HttpCapabilitiesBase</c> types, which were backed by the
    /// <c>.browser</c> capability files of ASP.NET and have no equivalent on modern .NET. The
    /// values are populated by user-agent parsing, see
    /// <see cref="AspNetClientCapabilityProvider.GetBrowserCapabilities(Microsoft.AspNetCore.Http.IHeaderDictionary, string)"/>.
    /// </remarks>
    public class BrowserCapabilities
    {
        /// <summary>Initializes a new instance of the <see cref="BrowserCapabilities"/> class.</summary>
        public BrowserCapabilities()
        {
            this.Capabilities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Gets or sets the identifier of the browser, for example <c>Chrome</c>.</summary>
        public string Id { get; set; }

        /// <summary>Gets or sets the name of the browser, for example <c>Chrome</c>.</summary>
        public string Browser { get; set; }

        /// <summary>Gets or sets the version of the browser, for example <c>120.0</c>.</summary>
        public string Version { get; set; }

        /// <summary>Gets or sets a value indicating whether the browser runs on a mobile device.</summary>
        public bool IsMobileDevice { get; set; }

        /// <summary>Gets or sets the approximate width of the device screen, in pixels.</summary>
        public int ScreenPixelsWidth { get; set; }

        /// <summary>Gets or sets the approximate height of the device screen, in pixels.</summary>
        public int ScreenPixelsHeight { get; set; }

        /// <summary>Gets the raw capability values, keyed case-insensitively.</summary>
        /// <remarks>
        /// Mirrors the old <c>HttpCapabilitiesBase.Capabilities</c> dictionary; the entry with an
        /// empty key holds the raw user-agent string, as it did on ASP.NET.
        /// </remarks>
        public IDictionary<string, string> Capabilities { get; }
    }
}
