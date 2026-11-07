// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Security.Components.Checks
{
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Audits whether request-level diagnostic tracing is exposing server internals to clients.
    /// </summary>
    /// <remarks>
    /// On .NET Framework this check had two probes, and neither mechanism exists on modern .NET:
    /// the WebForms <c>&lt;%@ Page Trace="true" %&gt;</c> directive of <c>Default.aspx</c>, and the
    /// <c>system.web/trace</c> configuration section (<c>trace.axd</c>) read through
    /// <c>WebConfigurationManager</c>. ASP.NET Core honours no <c>&lt;system.web&gt;</c>
    /// configuration and has no trace handler, so those two flags can no longer be the answer.
    /// <para>
    /// What the check protected against, however, is very much alive: verbose per-request
    /// diagnostics being rendered into the response where any visitor can read them. On this
    /// platform that is controlled by two switches, and this check reports on them:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// The DNN host <c>DebugMode</c> setting (<see cref="IHostSettings.DebugMode"/>), which the
    /// framework and extensions use to emit extra on-screen/diagnostic information.
    /// </description></item>
    /// <item><description>
    /// The hosting environment being <c>Development</c>, which is what enables the developer
    /// exception page and detailed error output for every request.
    /// </description></item>
    /// </list>
    /// <para>
    /// Either of those is reported as a <see cref="SeverityEnum.Failure"/> — the same severity the
    /// legacy check used for both of its probes — and a clean installation reports
    /// <see cref="SeverityEnum.Pass"/>, exactly as before. The severity is never left
    /// <see cref="SeverityEnum.Unverified"/> unless the state genuinely cannot be read.
    /// </para>
    /// </remarks>
    public class CheckTracing : IAuditCheck
    {
        private readonly IHostSettings hostSettings;

        /// <summary>Initializes a new instance of the <see cref="CheckTracing"/> class.</summary>
        public CheckTracing()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="CheckTracing"/> class.</summary>
        /// <param name="hostSettings">Provides information about host settings.</param>
        public CheckTracing(IHostSettings hostSettings)
        {
            this.hostSettings = hostSettings;
        }

        /// <inheritdoc />
        public string Id => "CheckTracing";

        /// <inheritdoc />
        public bool LazyLoad => false;

        /// <inheritdoc />
        public CheckResult Execute()
        {
            var result = new CheckResult(SeverityEnum.Unverified, this.Id);

            var settings = this.hostSettings ?? Globals.DependencyProvider?.GetService<IHostSettings>();
            var environment = Globals.DependencyProvider?.GetService<IWebHostEnvironment>();

            if (settings == null && environment == null)
            {
                result.Notes.Add("Neither the host settings nor the hosting environment could be read.");
                return result;
            }

            var debugMode = settings != null && settings.DebugMode;
            var isDevelopmentEnvironment = environment != null && environment.IsDevelopment();

            result.Severity = debugMode || isDevelopmentEnvironment
                ? SeverityEnum.Failure
                : SeverityEnum.Pass;

            if (debugMode)
            {
                result.Notes.Add("The host 'Debug Mode' setting is enabled, which emits verbose diagnostic output. Turn it off on production installations.");
            }

            if (isDevelopmentEnvironment)
            {
                result.Notes.Add("The application is running in the 'Development' hosting environment, which serves detailed error and diagnostic pages to clients. Use 'Production' on public installations.");
            }

            if (settings == null)
            {
                result.Notes.Add("The host 'Debug Mode' setting could not be read; only the hosting environment was evaluated.");
            }

            if (environment == null)
            {
                result.Notes.Add("The hosting environment could not be read; only the host 'Debug Mode' setting was evaluated.");
            }

            return result;
        }
    }
}
