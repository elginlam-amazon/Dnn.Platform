// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

#nullable enable
namespace DotNetNuke.Web.Api
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    using DotNetNuke.Common;

    /// <summary>The registry of the <see cref="ITabAndModuleInfoProvider"/> instances used by the services framework (replaces the Web API <c>HttpConfiguration</c> properties).</summary>
    public static class TabAndModuleInfoProviderRegistry
    {
        private static readonly ConcurrentQueue<ITabAndModuleInfoProvider> Providers = new ConcurrentQueue<ITabAndModuleInfoProvider>();

        /// <summary>Adds a tab and module info provider.</summary>
        /// <param name="tabAndModuleInfoProvider">The provider.</param>
        public static void AddTabAndModuleInfoProvider(ITabAndModuleInfoProvider tabAndModuleInfoProvider)
        {
            Requires.NotNull("tabAndModuleInfoProvider", tabAndModuleInfoProvider);
            Providers.Enqueue(tabAndModuleInfoProvider);
        }

        /// <summary>Gets the registered tab and module info providers.</summary>
        /// <returns>A sequence of <see cref="ITabAndModuleInfoProvider"/> instances.</returns>
        public static IEnumerable<ITabAndModuleInfoProvider> GetTabAndModuleInfoProviders()
        {
            return Providers.ToArray();
        }
    }
}
