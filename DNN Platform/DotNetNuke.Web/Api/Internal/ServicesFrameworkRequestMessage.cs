// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Net.Http;
    using System.Web;

    using Microsoft.AspNetCore.Http.Extensions;

    using HttpContext = Microsoft.AspNetCore.Http.HttpContext;

    /// <summary>Creates the <see cref="HttpRequestMessage"/> (method, URI and headers) of an ASP.NET Core request, used by the authentication message handlers of the services framework.</summary>
    public static class ServicesFrameworkRequestMessage
    {
        /// <summary>The option key of the System.Web <see cref="HttpContextBase"/> of the request.</summary>
        public static readonly HttpRequestOptionsKey<HttpContextBase> HttpContextKey = new HttpRequestOptionsKey<HttpContextBase>("MS_HttpContext");

        /// <summary>The option key of the ASP.NET Core <see cref="HttpContext"/> of the request.</summary>
        public static readonly HttpRequestOptionsKey<HttpContext> CoreHttpContextKey = new HttpRequestOptionsKey<HttpContext>("DNN_CoreHttpContext");

        /// <summary>Creates the request message of the given ASP.NET Core request (the content is not copied).</summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <returns>The request message.</returns>
        public static HttpRequestMessage Create(HttpContext httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            var request = httpContext.Request;
            var message = new HttpRequestMessage(new HttpMethod(request.Method), new Uri(request.GetEncodedUrl()));
            foreach (var header in request.Headers)
            {
                if (!message.Headers.TryAddWithoutValidation(header.Key, (string[])header.Value))
                {
                    message.Content ??= new ByteArrayContent([]);
                    message.Content.Headers.TryAddWithoutValidation(header.Key, (string[])header.Value);
                }
            }

            System.Web.HttpContext systemWebContext = httpContext;
            message.Options.Set(HttpContextKey, new HttpContextWrapper(systemWebContext));
            message.Options.Set(CoreHttpContextKey, httpContext);
            return message;
        }
    }
}
