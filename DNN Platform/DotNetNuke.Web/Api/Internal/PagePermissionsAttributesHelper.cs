// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System.Security.Principal;
    using System.Threading;

    using DotNetNuke.Common;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Permissions;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A helper class for auth filter attributes that are based on page permissions.</summary>
    public class PagePermissionsAttributesHelper
    {
        /// <summary>Whether the current user has access to the <paramref name="permissionKey"/> for the current page.</summary>
        /// <param name="permissionKey">The permission key.</param>
        /// <returns><see langword="true"/> if the user has access, otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// Kept as the public contract. The principal was read from
        /// <see cref="Thread.CurrentPrincipal"/>, which the <c>System.Web</c> pipeline populated; it is
        /// now taken from the ambient <see cref="HttpContext.User"/> when a request is in flight, falling
        /// back to <see cref="Thread.CurrentPrincipal"/> (which the DNN authentication handlers still
        /// populate). Prefer <see cref="HasTabPermission(string, IPrincipal)"/>, which takes the principal
        /// from the filter context and needs no ambient lookup.
        /// </remarks>
        public static bool HasTabPermission(string permissionKey)
        {
            return HasTabPermission(permissionKey, GetAmbientPrincipal());
        }

        /// <summary>Whether <paramref name="principal"/> has access to the <paramref name="permissionKey"/> for the current page.</summary>
        /// <param name="permissionKey">The permission key.</param>
        /// <param name="principal">The principal whose access is being checked.</param>
        /// <returns><see langword="true"/> if the user has access, otherwise <see langword="false"/>.</returns>
        public static bool HasTabPermission(string permissionKey, IPrincipal principal)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var currentPortal = PortalController.Instance.GetCurrentSettings();
            var currentUser = UserController.Instance.GetCurrentUserInfo();

            if (currentUser.IsSuperUser || PortalSecurity.IsInRole(currentPortal.AdministratorRoleName))
            {
                return true;
            }

            return TabPermissionController.HasTabPermission(permissionKey);
        }

        private static IPrincipal GetAmbientPrincipal()
        {
            var httpContext = Globals.GetCurrentServiceProvider()?.GetService<IHttpContextAccessor>()?.HttpContext;
            return httpContext?.User ?? Thread.CurrentPrincipal;
        }
    }
}
