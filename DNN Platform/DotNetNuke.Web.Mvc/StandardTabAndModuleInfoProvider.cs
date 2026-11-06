// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc
{
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Instrumentation;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    public sealed class StandardTabAndModuleInfoProvider : ITabAndModuleInfoProvider
    {
        private const string ModuleIdKey = "ModuleId";
        private const string TabIdKey = "TabId";
        private const string MonikerQueryKey = "Moniker";
        private const string MonikerHeaderKey = "X-DNN-MONIKER";
        private const string MonikerSettingsKey = "Moniker";
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<StandardTabAndModuleInfoProvider>();

        /// <inheritdoc />
        public bool TryFindTabId(HttpRequest request, out int tabId)
        {
            return TryFindTabId(request, out tabId, true);
        }

        /// <inheritdoc />
        public bool TryFindModuleId(HttpRequest request, out int moduleId)
        {
            return TryFindModuleId(request, out moduleId, true);
        }

        /// <inheritdoc />
        public bool TryFindModuleInfo(HttpRequest request, out ModuleInfo moduleInfo)
        {
            int tabId, moduleId;
            if (TryFindTabId(request, out tabId, false) && TryFindModuleId(request, out moduleId, false))
            {
                moduleInfo = ModuleController.Instance.GetModule(moduleId, tabId, false);
                return moduleInfo != null;
            }

            return TryFindByMoniker(request, out moduleInfo);
        }

        private static bool TryFindTabId(HttpRequest request, out int tabId, bool tryMoniker)
        {
            tabId = FindInt(request, TabIdKey);
            if (tabId > Null.NullInteger)
            {
                return true;
            }

            if (tryMoniker)
            {
                ModuleInfo moduleInfo;
                if (TryFindByMoniker(request, out moduleInfo))
                {
                    tabId = moduleInfo.TabID;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindModuleId(HttpRequest request, out int moduleId, bool tryMoniker)
        {
            moduleId = FindInt(request, ModuleIdKey);
            if (moduleId > Null.NullInteger)
            {
                return true;
            }

            if (tryMoniker)
            {
                ModuleInfo moduleInfo;
                if (TryFindByMoniker(request, out moduleInfo))
                {
                    moduleId = moduleInfo.ModuleID;
                    return true;
                }
            }

            return false;
        }

        private static int FindInt(HttpRequest request, string key)
        {
            var value = FindValue(request, key);

            int id;
            return int.TryParse(value, out id) ? id : Null.NullInteger;
        }

        /// <summary>Reads a value from the request headers, falling back to the query string and the route values.</summary>
        private static string FindValue(HttpRequest request, string key)
        {
            if (request is null)
            {
                return null;
            }

            if (request.Headers.TryGetValue(key, out var headerValues) && !StringValues.IsNullOrEmpty(headerValues))
            {
                return headerValues.ToString();
            }

            if (request.Query.TryGetValue(key, out var queryValues) && !StringValues.IsNullOrEmpty(queryValues))
            {
                return queryValues.ToString();
            }

            var routeValue = request.HttpContext?.GetRouteValue(key);
            return routeValue?.ToString();
        }

        private static bool TryFindByMoniker(HttpRequest requestBase, out ModuleInfo moduleInfo)
        {
            var id = FindIntInHeader(requestBase, MonikerHeaderKey);
            if (id <= Null.NullInteger)
            {
                id = FindIntInQueryString(requestBase, MonikerQueryKey);
            }

            moduleInfo = id > Null.NullInteger ? ModuleController.Instance.GetTabModule(id) : null;
            return moduleInfo != null;
        }

        private static int FindIntInHeader(HttpRequest requestBase, string key)
        {
            string value = null;
            if (requestBase is not null && requestBase.Headers.TryGetValue(key, out var headerValues) && !StringValues.IsNullOrEmpty(headerValues))
            {
                value = headerValues.ToString();
            }

            return GetTabModuleInfoFromMoniker(value);
        }

        private static int FindIntInQueryString(HttpRequest requestBase, string key)
        {
            string value = null;
            if (requestBase is not null && requestBase.Query.TryGetValue(key, out var queryValues) && !StringValues.IsNullOrEmpty(queryValues))
            {
                value = queryValues.ToString();
            }

            return GetTabModuleInfoFromMoniker(value);
        }

        private static int GetTabModuleInfoFromMoniker(string monikerValue)
        {
            monikerValue = (monikerValue ?? string.Empty).Trim();
            if (monikerValue.Length > 0)
            {
                var ids = TabModulesController.Instance.GetTabModuleIdsBySetting(MonikerSettingsKey, monikerValue);
                if (ids != null && ids.Any())
                {
                    return ids.First();
                }

                Logger.StandardTabAndModuleInfoProviderMonikerIsNotDefined(monikerValue);
            }

            return Null.NullInteger;
        }
    }
}
