// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionFilters
{
    using System;

    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>An exception filter which logs unhandled DNN module exceptions.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class DnnHandleErrorAttribute : Attribute, IExceptionFilter
    {
        /// <inheritdoc />
        public virtual void OnException(ExceptionContext context)
        {
            // ExceptionContext does not expose the controller instance in ASP.NET Core; the module
            // application publishes the executing DNN controller on the request instead.
            var controller = ModuleApplication.GetCurrentDnnController(context.HttpContext);

            if (controller == null)
            {
                throw new InvalidOperationException("This attribute can only be applied to Controllers that implement IDnnController");
            }

            this.LogException(context.Exception);
        }

        /// <summary>Logs the exception.</summary>
        /// <param name="exception">The exception to log.</param>
        protected virtual void LogException(Exception exception)
        {
            Exceptions.LogException(exception);
        }
    }
}
