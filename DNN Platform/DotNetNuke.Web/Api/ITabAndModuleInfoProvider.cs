// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

#nullable enable
namespace DotNetNuke.Web.Api
{
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;

    using Microsoft.AspNetCore.Http;

    /// <summary>Provides the ability to identify the tab and module of a services framework request.</summary>
    public interface ITabAndModuleInfoProvider
    {
        /// <summary>Tries to find the tab ID of the request.</summary>
        /// <param name="request">The request.</param>
        /// <param name="tabId">The tab ID, or <see cref="Null.NullInteger"/>.</param>
        /// <returns><see langword="true"/> if the tab ID was found.</returns>
        bool TryFindTabId(HttpRequest request, out int tabId);

        /// <summary>Tries to find the module ID of the request.</summary>
        /// <param name="request">The request.</param>
        /// <param name="moduleId">The module ID, or <see cref="Null.NullInteger"/>.</param>
        /// <returns><see langword="true"/> if the module ID was found.</returns>
        bool TryFindModuleId(HttpRequest request, out int moduleId);

        /// <summary>Tries to find the module of the request.</summary>
        /// <param name="request">The request.</param>
        /// <param name="moduleInfo">The module, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the module was found.</returns>
        bool TryFindModuleInfo(HttpRequest request, out ModuleInfo? moduleInfo);
    }
}
