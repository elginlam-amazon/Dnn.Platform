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

    /// <summary>Carries context for handling an unauthorized MVC module request.</summary>
    public class AuthFilterContext
    {
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

        /// <summary>Gets the authorization filter context.</summary>
        public AuthorizationFilterContext ActionContext { get; private set; }

        /// <summary>Gets or sets the authorization failure message.</summary>
        public string AuthFailureMessage { get; set; }

        /// <summary>Processes requests that fail authorization by returning an Unauthorized result.</summary>
        public virtual void HandleUnauthorizedRequest()
        {
            this.ActionContext.Result = new UnauthorizedResult();
        }
    }
}
