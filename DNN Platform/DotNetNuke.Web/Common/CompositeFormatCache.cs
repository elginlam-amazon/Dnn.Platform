// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Common.Internal
{
    using System.Collections.Concurrent;
    using System.Text;

    /// <summary>Caches the parsed <see cref="CompositeFormat"/> of format strings that are only known at run time.</summary>
    /// <remarks>
    /// Some format strings come from the localization resource files, so they cannot be stored in a
    /// <see langword="static" /> <see langword="readonly" /> <see cref="CompositeFormat"/> field. This cache keeps the
    /// parsing cost of such a format string to once per distinct string, which is what CA1863 asks for.
    /// </remarks>
    internal static class CompositeFormatCache
    {
        private static readonly ConcurrentDictionary<string, CompositeFormat> Cache = new ConcurrentDictionary<string, CompositeFormat>();

        /// <summary>Gets the (cached) <see cref="CompositeFormat"/> for the given <paramref name="format"/>.</summary>
        /// <param name="format">The composite format string.</param>
        /// <returns>A <see cref="CompositeFormat"/> instance.</returns>
        internal static CompositeFormat Get(string format)
        {
            return Cache.GetOrAdd(format ?? string.Empty, static value => CompositeFormat.Parse(value));
        }
    }
}
