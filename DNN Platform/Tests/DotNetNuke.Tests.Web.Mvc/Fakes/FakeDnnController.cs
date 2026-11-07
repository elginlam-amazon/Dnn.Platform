// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Fakes
{
    using System;

    using DotNetNuke.Tests.Web.Mvc.Fakes.Filters;
    using DotNetNuke.Web.Mvc.Framework.Controllers;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>A DNN module controller used by the framework tests.</summary>
    public class FakeDnnController : DnnController
    {
        /// <summary>An action rendering a view.</summary>
        /// <returns>A <c>DnnViewResult</c> for the <c>Action1</c> view.</returns>
        public IActionResult Action1()
        {
            return this.View("Action1", null, null);
        }

        /// <summary>An action rendering a view with a master page.</summary>
        /// <returns>A <c>DnnViewResult</c> for the <c>Action2</c> view.</returns>
        public IActionResult Action2()
        {
            return this.View("Action2", "Master2", null);
        }

        /// <summary>An action rendering a view with a master page and a model.</summary>
        /// <param name="dog">The model.</param>
        /// <returns>A <c>DnnViewResult</c> for the <c>Action3</c> view.</returns>
        public IActionResult Action3(Dog dog)
        {
            return this.View("Action3", "Master3", dog);
        }

        /// <summary>An action which always throws, so the exception filter runs.</summary>
        /// <returns>Never returns.</returns>
        [FakeHandleExceptionRedirect]
        public IActionResult ActionWithExceptionFilter()
        {
            throw new Exception();
        }

        /// <summary>An action decorated with a filter which short circuits it.</summary>
        /// <returns>A <c>DnnViewResult</c> for the <c>Action1</c> view.</returns>
        [FakeOnExecutingRedirect]
        public IActionResult ActionWithOnExecutingFilter()
        {
            return this.View("Action1", null, null);
        }

        /// <summary>An action decorated with a filter which replaces its result.</summary>
        /// <returns>A <c>DnnViewResult</c> for the <c>Action1</c> view.</returns>
        [FakeOnExecutedRedirect]
        public IActionResult ActionWithOnExecutedFilter()
        {
            return this.View("Action1", null, null);
        }

        /// <summary>Runs the per-request DNN initialization of the controller.</summary>
        /// <param name="actionContext">The context of the executing action.</param>
        /// <remarks>
        /// Mocking out the entire controller lifecycle proved to be difficult. This method executes the
        /// initialization logic which runs on every request, which ASP.NET Core performs from
        /// <see cref="DnnController.OnActionExecuting"/> (it was the MVC 5 <c>Controller.Initialize</c> overload
        /// on MVC 5).
        /// </remarks>
        public void MockInitialize(ActionContext actionContext)
        {
            this.Initialize(actionContext);
        }
    }
}
