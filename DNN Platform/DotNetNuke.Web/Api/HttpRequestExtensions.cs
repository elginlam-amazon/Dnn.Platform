// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

#nullable enable
namespace DotNetNuke.Web.Api
{
    using System;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Services.UserRequest;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Extension methods for <see cref="HttpRequest"/>. These were the
    /// <c>HttpRequestMessageExtensions</c> of Web API 2; <c>HttpRequestMessage</c> is a client type in
    /// ASP.NET Core, so every extension now hangs off the server-side <see cref="HttpRequest"/>. The
    /// method names are unchanged.
    /// </summary>
    public static class HttpRequestExtensions
    {
        private delegate bool TryMethod<T>(ITabAndModuleInfoProvider provider, HttpRequest request, out T output);

        /// <summary>Gets the Tab ID associated with the <paramref name="request"/>.</summary>
        /// <param name="request">The web API request.</param>
        /// <returns>The tab ID or <c>-1</c>.</returns>
        public static int FindTabId(this HttpRequest request)
        {
            return IterateTabAndModuleInfoProviders(request, TryFindTabId, -1);
        }

        /// <summary>Gets the module associated with the <paramref name="request"/>.</summary>
        /// <param name="request">The web API request.</param>
        /// <returns>The <see cref="ModuleInfo"/> instance or <see langword="null"/>.</returns>
        public static ModuleInfo? FindModuleInfo(this HttpRequest request)
        {
            return IterateTabAndModuleInfoProviders<ModuleInfo>(request, TryFindModuleInfo, null);
        }

        /// <summary>Gets the module ID associated with the <paramref name="request"/>.</summary>
        /// <param name="request">The web API request.</param>
        /// <returns>The module ID or <c>-1</c>.</returns>
        public static int FindModuleId(this HttpRequest request)
        {
            return IterateTabAndModuleInfoProviders(request, TryFindModuleId, -1);
        }

        /// <summary>Gets the <see cref="HttpContext"/> connected to the <paramref name="request"/>.</summary>
        /// <param name="request">The web API request.</param>
        /// <returns>The <see cref="HttpContext"/> or <see langword="null"/>.</returns>
        /// <remarks>Replaces reading the <c>MS_HttpContext</c> property of the legacy request message.</remarks>
        public static HttpContext? GetHttpContext(this HttpRequest request)
        {
            return request?.HttpContext;
        }

        /// <summary>Get the IP address of the request.</summary>
        /// <param name="request">The web API request.</param>
        /// <returns>The IPv4 address or <see cref="string.Empty"/> if the IP address of the request is not available.</returns>
        public static string GetIPAddress(this HttpRequest request)
        {
            var context = GetHttpContext(request);
            if (context is null)
            {
                throw new InvalidOperationException("Request does not have an associated HTTP Context, cannot retrieve IP address");
            }

            return UserRequestIPAddressController.Instance.GetUserRequestIPAddress(context.Request);
        }

        /// <summary>Gets the query string of the <paramref name="request"/> as a dictionary.</summary>
        /// <param name="request">The web API request.</param>
        /// <returns>A case-insensitive dictionary of the query string values.</returns>
        public static System.Collections.Generic.IDictionary<string, string> GetQueryStringDictionary(this HttpRequest request)
        {
            var dictionary = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (request is null)
            {
                return dictionary;
            }

            foreach (var pair in request.Query)
            {
                dictionary[pair.Key] = pair.Value.ToString();
            }

            return dictionary;
        }

        /// <summary>Gets a single request value, replacing the legacy <c>HttpRequest.Params[name]</c> indexer.</summary>
        /// <param name="request">The web API request.</param>
        /// <param name="name">The name of the value to read.</param>
        /// <returns>The value or <see langword="null"/> when it is not present.</returns>
        /// <remarks>
        /// ASP.NET Core has no combined <c>Params</c> collection. The legacy collection was composed of
        /// the query string, then the form, then the cookies, then the server variables, and the indexer
        /// returned the first match in that order — that lookup order is reproduced here (server
        /// variables have no Core equivalent and were never used by the callers).
        /// </remarks>
        public static string? GetParam(this HttpRequest request, string name)
        {
            if (request is null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (request.Query.TryGetValue(name, out var queryValue))
            {
                return queryValue.ToString();
            }

            if (request.HasFormContentType && request.Form.TryGetValue(name, out var formValue))
            {
                return formValue.ToString();
            }

            return request.Cookies.TryGetValue(name, out var cookieValue) ? cookieValue : null;
        }

        private static bool TryFindTabId(ITabAndModuleInfoProvider provider, HttpRequest request, out int output)
        {
            return provider.TryFindTabId(request, out output);
        }

        private static bool TryFindModuleInfo(ITabAndModuleInfoProvider provider, HttpRequest request, out ModuleInfo? output)
        {
            return provider.TryFindModuleInfo(request, out output);
        }

        private static bool TryFindModuleId(ITabAndModuleInfoProvider provider, HttpRequest request, out int output)
        {
            return provider.TryFindModuleId(request, out output);
        }

        private static T? IterateTabAndModuleInfoProviders<T>(HttpRequest request, TryMethod<T?> func, T? fallback)
        {
            var providers = request.GetTabAndModuleInfoProviders();

            foreach (var provider in providers)
            {
                if (func(provider, request, out var output))
                {
                    return output;
                }
            }

            return fallback;
        }
    }
}
