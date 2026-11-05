// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common
{
    using System;
    using System.Diagnostics;
    using System.IO;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// A unit testable alternative to the legacy <c>System.Web.HttpContext.Current</c> static.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On ASP.NET Core there is no ambient static request context. This class is the single seam
    /// through which legacy static call sites reach the current
    /// <see cref="Microsoft.AspNetCore.Http.HttpContext"/>. It is backed by an
    /// <see cref="IHttpContextAccessor"/> that the host application MUST register at startup by
    /// calling <see cref="RegisterAccessor"/> (and <see cref="RegisterWebHostEnvironment"/> for
    /// physical-path resolution). Forgetting to do so is a configuration error, and this class
    /// reports it loudly (trace error + <see cref="CurrentOrThrow"/> throwing) rather than
    /// silently behaving as if there were no request.
    /// </para>
    /// <para>
    /// <b>IMPORTANT — Blazor Server:</b> <see cref="Current"/> is only non-<see langword="null"/>
    /// while an actual HTTP request is in flight. During interactive (SignalR circuit) rendering
    /// — i.e. every <c>@onclick</c>/<c>@onchange</c> event handler and every render after the
    /// first — <see cref="IHttpContextAccessor.HttpContext"/> is <see langword="null"/>.
    /// Component code must therefore read whatever it needs from <see cref="Current"/> during the
    /// initial (static) render, in <c>OnInitialized</c>/<c>OnInitializedAsync</c>, and capture the
    /// values in component fields, a cascading parameter, or a scoped state service. Never call
    /// into this seam from an interactive event handler.
    /// </para>
    /// </remarks>
    public class HttpContextSource
    {
        private static IHttpContextAccessor accessor;
        private static IWebHostEnvironment webHostEnvironment;
        private static HttpContext fakeContext;
        private static int accessorWarningLogged;
        private static int environmentWarningLogged;

        /// <summary>
        /// Gets the current <see cref="Microsoft.AspNetCore.Http.HttpContext"/>, or
        /// <see langword="null"/> when no HTTP request is in flight (for example during Blazor
        /// Server interactive rendering, on a background thread, or during application start-up).
        /// </summary>
        /// <remarks>
        /// When the <see cref="IHttpContextAccessor"/> has never been registered — a host
        /// mis-configuration rather than a legitimate "no current request" — a trace error is
        /// emitted once so the condition cannot pass unnoticed. Use <see cref="CurrentOrThrow"/>
        /// from code that genuinely cannot proceed without a request.
        /// </remarks>
        public static HttpContext Current
        {
            get
            {
                if (fakeContext != null)
                {
                    return fakeContext;
                }

                var currentAccessor = accessor;
                if (currentAccessor == null)
                {
                    WarnAccessorNotRegistered();
                    return null;
                }

                return currentAccessor.HttpContext;
            }
        }

        /// <summary>
        /// Gets the current <see cref="Microsoft.AspNetCore.Http.HttpContext"/> and throws when
        /// there is none, instead of returning <see langword="null"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// No <see cref="IHttpContextAccessor"/> was registered, or there is no HTTP request in
        /// flight on the current execution context.
        /// </exception>
        public static HttpContext CurrentOrThrow
        {
            get
            {
                var context = Current;
                if (context == null)
                {
                    throw new InvalidOperationException(
                        accessor == null && fakeContext == null
                            ? $"No {nameof(IHttpContextAccessor)} has been registered with {nameof(HttpContextSource)}. Call HttpContextSource.RegisterAccessor(...) during application start-up."
                            : "There is no HTTP request in flight on the current execution context. Note that HttpContext is null during Blazor Server interactive (SignalR) rendering; capture the values you need during the initial render.");
                }

                return context;
            }
        }

        /// <summary>Gets a value indicating whether an <see cref="IHttpContextAccessor"/> (or a fake context) has been registered.</summary>
        public static bool IsRegistered => fakeContext != null || accessor != null;

        /// <summary>
        /// Gets the hosting environment used to resolve virtual paths to physical paths. Replaces
        /// the legacy <c>HttpRuntime.AppDomainAppPath</c> / <c>HttpServerUtility.MapPath</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">No <see cref="IWebHostEnvironment"/> was registered.</exception>
        public static IWebHostEnvironment WebHostEnvironment
        {
            get
            {
                var environment = webHostEnvironment;
                if (environment == null)
                {
                    WarnEnvironmentNotRegistered();
                    throw new InvalidOperationException(
                        $"No {nameof(IWebHostEnvironment)} has been registered with {nameof(HttpContextSource)}. Call HttpContextSource.RegisterWebHostEnvironment(...) during application start-up.");
                }

                return environment;
            }
        }

        /// <summary>
        /// Gets the physical path of the application root (the legacy
        /// <c>HttpRuntime.AppDomainAppPath</c> / <c>Request.PhysicalApplicationPath</c>), without
        /// a trailing directory separator.
        /// </summary>
        /// <exception cref="InvalidOperationException">No <see cref="IWebHostEnvironment"/> was registered.</exception>
        public static string ApplicationPhysicalPath =>
            WebHostEnvironment.ContentRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        /// <summary>Gets the physical path of the static web asset root (<c>wwwroot</c>).</summary>
        /// <exception cref="InvalidOperationException">No <see cref="IWebHostEnvironment"/> was registered.</exception>
        public static string WebRootPhysicalPath =>
            (WebHostEnvironment.WebRootPath ?? WebHostEnvironment.ContentRootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        /// <summary>Gets the application's virtual path base (the legacy <c>Request.ApplicationPath</c>), e.g. <c>/</c> or <c>/dnn</c>.</summary>
        public static string ApplicationVirtualPath
        {
            get
            {
                var pathBase = Current?.Request?.PathBase.Value;
                return string.IsNullOrEmpty(pathBase) ? "/" : pathBase;
            }
        }

        /// <summary>Injects a context instance, for unit testing.</summary>
        /// <param name="instance">The context to inject, or <see langword="null"/> to clear a previously injected one.</param>
        public static void RegisterInstance(HttpContext instance)
        {
            fakeContext = instance;
        }

        /// <summary>
        /// Registers the DI-resolved <see cref="IHttpContextAccessor"/> that backs
        /// <see cref="Current"/>. The host application must call this once during start-up
        /// (<c>Program.cs</c>/<c>Startup.cs</c>) after <c>AddHttpContextAccessor()</c>.
        /// </summary>
        /// <param name="httpContextAccessor">The accessor resolved from the application's service provider.</param>
        /// <exception cref="ArgumentNullException"><paramref name="httpContextAccessor"/> is <see langword="null"/>.</exception>
        public static void RegisterAccessor(IHttpContextAccessor httpContextAccessor)
        {
            accessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            accessorWarningLogged = 0;
        }

        /// <summary>
        /// Registers the DI-resolved <see cref="IWebHostEnvironment"/> used by
        /// <see cref="MapPath"/>, <see cref="ApplicationPhysicalPath"/> and
        /// <see cref="WebRootPhysicalPath"/>. The host application must call this once during
        /// start-up.
        /// </summary>
        /// <param name="environment">The hosting environment resolved from the application's service provider.</param>
        /// <exception cref="ArgumentNullException"><paramref name="environment"/> is <see langword="null"/>.</exception>
        public static void RegisterWebHostEnvironment(IWebHostEnvironment environment)
        {
            webHostEnvironment = environment ?? throw new ArgumentNullException(nameof(environment));
            environmentWarningLogged = 0;
        }

        /// <summary>
        /// Resolves an application-relative virtual path (<c>~/Portals/0</c>, <c>/Portals/0</c> or
        /// <c>Portals/0</c>) to its physical path. Replaces <c>HttpContext.Current.Server.MapPath</c>.
        /// </summary>
        /// <param name="virtualPath">The virtual path to map. <see langword="null"/>, empty or <c>~/</c> map to the application root.</param>
        /// <returns>The physical path.</returns>
        /// <exception cref="InvalidOperationException">No <see cref="IWebHostEnvironment"/> was registered.</exception>
        public static string MapPath(string virtualPath)
        {
            var root = ApplicationPhysicalPath;
            if (string.IsNullOrEmpty(virtualPath))
            {
                return root;
            }

            var relative = virtualPath.Replace('/', Path.DirectorySeparatorChar)
                                      .Replace('\\', Path.DirectorySeparatorChar);

            if (relative.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                relative = relative.Substring(2);
            }
            else if (relative == "~")
            {
                relative = string.Empty;
            }

            // An already-physical (rooted, non virtual) path is returned unchanged, matching
            // Server.MapPath behavior for callers that pre-resolved the path.
            if (Path.IsPathRooted(relative) && relative.Length > 1 && relative[1] == ':')
            {
                return relative;
            }

            relative = relative.TrimStart(Path.DirectorySeparatorChar);
            return relative.Length == 0 ? root : Path.Combine(root, relative);
        }

        /// <summary>Resolves a physical path back to an application-relative virtual path.</summary>
        /// <param name="physicalPath">The physical path.</param>
        /// <returns>The virtual path, prefixed with <c>~/</c>, or the input when it is outside the application root.</returns>
        public static string ToVirtualPath(string physicalPath)
        {
            if (string.IsNullOrEmpty(physicalPath))
            {
                return physicalPath;
            }

            var root = ApplicationPhysicalPath;
            if (physicalPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                var relative = physicalPath.Substring(root.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace(Path.DirectorySeparatorChar, '/');
                return "~/" + relative;
            }

            return physicalPath;
        }

        private static void WarnAccessorNotRegistered()
        {
            if (System.Threading.Interlocked.Exchange(ref accessorWarningLogged, 1) == 0)
            {
                Trace.TraceError(
                    $"{nameof(HttpContextSource)}.{nameof(Current)} was read before any {nameof(IHttpContextAccessor)} was registered. " +
                    "Call HttpContextSource.RegisterAccessor(app.Services.GetRequiredService<IHttpContextAccessor>()) during application start-up, " +
                    "otherwise every request-scoped lookup in the platform will behave as if there were no request.");
            }
        }

        private static void WarnEnvironmentNotRegistered()
        {
            if (System.Threading.Interlocked.Exchange(ref environmentWarningLogged, 1) == 0)
            {
                Trace.TraceError(
                    $"{nameof(HttpContextSource)}.{nameof(WebHostEnvironment)} was read before any {nameof(IWebHostEnvironment)} was registered. " +
                    "Call HttpContextSource.RegisterWebHostEnvironment(app.Services.GetRequiredService<IWebHostEnvironment>()) during application start-up.");
            }
        }
    }
}
