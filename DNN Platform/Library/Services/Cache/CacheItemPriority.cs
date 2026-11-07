// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Cache
{
    /// <summary>
    /// The relative priority of a cached item, used by the caching provider to decide which items
    /// to evict first when the process comes under memory pressure.
    /// </summary>
    /// <remarks>
    /// This enumeration replaces the legacy <c>System.Web.Caching.CacheItemPriority</c>. The member
    /// names and ordinal values are identical to the legacy enumeration so that the hundreds of
    /// existing call sites (including <see cref="DotNetNuke.Common.Utilities.DataCache"/>'s
    /// <c>const CacheItemPriority</c> fields) remain source compatible. Values are mapped onto
    /// <see cref="Microsoft.Extensions.Caching.Memory.CacheItemPriority"/> by the caching provider
    /// — in particular <see cref="NotRemovable"/> maps onto
    /// <c>Microsoft.Extensions.Caching.Memory.CacheItemPriority.NeverRemove</c>.
    /// </remarks>
    public enum CacheItemPriority
    {
        /// <summary>The lowest priority, and therefore the first to be evicted.</summary>
        Low = 1,

        /// <summary>A priority below <see cref="Normal"/>.</summary>
        BelowNormal = 2,

        /// <summary>The default priority.</summary>
        Normal = 3,

        /// <summary>The default priority (same as <see cref="Normal"/>).</summary>
        Default = 3,

        /// <summary>A priority above <see cref="Normal"/>.</summary>
        AboveNormal = 4,

        /// <summary>A high priority, evicted only after lower priority items.</summary>
        High = 5,

        /// <summary>The item is never evicted to recover memory. It can still expire or be removed explicitly.</summary>
        NotRemovable = 6,
    }
}
