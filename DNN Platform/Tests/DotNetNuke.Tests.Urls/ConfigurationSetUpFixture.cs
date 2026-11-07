// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Urls
{
    using DotNetNuke.Common;
    using DotNetNuke.Tests.Instance.Utilities;

    using NUnit.Framework;

    /// <summary>
    /// Registers the test assembly's configuration with the platform's static configuration seam,
    /// once for the whole assembly.
    /// </summary>
    /// <remarks>
    /// On net48 the fixtures in this assembly reached their <c>appSettings</c>,
    /// <c>connectionStrings</c> and <c>&lt;dotnetnuke&gt;</c> provider configuration through
    /// <c>System.Configuration.ConfigurationManager</c>, which read the test host's
    /// <c>app.config</c> automatically and simply returned <see langword="null"/> for a missing
    /// key. Modern .NET has no such ambient reader, so the platform resolves every static
    /// configuration read through <see cref="ConfigurationSource"/>, which throws rather than
    /// reporting every setting as silently unset. Registering
    /// <see cref="TestConfiguration.Configuration"/> here makes the code under test
    /// (<c>Config</c>, <c>ProviderConfiguration</c>, <c>Localization</c>, the friendly-URL and
    /// URL-rewriting providers, …) read from the same store these fixtures assume — the guarantee
    /// the legacy <c>ConfigurationManager</c> used to provide. This mirrors
    /// <c>DotNetNuke.Tests.Data.ConfigurationSetUpFixture</c> and
    /// <c>DotNetNuke.Tests.Web.ConfigurationSetUpFixture</c>.
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
