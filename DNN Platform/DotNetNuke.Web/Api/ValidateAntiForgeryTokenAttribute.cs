// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;

    using DotNetNuke.Web.Api.Internal;

    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>A services framework action filter which validates the anti-forgery token.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ValidateAntiForgeryTokenAttribute : ActionFilterAttribute
    {
        /// <summary>The success result.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]

        // ReSharper disable once InconsistentNaming
        protected static readonly Tuple<bool, string> SuccessResult = new Tuple<bool, string>(true, null);

        private static readonly List<string> BypassedAuthTypes = new List<string>();

        /// <inheritdoc />
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!BypassTokenCheck(context))
            {
                var result = this.IsAuthorized(context);
                if (!result.Item1)
                {
                    throw new UnauthorizedAccessException(result.Item2);
                }
            }
        }

        /// <summary>Adds an authentication type for which the anti-forgery token is not validated.</summary>
        /// <param name="authType">The authentication type.</param>
        internal static void AppendToBypassAuthTypes(string authType)
        {
            var text = (authType ?? string.Empty).Trim();
            if (text.Length > 0)
            {
                BypassedAuthTypes.Add(text);
            }
        }

        /// <summary>Gets the value of the anti-forgery cookie of the request.</summary>
        /// <param name="actionContext">The action context.</param>
        /// <returns>The cookie value or <see cref="string.Empty"/>.</returns>
        protected static string GetAntiForgeryCookieValue(FilterContext actionContext)
        {
            var cookies = actionContext?.HttpContext.Request.Cookies;
            if (cookies != null && cookies.TryGetValue(AntiForgery.Instance.CookieName, out var cookieValue))
            {
                return cookieValue ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>Validates the anti-forgery token of the request (from the <c>RequestVerificationToken</c> header).</summary>
        /// <param name="actionContext">The action context.</param>
        /// <returns>A tuple with a value indicating whether the token is valid and the error message.</returns>
        protected virtual Tuple<bool, string> IsAuthorized(FilterContext actionContext)
        {
            try
            {
                if (!BypassTokenCheck(actionContext))
                {
                    string token = null;
                    if (actionContext?.HttpContext.Request.Headers.TryGetValue("RequestVerificationToken", out var values) == true)
                    {
                        token = values.FirstOrDefault();
                    }

                    if (string.IsNullOrEmpty(token))
                    {
                        return new Tuple<bool, string>(false, "RequestVerificationToken not present");
                    }

                    var cookieValue = GetAntiForgeryCookieValue(actionContext);
                    AntiForgery.Instance.Validate(cookieValue, token);
                }
            }
            catch (Exception e)
            {
                return new Tuple<bool, string>(false, e.Message);
            }

            return SuccessResult;
        }

        private static bool BypassTokenCheck(FilterContext actionContext)
        {
            // bypass anti-forgery for those handlers that request so.
            var authType = actionContext?.HttpContext.User?.Identity?.AuthenticationType ?? Thread.CurrentPrincipal?.Identity?.AuthenticationType;
            return !string.IsNullOrEmpty(authType) && BypassedAuthTypes.Contains(authType);
        }
    }
}
