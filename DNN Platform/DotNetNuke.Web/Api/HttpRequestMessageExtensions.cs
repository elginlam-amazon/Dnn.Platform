// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

#nullable enable
namespace DotNetNuke.Web.Api
{
    using System;
    using System.Net.Http;
    using System.Web;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Services.UserRequest;

    using HttpRequest = Microsoft.AspNetCore.Http.HttpRequest;

    /// <summary>Extension methods for the request of a services framework call.</summary>
    public static class HttpRequestMessageExtensions
    {
        private delegate bool TryMethod<T>(ITabAndModuleInfoProvider provider, HttpRequest request, out T output);

        /// <summary>Finds the tab ID of the request.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The tab ID or <c>-1</c>.</returns>
        public static int FindTabId(this HttpRequest request)
        {
            return IterateTabAndModuleInfoProviders(request, TryFindTabId, -1);
        }

        /// <summary>Finds the module of the request.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The module or <see langword="null"/>.</returns>
        public static ModuleInfo? FindModuleInfo(this HttpRequest request)
        {
            return IterateTabAndModuleInfoProviders<ModuleInfo>(request, TryFindModuleInfo, null);
        }

        /// <summary>Finds the module ID of the request.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The module ID or <c>-1</c>.</returns>
        public static int FindModuleId(this HttpRequest request)
        {
            return IterateTabAndModuleInfoProviders(request, TryFindModuleId, -1);
        }

        /// <summary>Gets the System.Web context of the request.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The <see cref="HttpContextBase"/> or <see langword="null"/>.</returns>
        public static HttpContextBase? GetHttpContext(this HttpRequest request)
        {
            if (request?.HttpContext is null)
            {
                return null;
            }

            System.Web.HttpContext context = request.HttpContext;
            return new HttpContextWrapper(context);
        }

        /// <summary>Gets the IP address of the client of the request.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The IP address.</returns>
        public static string GetIPAddress(this HttpRequest request)
        {
            var context = GetHttpContext(request);
            if (context is null)
            {
                throw new InvalidOperationException("Request does not have an associated HTTP Context, cannot retrieve IP address");
            }

            return UserRequestIPAddressController.Instance.GetUserRequestIPAddress(context.Request);
        }

        /// <summary>Finds the tab ID of the request (for the authentication message handlers).</summary>
        /// <param name="request">The request message.</param>
        /// <returns>The tab ID or <c>-1</c>.</returns>
        public static int FindTabId(this HttpRequestMessage request)
        {
            return GetCoreRequest(request)?.FindTabId() ?? -1;
        }

        /// <summary>Finds the module of the request (for the authentication message handlers).</summary>
        /// <param name="request">The request message.</param>
        /// <returns>The module or <see langword="null"/>.</returns>
        public static ModuleInfo? FindModuleInfo(this HttpRequestMessage request)
        {
            return GetCoreRequest(request)?.FindModuleInfo();
        }

        /// <summary>Finds the module ID of the request (for the authentication message handlers).</summary>
        /// <param name="request">The request message.</param>
        /// <returns>The module ID or <c>-1</c>.</returns>
        public static int FindModuleId(this HttpRequestMessage request)
        {
            return GetCoreRequest(request)?.FindModuleId() ?? -1;
        }

        /// <summary>Gets the System.Web context of the request message created by the services framework for the authentication message handlers.</summary>
        /// <param name="request">The request message.</param>
        /// <returns>The <see cref="HttpContextBase"/> or <see langword="null"/>.</returns>
        public static HttpContextBase? GetHttpContext(this HttpRequestMessage request)
        {
            if (request is null || !request.Options.TryGetValue(ServicesFrameworkRequestMessage.HttpContextKey, out var context))
            {
                return null;
            }

            return context;
        }

        /// <summary>Gets the IP address of the client of the request message.</summary>
        /// <param name="request">The request message.</param>
        /// <returns>The IP address.</returns>
        public static string GetIPAddress(this HttpRequestMessage request)
        {
            var context = GetHttpContext(request);
            if (context is null)
            {
                throw new InvalidOperationException("Request does not have an associated HTTP Context, cannot retrieve IP address");
            }

            return UserRequestIPAddressController.Instance.GetUserRequestIPAddress(context.Request);
        }

        private static HttpRequest? GetCoreRequest(HttpRequestMessage request)
        {
            if (request is null || !request.Options.TryGetValue(ServicesFrameworkRequestMessage.CoreHttpContextKey, out var context))
            {
                return null;
            }

            return context.Request;
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
            foreach (var provider in TabAndModuleInfoProviderRegistry.GetTabAndModuleInfoProviders())
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
