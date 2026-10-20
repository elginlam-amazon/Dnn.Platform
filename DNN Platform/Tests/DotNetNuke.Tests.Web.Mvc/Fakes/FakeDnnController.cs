// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Fakes
{
    using DotNetNuke.Web.Mvc.Framework.ActionResults;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Helpers;

    using Microsoft.AspNetCore.Mvc;

    public class FakeDnnController : DnnController
    {
        public IActionResult Action1()
        {
            return this.View("Action1", (object)null);
        }

        public IActionResult Action2()
        {
            var result = (DnnViewResult)this.View("Action2", (object)null);
            result.MasterName = "Master2";
            return result;
        }

        public IActionResult Action3(Dog dog)
        {
            var result = (DnnViewResult)this.View("Action3", dog);
            result.MasterName = "Master3";
            return result;
        }

        public void MockInitialize()
        {
            // The ASP.NET Core DnnController creates its DnnUrlHelper lazily during OnActionExecuting.
            // This mirrors that initialization for tests that do not run the full action pipeline.
            this.Url ??= new DnnUrlHelper(this);
        }
    }
}
