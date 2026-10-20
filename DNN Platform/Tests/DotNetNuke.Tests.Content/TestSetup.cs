// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System.Security.Claims;
using System.Threading;

using NUnit.Framework;

[SetUpFixture]
public class TestSetup
{
    [OneTimeSetUp]
    public void SetUp()
    {
        // On .NET Framework Thread.CurrentPrincipal defaulted to an unauthenticated
        // principal; on .NET it is null unless explicitly set. Production code such as
        // UserController.GetCurrentUserInternal() reads Thread.CurrentPrincipal.Identity,
        // so restore the historic anonymous principal for the whole test assembly.
        Thread.CurrentPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
    }
}
