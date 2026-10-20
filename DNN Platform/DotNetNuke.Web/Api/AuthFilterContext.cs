// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System.Net;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>The context of an authorization filter of the services framework.</summary>
    public class AuthFilterContext
    {
        /// <summary>Initializes a new instance of the <see cref="AuthFilterContext"/> class.</summary>
        /// <param name="actionContext">The authorization filter context.</param>
        /// <param name="authFailureMessage">The message sent when the authorization fails.</param>
        public AuthFilterContext(AuthorizationFilterContext actionContext, string authFailureMessage)
        {
            this.ActionContext = actionContext;
            this.AuthFailureMessage = authFailureMessage;
        }

        /// <summary>Gets the authorization filter context.</summary>
        public AuthorizationFilterContext ActionContext { get; private set; }

        /// <summary>Gets or sets the message sent when the authorization fails.</summary>
        public string AuthFailureMessage { get; set; }

        /// <summary>Processes requests that fail authorization (responds with <c>401 Unauthorized</c> and the failure message).</summary>
        public virtual void HandleUnauthorizedRequest()
        {
            this.ActionContext.Result = new ObjectResult(new HttpErrorMessage(this.AuthFailureMessage)) { StatusCode = (int)HttpStatusCode.Unauthorized, };
        }
    }
}
