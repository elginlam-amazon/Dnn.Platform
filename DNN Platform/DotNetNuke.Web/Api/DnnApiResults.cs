// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Collections.Generic;
    using System.Net;

    using DotNetNuke.Common;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// The replacement for the Web API 2 <c>Request.CreateResponse(...)</c> /
    /// <c>Request.CreateErrorResponse(...)</c> helpers, which produced an <c>HttpResponseMessage</c>.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core actions return an <see cref="IActionResult"/>. Use these helpers wherever the legacy
    /// code created a response message, so that the status codes AND the response payload shapes stay
    /// byte-identical:
    /// <list type="bullet">
    /// <item><description><c>Request.CreateResponse(HttpStatusCode.OK, value)</c> → <c>this.Ok(value)</c>
    /// (or <see cref="Response(HttpStatusCode, object)"/> for any other status code).</description></item>
    /// <item><description><c>Request.CreateResponse(HttpStatusCode.OK)</c> →
    /// <see cref="Response(HttpStatusCode)"/>.</description></item>
    /// <item><description><c>Request.CreateErrorResponse(code, message)</c> →
    /// <see cref="Error(HttpStatusCode, string)"/>, which keeps the legacy
    /// <c>{"Message":"…"}</c> body of <c>HttpError</c>.</description></item>
    /// <item><description><c>Request.CreateErrorResponse(code, exception)</c> →
    /// <see cref="Error(HttpStatusCode, Exception)"/>.</description></item>
    /// </list>
    /// </remarks>
    public static class DnnApiResults
    {
        /// <summary>Creates a result with the given status code and no body.</summary>
        /// <param name="statusCode">The status code.</param>
        /// <returns>An <see cref="IActionResult"/>.</returns>
        public static IActionResult Response(HttpStatusCode statusCode)
        {
            return new StatusCodeResult((int)statusCode);
        }

        /// <summary>Creates a result with the given status code and body.</summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="value">The value to serialize into the response body.</param>
        /// <returns>An <see cref="IActionResult"/>.</returns>
        public static IActionResult Response(HttpStatusCode statusCode, object value)
        {
            return new ObjectResult(value) { StatusCode = (int)statusCode, };
        }

        /// <summary>Creates an error result with the legacy <c>HttpError</c> body shape.</summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="message">The error message.</param>
        /// <returns>An <see cref="IActionResult"/> whose body is <c>{"Message":"…"}</c>.</returns>
        public static IActionResult Error(HttpStatusCode statusCode, string message)
        {
            return new ObjectResult(new Dictionary<string, string> { ["Message"] = message, })
            {
                StatusCode = (int)statusCode,
            };
        }

        /// <summary>Creates an error result with the legacy <c>HttpError</c> body shape for an exception.</summary>
        /// <param name="statusCode">The status code.</param>
        /// <param name="exception">The exception.</param>
        /// <returns>An <see cref="IActionResult"/> whose body is <c>{"Message":"…","ExceptionType":"…"}</c>.</returns>
        /// <remarks>
        /// Web API 2's default <c>IncludeErrorDetailPolicy.LocalOnly</c> is preserved: the exception message and
        /// type are only disclosed to requests originating from the server itself. Remote callers get the same
        /// generic text Web API 2 produced, so exception detail is not leaked to clients in production.
        /// </remarks>
        public static IActionResult Error(HttpStatusCode statusCode, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            var body = IncludeErrorDetail()
                ? new Dictionary<string, string>
                {
                    ["Message"] = exception.Message,
                    ["ExceptionType"] = exception.GetType().FullName,
                }
                : new Dictionary<string, string> { ["Message"] = "An error has occurred.", };

            return new ObjectResult(body)
            {
                StatusCode = (int)statusCode,
            };
        }

        /// <summary>
        /// Reproduces Web API 2's default <c>IncludeErrorDetailPolicy.LocalOnly</c>: error detail is included
        /// only when the request comes from the local machine.
        /// </summary>
        /// <returns><see langword="true"/> when exception detail may be disclosed.</returns>
        private static bool IncludeErrorDetail()
        {
            var connection = HttpContextSource.Current?.Connection;
            if (connection?.RemoteIpAddress is null)
            {
                return false;
            }

            return IPAddress.IsLoopback(connection.RemoteIpAddress)
                || connection.RemoteIpAddress.Equals(connection.LocalIpAddress);
        }
    }
}
