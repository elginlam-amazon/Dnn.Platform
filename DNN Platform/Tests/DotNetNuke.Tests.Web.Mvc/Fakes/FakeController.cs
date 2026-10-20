// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Fakes
{
    using Microsoft.AspNetCore.Mvc;

    public class FakeController : Controller
    {
        public IActionResult Index()
        {
            return new ViewResult();
        }
    }
}
