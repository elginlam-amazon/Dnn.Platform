// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System.Threading.Tasks;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>A base class for implementing a Web API authorization filter.</summary>
    /// <remarks>
    /// The legacy implementation was a Web API 2 <c>System.Web.Http.Filters.IAuthorizationFilter</c>,
    /// whose <c>ExecuteAuthorizationFilterAsync</c> ran <see cref="OnAuthorization"/>, returned
    /// <c>actionContext.Response</c> when the filter had produced one, and otherwise called the
    /// continuation. In ASP.NET Core an authorization filter short-circuits by assigning
    /// <see cref="AuthorizationFilterContext.Result"/>, which <see cref="AuthFilterContext.HandleUnauthorizedRequest"/>
    /// does; when it is left <see langword="null"/> the pipeline continues on its own. The
    /// <see cref="IsAuthorized(AuthFilterContext)"/> and <see cref="AllowMultiple"/> members are kept
    /// unchanged because they are the public contract of this base class.
    /// </remarks>
    public abstract class AuthFilterBase : IAsyncAuthorizationFilter
    {
        /// <summary>Gets a value indicating whether more than one instance of the indicated attribute can be specified for a single program element.</summary>
        public abstract bool AllowMultiple { get; }

        /// <summary>Tests if the request passes the authorization requirements.</summary>
        /// <param name="context">The auth filter context.</param>
        /// <returns>True when authorization is successful.</returns>
        public abstract bool IsAuthorized(AuthFilterContext context);

        /// <inheritdoc />
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            Requires.NotNull("actionContext", context);

            this.OnAuthorization(context);
            return Task.CompletedTask;
        }

        /// <summary>Co-ordinates check of authorization and handles Auth failure.  Should rarely be overridden.</summary>
        /// <param name="actionContext">The action context.</param>
        protected virtual void OnAuthorization(AuthorizationFilterContext actionContext)
        {
            Requires.NotNull("actionContext", actionContext);

            var authFilterContext = new AuthFilterContext(actionContext, DnnAuthorizeBaseAttribute.AuthorizationDeniedMessage);
            if (!this.IsAuthorized(authFilterContext))
            {
                authFilterContext.HandleUnauthorizedRequest();
            }
        }
    }
}
