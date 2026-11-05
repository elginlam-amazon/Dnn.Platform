// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Cache
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;

    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    /// Creates <see cref="IChangeToken"/> instances that fire when a file or directory on disk
    /// changes. This is the ASP.NET Core replacement for the file-monitoring half of the legacy
    /// <c>System.Web.Caching.CacheDependency</c>.
    /// </summary>
    internal static class CacheFileWatcher
    {
        private static readonly ConcurrentDictionary<string, PhysicalFileProvider> Providers =
            new ConcurrentDictionary<string, PhysicalFileProvider>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Creates a change token that fires when the given file or directory changes.</summary>
        /// <param name="path">An absolute (or application-relative) path to a file or a directory.</param>
        /// <returns>An <see cref="IChangeToken"/> which never returns <see langword="null"/>.</returns>
        public static IChangeToken Watch(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return NullChangeToken.Singleton;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);

                if (Directory.Exists(fullPath))
                {
                    // A directory dependency: watch everything beneath it.
                    return WatchFrom(fullPath, "**/*");
                }

                var directory = Path.GetDirectoryName(fullPath);
                var fileName = Path.GetFileName(fullPath);
                if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
                {
                    return UnwatchablePath(path, "the path has no directory or file component");
                }

                // Note: the file itself does not have to exist — PhysicalFileProvider also raises
                // the token when a watched, missing file is created.
                return WatchFrom(directory, fileName);
            }
            catch (Exception ex)
            {
                // An unwatchable path (invalid characters, no permission) must not break caching.
                return UnwatchablePath(path, ex.Message);
            }
        }

        /// <summary>Gets the most recent write time (UTC) of the given paths.</summary>
        /// <param name="paths">The file or directory paths.</param>
        /// <returns>The latest last-write time in UTC, or <see cref="DateTime.MinValue"/> when unknown.</returns>
        public static DateTime GetUtcLastModified(string[] paths)
        {
            var result = DateTime.MinValue;
            if (paths == null)
            {
                return result;
            }

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                try
                {
                    var lastWrite = Directory.Exists(path)
                        ? Directory.GetLastWriteTimeUtc(path)
                        : (File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue);
                    if (lastWrite > result)
                    {
                        result = lastWrite;
                    }
                }
                catch (Exception)
                {
                    // Unreadable path: ignore it for the purposes of the last-modified calculation.
                }
            }

            return result;
        }

        /// <summary>
        /// Watches <paramref name="filter"/> relative to <paramref name="directory"/>, walking up to
        /// the nearest ancestor that actually exists when the directory itself does not.
        /// </summary>
        /// <param name="directory">The directory the filter is relative to.</param>
        /// <param name="filter">The glob to watch within that directory.</param>
        /// <returns>A change token, never <see langword="null"/>.</returns>
        /// <remarks>
        /// <para>
        /// The legacy <c>CacheDependency(path)</c> watched a path and fired when it was
        /// <em>created</em>, and DNN deliberately takes file dependencies on paths that do not
        /// exist yet — portal skin/template folders, <c>App_LocalResources</c> for a language that
        /// is not installed, and the sitemap/URL files in <c>Entities/Urls/CacheController</c>.
        /// </para>
        /// <para>
        /// <see cref="PhysicalFileProvider"/> cannot be rooted at a missing directory, so this
        /// walks up to the nearest existing ancestor and watches the remaining relative path from
        /// there. Without that, those entries would be cached permanently stale until a manual
        /// cache clear.
        /// </para>
        /// </remarks>
        private static IChangeToken WatchFrom(string directory, string filter)
        {
            var current = directory;
            var relative = filter;

            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(current))
                {
                    var provider = Providers.GetOrAdd(current, dir => new PhysicalFileProvider(dir));
                    return provider.Watch(relative) ?? NullChangeToken.Singleton;
                }

                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal))
                {
                    break;
                }

                // Re-root the glob one level up, so creating the missing directory is itself a change.
                relative = string.Concat(Path.GetFileName(current), "/", relative);
                current = parent;
            }

            return UnwatchablePath(
                Path.Combine(directory, filter),
                "no ancestor directory of the path exists, so it cannot be watched");
        }

        /// <summary>
        /// Returns a never-firing token for a path that genuinely cannot be watched, after tracing
        /// the reason so the fallback is distinguishable from "no dependency was requested".
        /// </summary>
        /// <param name="path">The path that could not be watched.</param>
        /// <param name="reason">Why it could not be watched.</param>
        /// <returns><see cref="NullChangeToken.Singleton"/>.</returns>
        private static IChangeToken UnwatchablePath(string path, string reason)
        {
            System.Diagnostics.Trace.TraceWarning(
                "DotNetNuke cache file dependency on '{0}' will never expire: {1}. " +
                "Entries depending on this path stay cached until the cache is cleared.",
                path,
                reason);
            return NullChangeToken.Singleton;
        }
    }
}
