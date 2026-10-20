// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Framework.ActionFilters
{
    using System;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>Authorizes access to an MVC module action based on module permissions.</summary>
    public class DnnModuleAuthorizeAttribute : AuthorizeAttributeBase
    {
        private ModuleInfo module;

        /// <summary>Initializes a new instance of the <see cref="DnnModuleAuthorizeAttribute"/> class.</summary>
        public DnnModuleAuthorizeAttribute()
        {
            this.AccessLevel = SecurityAccessLevel.Host;
        }

        /// <summary>Gets or sets the required security access level.</summary>
        public SecurityAccessLevel AccessLevel { get; set; }

        /// <summary>Gets or sets the permission key.</summary>
        public string PermissionKey { get; set; }

        /// <inheritdoc />
        public override void OnAuthorization(AuthorizationFilterContext filterContext)
        {
            if (filterContext.HttpContext.Items.TryGetValue(typeof(DnnModuleState), out var stateObj) && stateObj is DnnModuleState state)
            {
                this.module = state.ModuleContext?.Configuration;
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
            if (this.module != null)
            {
                return this.HasModuleAccess();
            }

            return false;
        }

        /// <summary>Determines whether the current user has access to the module.</summary>
        /// <returns><see langword="true"/> if access is allowed.</returns>
        protected virtual bool HasModuleAccess()
        {
            return ModulePermissionController.HasModuleAccess(this.AccessLevel, this.PermissionKey, this.module);
        }
    }
}
