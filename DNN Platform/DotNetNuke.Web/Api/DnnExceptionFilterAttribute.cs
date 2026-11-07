// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Net;

    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>Automatically handles WebAPI exceptions.</summary>
    /// <remarks>
    /// Was an <c>System.Web.Http.Filters.ExceptionFilterAttribute</c>; it is applied to
    /// <see cref="DnnApiController"/> (and to individual controllers/actions), not globally, so it stays a
    /// filter rather than becoming exception-handling middleware. The status code per exception type and
    /// the response body (an empty body with the localized reason phrase) are unchanged.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class DnnExceptionFilterAttribute : Attribute, IExceptionFilter
    {
        /// <summary>
        /// Gets or sets the message key for exception localization.
        /// ie: "controller_action.Error".
        /// </summary>
        public string MessageKey { get; set; }

        /// <summary>Gets or sets the resource file used to localize the exception messages.</summary>
        public string LocalResourceFile { get; set; }

        /// <inheritdoc />
        public void OnException(ExceptionContext context)
        {
            if (context?.Exception is null)
            {
                return;
            }

            var exception = context.Exception;
            var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var actionName = actionDescriptor?.ActionName;
            var controllerName = actionDescriptor?.ControllerName;

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

            // The legacy filter returned an HttpResponseMessage with no content and the localized message
            // as its reason phrase; the reason phrase is set through the response feature in ASP.NET Core.
            var responseFeature = context.HttpContext?.Features.Get<IHttpResponseFeature>();
            if (responseFeature != null)
            {
                responseFeature.ReasonPhrase = Localization.GetString(key, resourceFile);
            }

            context.Result = new StatusCodeResult((int)statusCode);
            context.ExceptionHandled = true;
            Exceptions.LogException(exception);
        }
    }
}
