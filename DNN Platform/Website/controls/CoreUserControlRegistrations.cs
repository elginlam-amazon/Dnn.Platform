// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Website.Controls
{
    using System;
    using System.Runtime.CompilerServices;

    using DotNetNuke.UI;

    /// <summary>
    /// Maps the stored control sources of the shared user controls that used to live under
    /// <c>Website/controls</c> onto the Blazor components that replaced their <c>.ascx</c> files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Most of the <c>controls/*.ascx</c> files were consumed through a
    /// <c>&lt;%@ Register TagPrefix="dnn" Src="~/controls/LabelControl.ascx" %&gt;</c> directive,
    /// which is a compile-time construct; those usages became component tags and need no runtime
    /// resolution. The two entries below are registered because they can still be asked for BY
    /// PATH — from a stored <c>ControlSrc</c> row or from third-party markup — and because the
    /// convention resolver does not produce the right answer for them.
    /// </para>
    /// <para>
    /// <b><c>urlcontrol.ascx</c> is the important one, and leaving it unregistered would silently
    /// bind the WRONG control.</b> Two non-abstract components are named <c>UrlControl</c>:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <c>DotNetNuke.UI.UserControls.UrlControl</c> (<c>Library/UI/UserControls/UrlControl.razor</c>)
    /// — the correct one. The <c>.ascx</c> declared
    /// <c>Inherits="DotNetNuke.UI.UserControls.UrlControl"</c>, and the component reproduces that
    /// file's markup exactly (the <c>TypeRow</c>/<c>URLRow</c>/<c>TabRow</c>/<c>FileRow</c>/
    /// <c>ImagesRow</c>/<c>UserRow</c>/<c>ErrorRow</c> panels plus the
    /// track/log/new-window checkboxes).
    /// </description></item>
    /// <item><description>
    /// <c>DNNConnect.CKEditorProvider.Controls.UrlControl</c> — a DIFFERENT control. It replaced
    /// the CKEditor provider's own <c>Controls/UrlControl.cs</c> + <c>UrlControl.ascx</c>, whose
    /// markup is just a folder drop-down and a file drop-down.
    /// </description></item>
    /// </list>
    /// <para>
    /// <c>DnnControlTypeResolver.FindByConvention</c> breaks a tie by preferring a type whose
    /// namespace ends with the containing directory segment. For <c>~/controls/urlcontrol.ascx</c>
    /// that segment is <c>controls</c>, and it is
    /// <c>DNNConnect.CKEditorProvider.<b>Controls</b></c> that ends with it — not
    /// <c>DotNetNuke.UI.UserControls</c>. So convention would resolve this path to the CKEditor
    /// provider's picker. The explicit registration below is what prevents that.
    /// </para>
    /// <para>
    /// <c>DnnUrlControl</c> is unambiguous by name and would resolve by convention; it is listed so
    /// the two URL pickers are declared together and so the binding survives any future type named
    /// <c>DnnUrlControl</c>. Note that it resolves at all only because the class is no longer
    /// <c>abstract</c> — <c>FindByConvention</c> filters out abstract types, and
    /// <c>ControlUtilities.LoadControl</c> cannot instantiate one.
    /// </para>
    /// <para>
    /// Follows the precedent of <c>admin/Skins/CoreSkinObjectRegistrations</c> and
    /// <c>admin/CoreAdminControlRegistrations</c>: both the historical <c>.ascx</c> source and the
    /// new <c>.razor</c> source are registered, so a registry row that is later migrated to the new
    /// file name keeps resolving. A host that registers its own
    /// <see cref="IDnnControlTypeResolver"/> in DI must call <see cref="Register"/> on it.
    /// </para>
    /// </remarks>
    public static class CoreUserControlRegistrations
    {
        /// <summary>Registers the shared <c>controls/</c> user controls with the given resolver.</summary>
        /// <param name="resolver">The resolver to register with.</param>
        public static void Register(IDnnControlTypeResolver resolver)
        {
            if (resolver is null)
            {
                return;
            }

            RegisterBoth(resolver, "~/controls/UrlControl", typeof(DotNetNuke.UI.UserControls.UrlControl));
            RegisterBoth(resolver, "~/controls/DnnUrlControl", typeof(DotNetNuke.Web.UI.WebControls.DnnUrlControl));

            // SILENT WRONG-BIND GUARD. Two IComponents are named Help:
            //   DotNetNuke.UI.UserControls.Help      - Library/UI/UserControls/Help.razor, the
            //                                          per-module help viewer. THIS is the target
            //                                          of the stored ControlSrc ~/controls/Help.ascx
            //                                          (ModuleControls ControlKey 'Help').
            //   DotNetNuke.UI.Skins.Controls.Help    - admin/Skins/Help.razor, the dnn:HELP skin
            //                                          object, which is a mailto link to the admin.
            // FindByConvention breaks the tie by asking whether the candidate's namespace ends with
            // the containing directory segment, here "controls". "DotNetNuke.UI.Skins.Controls"
            // ends with ".Controls" -> TRUE; "DotNetNuke.UI.UserControls" does NOT (its last nine
            // characters are "rControls") -> FALSE. So exactly one candidate survives and it is the
            // WRONG one: Module Help would silently render the administrator mailto link, with no
            // exception to reveal it. An explicit registration is a dictionary hit and takes
            // precedence over FindByConvention, which both fixes the bind and records the intent.
            // NOTE for future work: any ~/controls/*.ascx whose name is also used by a skin object
            // in DotNetNuke.UI.Skins.Controls has this same latent collision.
            RegisterBoth(resolver, "~/controls/Help", typeof(DotNetNuke.UI.UserControls.Help));
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
