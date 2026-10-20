// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    using DotNetNuke.Common;

    /// <summary>A registry of <see cref="ITabAndModuleInfoProvider"/> instances used to resolve the current tab and module.</summary>
    /// <remarks>Replaces the former <c>System.Web.Http.HttpConfiguration</c>-based registration used on ASP.NET MVC 5.</remarks>
    public static class TabAndModuleInfoProviderRegistry
    {
        private static readonly ConcurrentQueue<ITabAndModuleInfoProvider> Providers = new ConcurrentQueue<ITabAndModuleInfoProvider>();

        /// <summary>Adds a provider to the registry.</summary>
        /// <param name="tabAndModuleInfoProvider">The provider to add.</param>
        public static void AddTabAndModuleInfoProvider(ITabAndModuleInfoProvider tabAndModuleInfoProvider)
        {
            Requires.NotNull("tabAndModuleInfoProvider", tabAndModuleInfoProvider);
            Providers.Enqueue(tabAndModuleInfoProvider);
        }

        /// <summary>Gets all registered providers.</summary>
        /// <returns>The registered providers.</returns>
        public static IEnumerable<ITabAndModuleInfoProvider> GetTabAndModuleInfoProviders()
        {
            return Providers.ToArray();
        }
    }
}
