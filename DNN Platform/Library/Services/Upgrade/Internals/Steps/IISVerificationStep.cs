// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.Upgrade.Internals.Steps
{
    using System;
    using System.Globalization;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Hosting.Server;
    using Microsoft.AspNetCore.Hosting.Server.Features;
    using Microsoft.Extensions.DependencyInjection;

    using Localization = DotNetNuke.Services.Localization.Localization;

    /// <summary>Performs verifications about the web server environment.</summary>
    /// <remarks>
    /// <para>
    /// On .NET Framework this step verified two things: that IIS was running the application in
    /// <em>integrated pipeline</em> mode (<c>HttpRuntime.UsingIntegratedPipeline</c>) and that
    /// .NET Framework 4.8 or later was installed (read from the <c>NDP\v4\Full</c> registry key).
    /// Neither API exists on modern .NET, and neither condition can be false in the same way:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// There is no classic/integrated pipeline distinction. ASP.NET Core is hosted by Kestrel,
    /// HTTP.sys, or IIS via the ASP.NET Core Module — all of which are "integrated". What this
    /// step can still verify meaningfully is that a web server implementation is actually present
    /// and listening, which is checked through <see cref="IServer"/> and
    /// <see cref="IServerAddressesFeature"/>. The detected server is reported in
    /// <see cref="BaseInstallationStep.Details"/>; the step only fails when no server is resolvable
    /// at all, which means the application is not hosted by ASP.NET Core and DNN cannot run.
    /// </description></item>
    /// <item><description>
    /// The .NET runtime version is read from <see cref="Environment.Version"/>, which reports the
    /// true version on every platform (the registry probe was Windows-only and would have thrown
    /// on Linux).
    /// </description></item>
    /// </list>
    /// </remarks>
    public class IISVerificationStep : BaseInstallationStep
    {
        /// <summary>The oldest .NET runtime this version of DNN supports.</summary>
        private static readonly Version MinimumRuntimeVersion = new Version(8, 0);

        /// <summary>Executes verifications on the web server environment.</summary>
        public override void Execute()
        {
            this.Status = StepStatus.Running;

            this.Details = Localization.GetString("CheckingIIS", this.LocalInstallResourceFile);

            // Verify that an ASP.NET Core web server implementation is hosting the application.
            // This replaces the integrated-pipeline check: a modern host either has a server or
            // is not a web application at all.
            var serverDescription = GetWebServerDescription();
            if (string.IsNullOrEmpty(serverDescription))
            {
                this.Errors.Add(Localization.GetString("IISVerificationFail", this.LocalInstallResourceFile));
                this.Status = StepStatus.Abort;
                return;
            }

            this.Details += string.Format(CultureInfo.CurrentCulture, " ({0})", serverDescription);

            // Check the .NET runtime version.
            if (GetRuntimeVersion() < MinimumRuntimeVersion)
            {
                this.Errors.Add(Localization.GetString("DotNetVersion48Required", this.LocalInstallResourceFile));
                this.Status = StepStatus.Abort;
                return;
            }

            this.Status = StepStatus.Done;
        }

        /// <summary>Describes the web server implementation hosting this application, and the addresses it listens on.</summary>
        /// <returns>A human readable description, or <see cref="string.Empty"/> when no ASP.NET Core server is hosting the application.</returns>
        private static string GetWebServerDescription()
        {
            var services = Globals.GetCurrentServiceProvider();
            var server = services?.GetService<IServer>();
            if (server == null)
            {
                return string.Empty;
            }

            var description = server.GetType().Name;

            var addresses = server.Features?.Get<IServerAddressesFeature>()?.Addresses;
            if (addresses != null && addresses.Count > 0)
            {
                description += ": " + string.Join(", ", addresses);
            }

            return description;
        }

        /// <summary>Gets the version of the .NET runtime the application is executing on.</summary>
        /// <returns>The runtime version.</returns>
        private static Version GetRuntimeVersion()
        {
            return Environment.Version;
        }
    }
}
