// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Skins.Controls
{
    using System.Runtime.CompilerServices;

    using DotNetNuke.UI;

    /// <summary>
    /// Maps the stored skin-object sources of the client-resource and head-content skin objects onto
    /// the Blazor components that replaced their <c>.ascx</c> files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The skin-object registry (the <c>SkinControls</c> table, filled by the package manifests)
    /// stores a control source such as <c>~/Admin/Skins/Login.ascx</c>, and
    /// <see cref="ControlUtilities.LoadControl{T}"/> resolves it through
    /// <see cref="IDnnControlTypeResolver"/>. The default convention — match a component type whose
    /// name equals the file name, preferring one whose namespace ends with the containing directory
    /// — is AMBIGUOUS for these skin objects: <c>Login</c>, <c>Help</c>, <c>Meta</c>, <c>Tags</c>,
    /// <c>Styles</c>, <c>Privacy</c> and <c>Terms</c> are type names used elsewhere in the
    /// installation (every authentication provider ships a <c>Login</c>), and these components live
    /// in <c>DotNetNuke.UI.Skins.Controls</c>, which does not end with the <c>Skins</c> directory
    /// segment. The resolver fails loudly in that case, so the mapping is registered explicitly.
    /// </para>
    /// <para>
    /// The module initializer registers into the process wide
    /// <see cref="DnnControlTypeResolver.Instance"/>, which is the resolver
    /// <see cref="ControlUtilities"/> falls back to. A host that registers its own
    /// <see cref="IDnnControlTypeResolver"/> in DI must call
    /// <see cref="Register(IDnnControlTypeResolver)"/> on it.
    /// </para>
    /// </remarks>
    public static class CoreSkinObjectRegistrations
    {
        /// <summary>Registers the skin-object components with the given resolver.</summary>
        /// <param name="resolver">The resolver to register with.</param>
        public static void Register(IDnnControlTypeResolver resolver)
        {
            if (resolver is null)
            {
                return;
            }

            // Both the historical .ascx source and the .razor source are registered, so a registry
            // row that is migrated to the new file name keeps resolving.
            RegisterBoth(resolver, "Logo", typeof(Logo));
            RegisterBoth(resolver, "Login", typeof(Login));
            RegisterBoth(resolver, "Styles", typeof(Styles));
            RegisterBoth(resolver, "DnnJsInclude", typeof(DnnJsInclude));
            RegisterBoth(resolver, "DnnCssInclude", typeof(DnnCssInclude));
            RegisterBoth(resolver, "Meta", typeof(Meta));
            RegisterBoth(resolver, "Help", typeof(Help));
            RegisterBoth(resolver, "DnnLink", typeof(DnnLink));
            RegisterBoth(resolver, "Terms", typeof(Terms));
            RegisterBoth(resolver, "Privacy", typeof(Privacy));
            RegisterBoth(resolver, "Tags", typeof(Tags));

            // The large skin objects (admin/Skins batch 1). Same reason: the file name alone is
            // ambiguous across the installation - "User" is also DotNetNuke.UI.UserControls.User and
            // DotNetNuke.Modules.Admin.Users.User, "Search" and "Language" are common type names -
            // and DotNetNuke.UI.Skins.Controls does not end with the "Skins" directory segment.
            RegisterBoth(resolver, "TreeViewMenu", typeof(TreeViewMenu));
            RegisterBoth(resolver, "Search", typeof(Search));
            RegisterBoth(resolver, "Language", typeof(Language));
            RegisterBoth(resolver, "BreadCrumb", typeof(BreadCrumb));
            RegisterBoth(resolver, "UserAndLogin", typeof(UserAndLogin));
            RegisterBoth(resolver, "User", typeof(User));
            RegisterBoth(resolver, "Toast", typeof(Toast));
            RegisterBoth(resolver, "Links", typeof(Links));

            // The two remaining admin/Skins skin objects, whose .ascx markup was superseded by the
            // component building its own child tree (admin/Skins batch 2). Both are registered for
            // the same reason as the batch above - DotNetNuke.UI.Skins.Controls does not end with
            // the "Skins" directory segment, so the directory cannot disambiguate them - and both
            // are load bearing: Skin.AddModuleMessage/AddLocalizedModuleMessage render through
            // ModuleMessage, and ControlPanel is the host of the permission-gated edit bar.
            RegisterBoth(resolver, "ModuleMessage", typeof(ModuleMessage));
            RegisterBoth(resolver, "ControlPanel", typeof(ControlPanel));

            // The [DOTNETNUKE] skin token. Its class has ALWAYS been named DotNetNukeControl
            // (a component file cannot be called DotNetNuke.razor, because "DotNetNuke" is a
            // namespace), so the stored SkinControls source ~/Admin/Skins/DotNetNuke.ascx can
            // never be matched by FindByConvention's "type name == file base name" rule. Without
            // this entry EVERY skin that uses the [DOTNETNUKE] token fails to render.
            RegisterBoth(resolver, "DotNetNuke", typeof(DotNetNukeControl));
        }

        [ModuleInitializer]
        internal static void RegisterWithSharedResolver()
        {
            Register(DnnControlTypeResolver.Instance);
        }

        private static void RegisterBoth(IDnnControlTypeResolver resolver, string fileBaseName, System.Type componentType)
        {
            resolver.Register($"~/Admin/Skins/{fileBaseName}.ascx", componentType);
            resolver.Register($"~/Admin/Skins/{fileBaseName}.razor", componentType);
        }
    }
}
