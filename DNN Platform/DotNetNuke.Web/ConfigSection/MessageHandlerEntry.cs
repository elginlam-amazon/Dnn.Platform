// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.ConfigSection
{
    using DotNetNuke.Web.Api;
    using Microsoft.Extensions.Configuration;

    /// <summary>An auth message handler entry of the <see cref="AuthServicesOptions"/>.</summary>
    /// <remarks>
    /// This was a <c>System.Configuration.ConfigurationElement</c> bound to
    /// <c>&lt;dotnetnuke&gt;&lt;authServices&gt;&lt;messageHandlers&gt;&lt;add …/&gt;</c>. It is now a
    /// plain options type bound from the <c>DotNetNuke:authServices:messageHandlers</c> array of
    /// <c>appsettings.json</c>; the entry shape (<c>name</c>/<c>type</c>/<c>enabled</c>/
    /// <c>defaultInclude</c>/<c>forceSSL</c>) and every property name are unchanged, so
    /// <c>ServicesRoutingManager.RegisterAuthenticationHandlers</c> reads it exactly as before.
    /// </remarks>
    public class MessageHandlerEntry
    {
        /// <summary>Gets or sets the name of the message handler.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the fully qualified name of the class that implements this message handler.</summary>
        /// <remarks>Bound from the <c>type</c> key, which is what the legacy <c>&lt;add type="…"/&gt;</c> attribute was called.</remarks>
        [ConfigurationKeyName("type")]
        public string ClassName { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether this message handler is enabled.</summary>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets a value indicating whether this is automatically included when the <see cref="DnnAuthorizeAttribute"/> does not name an auth type.</summary>
        public bool DefaultInclude { get; set; }

        /// <summary>Gets or sets a value indicating whether this message handler always uses HTTPS.</summary>
        /// <remarks>Bound from <c>forceSSL</c> (configuration binding is case-insensitive). Defaults to <see langword="true"/>, as the legacy element did.</remarks>
        public bool ForceSsl { get; set; } = true;
    }
}
