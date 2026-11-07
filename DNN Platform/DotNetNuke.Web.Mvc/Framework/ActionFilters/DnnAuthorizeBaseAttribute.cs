// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionFilters
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Base class for the DNN MVC authorization filters.</summary>
    public abstract class DnnAuthorizeBaseAttribute : Attribute, IAuthorizationFilter, IAsyncAuthorizationFilter, IAsyncActionFilter
    {
        /// <summary>
        /// Gets a value indicating whether the authorization rule needs the DNN module controller
        /// (for example its <c>ModuleContext</c>). ASP.NET Core creates and configures the controller
        /// after the authorization filters have run, so such rules are evaluated on the action filter
        /// stage instead, where the controller exists but the action body has not run yet. Denying there
        /// still short-circuits the request with the unauthorized result.
        /// </summary>
        protected virtual bool RequiresController => false;

        /// <summary>Determines whether anonymous access has been allowed for the current endpoint.</summary>
        /// <param name="context">The authorization filter context.</param>
        /// <returns><see langword="true"/> when <see cref="IAllowAnonymous"/> metadata (for example <see cref="AllowAnonymousAttribute"/>) is present.</returns>
        public static bool IsAnonymousAttributePresent(AuthorizationFilterContext context)
        {
            Requires.NotNull("context", context);

            // In ASP.NET Core, [AllowAnonymous] applied to the action or the controller surfaces as
            // IAllowAnonymous in the endpoint metadata and in the effective filter list for the action.
            if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() != null)
            {
                return true;
            }

            return context.Filters.Any(f => f is IAllowAnonymous)
                   || context.ActionDescriptor.EndpointMetadata?.Any(m => m is IAllowAnonymous) == true;
        }

        /// <inheritdoc />
        public virtual void OnAuthorization(AuthorizationFilterContext context)
        {
            Requires.NotNull("context", context);

            if (this.SkipAuthorization(context))
            {
                return;
            }

            if (this.AuthorizeCore(context.HttpContext))
            {
                this.HandleAuthorizedRequest(context);
            }
            else
            {
                this.HandleUnauthorizedRequest(context);
            }
        }

        /// <inheritdoc />
        public virtual Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (this.RequiresController)
            {
                // Evaluated on the action stage, see RequiresController.
                return Task.CompletedTask;
            }

            // The default asynchronous entry point simply runs the synchronous rule. Filters whose
            // authorization rule needs to await (for example anti-forgery validation) override this.
            this.OnAuthorization(context);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            Requires.NotNull("context", context);
            Requires.NotNull("next", next);

            if (this.RequiresController)
            {
                var authorizationContext = new AuthorizationFilterContext(context, context.Filters);
                await this.OnAuthorizationAtActionStageAsync(authorizationContext);

                if (authorizationContext.Result != null)
                {
                    // Denied: short-circuit the pipeline with the unauthorized result.
                    context.Result = authorizationContext.Result;
                    return;
                }
            }

            await next();
        }

        /// <summary>Runs the authorization rule once the controller is available.</summary>
        /// <param name="context">An authorization filter context built over the action context.</param>
        /// <returns>A task that completes when the rule has been evaluated.</returns>
        protected virtual Task OnAuthorizationAtActionStageAsync(AuthorizationFilterContext context)
        {
            this.OnAuthorization(context);
            return Task.CompletedTask;
        }

        /// <summary>Evaluates the authorization rule for the current request.</summary>
        /// <param name="httpContext">The current HTTP context.</param>
        /// <returns><see langword="true"/> when the request is authorized.</returns>
        protected virtual bool AuthorizeCore(HttpContext httpContext)
        {
            return true;
        }

        /// <summary>Called when the request has been authorized.</summary>
        /// <param name="context">The authorization filter context.</param>
        protected virtual void HandleAuthorizedRequest(AuthorizationFilterContext context)
        {
            Requires.NotNull("context", context);

            // The original implementation set a proxy max-age of zero and registered a cache validation
            // callback so that every request re-evaluated authorization. ASP.NET Core has no cache
            // validation callback, so the equivalent guarantee is expressed through response headers:
            // shared (proxy) caches must revalidate on every request instead of serving a cached
            // response for a different user.
            context.HttpContext.Response.Headers.CacheControl = "no-cache, s-maxage=0";
        }

        /// <summary>Called when the request failed authorization.</summary>
        /// <param name="context">The authorization filter context.</param>
        protected virtual void HandleUnauthorizedRequest(AuthorizationFilterContext context)
        {
            const string failureMessage = "Authorization has been denied for this request.";
            var authFilterContext = new AuthFilterContext(
                Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>(),
                context,
                failureMessage);
            authFilterContext.HandleUnauthorizedRequest();
        }

        /// <summary>Skips this authorization step if anonymous access is allowed, override if auth should never be skipped, or other conditions are required.</summary>
        /// <param name="context">The context.</param>
        /// <returns>Whether anonymous access is allowed for the current endpoint.</returns>
        protected virtual bool SkipAuthorization(AuthorizationFilterContext context)
        {
            return IsAnonymousAttributePresent(context);
        }
    }
}
