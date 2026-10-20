// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Net.Http;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;

#pragma warning disable SA1101 // StyleCop does not understand C# 14 extension members (the receiver is not an instance member).

    /// <summary>The ASP.NET Web API request properties (<c>Request.RequestUri</c>, <c>Request.Content</c>) on top of the ASP.NET Core <see cref="HttpRequest"/>.</summary>
    public static class ServicesFrameworkHttpRequestProperties
    {
        /// <summary>The ASP.NET Web API request members.</summary>
        /// <param name="request">The request.</param>
        extension(HttpRequest request)
        {
            /// <summary>Gets the absolute URI of the request.</summary>
            public Uri RequestUri => new Uri(request.GetEncodedUrl());

            /// <summary>Gets the body of the request as <see cref="HttpContent"/> (with its content headers), e.g. to read multipart content with <c>ReadAsMultipartAsync</c>.</summary>
            public HttpContent Content
            {
                get
                {
                    var items = request.HttpContext.Items;
                    if (items.TryGetValue(ServicesFrameworkHttpRequestExtensions.ContentKey, out var existing) && existing is HttpContent existingContent)
                    {
                        return existingContent;
                    }

                    var content = new StreamContent(request.Body);
                    foreach (var header in request.Headers)
                    {
                        // only the content headers (Content-Type, Content-Length, Content-Disposition...) are accepted
                        content.Headers.TryAddWithoutValidation(header.Key, (string[])header.Value);
                    }

                    items[ServicesFrameworkHttpRequestExtensions.ContentKey] = content;
                    request.HttpContext.Response.RegisterForDispose(content);
                    return content;
                }
            }
        }
    }
}
