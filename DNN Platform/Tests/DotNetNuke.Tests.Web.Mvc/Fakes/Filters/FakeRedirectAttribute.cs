// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Fakes.Filters
{
    using System;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>The base class of the fake filters which replace the result of an action.</summary>
    /// <remarks>Was an MVC 5 action-filter attribute; ASP.NET Core filters implement
    /// <see cref="IActionFilter"/> (or its async variant) instead of deriving from a base attribute.</remarks>
    public abstract class FakeRedirectAttribute : Attribute, IActionFilter
    {
        /// <summary>The result the fake filters redirect to.</summary>
        public static IActionResult Result = new EmptyResult();

        /// <inheritdoc />
        public virtual void OnActionExecuting(ActionExecutingContext context)
        {
        }

        /// <inheritdoc />
        public virtual void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
