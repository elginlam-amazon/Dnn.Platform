// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI
{
    using System;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// The browser capabilities DNN's UI decisions depend on.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core has no <c>HttpRequest.Browser</c> (<c>HttpBrowserCapabilities</c>) and no
    /// <c>browscap</c> database, so the two capabilities DNN actually consumed —
    /// <c>Browser.Crawler</c> and <c>Browser.EcmaScriptVersion</c> — are determined from the
    /// <c>User-Agent</c> header here.
    /// </remarks>
    public static class BrowserCapabilities
    {
        private static readonly string[] CrawlerTokens =
        {
            "bot", "crawl", "spider", "slurp", "mediapartners", "facebookexternalhit",
            "feedfetcher", "bingpreview", "pingdom", "ia_archiver", "curl", "wget",
            "python-requests", "httpclient", "headlesschrome", "lighthouse",
        };

        /// <summary>Determines whether the request comes from a search engine crawler.</summary>
        /// <param name="request">The current request, or <see langword="null"/> when there is none.</param>
        /// <returns><see langword="true"/> when the user agent looks like a crawler.</returns>
        /// <remarks>Replaces <c>HttpBrowserCapabilities.Crawler</c>.</remarks>
        public static bool IsCrawler(HttpRequest request)
        {
            var userAgent = GetUserAgent(request);
            if (string.IsNullOrEmpty(userAgent))
            {
                // WebForms reported Crawler = false for an unknown agent.
                return false;
            }

            foreach (var token in CrawlerTokens)
            {
                if (userAgent.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Gets the ECMAScript version the requesting browser supports.</summary>
        /// <param name="request">The current request, or <see langword="null"/> when there is none.</param>
        /// <returns>The supported ECMAScript version.</returns>
        /// <remarks>
        /// Replaces <c>HttpBrowserCapabilities.EcmaScriptVersion</c>. Every browser DNN can be
        /// reached from supports ECMAScript; a crawler (or a request with no user agent, which is
        /// how WebForms treated an unknown agent) reports version 0.0 so that the callers' legacy-UI
        /// fallbacks still trigger.
        /// </remarks>
        public static Version GetEcmaScriptVersion(HttpRequest request)
        {
            var userAgent = GetUserAgent(request);
            if (string.IsNullOrEmpty(userAgent) || IsCrawler(request))
            {
                return new Version(0, 0);
            }

            return new Version(5, 0);
        }

        private static string GetUserAgent(HttpRequest request)
        {
            if (request == null)
            {
                return null;
            }

            return request.Headers["User-Agent"].ToString();
        }
    }
}
