// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Cache
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;

    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    /// The DNN cache store. Provides the <c>System.Web.Caching.Cache</c> semantics that the
    /// platform relies on — keyed insert/get/remove, absolute and sliding expiration, item
    /// priority, removal callbacks, file and cache-key dependencies and <b>key enumeration</b> —
    /// on top of <see cref="IMemoryCache"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IMemoryCache"/> deliberately offers no way to enumerate its keys, yet DNN needs
    /// exactly that for <c>CachingProvider.Clear("Prefix", …)</c>, for
    /// <c>DataCache.ClearCache(prefix)</c> and for the output/module cache providers that count
    /// and purge their own keys. This class therefore maintains its own key registry, kept in
    /// sync with the underlying cache through post-eviction callbacks.
    /// </para>
    /// <para>
    /// Cache-key dependencies (one entry invalidated when another entry changes) are implemented
    /// with a <see cref="CancellationChangeToken"/> per cache key: whenever an entry is removed,
    /// replaced or expires, the token associated with its key is cancelled, which evicts every
    /// entry that declared a dependency on it.
    /// </para>
    /// </remarks>
    public sealed class DnnCacheStore
    {
        /// <summary>Indicates that an item has no absolute expiration. Same value as the legacy <c>Cache.NoAbsoluteExpiration</c>.</summary>
        public static readonly DateTime NoAbsoluteExpiration = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);

        /// <summary>Indicates that sliding expiration is disabled. Same value as the legacy <c>Cache.NoSlidingExpiration</c>.</summary>
        public static readonly TimeSpan NoSlidingExpiration = TimeSpan.Zero;

        private static readonly DnnCacheStore InstanceField = new DnnCacheStore();

        private readonly ConcurrentDictionary<string, CacheEntryRecord> entries =
            new ConcurrentDictionary<string, CacheEntryRecord>(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, CancellationTokenSource> keyDependencies =
            new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.Ordinal);

        private long generationCounter;

        private DnnCacheStore()
        {
        }

        /// <summary>Gets the singleton cache store.</summary>
        public static DnnCacheStore Instance => InstanceField;

        /// <summary>Gets the number of items currently tracked in the cache.</summary>
        public int Count => this.entries.Count;

        /// <summary>Gets or sets the cached value for a key.</summary>
        /// <param name="key">The cache key.</param>
        /// <returns>The cached value, or <see langword="null"/>.</returns>
        public object this[string key]
        {
            get => this.Get(key);
            set => this.Insert(key, value);
        }

        /// <summary>Gets the cached value for a key.</summary>
        /// <param name="key">The cache key.</param>
        /// <returns>The cached value, or <see langword="null"/> when the key is not cached.</returns>
        public object Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return MemoryCacheSource.Current.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>Gets a snapshot of the keys currently in the cache.</summary>
        /// <returns>A list of cache keys.</returns>
        public IList<string> GetKeys()
        {
            return new List<string>(this.entries.Keys);
        }

        /// <summary>Enumerates the cache, preserving the legacy <c>Cache.GetEnumerator()</c> behavior.</summary>
        /// <returns>An enumerator over key/value pairs.</returns>
        public IDictionaryEnumerator GetEnumerator()
        {
            var snapshot = new Hashtable(this.entries.Count, StringComparer.Ordinal);
            foreach (var pair in this.entries)
            {
                snapshot[pair.Key] = pair.Value.Value;
            }

            return snapshot.GetEnumerator();
        }

        /// <summary>Inserts an item into the cache with no dependency, no expiration and default priority.</summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        public void Insert(string key, object value)
        {
            this.Insert(key, value, null, NoAbsoluteExpiration, NoSlidingExpiration, CacheItemPriority.Default, null);
        }

        /// <summary>Inserts an item into the cache.</summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="dependency">The dependency which invalidates the item, or <see langword="null"/>.</param>
        public void Insert(string key, object value, DNNCacheDependency dependency)
        {
            this.Insert(key, value, dependency, NoAbsoluteExpiration, NoSlidingExpiration, CacheItemPriority.Default, null);
        }

        /// <summary>Inserts an item into the cache.</summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="dependency">The dependency which invalidates the item, or <see langword="null"/>.</param>
        /// <param name="absoluteExpiration">The absolute expiration, or <see cref="NoAbsoluteExpiration"/>.</param>
        /// <param name="slidingExpiration">The sliding expiration, or <see cref="NoSlidingExpiration"/>.</param>
        public void Insert(string key, object value, DNNCacheDependency dependency, DateTime absoluteExpiration, TimeSpan slidingExpiration)
        {
            this.Insert(key, value, dependency, absoluteExpiration, slidingExpiration, CacheItemPriority.Default, null);
        }

        /// <summary>Inserts an item into the cache.</summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="dependency">The dependency which invalidates the item, or <see langword="null"/>.</param>
        /// <param name="absoluteExpiration">The absolute expiration, or <see cref="NoAbsoluteExpiration"/>.</param>
        /// <param name="slidingExpiration">The sliding expiration, or <see cref="NoSlidingExpiration"/>.</param>
        /// <param name="priority">The relative priority of the item.</param>
        /// <param name="onRemoveCallback">A callback invoked once the item leaves the cache, or <see langword="null"/>.</param>
        public void Insert(
            string key,
            object value,
            DNNCacheDependency dependency,
            DateTime absoluteExpiration,
            TimeSpan slidingExpiration,
            CacheItemPriority priority,
            CacheItemRemovedCallback onRemoveCallback)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Argument cannot be null or an empty string", nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var generation = Interlocked.Increment(ref this.generationCounter);
            var options = new MemoryCacheEntryOptions { Priority = MapPriority(priority) };

            if (absoluteExpiration != NoAbsoluteExpiration && absoluteExpiration != DateTime.MaxValue)
            {
                options.AbsoluteExpiration = new DateTimeOffset(absoluteExpiration.ToUniversalTime(), TimeSpan.Zero);
            }

            if (slidingExpiration != NoSlidingExpiration && slidingExpiration > TimeSpan.Zero && slidingExpiration < TimeSpan.MaxValue)
            {
                options.SlidingExpiration = slidingExpiration;
            }

            if (dependency != null)
            {
                foreach (var token in dependency.GetChangeTokens())
                {
                    options.AddExpirationToken(token);
                }
            }

            options.RegisterPostEvictionCallback(
                (evictedKey, evictedValue, reason, state) =>
                    this.OnEvicted(Convert.ToString(evictedKey), evictedValue, reason, generation, onRemoveCallback));

            // Track the key before handing the entry to the memory cache so that the registry is
            // never missing a live entry (the eviction callback for the replaced entry, if any,
            // is generation-guarded and will not delete this record).
            this.entries[key] = new CacheEntryRecord(generation, value);
            MemoryCacheSource.Current.Set(key, value, options);
        }

        /// <summary>Removes an item from the cache.</summary>
        /// <param name="key">The cache key.</param>
        /// <returns>The value that was removed, or <see langword="null"/>.</returns>
        public object Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            var existing = this.Get(key);

            // Removing triggers the post-eviction callback, which clears the registry entry and
            // cancels the key dependency token.
            MemoryCacheSource.Current.Remove(key);

            if (this.entries.TryRemove(key, out _))
            {
                this.CancelKeyDependency(key);
            }

            return existing;
        }

        /// <summary>Gets a change token that fires when the cached item with the given key is removed, replaced or expires.</summary>
        /// <param name="key">The cache key to depend upon.</param>
        /// <returns>An <see cref="IChangeToken"/> for the key.</returns>
        /// <remarks>
        /// This reproduces the cache-key half of the legacy <c>CacheDependency</c>. Note one
        /// deliberate deviation: the legacy implementation considered a dependency on a key that is
        /// not currently cached to have changed <i>immediately</i>, which under ASP.NET Core would
        /// evict the dependent entry on the very insert that created it. Here the dependency simply
        /// waits for the first change of that key.
        /// </remarks>
        internal IChangeToken GetKeyChangeToken(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return Microsoft.Extensions.FileProviders.NullChangeToken.Singleton;
            }

            var source = this.keyDependencies.GetOrAdd(key, _ => new CancellationTokenSource());
            try
            {
                return new CancellationChangeToken(source.Token);
            }
            catch (ObjectDisposedException)
            {
                // The source was cancelled and disposed concurrently; start a new generation.
                var replacement = this.keyDependencies.AddOrUpdate(key, _ => new CancellationTokenSource(), (_, existing) => existing.IsCancellationRequested ? new CancellationTokenSource() : existing);
                return new CancellationChangeToken(replacement.Token);
            }
        }

        private static Microsoft.Extensions.Caching.Memory.CacheItemPriority MapPriority(CacheItemPriority priority)
        {
            switch (priority)
            {
                case CacheItemPriority.NotRemovable:
                    return Microsoft.Extensions.Caching.Memory.CacheItemPriority.NeverRemove;
                case CacheItemPriority.High:
                case CacheItemPriority.AboveNormal:
                    return Microsoft.Extensions.Caching.Memory.CacheItemPriority.High;
                case CacheItemPriority.BelowNormal:
                case CacheItemPriority.Low:
                    return Microsoft.Extensions.Caching.Memory.CacheItemPriority.Low;
                default:
                    return Microsoft.Extensions.Caching.Memory.CacheItemPriority.Normal;
            }
        }

        private static CacheItemRemovedReason MapReason(EvictionReason reason)
        {
            switch (reason)
            {
                case EvictionReason.Expired:
                    return CacheItemRemovedReason.Expired;
                case EvictionReason.TokenExpired:
                    return CacheItemRemovedReason.DependencyChanged;
                case EvictionReason.Capacity:
                    return CacheItemRemovedReason.Underused;
                default:
                    return CacheItemRemovedReason.Removed;
            }
        }

        private void OnEvicted(string key, object value, EvictionReason reason, long generation, CacheItemRemovedCallback onRemoveCallback)
        {
            if (!string.IsNullOrEmpty(key))
            {
                // Only drop the registry record if it still belongs to this generation; a newer
                // Insert for the same key must keep its record.
                if (this.entries.TryGetValue(key, out var record) && record.Generation == generation)
                {
                    this.entries.TryRemove(key, out _);
                }

                // Any entry that declared a dependency on this key must now be invalidated.
                this.CancelKeyDependency(key);
            }

            if (onRemoveCallback == null)
            {
                return;
            }

            try
            {
                onRemoveCallback(key, value, MapReason(reason));
            }
            catch (Exception ex)
            {
                // A faulty removal callback must never take down the cache or the request.
                Trace.TraceError($"DNN cache item removed callback failed for key '{key}': {ex}");
            }
        }

        private void CancelKeyDependency(string key)
        {
            if (this.keyDependencies.TryRemove(key, out var source))
            {
                try
                {
                    source.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Already cancelled and disposed elsewhere.
                }
                finally
                {
                    source.Dispose();
                }
            }
        }

        private sealed class CacheEntryRecord
        {
            public CacheEntryRecord(long generation, object value)
            {
                this.Generation = generation;
                this.Value = value;
            }

            public long Generation { get; }

            public object Value { get; }
        }
    }
}
