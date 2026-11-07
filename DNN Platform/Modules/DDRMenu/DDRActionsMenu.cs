// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.DDRMenu
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;
    using DotNetNuke.Web.Client.ResourceManager;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    /// <summary>The DDR-based module action menu (the <c>DNNActions</c> menu style).</summary>
    /// <remarks>
    /// <para>
    /// Replaces <c>Website/admin/Menus/DNNActions/DDRActionsMenu.ascx</c>. That file had no
    /// code-behind: it was a <c>&lt;%@ Control %&gt;</c> with an inline
    /// <c>&lt;script runat="server"&gt;</c> block, and it belongs to this module because the only
    /// thing it rendered was this module's <see cref="Actions"/> control. Its sibling assets
    /// (<c>menudef.xml</c>, <c>ULXSLT.xslt</c>, <c>dnnactions.js</c>,
    /// <c>dnnactions.debug.js</c>) are template/script content, not markup, and stay where they
    /// are — <see cref="MenuStyleFolder"/> is still the folder path they live in.
    /// </para>
    /// <para>
    /// The stored <c>ControlSrc</c> for this control is unchanged; it is bound to this type in
    /// <c>Website/admin/CoreAdminControlRegistrations.cs</c>, next to the registration of the
    /// alternative <c>admin/Menus/ModuleActions/ModuleActions</c> menu.
    /// </para>
    /// <para>
    /// <see cref="Actions"/> is added as a CHILD CONTROL in <see cref="OnInit"/> rather than
    /// rendered as a nested component from a <c>.razor</c> markup file, exactly as the
    /// <c>&lt;dnn:ACTIONS ID="actionMenu" MenuStyle="admin/Menus/DNNActions" runat="server" /&gt;</c>
    /// tag did. That matters: <see cref="Actions"/> derives from
    /// <c>DotNetNuke.UI.Containers.ActionBase</c>, which reaches its module through the control
    /// tree, and only <c>Controls.Add</c> establishes the parent link.
    /// </para>
    /// <para>
    /// The <c>&lt;dnn:DnnScriptBlock&gt;</c> block moved to
    /// <c>wwwroot/admin/Menus/DNNActions/dnnactionsConfirm.js</c>, because a component may not
    /// emit a <c>&lt;script&gt;</c> element. Its once-per-request behaviour is preserved: the
    /// <c>.ascx</c> set <c>confirmScript.Visible = false</c> on every instance after the first by
    /// flagging <c>Context.Items["ActionsMenuLoaded"]</c>, and <see cref="OnLoad"/> uses the same
    /// request item to decide whether this instance is the one that wires up the dialogs. Several
    /// modules on a page each get an action menu, and the jQuery selector inside the script is
    /// document-wide, so running it more than once per request would stack duplicate handlers.
    /// </para>
    /// </remarks>
    public class DDRActionsMenu : DnnComponentBase
    {
        /// <summary>The menu style (and asset folder) this action menu renders with.</summary>
        internal const string MenuStyleFolder = "admin/Menus/DNNActions";

        private const string ActionsMenuLoadedKey = "ActionsMenuLoaded";

        private readonly IServiceProvider serviceProvider;

        private Actions actionMenu;
        private bool shouldWireConfirmDialogs;
        private bool confirmDialogsWired;

        /// <summary>Initializes a new instance of the <see cref="DDRActionsMenu"/> class.</summary>
        public DDRActionsMenu()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DDRActionsMenu"/> class.</summary>
        /// <param name="serviceProvider">The DI container.</param>
        public DDRActionsMenu(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider ?? Globals.GetCurrentServiceProvider();
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.actionMenu = ActivatorUtilities.CreateInstance<Actions>(this.serviceProvider);
            this.actionMenu.ID = "actionMenu";
            this.actionMenu.MenuStyle = MenuStyleFolder;
            this.Controls.Add(this.actionMenu);
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Ported from the .ascx's <script runat="server"> OnLoad override.
            // Context.Items.Contains(...) / .Add(...) becomes the ASP.NET Core request items bag.
            // When there is no HttpContext (an interactive re-render carries no request) this
            // instance falls back to its own confirmDialogsWired flag, so the dialogs are still
            // wired at most once for this component.
            var items = HttpContextSource.Current?.Items;
            if (items is null)
            {
                this.shouldWireConfirmDialogs = true;
            }
            else if (!items.ContainsKey(ActionsMenuLoadedKey))
            {
                items.Add(ActionsMenuLoadedKey, true);
                this.shouldWireConfirmDialogs = true;
            }
            else
            {
                this.shouldWireConfirmDialogs = false;
            }

            // Was <dnn:DnnJsInclude runat="server" FilePath="~/admin/Menus/DNNActions/dnnactions.debug.js" />.
            // The WebForms control is a Tag Helper now and @addTagHelper is invalid in a component,
            // so the registration the Tag Helper performs is done against IClientResourceController
            // directly, with the Tag Helper's own defaults (which are the ordering contract):
            // priority = FileOrder.Js.DefaultPriority, provider = DefaultJsProvider.
            var clientResources = this.serviceProvider.GetService<IClientResourceController>();
            clientResources?
                .CreateScript("~/admin/Menus/DNNActions/dnnactions.debug.js")
                .SetProvider(ClientResourceProviders.DefaultJsProvider)
                .SetPriority((int)DotNetNuke.Web.Client.FileOrder.Js.DefaultPriority)
                .Register();

            // The five JavaScript library requests, in the order the .ascx made them.
            var appStatus = this.serviceProvider.GetRequiredService<IApplicationStatusInfo>();
            var eventLogger = this.serviceProvider.GetRequiredService<IEventLogger>();
            var portalSettings = Entities.Portals.PortalSettings.Current;

            JavaScript.RequestRegistration(appStatus, eventLogger, portalSettings, CommonJs.jQuery);
            JavaScript.RequestRegistration(appStatus, eventLogger, portalSettings, CommonJs.jQueryMigrate);
            JavaScript.RequestRegistration(appStatus, eventLogger, portalSettings, CommonJs.jQueryUI);
            JavaScript.RequestRegistration(appStatus, eventLogger, portalSettings, CommonJs.HoverIntent);
            JavaScript.RequestRegistration(appStatus, eventLogger, portalSettings, CommonJs.DnnPlugins);
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (!this.shouldWireConfirmDialogs || this.confirmDialogsWired)
            {
                return;
            }

            var jsRuntime = this.serviceProvider.GetService<IJSRuntime>();
            if (jsRuntime is null)
            {
                return;
            }

            this.confirmDialogsWired = true;

            // *** The key and the text are deliberately different calls; see the comment in
            // dnnactionsConfirm.js. The single-argument GetSafeJSString does NOT look up a
            // resource - it only JavaScript-encodes its argument - and the raw key is what the
            // rendered anchors' confirm('...') contains, so it is what the selector must match.
            // The dialog text is the localized string from the shared resource file.
            await jsRuntime.InvokeVoidAsync(
                "dnn.setUpDnnActionsConfirm",
                new Dictionary<string, string>
                {
                    ["deleteModuleKey"] = Localization.GetSafeJSString("DeleteModule.Confirm"),
                    ["deleteModuleText"] = Localization.GetSafeJSString("DeleteModule.Confirm", Localization.SharedResourceFile),
                    ["deleteSharedModuleKey"] = Localization.GetSafeJSString("DeleteSharedModule.Confirm"),
                    ["deleteSharedModuleText"] = Localization.GetSafeJSString("DeleteSharedModule.Confirm", Localization.SharedResourceFile),
                    ["yesText"] = Localization.GetSafeJSString("Yes.Text", Localization.SharedResourceFile),
                    ["noText"] = Localization.GetSafeJSString("No.Text", Localization.SharedResourceFile),
                    ["title"] = Localization.GetSafeJSString("Confirm.Text", Localization.SharedResourceFile),
                });
        }
    }
}
