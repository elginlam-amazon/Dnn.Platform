// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Linq;
    using System.Reflection;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>The base class of the authorization attributes of the services framework.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "Breaking change")]
    public abstract class AuthorizeAttributeBase : Attribute, IAuthorizationFilter
    {
        /// <summary>Gets a value indicating whether the action (or its controller, when the action has no other authorization attributes than <see cref="SupportedModulesAttribute"/>) allows anonymous access.</summary>
        /// <param name="actionContext">The authorization filter context.</param>
        /// <returns><see langword="true"/> if anonymous access is allowed.</returns>
        public static bool IsAnonymousAttributePresent(AuthorizationFilterContext actionContext)
        {
            Requires.NotNull("actionContext", actionContext);

            if (actionContext.ActionDescriptor is not ControllerActionDescriptor descriptor)
            {
                return actionContext.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
            }

            return descriptor.MethodInfo.GetCustomAttributes<AllowAnonymousAttribute>(true).Any()
                   || (descriptor.ControllerTypeInfo.GetCustomAttributes<AllowAnonymousAttribute>(true).Any()
                       && descriptor.MethodInfo.GetCustomAttributes<AuthorizeAttributeBase>(true).All(t => t is SupportedModulesAttribute));
        }

        /// <summary>Tests whether the request passes the authorization requirements.</summary>
        /// <param name="context">The auth filter context.</param>
        /// <returns><see langword="true"/> if the request is authorized.</returns>
        public abstract bool IsAuthorized(AuthFilterContext context);

        /// <inheritdoc />
        public virtual void OnAuthorization(AuthorizationFilterContext context)
        {
            Requires.NotNull("context", context);

            if (context.Result != null || this.SkipAuthorization(context))
            {
                return;
            }

            const string failureMessage = "Authorization has been denied for this request.";
            var authFilterContext = new AuthFilterContext(context, failureMessage);
            if (!this.IsAuthorized(authFilterContext))
            {
                authFilterContext.HandleUnauthorizedRequest();
            }
        }

        /// <summary>Gets a value indicating whether the authorization is skipped for this request.</summary>
        /// <param name="actionContext">The authorization filter context.</param>
        /// <returns><see langword="true"/> to skip the authorization.</returns>
        protected virtual bool SkipAuthorization(AuthorizationFilterContext actionContext)
        {
            return IsAnonymousAttributePresent(actionContext);
        }
    }
}
