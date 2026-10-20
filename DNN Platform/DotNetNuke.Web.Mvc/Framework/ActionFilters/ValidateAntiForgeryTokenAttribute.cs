// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionFilters
{
    using System;
    using System.Threading;

    using DotNetNuke.Web.Mvc.Common;

    using Microsoft.AspNetCore.Http;

    /// <summary>Validates the DNN anti-forgery token, read from the <c>RequestVerificationToken</c> header or the <c>__RequestVerificationToken</c> form field.</summary>
    public class ValidateAntiForgeryTokenAttribute : AuthorizeAttributeBase
    {
        /// <summary>Determines whether the request carries a valid anti-forgery token.</summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <returns><see langword="true"/> if the token is valid.</returns>
        public virtual bool IsAuthenticated(HttpContext httpContext)
        {
            try
            {
                if (Thread.CurrentPrincipal?.Identity?.IsAuthenticated != true)
                {
                    return false;
                }

                string token = httpContext.Request.Headers["RequestVerificationToken"];
                if (string.IsNullOrEmpty(token) && httpContext.Request.HasFormContentType)
                {
                    token = httpContext.Request.Form["__RequestVerificationToken"];
                }

                if (string.IsNullOrEmpty(token))
                {
                    return false;
                }

                var cookieValue = this.GetAntiForgeryCookieValue(httpContext);
                AntiForgery.Instance.Validate(cookieValue, token);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>Gets the anti-forgery cookie value from the request.</summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The cookie value, or an empty string.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Breaking change")]
        protected string GetAntiForgeryCookieValue(HttpContext context)
        {
            var cookieName = AntiForgery.Instance.CookieName;
            return context.Request.Cookies.TryGetValue(cookieName, out var value) ? value : string.Empty;
        }

        /// <inheritdoc />
        protected override bool AuthorizeCore(HttpContext httpContext)
        {
            return this.IsAuthenticated(httpContext);
        }
    }
}
