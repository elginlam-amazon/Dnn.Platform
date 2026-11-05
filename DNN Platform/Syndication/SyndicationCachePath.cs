// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Syndication
{
    using System;
    using System.IO;

    /// <summary>
    /// Resolves application-relative syndication cache paths to physical paths.
    /// </summary>
    /// <remarks>
    /// Replaces <c>HttpContext.Current.Server.MapPath</c>, which does not exist in ASP.NET Core.
    /// The host should call <see cref="Configure"/> (directly, or through
    /// <see cref="ServiceCollectionExtensions.AddDnnSyndication"/> with an
    /// <c>IWebHostEnvironment</c>-based resolver) so that the feed caches land in the website
    /// content root. When it is not configured, paths are resolved against
    /// <see cref="AppContext.BaseDirectory"/>.
    /// </remarks>
    public static class SyndicationCachePath
    {
        private static Func<string, string> mapPath;

        /// <summary>Configures the resolver used to turn an application-relative path into a physical path.</summary>
        /// <param name="resolver">The resolver, e.g. one built over <c>IWebHostEnvironment.ContentRootPath</c>.</param>
        public static void Configure(Func<string, string> resolver)
        {
            ArgumentNullException.ThrowIfNull(resolver);

            mapPath = resolver;
        }

        /// <summary>Maps an application-relative path (e.g. <c>Portals/_default/Cache/RSS/</c>) to a physical path.</summary>
        /// <param name="appRelativePath">The application-relative path.</param>
        /// <returns>The physical path.</returns>
        public static string MapPath(string appRelativePath)
        {
            ArgumentNullException.ThrowIfNull(appRelativePath);

            var resolver = mapPath;
            if (resolver != null)
            {
                return resolver(appRelativePath);
            }

            return Combine(AppContext.BaseDirectory, appRelativePath);
        }

        /// <summary>Combines a root physical path with an application-relative path, normalizing separators.</summary>
        /// <param name="rootPath">The physical root path.</param>
        /// <param name="appRelativePath">The application-relative path.</param>
        /// <returns>The physical path.</returns>
        public static string Combine(string rootPath, string appRelativePath)
        {
            ArgumentNullException.ThrowIfNull(rootPath);
            ArgumentNullException.ThrowIfNull(appRelativePath);

            var relative = appRelativePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart('~', Path.DirectorySeparatorChar);

            return Path.Combine(rootPath, relative);
        }
    }
}
