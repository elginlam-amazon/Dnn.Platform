// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Helpers
{
    using System.Net;

    using Microsoft.AspNetCore.Antiforgery;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Replacement for the System.Web.WebPages <c>AntiForgery</c> helper on .NET 10, implemented on top of ASP.NET Core <see cref="IAntiforgery"/>.</summary>
    public static class AntiForgery
    {
        /// <summary>Generates the hidden form field which contains the request token (and sets the anti-forgery cookie).</summary>
        /// <returns>The HTML of the hidden field.</returns>
        public static HtmlString GetHtml()
        {
            var tokens = GetTokens(GetCurrentContext());
            return new HtmlString($"<input name=\"{WebUtility.HtmlEncode(tokens.FormFieldName)}\" type=\"hidden\" value=\"{WebUtility.HtmlEncode(tokens.RequestToken)}\" />");
        }

        /// <summary>Gets the anti-forgery tokens (and sets the anti-forgery cookie).</summary>
        /// <param name="oldCookieToken">The current cookie token (ignored, ASP.NET Core reads it from the request).</param>
        /// <param name="newCookieToken">The new cookie token, or <see langword="null"/> when the current cookie token is still valid.</param>
        /// <param name="formToken">The request token.</param>
        public static void GetTokens(string oldCookieToken, out string newCookieToken, out string formToken)
        {
            var context = GetCurrentContext();
            var antiforgery = GetAntiforgery(context);
            var tokens = antiforgery.GetAndStoreTokens(context);
            newCookieToken = tokens.CookieToken;
            formToken = tokens.RequestToken;
        }

        /// <summary>Validates the anti-forgery tokens of the current request.</summary>
        /// <exception cref="HttpAntiForgeryException">The tokens are invalid.</exception>
        public static void Validate()
        {
            var context = GetCurrentContext();
            ValidateRequest(context);
        }

        /// <summary>Validates the anti-forgery tokens of the current request.</summary>
        /// <param name="cookieToken">The cookie token of the request.</param>
        /// <param name="formToken">The request token sent in the form or header of the request.</param>
        /// <remarks>
        /// ASP.NET Core validates the cookie and request tokens it reads from the request (cookie, form field or the
        /// <c>RequestVerificationToken</c> header configured in <c>AntiforgeryOptions</c>); the given tokens must be the
        /// tokens of the current request.
        /// </remarks>
        /// <exception cref="HttpAntiForgeryException">The tokens are invalid.</exception>
        public static void Validate(string cookieToken, string formToken)
        {
            if (string.IsNullOrEmpty(cookieToken) || string.IsNullOrEmpty(formToken))
            {
                throw new HttpAntiForgeryException("The required anti-forgery token is not present.");
            }

            ValidateRequest(GetCurrentContext());
        }

        private static void ValidateRequest(Microsoft.AspNetCore.Http.HttpContext context)
        {
            try
            {
                GetAntiforgery(context).ValidateRequestAsync(context).GetAwaiter().GetResult();
            }
            catch (AntiforgeryValidationException ex)
            {
                throw new HttpAntiForgeryException(ex.Message, ex);
            }
        }

        private static AntiforgeryTokenSet GetTokens(Microsoft.AspNetCore.Http.HttpContext context)
        {
            return GetAntiforgery(context).GetAndStoreTokens(context);
        }

        private static IAntiforgery GetAntiforgery(Microsoft.AspNetCore.Http.HttpContext context)
        {
            return context.RequestServices.GetRequiredService<IAntiforgery>();
        }

        private static Microsoft.AspNetCore.Http.HttpContext GetCurrentContext()
        {
            HttpContext context = HttpContext.Current ?? throw new InvalidOperationException("There is no current HTTP context.");
            return context;
        }
    }
}
