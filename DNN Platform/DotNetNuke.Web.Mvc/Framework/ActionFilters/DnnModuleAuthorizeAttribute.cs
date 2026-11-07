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

    public class DnnModuleAuthorizeAttribute : DnnAuthorizeBaseAttribute
    {
        private ModuleInfo module;

        public DnnModuleAuthorizeAttribute()
        {
            this.AccessLevel = SecurityAccessLevel.Host;
        }

        public SecurityAccessLevel AccessLevel { get; set; }

        public string PermissionKey { get; set; }

        /// <inheritdoc />
        /// <remarks>This rule needs the DNN module controller, so it is evaluated on the action stage.</remarks>
        protected override bool RequiresController => true;

        /// <inheritdoc />
        public override void OnAuthorization(AuthorizationFilterContext context)
        {
            // The module application publishes the configured DNN controller on the request, which is
            // where it is read from (the filter context exposes the controller only on the action stage,
            // and this rule is evaluated there - see RequiresController).
            var controller = ModuleApplication.GetCurrentDnnController(context.HttpContext);

            if (controller == null)
            {
                throw new InvalidOperationException("This attribute can only be applied to Controllers that implement IDnnController");
            }

            this.module = controller.ModuleContext.Configuration;

            base.OnAuthorization(context);
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

        protected virtual bool HasModuleAccess()
        {
            return ModulePermissionController.HasModuleAccess(this.AccessLevel, this.PermissionKey, this.module);
        }
    }
}
