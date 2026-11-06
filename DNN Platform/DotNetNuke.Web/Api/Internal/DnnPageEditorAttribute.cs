// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Permissions;

    /// <summary>Requires that the user is an editor on the page.</summary>
    public sealed class DnnPageEditorAttribute : DnnAuthorizeBaseAttribute, IOverrideDefaultAuthLevel
    {
        /// <inheritdoc />
        public override bool IsAuthorized(AuthFilterContext context)
        {
            Requires.NotNull("context", context);

            return PagePermissionsAttributesHelper.HasTabPermission("EDIT,CONTENT,MANAGE", context.Principal)
                   || IsModuleAdmin(GetPortalSettings());
        }

        /// <summary>Gets the portal settings of the current request.</summary>
        /// <remarks>
        /// The legacy filter reached the controller
        /// (<c>((DnnApiController)context.ActionContext.ControllerContext.Controller).PortalSettings</c>).
        /// In ASP.NET Core the controller instance does not exist yet while the authorization filters run,
        /// so the settings are taken from the same source <c>DnnApiController.PortalSettings</c> itself
        /// exposes — the portal settings the <c>DnnContextMiddleware</c> established for this request.
        /// </remarks>
        /// <returns>The current <see cref="PortalSettings"/>.</returns>
        private static PortalSettings GetPortalSettings()
        {
            return PortalController.Instance.GetCurrentSettings() as PortalSettings;
        }

        private static bool IsModuleAdmin(PortalSettings portalSettings)
        {
            if (portalSettings == null)
            {
                return false;
            }

            bool isModuleAdmin = false;
            foreach (ModuleInfo objModule in TabController.CurrentPage.Modules)
            {
                if (!objModule.IsDeleted)
                {
                    bool blnHasModuleEditPermissions = ModulePermissionController.HasModuleAccess(SecurityAccessLevel.Edit, Null.NullString, objModule);
                    if (blnHasModuleEditPermissions)
                    {
                        isModuleAdmin = true;
                        break;
                    }
                }
            }

            return portalSettings.ControlPanelSecurity == PortalSettings.ControlPanelPermission.ModuleEditor && isModuleAdmin;
        }
    }
}
