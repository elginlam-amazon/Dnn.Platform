// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>The base class of the (non attribute) authorization filters of the services framework.</summary>
    public abstract class AuthFilterBase : IAuthorizationFilter
    {
        /// <summary>Gets a value indicating whether multiple instances of the filter are allowed.</summary>
        public abstract bool AllowMultiple { get; }

        /// <summary>Tests whether the request passes the authorization requirements.</summary>
        /// <param name="context">The auth filter context.</param>
        /// <returns><see langword="true"/> if the request is authorized.</returns>
        public abstract bool IsAuthorized(AuthFilterContext context);

        /// <inheritdoc />
        void IAuthorizationFilter.OnAuthorization(AuthorizationFilterContext context)
        {
            this.OnAuthorization(context);
        }

        /// <summary>Authorizes the request.</summary>
        /// <param name="actionContext">The authorization filter context.</param>
        protected virtual void OnAuthorization(AuthorizationFilterContext actionContext)
        {
            Requires.NotNull("actionContext", actionContext);

            if (actionContext.Result != null)
            {
                return;
            }

            const string failureMessage = "Authorization has been denied for this request.";
            var authFilterContext = new AuthFilterContext(actionContext, failureMessage);
            if (!this.IsAuthorized(authFilterContext))
            {
                authFilterContext.HandleUnauthorizedRequest();
            }
        }
    }
}
