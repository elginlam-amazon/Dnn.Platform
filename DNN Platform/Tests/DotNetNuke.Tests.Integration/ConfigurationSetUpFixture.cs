// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Integration
{
    using DotNetNuke.Common;
    using DotNetNuke.Tests.Instance.Utilities;

    using NUnit.Framework;

    /// <summary>
    /// Registers the test assembly's <c>appsettings.json</c>-backed configuration with the
    /// platform's static configuration seam, once for the whole assembly.
    /// </summary>
    /// <remarks>
    /// On net48 these fixtures reached their connection strings, <c>appSettings</c> and
    /// <c>&lt;dotnetnuke&gt;</c> provider configuration through
    /// <c>System.Configuration.ConfigurationManager</c>, which read the test host's
    /// <c>app.config</c> automatically. Modern .NET has no such ambient reader, so the platform
    /// resolves every static configuration read through <see cref="ConfigurationSource"/>, which
    /// throws while unregistered. Registering <see cref="TestConfiguration.Configuration"/> here
    /// makes the code under test (<c>Config.GetConnectionString()</c>,
    /// <c>PetaPocoDataContext</c>, <c>ProviderConfiguration</c>, …) and the assertions in the
    /// fixtures read from the very same store, which is what the legacy
    /// <c>ConfigurationManager</c> guaranteed.
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
