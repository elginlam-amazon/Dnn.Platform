// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.PersonaBar.Library
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Extension methods for <see cref="HttpRequest"/>. These extended the Web API 2
    /// <c>HttpRequestMessage</c>, which is a client type in ASP.NET Core, so every extension now
    /// hangs off the server-side <see cref="HttpRequest"/>. The method names and their contracts
    /// are unchanged.
    /// </summary>
    public static class HttpRequestMessageExtensions
    {
        /// <summary>
        /// Returns a dictionary of QueryStrings that's easier to work with
        /// than the raw query collection.
        ///
        /// If you need to pull a few single values use GetQueryString instead.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>A <see cref="Dictionary{TKey,TValue}"/> of query string keys to values.</returns>
        public static Dictionary<string, string> GetQueryStrings(this HttpRequest request)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (request?.Query == null)
            {
                return dictionary;
            }

            foreach (var pair in request.Query)
            {
                dictionary[pair.Key] = pair.Value.ToString();
            }

            return dictionary;
        }

        /// <summary>Returns an individual querystring value.</summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="key">The query string key.</param>
        /// <returns>The query string value or <see langword="null"/>.</returns>
        public static string GetQueryString(this HttpRequest request, string key)
        {
            var queryStrings = request?.Query;
            if (queryStrings == null)
            {
                return null;
            }

            var match = queryStrings.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
            var value = match.Value.ToString();
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return value;
        }

        /// <summary>Returns an individual querystring value as integer.</summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="key">The query string key.</param>
        /// <returns>The query string value or <c>-1</c>.</returns>
        public static int GetQueryStringAsInteger(this HttpRequest request, string key)
        {
            var value = request.GetQueryString(key);
            if (!string.IsNullOrEmpty(value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
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
            if (request?.Headers == null || !request.Headers.TryGetValue(key, out var values))
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
            if (request?.Cookies != null && request.Cookies.TryGetValue(cookieName, out var value))
            {
                return value;
            }

            return null;
        }
    }
}
