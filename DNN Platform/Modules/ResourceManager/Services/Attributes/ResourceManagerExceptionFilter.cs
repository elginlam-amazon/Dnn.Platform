// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.Modules.ResourceManager.Services.Attributes
{
    using System.Net;

    using Dnn.Modules.ResourceManager.Exceptions;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>A custom <see cref="ExceptionFilterAttribute"/> for the resource manager.</summary>
    public class ResourceManagerExceptionFilter : ExceptionFilterAttribute
    {
        /// <inheritdoc />
        public override void OnException(ExceptionContext context)
        {
            if (context?.Exception == null || context.ExceptionHandled)
            {
                return;
            }

            var exception = context.Exception;

            if (exception is FolderPermissionNotMetException)
            {
                context.Result = new ObjectResult(new { message = exception.Message })
                {
                    StatusCode = (int)HttpStatusCode.Forbidden,
                };
                context.ExceptionHandled = true;
                return;
            }

            if (exception is NotFoundException)
            {
                context.Result = new ObjectResult(new { message = exception.Message })
                {
                    StatusCode = (int)HttpStatusCode.NotFound,
                };
                context.ExceptionHandled = true;
            }
        }
    }
}
