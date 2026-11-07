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
    [OneTimeSetUp]
    public void SetUp()
    {
        // On net48 the platform code these fixtures exercise reached its appSettings and
        // <dotnetnuke> provider configuration through System.Configuration.ConfigurationManager,
        // which read this assembly's ambient XML config. Modern .NET has no ambient configuration
        // reader, so the platform routes every static configuration read through
        // ConfigurationSource — and that seam throws instead of returning null when nothing has
        // been registered. Registering the project's appsettings.json-backed configuration here
        // restores the original semantics for the whole assembly.
        ConfigurationSource.RegisterConfiguration(TestConfiguration.Configuration);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        MockComponentProvider.ResetContainer();
    }
}
