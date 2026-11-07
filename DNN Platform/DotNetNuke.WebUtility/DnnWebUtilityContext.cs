// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Gives the library's static members access to the current request and to the application's services.
    /// </summary>
    /// <remarks>
    /// Replaces <c>System.Web.HttpContext.Current</c>, <c>HttpRuntime.Cache</c> and
    /// <c>ConfigurationManager</c>. Every member is null-safe, mirroring the original code which
    /// branched on <c>HttpContext.Current Is Nothing</c> so that the library also works outside of
    /// a request (scheduler, installer, unit tests). Call
    /// <see cref="ServiceCollectionExtensions.UseDnnWebUtility(Microsoft.AspNetCore.Builder.IApplicationBuilder)"/>
    /// during start-up to initialize it.
    /// </remarks>
    public static class DnnWebUtilityContext
    {
        private static IHttpContextAccessor httpContextAccessor;
        private static IServiceProvider applicationServices;

        /// <summary>
        /// Gets the <see cref="HttpContext"/> of the request being handled on the current thread,
        /// or <see langword="null"/> when there is no current request.
        /// </summary>
        public static HttpContext Current => httpContextAccessor?.HttpContext;

        /// <summary>
        /// Gets the application's root service provider, or <see langword="null"/> when the library
        /// has not been initialized.
        /// </summary>
        public static IServiceProvider ApplicationServices => applicationServices;

        /// <summary>
        /// Gets the services of the current request, falling back to the application's services when
        /// there is no current request.
        /// </summary>
        public static IServiceProvider RequestServices => Current?.RequestServices ?? applicationServices;

        /// <summary>
        /// Gets the shared memory cache, or <see langword="null"/> when the library has not been initialized.
        /// </summary>
        /// <remarks>Replaces <c>HttpRuntime.Cache</c>.</remarks>
        public static IMemoryCache Cache => RequestServices?.GetService<IMemoryCache>();

        /// <summary>
        /// Gets the current settings of the library, never <see langword="null"/>.
        /// </summary>
        /// <remarks>
        /// When the library has not been initialized the defaults are returned, which keeps the
        /// behavior of the original code when the <c>ClientAPI</c> app setting was absent.
        /// </remarks>
        public static DnnWebUtilityOptions Options =>
            RequestServices?.GetService<IOptions<DnnWebUtilityOptions>>()?.Value ?? new DnnWebUtilityOptions();

        /// <summary>
        /// Initializes the accessor with the application's services.
        /// </summary>
        /// <param name="services">The application's root service provider.</param>
        public static void Initialize(IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(services);

            applicationServices = services;
            httpContextAccessor = services.GetRequiredService<IHttpContextAccessor>();
        }
    }
}
