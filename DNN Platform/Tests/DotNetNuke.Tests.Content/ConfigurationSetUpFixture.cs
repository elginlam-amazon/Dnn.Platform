// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Content
{
    using DotNetNuke.Common;
    using DotNetNuke.Tests.Instance.Utilities;

    using NUnit.Framework;

    /// <summary>
    /// Registers an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> with the
    /// platform's static configuration seam, once for the whole test assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On net48 the platform code exercised by these fixtures reached its <c>appSettings</c> and
    /// <c>&lt;dotnetnuke&gt;</c> provider configuration through
    /// <c>System.Configuration.ConfigurationManager</c>, which silently returned empty/null for
    /// every key because this test assembly shipped no <c>app.config</c> of its own. Modern .NET
    /// has no ambient XML configuration reader, so the platform routes every static configuration
    /// read through <see cref="ConfigurationSource"/> — and that seam now throws
    /// <see cref="System.InvalidOperationException"/> when nothing has been registered, instead of
    /// returning an empty value.
    /// </para>
    /// <para>
    /// Registering <see cref="TestConfiguration.Configuration"/> restores the net48 semantics: the
    /// project deliberately carries no <c>appsettings.json</c>, so the configuration resolves to an
    /// empty (environment-variable-backed) store and configuration reads yield <see langword="null"/>
    /// exactly as <c>ConfigurationManager</c> did. This keeps the fixtures asserting the argument
    /// validation they were written to assert, rather than tripping over the unregistered seam.
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
