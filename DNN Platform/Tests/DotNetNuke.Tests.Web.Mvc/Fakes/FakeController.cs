// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Fakes
{
    using Microsoft.AspNetCore.Mvc;

    /// <summary>A plain ASP.NET Core MVC controller, used to capture the result of a non-DNN action.</summary>
    public class FakeController : Controller
    {
        /// <summary>The default action.</summary>
        /// <returns>An empty <see cref="ViewResult"/>.</returns>
        public IActionResult Index()
        {
            return new ViewResult();
        }
    }
}
