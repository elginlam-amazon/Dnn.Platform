// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Api
{
    using System.Collections.Concurrent;
    using System.Reflection;

    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Helpers for the <see cref="ITabAndModuleInfoProvider"/> fixtures. On .NET Framework the providers
    /// lived in the <c>Properties</c> dictionary of a per-test <c>HttpConfiguration</c> instance, which
    /// made every test isolated for free. In ASP.NET Core they are registered in the request container,
    /// plus a process-wide collection for the providers added after the container was built, so the
    /// fixtures build a request container per test and reset that process-wide collection in their setup.
    /// </summary>
    internal static class TabAndModuleInfoProviderTestUtilities
    {
        /// <summary>
        /// Empties the process-wide provider collection of
        /// <see cref="HttpConfigurationExtensions"/>, restoring the per-test isolation the legacy
        /// <c>HttpConfiguration.Properties</c> dictionary provided.
        /// </summary>
        internal static void ClearRuntimeProviders()
        {
            var field = typeof(HttpConfigurationExtensions)
                .GetField("RuntimeProviders", BindingFlags.Static | BindingFlags.NonPublic);
            if (field?.GetValue(null) is ConcurrentQueue<ITabAndModuleInfoProvider> providers)
            {
                while (providers.TryDequeue(out _))
                {
                    // drain the queue
                }
            }
        }

        /// <summary>Creates a request whose request container has the given <paramref name="providers"/> registered.</summary>
        /// <param name="providers">The providers to register, in order.</param>
        /// <returns>The <see cref="HttpRequest"/> of a new <see cref="DefaultHttpContext"/>.</returns>
        internal static HttpRequest CreateRequestWithProviders(params ITabAndModuleInfoProvider[] providers)
        {
            var services = new ServiceCollection();
            foreach (var provider in providers)
            {
                services.AddSingleton(provider);
            }

            var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider(), };
            return httpContext.Request;
        }
    }
}
