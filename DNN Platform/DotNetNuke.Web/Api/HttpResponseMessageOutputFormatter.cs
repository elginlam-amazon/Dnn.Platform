// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.Mvc.Formatters;

    /// <summary>Writes the <see cref="HttpResponseMessage"/> returned by services framework actions (status code, headers and content) to the ASP.NET Core response.</summary>
    public class HttpResponseMessageOutputFormatter : IOutputFormatter
    {
        /// <summary>Writes a response message to the HTTP response.</summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <param name="responseMessage">The response message.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task WriteResponseMessageAsync(HttpContext httpContext, HttpResponseMessage responseMessage)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (responseMessage == null)
            {
                throw new ArgumentNullException(nameof(responseMessage));
            }

            using (responseMessage)
            {
                var response = httpContext.Response;
                response.StatusCode = (int)responseMessage.StatusCode;
                if (!string.IsNullOrEmpty(responseMessage.ReasonPhrase))
                {
                    var responseFeature = httpContext.Features.Get<IHttpResponseFeature>();
                    if (responseFeature != null)
                    {
                        responseFeature.ReasonPhrase = responseMessage.ReasonPhrase;
                    }
                }

                foreach (var header in responseMessage.Headers)
                {
                    response.Headers[header.Key] = header.Value.ToArray();
                }

                if (responseMessage.Content == null)
                {
                    return;
                }

                foreach (var header in responseMessage.Content.Headers)
                {
                    response.Headers[header.Key] = header.Value.ToArray();
                }

                // the transfer encoding is handled by the server
                response.Headers.Remove("Transfer-Encoding");
                await responseMessage.Content.CopyToAsync(response.Body, httpContext.RequestAborted);
            }
        }

        /// <inheritdoc />
        public bool CanWriteResult(OutputFormatterCanWriteContext context)
        {
            return context?.Object is HttpResponseMessage;
        }

        /// <inheritdoc />
        public Task WriteAsync(OutputFormatterWriteContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return WriteResponseMessageAsync(context.HttpContext, (HttpResponseMessage)context.Object);
        }
    }
}
