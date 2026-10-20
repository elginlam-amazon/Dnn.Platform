// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Net;
    using System.Net.Http;

    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>Converts the exceptions thrown by services framework actions into error responses (and logs them).</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class DnnExceptionFilterAttribute : ExceptionFilterAttribute
    {
        /// <summary>Gets or sets the localization key of the error message.</summary>
        public string MessageKey { get; set; }

        /// <summary>Gets or sets the resource file of the error message.</summary>
        public string LocalResourceFile { get; set; }

        /// <inheritdoc />
        public override void OnException(ExceptionContext context)
        {
            if (context?.Exception == null || context.ExceptionHandled)
            {
                return;
            }

            var exception = context.Exception;
            if (exception is HttpResponseException responseException)
            {
                // the equivalent of the ASP.NET Web API HttpResponseException handling
                context.Result = new ObjectResult(responseException.Response);
                context.ExceptionHandled = true;
                return;
            }

            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var actionName = descriptor?.ActionName ?? context.ActionDescriptor.DisplayName;
            var controllerName = descriptor?.ControllerName;

            var resourceFile = string.IsNullOrEmpty(this.LocalResourceFile)
                ? Localization.ExceptionsResourceFile
                : this.LocalResourceFile;

            var key = string.IsNullOrEmpty(this.MessageKey) ? controllerName + "_" + actionName + ".Error" : this.MessageKey;

            HttpStatusCode statusCode;
            if (exception is NotImplementedException)
            {
                statusCode = HttpStatusCode.NotImplemented;
            }
            else if (exception is TimeoutException)
            {
                statusCode = HttpStatusCode.RequestTimeout;
            }
            else if (exception is WebApiException)
            {
                statusCode = HttpStatusCode.BadRequest;
            }
            else if (exception is UnauthorizedAccessException)
            {
                statusCode = HttpStatusCode.Unauthorized;
            }
            else
            {
                statusCode = HttpStatusCode.InternalServerError;
            }

            var response = new HttpResponseMessage
            {
                StatusCode = statusCode,
                ReasonPhrase = Localization.GetString(key, resourceFile),
            };

            context.Result = new ObjectResult(response);
            context.ExceptionHandled = true;
            Exceptions.LogException(exception);
        }
    }
}
