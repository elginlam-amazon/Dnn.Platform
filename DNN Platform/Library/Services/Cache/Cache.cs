// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Cache
{
    using System;

    /// <summary>
    /// Provides the expiration sentinel values used by the DNN caching API.
    /// </summary>
    /// <remarks>
    /// This type replaces the legacy <c>System.Web.Caching.Cache</c> for the two sentinel values
    /// (<see cref="NoAbsoluteExpiration"/> and <see cref="NoSlidingExpiration"/>) that call sites
    /// all over the platform pass to <see cref="DotNetNuke.Common.Utilities.DataCache.SetCache(string, object, DNNCacheDependency, DateTime, TimeSpan, CacheItemPriority, CacheItemRemovedCallback)"/>
    /// and <see cref="CachingProvider.Insert(string, object, DNNCacheDependency, DateTime, TimeSpan, CacheItemPriority, CacheItemRemovedCallback)"/>.
    /// The values are identical to the legacy ones, so existing call sites keep their meaning.
    /// The cache storage itself now lives in <see cref="DnnCacheStore"/>, which is backed by
    /// <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>.
    /// </remarks>
    public static class Cache
    {
        /// <summary>Used as the <c>absoluteExpiration</c> argument to indicate the item never expires at a fixed point in time.</summary>
        public static readonly DateTime NoAbsoluteExpiration = DnnCacheStore.NoAbsoluteExpiration;

        /// <summary>Used as the <c>slidingExpiration</c> argument to indicate that sliding expiration is disabled.</summary>
        public static readonly TimeSpan NoSlidingExpiration = DnnCacheStore.NoSlidingExpiration;
    }
}
