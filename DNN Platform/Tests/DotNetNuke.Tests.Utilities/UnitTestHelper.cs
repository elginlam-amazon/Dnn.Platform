// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Instance.Utilities
{
    using System.IO;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Http;

    using FakeWebHostEnvironment = DotNetNuke.Tests.Instance.Utilities.HttpSimulator.HttpSimulator.FakeWebHostEnvironment;

    /// <summary>Helpers for installing and clearing a simulated HTTP context in unit tests.</summary>
    public static class UnitTestHelper
    {
        /// <summary>Sets the HTTP context with a valid simulated request.</summary>
        /// <param name="host">Host.</param>
        /// <param name="application">Application.</param>
        /// <param name="appPhysicalDir">The physical path of the application.</param>
        /// <param name="pageName">The name of the requested page.</param>
        /// <remarks>
        /// On net48 this assigned <c>HttpContext.Current = new HttpContext(workerRequest)</c>.
        /// There is no ambient request static on ASP.NET Core, so a
        /// <see cref="DefaultHttpContext"/> is built from the simulated request and published
        /// through <see cref="HttpContextSource.RegisterInstance(HttpContext)"/>; a fake
        /// <see cref="Microsoft.AspNetCore.Hosting.IWebHostEnvironment"/> rooted at
        /// <paramref name="appPhysicalDir"/> is registered too, so
        /// <see cref="HttpContextSource.MapPath"/> resolves the same paths the old
        /// <c>Request.MapPath</c> did.
        /// </remarks>
        public static void SetHttpContextWithSimulatedRequest(string host, string application, string appPhysicalDir, string pageName)
        {
            const string appVirtualDir = "/";
            var page = application.Replace("/", string.Empty) + "/" + pageName;
            var query = string.Empty;
            TextWriter output = null;

            var workerRequest = new SimulatedHttpRequest(appVirtualDir, appPhysicalDir, page, query, output, host);

            var context = new DefaultHttpContext();
            workerRequest.ApplyTo(context);

            HttpContextSource.RegisterWebHostEnvironment(new FakeWebHostEnvironment(appPhysicalDir));
            HttpContextSource.RegisterInstance(context);
        }

        /// <summary>Clears the simulated HTTP context installed by <see cref="SetHttpContextWithSimulatedRequest"/>.</summary>
        public static void ClearHttpContext()
        {
            HttpContextSource.RegisterInstance(null);
        }
    }
}
