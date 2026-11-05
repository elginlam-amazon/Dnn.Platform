// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Entities.Modules
{
    using System.Collections.Generic;

    using DotNetNuke.Services.Tokens;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Components;

    public interface ICustomTokenProvider
    {
        /// <summary>Gets the custom tokens contributed for the current page/module.</summary>
        /// <param name="page">
        /// The component rendering the page. This was a <c>System.Web.UI.Page</c>; on ASP.NET
        /// Core the hosting page is a Blazor component, so the component contract replaces it.
        /// </param>
        /// <param name="moduleContext">The module instance context.</param>
        /// <returns>The tokens, keyed by token name.</returns>
        IDictionary<string, IPropertyAccess> GetTokens(IComponent page, ModuleInstanceContext moduleContext);
    }
}
