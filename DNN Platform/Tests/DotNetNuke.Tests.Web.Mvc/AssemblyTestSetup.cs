// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc
{
    using System.Security.Claims;
    using System.Threading;

    using NUnit.Framework;

    /// <summary>Assembly-wide test setup. On .NET, <see cref="Thread.CurrentPrincipal"/> is null by
    /// default, so establish an anonymous principal for code paths that read it.</summary>
    [SetUpFixture]
    public class AssemblyTestSetup
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            Thread.CurrentPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
