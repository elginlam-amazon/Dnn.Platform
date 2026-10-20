// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Instrumentation;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    /// <summary>The standard <see cref="ITabAndModuleInfoProvider"/> implementation.</summary>
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
            if (TryFindTabId(request, out var tabId, false) && TryFindModuleId(request, out var moduleId, false))
            {
                moduleInfo = ModuleController.Instance.GetModule(moduleId, tabId, false);
                if (moduleInfo is { IsDeleted: true, })
                {
                    moduleInfo = null;
                }

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
                if (TryFindByMoniker(request, out var moduleInfo))
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
                if (TryFindByMoniker(request, out var moduleInfo))
                {
                    moduleId = moduleInfo.ModuleID;
                    return true;
                }
            }

            return false;
        }

        private static int FindInt(HttpRequest requestMessage, string key)
        {
            string value = null;
            if (requestMessage != null && requestMessage.Headers.TryGetValue(key, out var values))
            {
                value = values.FirstOrDefault();
            }

            if (string.IsNullOrEmpty(value) && requestMessage != null)
            {
                value = requestMessage.Query[key].FirstOrDefault();
            }

            return int.TryParse(value, out var id) ? id : Null.NullInteger;
        }

        private static bool TryFindByMoniker(HttpRequest requestMessage, out ModuleInfo moduleInfo)
        {
            var id = FindIntInHeader(requestMessage, MonikerHeaderKey);
            if (id <= Null.NullInteger)
            {
                id = FindIntInQueryString(requestMessage, MonikerQueryKey);
            }

            moduleInfo = id > Null.NullInteger ? ModuleController.Instance.GetTabModule(id) : null;
            if (moduleInfo is { IsDeleted: true, })
            {
                moduleInfo = null;
            }

            return moduleInfo != null;
        }

        private static int FindIntInHeader(HttpRequest requestMessage, string key)
        {
            string value = null;
            if (requestMessage != null && requestMessage.Headers.TryGetValue(key, out var values))
            {
                value = values.FirstOrDefault();
            }

            return GetTabModuleInfoFromMoniker(value);
        }

        private static int FindIntInQueryString(HttpRequest requestMessage, string key)
        {
            string value = requestMessage?.Query[key].FirstOrDefault();

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
