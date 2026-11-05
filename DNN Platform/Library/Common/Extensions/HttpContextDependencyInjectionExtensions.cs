// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Extensions
{
    using System;
    using System.Collections.Generic;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Dependency injection extensions for <see cref="HttpContext"/>.</summary>
    /// <remarks>
    /// On ASP.NET Core the request already owns a service scope (<c>HttpContext.RequestServices</c>).
    /// These members are kept so the platform's legacy call sites keep compiling and behaving the
    /// same, but they now return the request's own scope whenever one is available instead of
    /// creating a second, competing scope.
    /// </remarks>
    public static class HttpContextDependencyInjectionExtensions
    {
        private static readonly object ScopeKey = typeof(IServiceScope);

        /// <summary>Sets the service scope for the http context.</summary>
        /// <param name="httpContext">The http context.</param>
        /// <param name="scope">The service scope.</param>
        public static void SetScope(this HttpContext httpContext, IServiceScope scope)
        {
            if (httpContext is null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (!httpContext.Items.ContainsKey(ScopeKey))
            {
                httpContext.Items[ScopeKey] = scope;
            }
        }

        /// <summary>Clears the service scope for the http context.</summary>
        /// <param name="httpContext">The http context on which to clear the service scope.</param>
        public static void ClearScope(this HttpContext httpContext)
        {
            if (httpContext is null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            httpContext.Items.Remove(ScopeKey);
        }

        /// <summary>Gets the http context service scope, creating one if necessary.</summary>
        /// <param name="httpContext">The http context from which to get the scope from.</param>
        /// <returns>A service scope, or <see langword="null"/> when no service provider is available yet.</returns>
        public static IServiceScope GetScope(this HttpContext httpContext)
        {
            if (httpContext is null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            var scope = GetStoredScope(httpContext.Items);
            if (scope is not null)
            {
                return scope;
            }

            // Prefer the scope ASP.NET Core already created for this request.
            if (httpContext.RequestServices is not null)
            {
                scope = new RequestServicesScope(httpContext.RequestServices);
                httpContext.Items[ScopeKey] = scope;
                return scope;
            }

            if (Globals.DependencyProvider is null)
            {
                return null;
            }

            lock (ScopeKey)
            {
                scope = GetStoredScope(httpContext.Items);
                if (scope is null)
                {
                    scope = Globals.DependencyProvider.CreateScope();
                    httpContext.Items[ScopeKey] = scope;
                    httpContext.Response.RegisterForDispose(new ScopeDisposer(httpContext));
                }
            }

            return scope;
        }

        private static IServiceScope GetStoredScope(IDictionary<object, object> httpContextItems)
        {
            return httpContextItems.TryGetValue(ScopeKey, out var scope) ? scope as IServiceScope : null;
        }

        /// <summary>An <see cref="IServiceScope"/> façade over the request's own service provider; disposing it is a no-op because the framework owns the lifetime.</summary>
        private sealed class RequestServicesScope : IServiceScope
        {
            public RequestServicesScope(IServiceProvider serviceProvider)
            {
                this.ServiceProvider = serviceProvider;
            }

            public IServiceProvider ServiceProvider { get; }

            public void Dispose()
            {
                // The request pipeline owns RequestServices; do not dispose it here.
            }
        }

        /// <summary>Disposes a platform-created scope when the response completes (replaces <c>HttpContext.AddOnRequestCompleted</c>).</summary>
        private sealed class ScopeDisposer : IDisposable
        {
            private readonly HttpContext httpContext;

            public ScopeDisposer(HttpContext httpContext)
            {
                this.httpContext = httpContext;
            }

            public void Dispose()
            {
                GetStoredScope(this.httpContext.Items)?.Dispose();
                this.httpContext.ClearScope();
            }
        }
    }
}
