// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionFilters
{
    using System;
    using System.Linq;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Authorization;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Base class for DNN MVC authorization filter attributes.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "Base type for DNN authorize attributes; renaming is a breaking change.")]
    public abstract class AuthorizeAttributeBase : Attribute, IAuthorizationFilter
    {
        /// <summary>Determines whether an <see cref="AllowAnonymousAttribute"/> is present on the action or controller.</summary>
        /// <param name="filterContext">The authorization filter context.</param>
        /// <returns><see langword="true"/> if anonymous access is allowed.</returns>
        public static bool IsAnonymousAttributePresent(AuthorizationFilterContext filterContext)
        {
            if (filterContext.ActionDescriptor is ControllerActionDescriptor descriptor)
            {
                return descriptor.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute), inherit: true)
                       || descriptor.ControllerTypeInfo.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
            }

            return filterContext.Filters.Any(f => f is IAllowAnonymousFilter);
        }

        /// <inheritdoc />
        public virtual void OnAuthorization(AuthorizationFilterContext filterContext)
        {
            Requires.NotNull("filterContext", filterContext);

            if (this.SkipAuthorization(filterContext))
            {
                return;
            }

            if (this.AuthorizeCore(filterContext.HttpContext))
            {
                this.HandleAuthorizedRequest(filterContext);
            }
            else
            {
                this.HandleUnauthorizedRequest(filterContext);
            }
        }

        /// <summary>Determines whether the current request is authorized.</summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <returns><see langword="true"/> if authorized.</returns>
        protected virtual bool AuthorizeCore(HttpContext httpContext)
        {
            return true;
        }

        /// <summary>Handles an authorized request.</summary>
        /// <param name="filterContext">The authorization filter context.</param>
        protected virtual void HandleAuthorizedRequest(AuthorizationFilterContext filterContext)
        {
            var response = filterContext.HttpContext.Response;
            response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, proxy-revalidate, max-age=0";
        }

        /// <summary>Handles an unauthorized request.</summary>
        /// <param name="filterContext">The authorization filter context.</param>
        protected virtual void HandleUnauthorizedRequest(AuthorizationFilterContext filterContext)
        {
            const string failureMessage = "Authorization has been denied for this request.";
            var authFilterContext = new AuthFilterContext(Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>(), filterContext, failureMessage);
            authFilterContext.HandleUnauthorizedRequest();
        }

        /// <summary>Skips this authorization step if anonymous attribute is applied.</summary>
        /// <param name="filterContext">The context.</param>
        /// <returns>Whether the <see cref="AllowAnonymousAttribute"/> is present.</returns>
        protected virtual bool SkipAuthorization(AuthorizationFilterContext filterContext)
        {
            return IsAnonymousAttributePresent(filterContext);
        }
    }
}
