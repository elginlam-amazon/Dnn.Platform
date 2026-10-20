// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Instance.Utilities
{
    using Microsoft.AspNetCore.Http;

    using SystemWeb = System.Web;

    public static class UnitTestHelper
    {
        /// <summary>Sets the HTTP context with a valid simulated request.</summary>
        /// <param name="host">Host.</param>
        /// <param name="application">Application.</param>
        /// <param name="appPhysicalDir">The physical directory of the application.</param>
        /// <param name="pageName">The page name.</param>
        public static void SetHttpContextWithSimulatedRequest(string host, string application, string appPhysicalDir, string pageName)
        {
            var page = application.Replace("/", string.Empty) + "/" + pageName;

            var coreContext = new DefaultHttpContext();
            var request = coreContext.Request;
            request.Method = "GET";
            request.Scheme = "http";
            request.Host = new HostString(host);
            request.PathBase = "/";
            request.Path = "/" + page;

            SystemWeb.HttpContext.Current = coreContext;
        }

        public static void ClearHttpContext()
        {
            SystemWeb.HttpContext.Current = null;
        }
    }
}
