// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Website.Admin
{
    using System;
    using System.Runtime.CompilerServices;

    using DotNetNuke.UI;

    /// <summary>
    /// Maps the stored module-control sources of the core admin controls under <c>admin/Portal</c>,
    /// <c>admin/Security</c>, <c>admin/Users</c> and <c>admin/Menus</c> onto the Blazor components
    /// that replaced their <c>.ascx</c> files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>ModuleControls</c> / <c>ModuleDefinitions</c> rows installed by
    /// <c>DotNetNuke.Data.SqlDataProvider</c> store sources such as
    /// <c>Admin/Security/AccessDenied.ascx</c>, <c>Admin/Portal/Terms.ascx</c> and
    /// <c>Admin/Users/ViewProfile.ascx</c>, and <c>PortalSettings.DefaultModuleActionMenu</c>
    /// defaults to <c>~/admin/Menus/ModuleActions/ModuleActions.ascx</c>. NONE of those stored
    /// values is changed by the conversion; they are bound to the new component types here, so every
    /// existing installation (and every <c>ctl=</c> link built from a ControlKey, including
    /// <c>Globals.AccessDeniedURL</c>'s <c>"Access Denied"</c>) keeps working.
    /// </para>
    /// <para>
    /// Explicit registration is REQUIRED rather than merely tidy, because
    /// <c>DnnControlTypeResolver.FindByConvention</c> throws when a file name matches more than one
    /// component type and the containing directory does not disambiguate it:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <c>Terms</c> and <c>Privacy</c> are also the skin objects
    /// <c>DotNetNuke.UI.Skins.Controls.Terms</c> / <c>.Privacy</c> (<c>admin/Skins</c>), and neither
    /// candidate's namespace ends with the <c>Portal</c> directory segment.
    /// </description></item>
    /// <item><description>
    /// <c>ViewProfile</c> is also <c>DesktopModules/Admin/ViewProfile/ViewProfile.razor</c>.
    /// </description></item>
    /// <item><description>
    /// <c>Message</c> and <c>NoContent</c> are generic names, and
    /// <c>DotNetNuke.Common.Controls</c> does not end with <c>.Portal</c> either.
    /// </description></item>
    /// <item><description>
    /// <c>AccessDenied</c> would not resolve at all by convention: the WebForms type was named
    /// <c>AccessDeniedPage</c>, which does not match the file name, whereas the Razor compiler names
    /// the component after its file (<c>AccessDenied.razor</c> -> <c>AccessDenied</c>).
    /// </description></item>
    /// </list>
    /// <para>
    /// Follows the precedent of <c>admin/Skins/CoreSkinObjectRegistrations</c>: both the historical
    /// <c>.ascx</c> source and the new <c>.razor</c> source are registered, so a registry row that
    /// is later migrated to the new file name keeps resolving. A host that registers its own
    /// <see cref="IDnnControlTypeResolver"/> in DI must call <see cref="Register"/> on it.
    /// </para>
    /// </remarks>
    public static class CoreAdminControlRegistrations
    {
        /// <summary>Registers the core admin module controls with the given resolver.</summary>
        /// <param name="resolver">The resolver to register with.</param>
        public static void Register(IDnnControlTypeResolver resolver)
        {
            if (resolver is null)
            {
                return;
            }

            RegisterBoth(resolver, "~/Admin/Portal/Terms", typeof(DotNetNuke.Common.Controls.Terms));
            RegisterBoth(resolver, "~/Admin/Portal/Privacy", typeof(DotNetNuke.Common.Controls.Privacy));
            RegisterBoth(resolver, "~/Admin/Portal/Message", typeof(DotNetNuke.Common.Controls.Message));
            RegisterBoth(resolver, "~/Admin/Portal/NoContent", typeof(DotNetNuke.Common.Controls.NoContent));

            RegisterBoth(resolver, "~/Admin/Security/AccessDenied", typeof(DotNetNuke.Modules.Admin.Security.AccessDenied));
            RegisterBoth(resolver, "~/Admin/Security/SendPassword", typeof(DotNetNuke.Modules.Admin.Security.SendPassword));
            RegisterBoth(resolver, "~/Admin/Security/PasswordReset", typeof(DotNetNuke.Modules.Admin.Security.PasswordReset));

            RegisterBoth(resolver, "~/Admin/Users/ViewProfile", typeof(DotNetNuke.Modules.Admin.Users.ViewProfile));

            RegisterBoth(resolver, "~/Admin/Menus/ModuleActions/ModuleActions", typeof(DotNetNuke.Admin.Containers.ModuleActions));

            // The alternative, DDR-driven module action menu. Its .ascx had no code-behind (an
            // inline <script runat="server">), so the component lives in the module that owned the
            // only control it rendered: DotNetNuke.Web.DDRMenu.
            RegisterBoth(resolver, "~/Admin/Menus/DNNActions/DDRActionsMenu", typeof(DotNetNuke.Web.DDRMenu.DDRActionsMenu));

            // The container action button. Markup-only .ascx over DotNetNuke.UI.Containers.ActionButton,
            // which builds its own ActionButtonList child; registered alongside the other
            // admin/Containers controls' stored sources for consistency.
            // ActionButton is deprecated in favour of ActionCommandButton/ActionButtonList, but the
            // whole purpose of this entry is to bind the LEGACY stored ControlSrc row
            // (~/Admin/Containers/ActionButton.ascx) to the LEGACY type, so the deprecated type is
            // exactly what must be named here. Narrow suppression on this one line only.
#pragma warning disable CS0618 // Type or member is obsolete
            RegisterBoth(resolver, "~/Admin/Containers/ActionButton", typeof(DotNetNuke.UI.Containers.ActionButton));
#pragma warning restore CS0618 // Type or member is obsolete

            // ---------------------------------------------------------------------------------
            // CONVERSION-TIME RENAMES. These seven controls were deliberately renamed during the
            // WebForms -> Blazor conversion (admin/Modules and admin/Tabs each shipped an
            // import.ascx and an export.ascx, so the components needed distinct names), or they
            // landed in a namespace that no longer echoes their directory. Stored ControlSrc rows
            // in the ModuleControls table still carry the ORIGINAL path, and
            // DnnControlTypeResolver.FindByConvention matches on "type name == file base name"
            // with only a "namespace ends with the directory segment" tie-break. It has no rename
            // map, so without these explicit entries each stored row resolves to zero candidates
            // and ControlUtilities throws "The control type '...' could not be found." at runtime.
            // That is invisible to the compiler and to every startup gate, so the registration is
            // the only thing keeping these features reachable.
            RegisterBoth(resolver, "~/Admin/Modules/Export", typeof(DotNetNuke.Modules.Admin.Modules.ModuleExport));
            RegisterBoth(resolver, "~/Admin/Modules/Import", typeof(DotNetNuke.Modules.Admin.Modules.ModuleImport));
            RegisterBoth(resolver, "~/Admin/Tabs/Export", typeof(DotNetNuke.Modules.Admin.Tabs.TabExport));
            RegisterBoth(resolver, "~/Admin/Tabs/Import", typeof(DotNetNuke.Modules.Admin.Tabs.TabImport));

            // Both of these are named Settings, and there are a dozen types with that name. The
            // directory tie-break cannot help: DotNetNuke.Modules.Admin.Users does not end with
            // ".ViewProfile", and DotNetNuke.Modules.Admin.Authentication does not end with
            // ".DNN", so convention resolves to zero candidates and throws the ambiguity error.
            RegisterBoth(resolver, "~/DesktopModules/Admin/ViewProfile/Settings", typeof(DotNetNuke.Modules.Admin.Users.Settings));
            RegisterBoth(resolver, "~/DesktopModules/AuthenticationServices/DNN/Settings", typeof(DotNetNuke.Modules.Admin.Authentication.Settings));
        }

        [ModuleInitializer]
        internal static void RegisterWithSharedResolver()
        {
            Register(DnnControlTypeResolver.Instance);
        }

        private static void RegisterBoth(IDnnControlTypeResolver resolver, string sourceWithoutExtension, Type componentType)
        {
            resolver.Register(sourceWithoutExtension + ".ascx", componentType);
            resolver.Register(sourceWithoutExtension + ".razor", componentType);
        }
    }
}
