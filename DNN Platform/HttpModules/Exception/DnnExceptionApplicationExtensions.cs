// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.HttpModules.Exceptions
{
    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Builder;

    /// <summary>
    /// The host application's entry point into the exception logging that used to be the
    /// <c>ExceptionModule</c> <c>IHttpModule</c> registration.
    /// </summary>
    public static class DnnExceptionApplicationExtensions
    {
        /// <summary>
        /// Adds <see cref="DnnUnhandledExceptionMiddleware"/>, which writes an
        /// <see cref="UnhandledException"/> entry to the DNN exception log for every failure that
        /// nothing else handled.
        /// </summary>
        /// <param name="app">The host's application builder.</param>
        /// <returns>The same <paramref name="app"/>, for chaining.</returns>
        /// <remarks>
        /// Install it either as the first middleware after <c>UseDnnExceptionHandler</c> (it then
        /// observes the exception as it propagates and re-throws it, leaving the error page to the
        /// handler) or inside the error-handling branch itself (it then reads the failure from
        /// <c>IExceptionHandlerFeature</c>). Doing both is safe: a failure is logged once.
        /// </remarks>
        public static IApplicationBuilder UseDnnUnhandledExceptionLogging(this IApplicationBuilder app)
        {
            Requires.NotNull(nameof(app), app);

            return app.UseMiddleware<DnnUnhandledExceptionMiddleware>();
        }
    }
}
