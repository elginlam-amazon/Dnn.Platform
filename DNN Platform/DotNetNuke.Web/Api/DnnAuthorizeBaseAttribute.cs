// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.DependencyInjection.Extensions;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>Provides Dnn specific details for authorization filter.</summary>
    /// <remarks>
    /// <para>
    /// Was a Web API 2 <c>AuthorizationFilterAttribute</c>; it is now an attribute implementing the
    /// ASP.NET Core <see cref="IAsyncAuthorizationFilter"/>. The abstract
    /// <see cref="IsAuthorized(AuthFilterContext)"/> contract is unchanged so that the authorization
    /// attributes of external modules keep compiling, and the class keeps its former name through the
    /// <see cref="AuthorizeAttributeBase"/> base class, which now derives from this type (the rename
    /// matches the <c>DotNetNuke.Web.Mvc</c> host surface).
    /// </para>
    /// <para>
    /// The decision itself is synchronous (<see cref="IsAuthorized(AuthFilterContext)"/> is the public
    /// contract and every DNN rule is synchronous), so <see cref="OnAuthorizationAsync"/> runs the rule
    /// inline and returns a completed task. There is deliberately no sync-over-async anywhere on the
    /// authorization path.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public abstract class DnnAuthorizeBaseAttribute : Attribute, IAsyncAuthorizationFilter
    {
        /// <summary>The message used when a request fails authorization.</summary>
        internal const string AuthorizationDeniedMessage = "Authorization has been denied for this request.";

        /// <summary>Checks if the <see cref="AllowAnonymousAttribute"/> is present.</summary>
        /// <param name="actionContext">The authorization filter context.</param>
        /// <returns>A value indicating whether the <see cref="AllowAnonymousAttribute"/> is present.</returns>
        public static bool IsAnonymousAttributePresent(AuthorizationFilterContext actionContext)
        {
            Requires.NotNull("actionContext", actionContext);

            if (actionContext.ActionDescriptor is not ControllerActionDescriptor descriptor)
            {
                return actionContext.ActionDescriptor?.EndpointMetadata?.OfType<AllowAnonymousAttribute>().Any() == true;
            }

            var actionAttributes = descriptor.MethodInfo.GetCustomAttributes(inherit: true);
            if (actionAttributes.OfType<AllowAnonymousAttribute>().Any())
            {
                return true;
            }

            // Matches the legacy rule: [AllowAnonymous] on the controller only takes effect when the
            // action itself carries no authorization attribute other than SupportedModulesAttribute.
            return descriptor.ControllerTypeInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length != 0
                   && actionAttributes.OfType<DnnAuthorizeBaseAttribute>().All(t => t is SupportedModulesAttribute);
        }

        /// <summary>Tests if the request passes the authorization requirements.</summary>
        /// <param name="context">The auth filter context.</param>
        /// <returns>True when authorization is successful.</returns>
        public abstract bool IsAuthorized(AuthFilterContext context);

        /// <inheritdoc />
        public virtual Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            this.OnAuthorization(context);
            return Task.CompletedTask;
        }

        /// <summary>Called by framework at start of Auth process, check if auth should be skipped and handles auth failure.  Should rarely need to be overridden.</summary>
        /// <param name="actionContext">The authorization filter context.</param>
        public virtual void OnAuthorization(AuthorizationFilterContext actionContext)
        {
            Requires.NotNull("actionContext", actionContext);

            // Property injection for filters the runtime instantiated as attributes (they cannot take
            // constructor dependencies). This is the port of the former
            // DnnActionFilterProvider -> container.BuildUp(filter) call; the services come from the
            // REQUEST container, which is the host's single container.
            actionContext.HttpContext?.RequestServices?.BuildUp(this);

            if (this.SkipAuthorization(actionContext))
            {
                return;
            }

            var authFilterContext = new AuthFilterContext(actionContext, AuthorizationDeniedMessage);
            if (!this.IsAuthorized(authFilterContext))
            {
                authFilterContext.HandleUnauthorizedRequest();
            }
        }

        /// <summary>Skips this authorization step if anonymous attribute is applied, override if auth should never be skipped, or other conditions are required.</summary>
        /// <param name="actionContext">The authorization filter context.</param>
        /// <returns>A value indicating whether to skip the authorization.</returns>
        protected virtual bool SkipAuthorization(AuthorizationFilterContext actionContext)
        {
            return IsAnonymousAttributePresent(actionContext);
        }
    }
}
