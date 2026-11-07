// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Permissions;

    using Microsoft.AspNetCore.Http;

    /// <summary>Determines web API authorization based on module permissions.</summary>
    public class DnnModuleAuthorizeAttribute : DnnAuthorizeBaseAttribute, IOverrideDefaultAuthLevel
    {
        /// <summary>Initializes a new instance of the <see cref="DnnModuleAuthorizeAttribute"/> class.</summary>
        public DnnModuleAuthorizeAttribute()
        {
            this.AccessLevel = SecurityAccessLevel.Host;
        }

        /// <summary>Gets or sets a custom permission key to check for access.</summary>
        public string PermissionKey { get; set; }

        /// <summary>Gets or sets an access level to verify the user has.</summary>
        public SecurityAccessLevel AccessLevel { get; set; }

        /// <inheritdoc />
        public override bool IsAuthorized(AuthFilterContext context)
        {
            var activeModule = this.FindModuleInfo(context?.Request);

            if (activeModule != null)
            {
                return ModulePermissionController.HasModuleAccess(this.AccessLevel, this.PermissionKey, activeModule);
            }

            return false;
        }

        /// <summary>Finds the module associated with the request.</summary>
        /// <param name="request">The web API request.</param>
        /// <returns>A <see cref="ModuleInfo"/> instance or <see langword="null"/>.</returns>
        /// <remarks>
        /// The parameter was an <c>HttpRequestMessage</c> on net48; it is now the ASP.NET Core
        /// <see cref="HttpRequest"/>. The module is still resolved through the registered
        /// <c>ITabAndModuleInfoProvider</c> chain (<c>FindModuleInfo</c>), in the same probing order.
        /// </remarks>
        protected virtual ModuleInfo FindModuleInfo(HttpRequest request)
        {
            return request?.FindModuleInfo();
        }
    }
}
