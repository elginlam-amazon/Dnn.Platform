// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.HttpModules.DependencyInjection
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.Common.Extensions;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Creates a dependency injection scope for each request and publishes it on the current
    /// <see cref="HttpContext"/>. This is the ASP.NET Core replacement for the
    /// <c>ServiceRequestScopeModule</c> <c>IHttpModule</c>, which created the scope on
    /// <c>BeginRequest</c> and disposed it on (async) <c>EndRequest</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The legacy module got its <see cref="IServiceProvider"/> from a static
    /// <c>SetServiceProvider</c> seam called during <c>Application_Start</c>. In ASP.NET Core the
    /// scope factory is a first-class service, so it is constructor-injected instead and the
    /// static seam is gone (its only call site, <c>DotNetNukeHttpApplication</c>, no longer needs
    /// to prime it).
    /// </para>
    /// <para>
    /// <b>Pipeline position:</b> first of the early (pre-routing) middleware, so that
    /// <see cref="HttpContextDependencyInjectionExtensions.GetScope"/> is non-<see langword="null"/>
    /// for everything that runs afterwards.
    /// </para>
    /// </remarks>
    public sealed class DnnServiceScopeMiddleware
    {
        private readonly RequestDelegate next;
        private readonly IServiceScopeFactory serviceScopeFactory;

        /// <summary>Initializes a new instance of the <see cref="DnnServiceScopeMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="serviceScopeFactory">The factory used to create the per-request scope.</param>
        public DnnServiceScopeMiddleware(RequestDelegate next, IServiceScopeFactory serviceScopeFactory)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        /// <summary>Creates the request scope, runs the rest of the pipeline, then disposes the scope.</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            context.SetScope(this.serviceScopeFactory.CreateScope());
            try
            {
                await this.next(context);
            }
            finally
            {
                // Same disposal order as the legacy async EndRequest handler: prefer the
                // asynchronous disposal path when the scope supports it.
                switch (context.GetScope())
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync();
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }

                context.ClearScope();
            }
        }
    }
}
