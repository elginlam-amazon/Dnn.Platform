// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Fakes.Filters
{
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>A fake exception filter which redirects to <see cref="FakeRedirectAttribute.Result"/>.</summary>
    public class FakeHandleExceptionRedirectAttribute : FakeRedirectAttribute, IExceptionFilter
    {
        /// <summary>Gets or sets a value indicating whether the filter marks the exception as handled.</summary>
        public static bool IsExceptionHandled { get; set; }

        /// <inheritdoc />
        public void OnException(ExceptionContext context)
        {
            context.Result = Result;
            context.ExceptionHandled = IsExceptionHandled;
        }
    }
}
