// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Website.Configuration
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Enforces <see cref="StaticContentGuardOptions"/>: the ASP.NET Core replacement for the
    /// <c>Portals/Web.config</c>, <c>DesktopModules/MVC/Web.config</c> and
    /// <c>Install/Web.config</c> content guards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs before <c>UseStaticFiles</c> (it is installed by an <see cref="Microsoft.AspNetCore.Hosting.IStartupFilter"/>,
    /// so it precedes everything the host composes) because a guard that runs after the file has
    /// been written to the response body is not a guard. It only compares path text, so it cannot
    /// itself throw — which is what makes it safe to sit ahead of
    /// <c>UseDnnExceptionHandler</c>.
    /// </para>
    /// <para>
    /// A blocked path answers <c>404 Not Found</c>, not <c>403 Forbidden</c>, matching what IIS
    /// returned for a forbidden-handler extension: a 403 would confirm the file exists and so leak
    /// the layout of the portal file system.
    /// </para>
    /// </remarks>
    public sealed class StaticContentGuardMiddleware
    {
        private readonly RequestDelegate next;
        private readonly StaticContentGuardOptions options;

        /// <summary>Initializes a new instance of the <see cref="StaticContentGuardMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="options">The guard rules.</param>
        public StaticContentGuardMiddleware(RequestDelegate next, IOptions<StaticContentGuardOptions> options)
        {
            ArgumentNullException.ThrowIfNull(next);
            ArgumentNullException.ThrowIfNull(options);

            this.next = next;
            this.options = options.Value;
        }

        /// <summary>Invokes the middleware.</summary>
        /// <param name="context">The current request.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var path = context.Request.Path.Value ?? string.Empty;

            if (this.IsBlocked(path))
            {
                // 404 rather than 403: parity with the IIS forbidden handler, and it does not
                // confirm that the file exists.
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            if (this.RequiresNoCache(path))
            {
                // Install/Web.config's <clientCache cacheControlMode="DisableCache" />.
                context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                context.Response.Headers.Pragma = "no-cache";
                context.Response.Headers.Expires = "0";
            }

            return this.next(context);
        }

        private bool IsBlocked(string path)
        {
            if (path.Length == 0)
            {
                return false;
            }

            // Any blocked extension, anywhere in the path. Checked against the whole path rather
            // than only the last segment so that "/Portals/0/file.resources/x" cannot slip past.
            foreach (var extension in this.options.BlockedExtensions)
            {
                if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                    || path.Contains(extension + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            return segments.Any(segment => this.options.BlockedPathSegments
                .Any(blocked => string.Equals(segment, blocked, StringComparison.OrdinalIgnoreCase)));
        }

        private bool RequiresNoCache(string path)
        {
            return this.options.NoCachePathPrefixes.Any(
                prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
    }
}
