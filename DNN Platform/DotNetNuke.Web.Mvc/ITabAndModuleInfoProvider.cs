// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc
{
    using DotNetNuke.Entities.Modules;
    using Microsoft.AspNetCore.Http;

    public interface ITabAndModuleInfoProvider
    {
        bool TryFindTabId(HttpRequest request, out int tabId);

        bool TryFindModuleId(HttpRequest request, out int moduleId);

        bool TryFindModuleInfo(HttpRequest request, out ModuleInfo moduleInfo);
    }
}
