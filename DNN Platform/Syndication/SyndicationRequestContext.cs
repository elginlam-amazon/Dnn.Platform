// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Syndication
{
    using System;

    using Microsoft.AspNetCore.Http;

    /// <summary>Provides access to the ambient <see cref="HttpContext"/> for the syndication library's static APIs.</summary>
    /// <remarks>
    /// Replaces the <c>HttpContext.Current</c> static, which does not exist in ASP.NET Core. The
    /// accessor is supplied by <see cref="ServiceCollectionExtensions.AddDnnSyndication"/> at
    /// application start-up; code which needs per-request information should prefer injecting
    /// <see cref="IHttpContextAccessor"/> directly.
    /// </remarks>
    public static class SyndicationRequestContext
    {
        private static IHttpContextAccessor accessor;

        /// <summary>Configures the accessor used to reach the ambient request.</summary>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        [CLSCompliant(false)]
        public static void Configure(IHttpContextAccessor httpContextAccessor)
        {
            ArgumentNullException.ThrowIfNull(httpContextAccessor);

            accessor = httpContextAccessor;
        }

        /// <summary>Gets the ambient <see cref="HttpContext"/>, or <see langword="null"/> when there is no request.</summary>
        /// <returns>The ambient <see cref="HttpContext"/> or <see langword="null"/>.</returns>
        [CLSCompliant(false)]
        public static HttpContext TryGetHttpContext()
        {
            return accessor?.HttpContext;
        }
    }
}
