// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Cache
{
    /// <summary>The reason an item was removed from the cache.</summary>
    /// <remarks>
    /// Replaces the legacy <c>System.Web.Caching.CacheItemRemovedReason</c>, preserving the member
    /// names and ordinal values. The caching provider maps
    /// <see cref="Microsoft.Extensions.Caching.Memory.EvictionReason"/> onto these values.
    /// </remarks>
    public enum CacheItemRemovedReason
    {
        /// <summary>The item was removed (or replaced) by an explicit call.</summary>
        Removed = 1,

        /// <summary>The item expired (absolute or sliding expiration elapsed).</summary>
        Expired = 2,

        /// <summary>The item was evicted to free memory.</summary>
        Underused = 3,

        /// <summary>A file or cache-key dependency of the item changed.</summary>
        DependencyChanged = 4,
    }
}
