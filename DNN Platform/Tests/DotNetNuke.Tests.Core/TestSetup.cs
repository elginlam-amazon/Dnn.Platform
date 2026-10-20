//
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace DotNetNuke.Tests.Core;

using System.Security.Claims;
using System.Threading;

using DotNetNuke.Tests.Utilities.Mocks;

using NUnit.Framework;

[SetUpFixture]
internal class TestSetup
{
    [OneTimeSetUp]
    public void SetUp()
    {
        // On .NET Framework Thread.CurrentPrincipal defaulted to an unauthenticated principal;
        // on .NET it is null unless explicitly set. Production code such as
        // UserController.GetCurrentUserInternal() reads Thread.CurrentPrincipal.Identity, so
        // restore the historic anonymous principal for the whole test assembly.
        Thread.CurrentPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        MockComponentProvider.ResetContainer();
    }
}
