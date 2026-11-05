// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.IO;

    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    /// Thin wrapper around the application's cache, copied from DotNetNuke Core.
    /// </summary>
    /// <remarks>
    /// The WebForms implementation used <c>HttpRuntime.Cache</c>; on ASP.NET Core the
    /// <see cref="IMemoryCache"/> resolved through <see cref="DnnWebUtilityContext.Cache"/> is used
    /// instead. Every member keeps its original name and signature, with
    /// <c>System.Web.Caching.CacheDependency</c> replaced by
    /// <see cref="MemoryCacheEntryOptions"/>/<see cref="IChangeToken"/>. All members are no-ops (or
    /// return <see langword="null"/>) when the library was not initialized, mirroring the original
    /// code's tolerance of running outside of a request.
    /// </remarks>
    public class DataCache
    {
        /// <summary>Gets the item cached under the given key.</summary>
        /// <param name="cacheKey">The key the item was cached under.</param>
        /// <returns>The cached item, or <see langword="null"/> when it is not (or no longer) cached.</returns>
        public static object GetCache(string cacheKey)
        {
            var objCache = DnnWebUtilityContext.Cache;
            if (objCache is null)
            {
                return null;
            }

            return objCache.TryGetValue(cacheKey, out var value) ? value : null;
        }

        /// <summary>Caches an item under the given key, without any expiration.</summary>
        /// <param name="cacheKey">The key to cache the item under.</param>
        /// <param name="objObject">The item to cache.</param>
        public static void SetCache(string cacheKey, object objObject)
        {
            SetCache(cacheKey, objObject, (MemoryCacheEntryOptions)null);
        }

        /// <summary>Caches an item under the given key with the given entry options.</summary>
        /// <param name="cacheKey">The key to cache the item under.</param>
        /// <param name="objObject">The item to cache.</param>
        /// <param name="objDependency">
        /// The entry options (expiration tokens, expiration times, callbacks) the entry depends on, or
        /// <see langword="null"/> for none. Replaces the original
        /// <c>System.Web.Caching.CacheDependency</c> argument.
        /// </param>
        public static void SetCache(string cacheKey, object objObject, MemoryCacheEntryOptions objDependency)
        {
            var objCache = DnnWebUtilityContext.Cache;
            if (objCache is null)
            {
                return;
            }

            if (objDependency is null)
            {
                objCache.Set(cacheKey, objObject);
            }
            else
            {
                objCache.Set(cacheKey, objObject, objDependency);
            }
        }

        /// <summary>
        /// Caches an item under the given key with the given entry options, absolute expiration and
        /// sliding expiration.
        /// </summary>
        /// <param name="cacheKey">The key to cache the item under.</param>
        /// <param name="objObject">The item to cache.</param>
        /// <param name="objDependency">
        /// The entry options the entry depends on, or <see langword="null"/> for none. Replaces the
        /// original <c>System.Web.Caching.CacheDependency</c> argument.
        /// </param>
        /// <param name="absoluteExpiration">
        /// The moment the entry expires, or <see cref="DateTime.MaxValue"/> for no absolute expiration
        /// (the equivalent of the original <c>Cache.NoAbsoluteExpiration</c>).
        /// </param>
        /// <param name="slidingExpiration">
        /// The period of inactivity after which the entry expires, or <see cref="TimeSpan.Zero"/> for no
        /// sliding expiration (the equivalent of the original <c>Cache.NoSlidingExpiration</c>).
        /// </param>
        public static void SetCache(string cacheKey, object objObject, MemoryCacheEntryOptions objDependency, DateTime absoluteExpiration, TimeSpan slidingExpiration)
        {
            var options = objDependency ?? new MemoryCacheEntryOptions();

            if (absoluteExpiration != DateTime.MaxValue && absoluteExpiration != DateTime.MinValue)
            {
                options.AbsoluteExpiration = new DateTimeOffset(absoluteExpiration.ToUniversalTime(), TimeSpan.Zero);
            }

            if (slidingExpiration > TimeSpan.Zero)
            {
                options.SlidingExpiration = slidingExpiration;
            }

            SetCache(cacheKey, objObject, options);
        }

        /// <summary>Caches an item under the given key with a sliding expiration in seconds.</summary>
        /// <param name="cacheKey">The key to cache the item under.</param>
        /// <param name="objObject">The item to cache.</param>
        /// <param name="slidingExpiration">The number of seconds of inactivity after which the entry expires.</param>
        public static void SetCache(string cacheKey, object objObject, int slidingExpiration)
        {
            SetCache(
                cacheKey,
                objObject,
                new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromSeconds(slidingExpiration) });
        }

        /// <summary>Caches an item under the given key until the given moment.</summary>
        /// <param name="cacheKey">The key to cache the item under.</param>
        /// <param name="objObject">The item to cache.</param>
        /// <param name="absoluteExpiration">The moment the entry expires.</param>
        public static void SetCache(string cacheKey, object objObject, DateTime absoluteExpiration)
        {
            SetCache(
                cacheKey,
                objObject,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = new DateTimeOffset(absoluteExpiration.ToUniversalTime(), TimeSpan.Zero),
                });
        }

        /// <summary>Removes the item cached under the given key, when there is one.</summary>
        /// <param name="cacheKey">The key the item was cached under.</param>
        public static void RemoveCache(string cacheKey)
        {
            var objCache = DnnWebUtilityContext.Cache;
            if (objCache is null)
            {
                return;
            }

            if (objCache.TryGetValue(cacheKey, out _))
            {
                objCache.Remove(cacheKey);
            }
        }

        /// <summary>
        /// Creates the entry options that make a cache entry depend on a file, the replacement for the
        /// original <c>New CacheDependency(path)</c> usage.
        /// </summary>
        /// <param name="filePath">The full path of the file the entry depends on.</param>
        /// <returns>
        /// Entry options holding a change token that fires when the file changes, or empty options when
        /// <paramref name="filePath"/> is empty.
        /// </returns>
        public static MemoryCacheEntryOptions CreateFileDependency(string filePath)
        {
            var options = new MemoryCacheEntryOptions();
            var token = CreateFileChangeToken(filePath);
            if (token is not null)
            {
                options.AddExpirationToken(token);
            }

            return options;
        }

        /// <summary>
        /// Creates a change token that fires when the given file changes, the replacement for the
        /// file-based <c>System.Web.Caching.CacheDependency</c>.
        /// </summary>
        /// <param name="filePath">The full path of the file to watch.</param>
        /// <returns>The change token, or <see langword="null"/> when the path cannot be watched.</returns>
        public static IChangeToken CreateFileChangeToken(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
                var fileName = Path.GetFileName(filePath);
                if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName) || !Directory.Exists(directory))
                {
                    return null;
                }

                var provider = new PhysicalFileProvider(directory);
                return provider.Watch(fileName);
            }
            catch (Exception)
            {
                // Mirrors the original code, which ignored failures to resolve a dependency path
                // (reverse proxies, virtual paths and the like) and simply cached without dependency.
                return null;
            }
        }
    }
}
