// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    using System.Web;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Services.UserRequest;

    internal static class HttpRequestExtensions
    {
        private delegate bool TryMethod<T>(ITabAndModuleInfoProvider provider, HttpRequestBase request, out T output);

        public static int FindTabId(this HttpRequestBase request)
        {
            return IterateTabAndModuleInfoProviders(request, TryFindTabId, -1);
        }

        public static ModuleInfo FindModuleInfo(this HttpRequestBase request)
        {
            return IterateTabAndModuleInfoProviders<ModuleInfo>(request, TryFindModuleInfo, null);
        }

        public static int FindModuleId(this HttpRequestBase request)
        {
            return IterateTabAndModuleInfoProviders(request, TryFindModuleId, -1);
        }

        public static string GetIPAddress(HttpRequestBase request)
        {
            return UserRequestIPAddressController.Instance.GetUserRequestIPAddress(request);
        }

        private static bool TryFindTabId(ITabAndModuleInfoProvider provider, HttpRequestBase request, out int output)
        {
            return provider.TryFindTabId(request, out output);
        }

        private static bool TryFindModuleInfo(ITabAndModuleInfoProvider provider, HttpRequestBase request, out ModuleInfo output)
        {
            return provider.TryFindModuleInfo(request, out output);
        }

        private static bool TryFindModuleId(ITabAndModuleInfoProvider provider, HttpRequestBase request, out int output)
        {
            return provider.TryFindModuleId(request, out output);
        }

        private static T IterateTabAndModuleInfoProviders<T>(HttpRequestBase request, TryMethod<T> func, T fallback)
        {
            var providers = TabAndModuleInfoProviderRegistry.GetTabAndModuleInfoProviders();

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
