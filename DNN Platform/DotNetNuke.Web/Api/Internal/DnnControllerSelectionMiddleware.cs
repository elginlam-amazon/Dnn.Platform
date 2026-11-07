// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Produces the "controller not found" (404) and "ambiguous controller" (409) responses the legacy
    /// <c>DnnHttpControllerSelector</c> threw as an <c>HttpResponseException</c>.
    /// </summary>
    /// <remarks>
    /// Must be registered after <c>UseRouting</c> (endpoint selection must have run) and before the
    /// endpoint is executed. <see cref="DnnControllerNamespaceMatcherPolicy"/> records the failed
    /// selection during endpoint selection; this middleware only short-circuits when endpoint selection
    /// actually ended without an endpoint.
    /// </remarks>
    public class DnnControllerSelectionMiddleware
    {
        private readonly RequestDelegate next;

        /// <summary>Initializes a new instance of the <see cref="DnnControllerSelectionMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public DnnControllerSelectionMiddleware(RequestDelegate next)
        {
            ArgumentNullException.ThrowIfNull(next);

            this.next = next;
        }

        /// <summary>Invokes the middleware.</summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.GetEndpoint() is null &&
                context.Items.TryGetValue(DnnControllerNamespaceMatcherPolicy.SelectionFailureKey, out var failure) &&
                failure is DnnControllerSelectionResult { Success: false, } result)
            {
                context.Response.StatusCode = (int)result.StatusCode;

                // The legacy IHttpControllerSelector threw an HttpResponseException whose response was
                // built by Request.CreateErrorResponse, i.e. an HttpError body: {"Message":"..."} as
                // application/json. Keep that shape (it matches DnnApiResults.Error) so a client reading
                // .Message still finds the ControllerNotFound/AmbiguousController text.
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(new Dictionary<string, string> { ["Message"] = result.Message }));
                return;
            }

            await this.next(context);
        }
    }
}
