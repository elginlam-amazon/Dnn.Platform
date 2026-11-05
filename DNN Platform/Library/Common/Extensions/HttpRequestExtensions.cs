// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Compatibility helpers that provide ASP.NET Core equivalents for
    /// <c>System.Web.HttpRequest</c> members which have no direct counterpart on
    /// <see cref="HttpRequest"/>.
    /// </summary>
    /// <remarks>
    /// These are real implementations over the ASP.NET Core request, not a
    /// <c>System.Web</c> compatibility shim: each one is the documented modern
    /// equivalent of the legacy member it replaces. They exist in one place so the
    /// platform does not repeat the same recomposition logic at every call site.
    /// </remarks>
    public static class HttpRequestExtensions
    {
        /// <summary>
        /// Gets a value indicating whether the current user has been authenticated.
        /// </summary>
        /// <param name="request">The current request.</param>
        /// <returns><see langword="true"/> when <c>HttpContext.User</c> carries an authenticated identity.</returns>
        /// <remarks>
        /// Replaces <c>System.Web.HttpRequest.IsAuthenticated</c>. The value is produced by the
        /// authentication middleware, so <c>UseAuthentication()</c> must run before this is read.
        /// </remarks>
        public static bool IsAuthenticated(this HttpRequest request)
        {
            return request?.HttpContext?.User?.Identity?.IsAuthenticated == true;
        }

        /// <summary>
        /// Gets the raw, un-rewritten URL of the current request, including the application path
        /// base and the query string.
        /// </summary>
        /// <param name="request">The current request.</param>
        /// <returns>The path and query of the request, for example <c>/site/page?id=1</c>.</returns>
        /// <remarks>
        /// Replaces <c>System.Web.HttpRequest.RawUrl</c>. ASP.NET Core splits the legacy raw URL
        /// across <see cref="HttpRequest.PathBase"/>, <see cref="HttpRequest.Path"/> and
        /// <see cref="HttpRequest.QueryString"/>, so it is recomposed here.
        /// </remarks>
        public static string GetRawUrl(this HttpRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            return $"{request.PathBase.Value}{request.Path.Value}{request.QueryString.Value}";
        }

        /// <summary>
        /// Gets the first value for <paramref name="key"/> from the query string, the form or the
        /// cookies of the request, in that order.
        /// </summary>
        /// <param name="request">The current request.</param>
        /// <param name="key">The name of the value to find.</param>
        /// <returns>The value when found; otherwise <see langword="null"/>.</returns>
        /// <remarks>
        /// Replaces the <c>System.Web.HttpRequest</c> indexer (<c>Request["key"]</c>), which
        /// searched QueryString, Form, Cookies and ServerVariables in order. ServerVariables is
        /// IIS-specific and has no Core equivalent, so it is not searched; read
        /// <see cref="HttpRequest.Headers"/> or <see cref="HttpContext.Connection"/> directly for
        /// those values.
        /// </remarks>
        public static string GetValue(this HttpRequest request, string key)
        {
            if (request == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (request.Query.TryGetValue(key, out var queryValue))
            {
                return queryValue.ToString();
            }

            if (request.HasFormContentType && request.Form.TryGetValue(key, out var formValue))
            {
                return formValue.ToString();
            }

            if (request.Cookies.TryGetValue(key, out var cookieValue))
            {
                return cookieValue;
            }

            return null;
        }

        /// <summary>
        /// Gets the query string of the request as a <see cref="NameValueCollection"/>.
        /// </summary>
        /// <param name="request">The current request.</param>
        /// <returns>A new collection holding every query-string key and value; never <see langword="null"/>.</returns>
        /// <remarks>
        /// Replaces <c>System.Web.HttpRequest.QueryString</c>, which was a
        /// <see cref="NameValueCollection"/>. ASP.NET Core models the query string as an
        /// <see cref="IQueryCollection"/> instead, so the values are copied into a detached
        /// collection. Mutating the result does not affect the request.
        /// </remarks>
        public static NameValueCollection GetQueryString(this HttpRequest request)
        {
            return request?.Query.ToNameValueCollection() ?? new NameValueCollection();
        }

        /// <summary>Gets the languages the client accepts, most preferred first.</summary>
        /// <param name="request">The request to read the preferences from.</param>
        /// <returns>
        /// The raw <c>Accept-Language</c> entries (each possibly carrying its <c>;q=</c> quality
        /// suffix, exactly as <c>HttpRequest.UserLanguages</c> returned them), or
        /// <see langword="null"/> when the client sent no preference.
        /// </returns>
        /// <remarks>
        /// Replaces <c>System.Web.HttpRequest.UserLanguages</c>, which has no ASP.NET Core
        /// counterpart. The entries are returned in the client's stated preference order, which is
        /// what <c>UserLanguages</c> guaranteed and what DNN's culture negotiation relies on.
        /// </remarks>
        public static string[] GetUserLanguages(this HttpRequest request)
        {
            var header = request?.Headers.AcceptLanguage;
            if (header is null || header.Value.Count == 0)
            {
                return null;
            }

            var languages = new List<string>();
            foreach (var value in header.Value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                foreach (var entry in value.Split(','))
                {
                    var trimmed = entry.Trim();
                    if (trimmed.Length > 0)
                    {
                        languages.Add(trimmed);
                    }
                }
            }

            if (languages.Count == 0)
            {
                return null;
            }

            // Order by the quality value so the most preferred language comes first, which is the
            // order System.Web materialised UserLanguages in. The sort is stable, so entries that
            // share a quality keep their header order.
            languages.Sort((left, right) => GetQuality(right).CompareTo(GetQuality(left)));
            return languages.ToArray();

            static double GetQuality(string language)
            {
                var separator = language.IndexOf(";q=", StringComparison.OrdinalIgnoreCase);
                if (separator < 0)
                {
                    return 1d;
                }

                return double.TryParse(
                    language.Substring(separator + 3),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var quality)
                    ? quality
                    : 1d;
            }
        }

        /// <summary>Copies an ASP.NET Core query collection into a <see cref="NameValueCollection"/>.</summary>
        /// <param name="query">The query collection to copy.</param>
        /// <returns>A new collection holding every key and value; never <see langword="null"/>.</returns>
        public static NameValueCollection ToNameValueCollection(this IQueryCollection query)
        {
            var collection = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            if (query == null)
            {
                return collection;
            }

            foreach (var pair in query)
            {
                foreach (var value in pair.Value)
                {
                    collection.Add(pair.Key, value);
                }
            }

            return collection;
        }

        /// <summary>
        /// Gets the absolute URI of the current request.
        /// </summary>
        /// <param name="request">The current request.</param>
        /// <returns>The absolute request URI, or <see langword="null"/> when there is no request.</returns>
        /// <remarks>
        /// Replaces <c>System.Web.HttpRequest.Url</c>. Prefer the more specific
        /// <see cref="HttpRequest.Scheme"/>/<see cref="HttpRequest.Host"/>/<see cref="HttpRequest.Path"/>
        /// members where only one component is needed.
        /// </remarks>
        public static Uri GetUrl(this HttpRequest request)
        {
            if (request == null)
            {
                return null;
            }

            return new Uri($"{request.Scheme}://{request.Host.Value}{request.PathBase.Value}{request.Path.Value}{request.QueryString.Value}");
        }
    }
}
