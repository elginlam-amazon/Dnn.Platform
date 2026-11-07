// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Cache
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    using Microsoft.Extensions.Caching.Memory;

    /// <summary>
    /// The seam through which the static DNN caching API reaches the application's
    /// <see cref="IMemoryCache"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DNN's caching API (<see cref="DotNetNuke.Common.Utilities.DataCache"/>,
    /// <see cref="CachingProvider"/>) is static, so it cannot take an <see cref="IMemoryCache"/>
    /// through constructor injection. The host application MUST call
    /// <see cref="RegisterInstance(IMemoryCache)"/> during start-up, after
    /// <c>services.AddMemoryCache()</c>:
    /// </para>
    /// <code lang="C#">
    /// DotNetNuke.Services.Cache.MemoryCacheSource.RegisterInstance(
    ///     app.Services.GetRequiredService&lt;IMemoryCache&gt;());
    /// </code>
    /// <para>
    /// If the host forgets, this class does NOT pretend there is no cache (that would turn every
    /// lookup into a miss and hammer the database). Instead it reports the mis-configuration
    /// loudly, once, and falls back to a process-wide <see cref="MemoryCache"/> so the platform
    /// keeps working. <see cref="IsRegistered"/> tells you whether the DI instance is in use.
    /// </para>
    /// </remarks>
    public static class MemoryCacheSource
    {
        private static IMemoryCache instance;
        private static IMemoryCache fallbackInstance;
        private static int fallbackWarningLogged;

        /// <summary>Gets a value indicating whether the host application has registered its <see cref="IMemoryCache"/>.</summary>
        public static bool IsRegistered => instance != null;

        /// <summary>Gets the <see cref="IMemoryCache"/> backing the DNN cache.</summary>
        public static IMemoryCache Current
        {
            get
            {
                var registered = instance;
                if (registered != null)
                {
                    return registered;
                }

                if (Interlocked.Exchange(ref fallbackWarningLogged, 1) == 0)
                {
                    Trace.TraceError(
                        "DotNetNuke.Services.Cache.MemoryCacheSource: no IMemoryCache has been registered. " +
                        "The host application must call services.AddMemoryCache() and then " +
                        "MemoryCacheSource.RegisterInstance(app.Services.GetRequiredService<IMemoryCache>()) " +
                        "during start-up. Falling back to a private MemoryCache instance; cache entries will " +
                        "not be shared with components that resolve IMemoryCache from the container.");
                }

                return LazyInitializer.EnsureInitialized(
                    ref fallbackInstance,
                    () => new MemoryCache(new MemoryCacheOptions()));
            }
        }

        /// <summary>Registers the application's <see cref="IMemoryCache"/> with the DNN caching API.</summary>
        /// <param name="memoryCache">The memory cache resolved from the service provider.</param>
        public static void RegisterInstance(IMemoryCache memoryCache)
        {
            instance = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }
    }
}
