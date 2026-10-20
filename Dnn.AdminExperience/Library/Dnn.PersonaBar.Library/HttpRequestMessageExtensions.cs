// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.PersonaBar.Library
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Web.Api;
    using Microsoft.AspNetCore.Http;

    /// <summary>Extends the HttpRequest collection.</summary>
    public static class HttpRequestMessageExtensions
    {
        /// <summary>
        /// Returns a dictionary of QueryStrings that's easier to work with
        /// than GetQueryNameValuePairs KevValuePairs collection.
        ///
        /// If you need to pull a few single values use GetQueryString instead.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>A <see cref="Dictionary{TKey,TValue}"/> of query string keys to values.</returns>
        public static Dictionary<string, string> GetQueryStrings(this HttpRequest request)
        {
            return request.GetQueryNameValuePairs()
                          .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Returns an individual querystring value.</summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="key">The query string key.</param>
        /// <returns>The query string value or <see langword="null"/>.</returns>
        public static string GetQueryString(this HttpRequest request, string key)
        {
            ////IEnumerable<KeyValuePair<string,string>> - right!
            var queryStrings = request.GetQueryNameValuePairs();
            if (queryStrings == null)
            {
                return null;
            }

            var match = queryStrings.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(match.Value))
            {
                return null;
            }

            return match.Value;
        }

        /// <summary>Returns an individual querystring value as integer.</summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="key">The query string key.</param>
        /// <returns>The query string value or <c>-1</c>.</returns>
        public static int GetQueryStringAsInteger(this HttpRequest request, string key)
        {
            ////IEnumerable<KeyValuePair<string,string>> - right!
            var value = request.GetQueryString(key);
            int intValue;
            if (!string.IsNullOrEmpty(value) && int.TryParse(value, out intValue))
            {
                return intValue;
            }

            return -1;
        }

        /// <summary>Returns an individual HTTP Header value.</summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="key">The header name.</param>
        /// <returns>The header value or <see langword="null"/>.</returns>
        public static string GetHeader(this HttpRequest request, string key)
        {
            if (!request.Headers.TryGetValue(key, out var values))
            {
                return null;
            }

            return values.FirstOrDefault();
        }

        /// <summary>Retrieves an individual cookie from the cookies collection.</summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="cookieName">The cookie name.</param>
        /// <returns>The cookie value or <see langword="null"/>.</returns>
        public static string GetCookie(this HttpRequest request, string cookieName)
        {
            if (request.Cookies.TryGetValue(cookieName, out var value))
            {
                return value;
            }

            return null;
        }
    }
}
