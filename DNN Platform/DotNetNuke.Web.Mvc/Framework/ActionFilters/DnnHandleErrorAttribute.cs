// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionFilters
{
    using System;

    using DotNetNuke.Services.Exceptions;

    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>Logs exceptions raised by MVC module actions.</summary>
    public class DnnHandleErrorAttribute : ExceptionFilterAttribute
    {
        /// <inheritdoc />
        public override void OnException(ExceptionContext filterContext)
        {
            this.LogException(filterContext.Exception);
        }

        /// <summary>Logs the given exception.</summary>
        /// <param name="exception">The exception to log.</param>
        protected virtual void LogException(Exception exception)
        {
            Exceptions.LogException(exception);
        }
    }
}
