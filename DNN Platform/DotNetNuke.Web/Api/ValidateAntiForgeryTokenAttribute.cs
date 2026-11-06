// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Web.Api.Internal;

    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>A web API action filter which validates the anti-forgery token.</summary>
    /// <remarks>
    /// <para>
    /// Was an <c>ActionFilterAttribute</c> whose <c>OnActionExecuting</c> threw
    /// <see cref="UnauthorizedAccessException"/>; it is now an <see cref="IAsyncAuthorizationFilter"/>,
    /// because the ASP.NET Core anti-forgery validation API is asynchronous and because rejecting a forged
    /// request belongs on the authorization stage.
    /// </para>
    /// <para>
    /// The failure outcome is byte-identical to net48: the thrown
    /// <see cref="UnauthorizedAccessException"/> used to be turned into a <c>401</c> with an empty body
    /// and the localized <c>{controller}_{action}.Error</c> reason phrase by
    /// <see cref="DnnExceptionFilterAttribute"/>. Since ASP.NET Core exception filters do not observe
    /// exceptions thrown from authorization filters, that same filter is invoked directly here with the
    /// same exception, so the status code, the reason phrase and the exception logging all stay the same.
    /// </para>
    /// <para>
    /// This type is deliberately kept (rather than replaced by
    /// <c>Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute</c>): it is part of the DNN public
    /// contract (third-party modules apply it to their service controllers), it carries the DNN-specific
    /// auth-type bypass list fed by the authentication handlers, and
    /// <see cref="Internal.IFrameSupportedValidateAntiForgeryTokenAttribute"/> derives from it.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ValidateAntiForgeryTokenAttribute : Attribute, IAsyncAuthorizationFilter
    {
        /// <summary>The success result.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]

        // ReSharper disable once InconsistentNaming
        protected static readonly Tuple<bool, string> SuccessResult = new Tuple<bool, string>(true, null);

        private const string HeaderTokenName = "RequestVerificationToken";

        private static readonly List<string> BypassedAuthTypes = new List<string>();

        /// <summary>Gets a value indicating whether more than one instance of this filter can be applied to a single program element.</summary>
        public virtual bool AllowMultiple => false;

        /// <inheritdoc />
        public virtual async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (BypassTokenCheck(context))
            {
                return;
            }

            var result = await this.IsAuthorizedAsync(context);
            if (!result.Item1)
            {
                HandleUnauthorizedRequest(context, new UnauthorizedAccessException(result.Item2));
            }
        }

        /// <summary>Appends the given <paramref name="authType"/> to the bypassed auth types.</summary>
        /// <param name="authType">The auth type to add.</param>
        internal static void AppendToBypassAuthTypes(string authType)
        {
            var text = (authType ?? string.Empty).Trim();
            if (text.Length > 0)
            {
                BypassedAuthTypes.Add(text);
            }
        }

        /// <summary>Gets the value of the anti-forgery cookie.</summary>
        /// <param name="actionContext">The request context.</param>
        /// <returns>The cookie value or <see cref="string.Empty"/>.</returns>
        /// <remarks>
        /// The legacy implementation scanned the raw <c>Cookie</c> header for the configured cookie name.
        /// ASP.NET Core parses the header for us, so the cookie is read from
        /// <c>HttpRequest.Cookies</c> — same cookie, same name, same value.
        /// </remarks>
        protected static string GetAntiForgeryCookieValue(AuthorizationFilterContext actionContext)
        {
            var cookies = actionContext?.HttpContext?.Request?.Cookies;
            if (cookies == null)
            {
                return string.Empty;
            }

            return cookies[AntiForgery.Instance.CookieName] ?? string.Empty;
        }

        /// <summary>Rejects the request exactly the way the legacy thrown exception was rejected.</summary>
        /// <param name="actionContext">The request context.</param>
        /// <param name="exception">The exception describing the failure.</param>
        protected static void HandleUnauthorizedRequest(AuthorizationFilterContext actionContext, UnauthorizedAccessException exception)
        {
            ArgumentNullException.ThrowIfNull(actionContext);

            if (actionContext.HttpContext != null)
            {
                // Tells the host's status-code middleware to return the raw 401 rather than redirecting
                // to the login page, as for every other services framework authorization failure.
                actionContext.HttpContext.Items[AuthFilterContext.SuppressAuthenticationRedirectKey] = true;
            }

            var exceptionContext = new ExceptionContext(actionContext, actionContext.Filters)
            {
                Exception = exception,
            };

            new DnnExceptionFilterAttribute().OnException(exceptionContext);
            actionContext.Result = exceptionContext.Result;
        }

        /// <summary>Whether the anti-forgery check is bypassed for the authentication type of the request.</summary>
        /// <param name="actionContext">The request context.</param>
        /// <returns><see langword="true"/> when the check must be skipped.</returns>
        protected static bool BypassTokenCheck(AuthorizationFilterContext actionContext)
        {
            // bypass anti-forgery for those handlers that request so.
            IPrincipal principal = actionContext?.HttpContext?.User ?? Thread.CurrentPrincipal;
            var authType = principal?.Identity?.AuthenticationType;
            return !string.IsNullOrEmpty(authType) && BypassedAuthTypes.Contains(authType);
        }

        /// <summary>Whether the request is authorized.</summary>
        /// <param name="actionContext">The request context.</param>
        /// <returns>A <see cref="Tuple"/> where the first value is whether the request is authorized, and the second value is a message.</returns>
        protected virtual async Task<Tuple<bool, string>> IsAuthorizedAsync(AuthorizationFilterContext actionContext)
        {
            try
            {
                if (!BypassTokenCheck(actionContext))
                {
                    var token = actionContext?.HttpContext?.Request?.Headers[HeaderTokenName].ToString();

                    if (string.IsNullOrEmpty(token))
                    {
                        return new Tuple<bool, string>(false, "RequestVerificationToken not present");
                    }

                    var cookieValue = GetAntiForgeryCookieValue(actionContext);
                    await AntiForgery.Instance.ValidateAsync(cookieValue, token);
                }
            }
            catch (Exception e)
            {
                return new Tuple<bool, string>(false, e.Message);
            }

            return SuccessResult;
        }
    }
}
