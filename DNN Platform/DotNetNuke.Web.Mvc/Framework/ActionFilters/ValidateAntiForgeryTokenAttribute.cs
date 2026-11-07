// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionFilters
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    using DotNetNuke.Web.Mvc.Common;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>An authorization filter which validates the DNN anti-forgery token.</summary>
    public class ValidateAntiForgeryTokenAttribute : DnnAuthorizeBaseAttribute
    {
        private const string HeaderTokenName = "RequestVerificationToken";
        private const string FormTokenName = "__RequestVerificationToken";

        /// <inheritdoc />
        /// <remarks>
        /// The ASP.NET Core anti-forgery validation API is asynchronous, so this rule is evaluated on the
        /// asynchronous authorization path instead of the synchronous <see cref="DnnAuthorizeBaseAttribute.AuthorizeCore"/>.
        /// </remarks>
        public override async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (this.SkipAuthorization(context))
            {
                return;
            }

            if (await this.IsAuthenticatedAsync(context.HttpContext))
            {
                this.HandleAuthorizedRequest(context);
            }
            else
            {
                this.HandleUnauthorizedRequest(context);
            }
        }

        /// <summary>Determines whether the request is authenticated and carries a valid anti-forgery token pair.</summary>
        /// <param name="httpContext">The current HTTP context.</param>
        /// <returns><see langword="true"/> when the user is authenticated and the token pair validates.</returns>
        public virtual async Task<bool> IsAuthenticatedAsync(HttpContext httpContext)
        {
            try
            {
                if (httpContext?.User?.Identity?.IsAuthenticated != true)
                {
                    return false;
                }

                // Try to fetch the token from Headers. (Used with Dnn service framework.).
                // If not found then fetch it from form fields. (Would be used with standard MVC call).
                var token = httpContext.Request.Headers[HeaderTokenName].ToString();
                if (string.IsNullOrEmpty(token) && httpContext.Request.HasFormContentType)
                {
                    token = httpContext.Request.Form[FormTokenName].ToString();
                }

                if (string.IsNullOrEmpty(token))
                {
                    return false;
                }

                var cookieValue = this.GetAntiForgeryCookieValue(httpContext);

                await AntiForgery.Instance.ValidateAsync(cookieValue, token);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>Gets the anti-forgery cookie token from the request.</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>The cookie token value, or <see cref="string.Empty"/> when the cookie is not present.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected string GetAntiForgeryCookieValue(HttpContext context)
        {
            return context.Request.Cookies[AntiForgery.Instance.CookieName] ?? string.Empty;
        }
    }
}
