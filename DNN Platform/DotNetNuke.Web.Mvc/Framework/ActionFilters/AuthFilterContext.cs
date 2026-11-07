// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionFilters
{
    using System;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Carries the authorization failure information for a failed DNN MVC authorization check.</summary>
    public class AuthFilterContext
    {
        /// <summary>
        /// Key set on <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> to signal that the
        /// unauthorized response must be returned as-is, instead of being turned into a login redirect
        /// by the authentication stack. This replaces the WebForms
        /// <c>HttpResponse.SuppressFormsAuthenticationRedirect</c> flag.
        /// </summary>
        public const string SuppressAuthenticationRedirectKey = "DNN:SuppressAuthenticationRedirect";

        private readonly IHostSettings hostSettings;

        /// <summary>Initializes a new instance of the <see cref="AuthFilterContext"/> class.</summary>
        /// <param name="filterContext">The authorization filter context.</param>
        /// <param name="authFailureMessage">The authorization failure message.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public AuthFilterContext(AuthorizationFilterContext filterContext, string authFailureMessage)
            : this(null, filterContext, authFailureMessage)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="AuthFilterContext"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="filterContext">The authorization filter context.</param>
        /// <param name="authFailureMessage">The authorization failure message.</param>
        public AuthFilterContext(IHostSettings hostSettings, AuthorizationFilterContext filterContext, string authFailureMessage)
        {
            this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            this.ActionContext = filterContext;
            this.AuthFailureMessage = authFailureMessage;
        }

        /// <summary>Gets the authorization filter context of the failed request.</summary>
        public AuthorizationFilterContext ActionContext { get; private set; }

        /// <summary>Gets or sets the authorization failure message.</summary>
        public string AuthFailureMessage { get; set; }

        /// <summary>
        /// Processes requests that fail authorization. This default implementation creates a new response with the
        /// Unauthorized status code. Override this method to provide your own handling for unauthorized requests.
        /// </summary>
        public virtual void HandleUnauthorizedRequest()
        {
            // UnauthorizedResult carries no payload, so the failure message is returned on an explicit
            // 401 result in order not to lose it.
            this.ActionContext.Result = new UnauthorizedObjectResult(this.AuthFailureMessage);

            if (!this.hostSettings.DebugMode)
            {
                this.ActionContext.HttpContext.Items[SuppressAuthenticationRedirectKey] = true;
            }
        }
    }
}
