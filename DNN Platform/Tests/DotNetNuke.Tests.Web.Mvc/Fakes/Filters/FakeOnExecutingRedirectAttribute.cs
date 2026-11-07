// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Fakes.Filters
{
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>A fake action filter which short circuits the action with <see cref="FakeRedirectAttribute.Result"/>.</summary>
    public class FakeOnExecutingRedirectAttribute : FakeRedirectAttribute
    {
        /// <inheritdoc />
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            context.Result = Result;
        }
    }
}
