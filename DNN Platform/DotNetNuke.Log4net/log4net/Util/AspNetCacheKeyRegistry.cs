// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
//
// Licensed to the Apache Software Foundation (ASF) under one or more
// contributor license agreements. See the NOTICE file distributed with
// this work for additional information regarding copyright ownership.
// The ASF licenses this file to you under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with
// the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System.Collections.Generic;

namespace log4net.Util
{
    /// <summary>
    /// Registry of cache keys that log4net is allowed to read back out of the application's
    /// <c>Microsoft.Extensions.Caching.Memory.IMemoryCache</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The classic <c>HttpRuntime.Cache</c> was enumerable, which is how the <c>%aspnet-cache</c>
    /// pattern converter could render every cache entry. <c>IMemoryCache</c> deliberately exposes
    /// no key enumeration, so log4net keeps its own key list instead. The hosting application
    /// registers the keys it wants visible in the logs:
    /// </para>
    /// <code>
    /// log4net.Util.AspNetCacheKeyRegistry.Register("MyCacheKey");
    /// </code>
    /// <para>
    /// Keys that are registered but no longer present in the cache render as the usual
    /// <see cref="SystemInfo.NullText"/> marker. All members are thread safe and never throw.
    /// </para>
    /// </remarks>
    public static class AspNetCacheKeyRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<object> KeyList = new List<object>();

        /// <summary>
        /// Adds a cache key to the registry so that it appears in the "all cache items" output.
        /// </summary>
        /// <param name="key">The cache key. Ignored when <c>null</c>.</param>
        public static void Register(object key)
        {
            if (key == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (!KeyList.Contains(key))
                {
                    KeyList.Add(key);
                }
            }
        }

        /// <summary>
        /// Removes a cache key from the registry.
        /// </summary>
        /// <param name="key">The cache key. Ignored when <c>null</c>.</param>
        public static void Unregister(object key)
        {
            if (key == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                KeyList.Remove(key);
            }
        }

        /// <summary>
        /// Removes every registered cache key.
        /// </summary>
        public static void Clear()
        {
            lock (SyncRoot)
            {
                KeyList.Clear();
            }
        }

        /// <summary>
        /// Gets a snapshot of the registered cache keys.
        /// </summary>
        /// <returns>A copy of the registered keys; never <c>null</c>.</returns>
        public static object[] GetKeys()
        {
            lock (SyncRoot)
            {
                return KeyList.ToArray();
            }
        }
    }
}
