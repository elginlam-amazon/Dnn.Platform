// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    /// <summary>Provides indexed access to the values of the current request.</summary>
    /// <remarks>
    /// Replaces the <c>Request["key"]</c> indexer of <c>System.Web.HttpRequestBase</c>, which
    /// ASP.NET Core's <see cref="HttpRequest"/> does not provide. Module razor scripts (and every
    /// third-party script installed into a live site) read posted and query-string values that
    /// way, so the indexer is reproduced here with the net48 probing order: form values first
    /// (only when the request actually has a form content type, since reading
    /// <see cref="HttpRequest.Form"/> otherwise throws), then the query string, then the route
    /// values. Unknown keys return <see langword="null"/>, as they did on net48.
    /// </remarks>
    public class RazorRequestValues
    {
        private readonly HttpContext httpContext;

        /// <summary>Initializes a new instance of the <see cref="RazorRequestValues"/> class.</summary>
        /// <param name="httpContext">The context of the current request, may be <see langword="null"/>.</param>
        public RazorRequestValues(HttpContext httpContext)
        {
            this.httpContext = httpContext;
        }

        /// <summary>Gets the underlying request, or <see langword="null"/> when there is none.</summary>
        public HttpRequest Request
        {
            get { return this.httpContext?.Request; }
        }

        /// <summary>Gets the value of the given request key.</summary>
        /// <param name="key">The name of the form field, query-string parameter or route value.</param>
        /// <returns>The value, or <see langword="null"/> when the key is not present.</returns>
        public string this[string key]
        {
            get
            {
                var request = this.Request;
                if (request == null || string.IsNullOrEmpty(key))
                {
                    return null;
                }

                if (request.HasFormContentType && request.Form.TryGetValue(key, out var formValue))
                {
                    return formValue.ToString();
                }

                if (request.Query.TryGetValue(key, out var queryValue))
                {
                    return queryValue.ToString();
                }

                var routeValues = this.httpContext.GetRouteData()?.Values;
                if (routeValues != null && routeValues.TryGetValue(key, out var routeValue))
                {
                    return routeValue?.ToString();
                }

                return null;
            }
        }
    }
}
