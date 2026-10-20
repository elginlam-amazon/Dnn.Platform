// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Framework.ActionFilters
{
    using System;
    using System.Threading;

    using DotNetNuke.Entities.Users;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>Restricts access to an MVC module action to super users (hosts).</summary>
    public class RequireHostAttribute : AuthorizeAttributeBase
    {
        private UserInfo user;

        /// <inheritdoc />
        public override void OnAuthorization(AuthorizationFilterContext filterContext)
        {
            if (filterContext.HttpContext.Items.TryGetValue(typeof(DnnModuleState), out var stateObj) && stateObj is DnnModuleState state)
            {
                this.user = state.ModuleContext?.PortalSettings?.UserInfo;
            }
            else
            {
                throw new InvalidOperationException("This attribute can only be applied to MVC module Controllers.");
            }

            base.OnAuthorization(filterContext);
        }

        /// <inheritdoc />
        protected override bool AuthorizeCore(HttpContext httpContext)
        {
            var principal = Thread.CurrentPrincipal;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            if (this.user != null)
            {
                return this.user.IsSuperUser;
            }

            return false;
        }
    }
}
