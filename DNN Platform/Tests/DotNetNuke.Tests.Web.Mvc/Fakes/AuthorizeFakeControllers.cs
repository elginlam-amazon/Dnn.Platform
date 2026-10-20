// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Fakes
{
    using DotNetNuke.Web.Mvc.Framework.Controllers;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>A controller with no anonymous access configured.</summary>
    public class SecuredFakeController : DnnController
    {
        public IActionResult Secured()
        {
            return this.Content("secured");
        }
    }

    /// <summary>A controller allowing anonymous access at the action level.</summary>
    public class AnonymousActionFakeController : DnnController
    {
        [AllowAnonymous]
        public IActionResult Anonymous()
        {
            return this.Content("anonymous");
        }
    }

    /// <summary>A controller allowing anonymous access at the controller level.</summary>
    [AllowAnonymous]
    public class AnonymousControllerFakeController : DnnController
    {
        public IActionResult Anonymous()
        {
            return this.Content("anonymous");
        }
    }
}
