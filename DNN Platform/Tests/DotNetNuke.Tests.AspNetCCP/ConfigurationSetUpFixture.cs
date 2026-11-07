// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.AspNetClientCapabilityProvider
{
    using DotNetNuke.Common;
    using DotNetNuke.Tests.Instance.Utilities;

    using NUnit.Framework;

    /// <summary>
    /// Registers the test assembly's <c>appsettings.json</c>-backed configuration with the
    /// platform's static configuration seam, once for the whole assembly.
    /// </summary>
    /// <remarks>
    /// On net48 the fixtures reached the <c>&lt;dotnetnuke&gt;</c> provider configuration and the
    /// <c>appSettings</c> keys through <c>System.Configuration.ConfigurationManager</c>, which
    /// read the test host's <c>app.config</c> automatically. Modern .NET has no such ambient
    /// reader, so the platform resolves every static configuration read through
    /// <see cref="ConfigurationSource"/>, which throws rather than silently returning
    /// <see langword="null"/> when nothing has been registered. Both fixtures in this assembly
    /// reach that seam indirectly — <c>Requires.NotNullOrEmpty</c> formats its message through
    /// <c>Localization.GetExceptionMessage</c> — so this registration is load-bearing, not
    /// optional.
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
