// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Caching
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.Caching;

    /// <summary>Enumeration of the System.Web <see cref="Cache"/>, which is not provided by the System.Web adapters for ASP.NET Core.</summary>
    public static class CacheEnumerationExtensions
    {
        private static readonly FieldInfo InnerCacheField = typeof(Cache).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Gets an enumerator over the items of the cache (keys and values).</summary>
        /// <param name="cache">The cache.</param>
        /// <returns>An <see cref="IDictionaryEnumerator"/>.</returns>
        public static IDictionaryEnumerator GetEnumerator(this Cache cache)
        {
            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            var objectCache = InnerCacheField?.GetValue(cache) as ObjectCache
                ?? throw new NotSupportedException("The items of the System.Web cache cannot be enumerated with this version of the System.Web adapters.");

            var items = new Hashtable();
            foreach (KeyValuePair<string, object> item in objectCache)
            {
                items[item.Key] = item.Value;
            }

            return items.GetEnumerator();
        }
    }
}
