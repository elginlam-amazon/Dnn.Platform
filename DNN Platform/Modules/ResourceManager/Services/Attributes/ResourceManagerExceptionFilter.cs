// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.Modules.ResourceManager.Services.Attributes
{
    using System;
    using System.Net;

    using Dnn.Modules.ResourceManager.Exceptions;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>A custom exception filter for the resource manager.</summary>
    /// <remarks>
    /// Was a <c>System.Web.Http.Filters.ExceptionFilterAttribute</c>. The status codes and the response
    /// body shapes (<c>{ message }</c>) are unchanged, as is the re-throw of every other exception type.
    /// <para>
    /// Exception filters are invoked innermost-first, i.e. in descending <see cref="Order"/>, so the
    /// explicit <see cref="Order"/> of 1 below guarantees this filter sees the exception before
    /// <c>DnnExceptionFilterAttribute</c> (which does not implement <see cref="IOrderedFilter"/> and
    /// therefore has the default order of 0). That ordering is what makes the 403/404 mapping win,
    /// and the React client depends on it - relying on declaration order alone would leave it to an
    /// implementation detail.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ResourceManagerExceptionFilter : Attribute, IExceptionFilter, IOrderedFilter
    {
        /// <summary>
        /// Gets the filter order. Higher than the default 0 of <c>DnnExceptionFilterAttribute</c> so
        /// that this filter, which maps the module's own exception types, is invoked first.
        /// </summary>
        public int Order => 1;

        /// <inheritdoc />
        public void OnException(ExceptionContext context)
        {
            if (context?.Exception == null)
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
                return;
            }

            throw exception;
        }
    }
}
