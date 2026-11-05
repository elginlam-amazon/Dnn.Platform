// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Options
{
    /// <summary>
    /// The replacement for the web.config <c>&lt;system.web&gt;&lt;machineKey&gt;</c> element
    /// (<c>System.Web.Configuration.MachineKeySection</c>, which does not exist on modern .NET).
    /// </summary>
    /// <remarks>
    /// <para>
    /// On ASP.NET Core, the framework's own cookie/anti-forgery/token protection is handled by
    /// <c>Microsoft.AspNetCore.DataProtection</c> and needs no key material here. These values
    /// exist solely because the platform exposes the decryption key as an application-wide
    /// <em>passphrase</em> for <c>PortalSecurity.Encrypt</c>/<c>Decrypt</c> and for the
    /// host/portal "encrypted setting" storage, so it must stay stable across restarts and across
    /// every node of a web farm — exactly like the legacy <c>machineKey</c> did. Configure it
    /// through the data-protection-backed <c>MachineKey</c> section, and keep the value out of
    /// source control (environment variable, user secrets or a secrets manager).
    /// </para>
    /// </remarks>
    public class MachineKeyOptions
    {
        /// <summary>The configuration section name.</summary>
        public const string SectionName = "MachineKey";

        /// <summary>Gets or sets the passphrase used by the platform's symmetric encryption helpers.</summary>
        public string DecryptionKey { get; set; }

        /// <summary>Gets or sets the key used by the platform when signing/validating its own payloads.</summary>
        public string ValidationKey { get; set; }

        /// <summary>Gets or sets the name of the symmetric algorithm, e.g. <c>AES</c>.</summary>
        public string Decryption { get; set; } = "AES";

        /// <summary>Gets or sets the name of the validation (HMAC) algorithm, e.g. <c>SHA1</c>.</summary>
        public string Validation { get; set; } = "SHA1";
    }
}
