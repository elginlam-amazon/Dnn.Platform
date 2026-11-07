//
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//

namespace DotNetNuke.Tests.Core;

using DotNetNuke.Common;
using DotNetNuke.Tests.Instance.Utilities;
using DotNetNuke.Tests.Utilities.Mocks;

using NUnit.Framework;

[SetUpFixture]
internal class TestSetup
{
    /// <summary>
    /// Registers the test assembly's <c>appsettings.json</c>-backed configuration with the
    /// platform's static configuration seam, once for the whole assembly.
    /// </summary>
    /// <remarks>
    /// On net48 the code under test reached its <c>appSettings</c> and <c>&lt;dotnetnuke&gt;</c>
    /// provider configuration through <c>System.Configuration.ConfigurationManager</c>, which read
    /// the test host's <c>app.config</c> automatically. Modern .NET has no such ambient reader, so
    /// the platform resolves every static configuration read through <see cref="ConfigurationSource"/>,
    /// which throws when nothing has been registered. Registering
    /// <see cref="TestConfiguration.Configuration"/> here is the same seam used by
    /// DotNetNuke.Tests.Data and DotNetNuke.Tests.Web.
    /// </remarks>
    [OneTimeSetUp]
    public void SetUp()
    {
        ConfigurationSource.RegisterConfiguration(TestConfiguration.Configuration);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        MockComponentProvider.ResetContainer();
    }
}
