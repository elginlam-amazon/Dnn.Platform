// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Urls
{
    using System;
    using System.Collections.Specialized;

    using DotNetNuke.Entities.Urls;

    using Microsoft.AspNetCore.Http;

    internal class UrlTestHelper
    {
        public Uri RequestUri { get; set; }

        public UrlAction Result { get; set; }

        public NameValueCollection QueryStringCol { get; set; }

        public string HttpAliasFull { get; set; }

        /// <summary>Gets or sets the response produced by the rewriter for the simulated request.</summary>
        /// <remarks>
        /// This was a <c>System.Web.HttpResponse</c> on net48; it is now the ASP.NET Core
        /// <see cref="HttpResponse"/> taken from the simulated <see cref="HttpContext"/>.
        /// Note that <see cref="HttpResponse.StatusCode"/> is an <see cref="int"/> on ASP.NET
        /// Core (it was an <see cref="int"/> on net48 too), so the status assertions are unchanged.
        /// </remarks>
        public HttpResponse Response { get; set; }
    }
}
