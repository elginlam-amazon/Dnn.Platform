// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Pages.Tests
{
    using DotNetNuke.Common;
    using DotNetNuke.Tests.Instance.Utilities;

    using NUnit.Framework;

    /// <summary>
    /// Registers the test assembly's configuration with the platform's static configuration seam,
    /// once for the whole assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On net48 the prompt-command fixtures in this assembly reached platform settings through
    /// <c>System.Configuration.ConfigurationManager</c>, which read the test host's
    /// <c>app.config</c> automatically and simply returned <see langword="null"/> for a missing
    /// key. Modern .NET has no such ambient reader, so the platform resolves every static
    /// configuration read through <see cref="ConfigurationSource"/>, which throws rather than
    /// reporting every setting as silently unset.
    /// </para>
    /// <para>
    /// The seam is genuinely reached here: every command's <c>Run()</c> calls
    /// <c>ConsoleCommandBase.LocalizeString</c>, which lands in <c>Localization</c> →
    /// <c>Globals.ApplicationPath</c> → <c>Config.GetSetting("InstallationSubfolder")</c>.
    /// Registering <see cref="TestConfiguration.Configuration"/> restores exactly the net48
    /// guarantee: that read resolves to "unset" instead of throwing, which is what these fixtures
    /// assume. No project-local <c>appsettings.json</c> is added, because
    /// <c>InstallationSubfolder</c> was equally absent on net48 — supplying a value would change
    /// behaviour rather than preserve it. This mirrors
    /// <c>DotNetNuke.Tests.Web.ConfigurationSetUpFixture</c> and the sibling
    /// <c>Dnn.PersonaBar.Users.Tests.ConfigurationSetUpFixture</c>.
    /// </para>
    /// </remarks>
    [SetUpFixture]
    public class ConfigurationSetUpFixture
    {
        /// <summary>Registers the test configuration before any fixture in the assembly runs.</summary>
        [OneTimeSetUp]
        public void RegisterConfiguration()
        {
            ConfigurationSource.RegisterConfiguration(TestConfiguration.Configuration);
        }
    }
}
