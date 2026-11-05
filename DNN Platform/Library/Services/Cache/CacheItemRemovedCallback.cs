// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Cache
{
    /// <summary>Called after an item has been removed from the cache.</summary>
    /// <param name="key">The key of the item that was removed.</param>
    /// <param name="value">The value that was removed.</param>
    /// <param name="reason">The reason the item was removed.</param>
    /// <remarks>Replaces the legacy <c>System.Web.Caching.CacheItemRemovedCallback</c> delegate.</remarks>
    public delegate void CacheItemRemovedCallback(string key, object value, CacheItemRemovedReason reason);
}
