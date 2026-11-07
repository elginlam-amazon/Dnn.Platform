// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Admin.Containers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Collections;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Security;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Containers;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    /// <summary>A control which renders module actions.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>ModuleActions.ascx</c> + <c>ModuleActions.ascx.cs</c> +
    /// <c>ModuleActions.ascx.designer.cs</c>; the markup is in <c>ModuleActions.razor</c> and the
    /// inline <c>&lt;script&gt;</c> block is in
    /// <c>wwwroot/admin/Menus/ModuleActions/ModuleActionsSetup.js</c>.
    /// </para>
    /// <para>
    /// <b>THE ACTION LIST IS THE AUTHORIZATION SURFACE FOR MODULE EDITING AND ITS FILTER IS
    /// PRESERVED VERBATIM.</b> <see cref="ProcessActions"/> keeps, in order and unchanged:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <c>if (!this.ActionRoot.Visible) return;</c> — when the root action is hidden, NO action is
    /// published to the client at all. <c>AdminActionsJSON</c>, <c>CustomActionsJSON</c> and
    /// <c>Panes</c> stay at their <c>"[]"</c> initial values, so the menu is empty.
    /// </description></item>
    /// <item><description>
    /// <c>if (action.Visible)</c> — an action the <c>ModuleActionCollection</c> marked invisible is
    /// dropped. That flag is where the module's own permission checks land.
    /// </description></item>
    /// <item><description>
    /// <c>if ((this.EditMode &amp;&amp; Globals.IsAdminControl() == false) || (action.Secure !=
    /// SecurityAccessLevel.Anonymous &amp;&amp; action.Secure != SecurityAccessLevel.View))</c> —
    /// copied character for character, including the <c>== false</c> and both <c>!=</c> comparisons.
    /// This is what keeps editing actions out of the menu for a user who is not in edit mode, and it
    /// is NOT simplified: inverting or merging either half changes which actions a non-editor sees.
    /// </description></item>
    /// <item><description>
    /// Only an action that survived all of the above is added to <c>actions</c>, and only such an
    /// action's id is added to <see cref="validIDs"/> (and only when it has no Url of its own, as
    /// before).
    /// </description></item>
    /// </list>
    /// <para>
    /// <b>The posted action id is still validated against that filtered list.</b> WebForms did this
    /// with <c>Page.ClientScript.RegisterForEventValidation(actionButton.UniqueID, id)</c> in
    /// <c>Render</c>: only the ids registered there could legally come back as
    /// <c>__EVENTARGUMENT</c>, and the framework rejected anything else. ASP.NET Core has no event
    /// validation, so <see cref="HandleActionAsync"/> checks the posted id against
    /// <see cref="validIDs"/> itself before dispatching. Dropping that check would have turned a
    /// framework-enforced allow-list into no check at all, which is exactly the kind of silent
    /// widening this must not do. <c>ActionBase.ProcessAction</c> then re-resolves the id against
    /// <c>ModuleContext.Actions</c> and runs <c>ActionManager.ProcessAction</c>, so a forged value
    /// is rejected twice.
    /// </para>
    /// <para>
    /// <b><c>Page.RegisterAsyncTask</c> / <c>IAsyncModuleControl</c>.</b> The original deferred
    /// <c>ProcessActions</c> with <c>this.Page.RegisterAsyncTask(new
    /// PageAsyncTask(this.ProcessActionsAsync))</c> when the module control implemented
    /// <see cref="IAsyncModuleControl"/>, because <c>this.Actions</c> was only reachable after the
    /// WebForms asynchronous point. A Blazor component has no such point, so the actions are read
    /// directly — which is precisely the decision <c>DotNetNuke.UI.Containers.ActionBase</c> already
    /// took for the same reason (see the comment in <c>ActionBase.ProcessAction</c> and its
    /// <c>OnLoad</c>, which calls <c>LoadActions()</c> inline instead of deferring it). NOTHING was
    /// added to the <c>IClientApiPage</c> seam for this: <c>RegisterAsyncTask</c> is genuinely
    /// absent there, and adding it would have introduced a WebForms page-lifecycle concept that has
    /// no meaning on a circuit. The behaviour is unchanged because both branches ran
    /// <c>ProcessActions</c> on the same request, only at different points of the page lifecycle.
    /// </para>
    /// <para>
    /// Other mechanical changes: <c>System.Web.Script.Serialization.JavaScriptSerializer</c> →
    /// <c>JsonExtensionsWeb.ToJsonString</c>, the designated replacement (Json.NET with
    /// <c>NullValueHandling.Include</c>, i.e. the same "every public property, including nulls"
    /// shape); <c>FileOrder.Css</c> is fully qualified because the name is ambiguous between
    /// <c>DotNetNuke.Web.Client</c> and <c>DotNetNuke.Abstractions.ClientResources</c>;
    /// <c>Page.ResolveClientUrl("~/")</c> → <c>Globals.ResolveUrl("~/")</c>;
    /// <c>actionButton.ClientID</c>/<c>UniqueID</c> → the stable per-module identifiers below;
    /// <c>quickSettings.Controls.Add(control)</c> → the component type plus its captured
    /// <c>[Parameter]</c> values rendered through <c>DynamicComponent</c>.
    /// </para>
    /// </remarks>
    public partial class ModuleActions : ActionBase
    {
        /// <summary>The name of the form field carrying the clicked action's id. Replaces <c>__EVENTARGUMENT</c>.</summary>
        private const string ActionField = "__dnnModuleAction";

        private readonly List<int> validIDs = [];
        private readonly IModuleControlPipeline moduleControlPipeline;
        private readonly IJavaScriptLibraryHelper javaScript;
        private readonly IClientResourceController clientResourceController;
        private readonly IServicesFramework servicesFramework;
        private readonly IHostSettings hostSettings;

        private bool scriptStarted;

        /// <summary>Initializes a new instance of the <see cref="ModuleActions"/> class.</summary>
        public ModuleActions()
            : base(Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>())
        {
            var services = Globals.GetCurrentServiceProvider();
            this.moduleControlPipeline = services.GetRequiredService<IModuleControlPipeline>();
            this.javaScript = services.GetRequiredService<IJavaScriptLibraryHelper>();
            this.clientResourceController = services.GetRequiredService<IClientResourceController>();
            this.servicesFramework = services.GetRequiredService<IServicesFramework>();
            this.hostSettings = services.GetRequiredService<IHostSettings>();
        }

        /// <summary>Gets or sets the JavaScript runtime the action menu plugin is set up through.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets the localized title of the generic (administrative) action group.</summary>
        protected string AdminText => Localization.GetString("ModuleGenericActions.Action", Localization.GlobalResourceFile);

        /// <summary>Gets the localized title of the module specific action group.</summary>
        protected string CustomText => Localization.GetString("ModuleSpecificActions.Action", Localization.GlobalResourceFile);

        /// <summary>Gets the localized title of the move action group.</summary>
        protected string MoveText => Localization.GetString(ModuleActionType.MoveRoot, Localization.GlobalResourceFile);

        /// <summary>Gets or sets the serialized administrative actions.</summary>
        protected string AdminActionsJSON { get; set; }

        /// <summary>Gets or sets the serialized module specific actions.</summary>
        protected string CustomActionsJSON { get; set; }

        /// <summary>Gets or sets a value indicating whether the quick settings are opened on load.</summary>
        protected bool DisplayQuickSettings { get; set; }

        /// <summary>Gets or sets the serialized pane names of the active tab.</summary>
        protected string Panes { get; set; }

        /// <summary>Gets or sets a value indicating whether the module can be moved.</summary>
        protected bool SupportsMove { get; set; }

        /// <summary>Gets or sets a value indicating whether the module has a QuickSettings control.</summary>
        protected bool SupportsQuickSettings { get; set; }

        /// <summary>Gets or sets a value indicating whether the module is shared.</summary>
        protected bool IsShared { get; set; }

        /// <summary>Gets or sets the module title.</summary>
        protected string ModuleTitle { get; set; }

        /// <summary>Gets or sets the component type of the loaded QuickSettings control.</summary>
        protected Type QuickSettingsControlType { get; set; }

        /// <summary>Gets or sets the captured parameters of the loaded QuickSettings control.</summary>
        protected IDictionary<string, object> QuickSettingsParameters { get; set; }

        /// <summary>Gets the name of the static form carrying the action post back.</summary>
        private string FormName => "moduleActions" + this.ModuleContext.ModuleId;

        /// <summary>
        /// Gets the client identifier of the action button. Replaces <c>actionButton.ClientID</c>,
        /// which the inline script used as the plugin's jQuery selector.
        /// </summary>
        private string ActionButtonClientId => "moduleActionsButton-" + this.ModuleContext.ModuleId;

        /// <summary>
        /// Gets the unique identifier of the action button. Replaces
        /// <c>actionButton.UniqueID</c>, which the plugin embeds in every action's
        /// <c>__doPostBack</c> Url.
        /// </summary>
        private string ActionButtonUniqueId => "moduleActions-" + this.ModuleContext.ModuleId;

        /// <summary>Gets the client identifier of the hidden field carrying the clicked action's id.</summary>
        private string ActionFieldClientId => "moduleActionsField-" + this.ModuleContext.ModuleId;

        /// <summary>Gets the localized text of the given key from the global resource file.</summary>
        /// <param name="key">The resource key.</param>
        /// <returns>The localized text.</returns>
        protected string LocalizeString(string key)
        {
            return Localization.GetString(key, Localization.GlobalResourceFile);
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.ID = "ModuleActions";

            this.javaScript.RequestRegistration(CommonJs.DnnPlugins);

            this.clientResourceController.RegisterStylesheet("~/admin/Menus/ModuleActions/ModuleActions.css", DotNetNuke.Abstractions.ClientResources.FileOrder.Css.ModuleCss);
            this.clientResourceController.RegisterStylesheet("~/resources/Shared/stylesheets/dnnicons/css/dnnicon.min.css", DotNetNuke.Abstractions.ClientResources.FileOrder.Css.ModuleCss);
            this.clientResourceController.RegisterScript("~/admin/Menus/ModuleActions/ModuleActions.js");
            this.clientResourceController.RegisterScript("~/admin/Menus/ModuleActions/ModuleActionsSetup.js");

            this.servicesFramework.RequestAjaxAntiForgerySupport();
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.AdminActionsJSON = "[]";
            this.CustomActionsJSON = "[]";
            this.Panes = "[]";
            try
            {
                this.SupportsQuickSettings = false;
                this.DisplayQuickSettings = false;
                this.ModuleTitle = this.ModuleContext.Configuration.ModuleTitle;
                var moduleDefinitionId = this.ModuleContext.Configuration.ModuleDefID;
                var quickSettingsControl = ModuleControlController.GetModuleControlByControlKey(this.hostSettings, "QuickSettings", moduleDefinitionId);

                if (quickSettingsControl != null)
                {
                    this.SupportsQuickSettings = true;
                    var control = this.moduleControlPipeline.LoadModuleControl(this, this.ModuleContext.Configuration, "QuickSettings", quickSettingsControl.ControlSrc);
                    if (control is not null)
                    {
                        // Was `control.ID += this.ModuleContext.ModuleId`, which made the loaded
                        // control's identifier unique on a page hosting several instances.
                        if (control is DotNetNuke.UI.Utilities.IClientApiControl identifiable)
                        {
                            identifiable.Id += this.ModuleContext.ModuleId;
                        }

                        this.QuickSettingsControlType = control.GetType();
                        this.QuickSettingsParameters = DnnComponentParameters.Capture(control);
                    }

                    this.DisplayQuickSettings = this.ModuleContext.Configuration.ModuleSettings.GetValueOrDefault("QS_FirstLoad", true);

                    // If we are displaying settings due to first load, then we need to update the setting to false, this WILL trigger a cache clear for the tab
                    if (this.DisplayQuickSettings)
                    {
                        ModuleController.Instance.UpdateModuleSetting(this.ModuleContext.ModuleId, "QS_FirstLoad", "False");
                    }

                    this.clientResourceController.RegisterScript("~/admin/Menus/ModuleActions/dnnQuickSettings.js");
                }

                // See the remarks: there is no WebForms asynchronous point to defer past any more,
                // so the IAsyncModuleControl branch and its PageAsyncTask are gone. Both branches
                // ran ProcessActions on this request.
                this.ProcessActions();
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (!firstRender || this.scriptStarted)
            {
                return;
            }

            this.scriptStarted = true;

            // Replaces the inline <script> block. Every value it interpolated is passed here, with
            // the same localization calls producing it.
            var settings = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["actionButton"] = this.ActionButtonUniqueId,
                ["actionButtonClientId"] = this.ActionButtonClientId,
                ["formId"] = this.FormName,
                ["fieldId"] = this.ActionFieldClientId,
                ["moduleId"] = this.ModuleContext.ModuleId,
                ["tabId"] = this.ModuleContext.TabId,
                ["customActionsJson"] = this.CustomActionsJSON,
                ["adminActionsJson"] = this.AdminActionsJSON,
                ["panesJson"] = this.Panes,
                ["customText"] = this.CustomText,
                ["adminText"] = this.AdminText,
                ["moveText"] = this.MoveText,
                ["topText"] = Localization.GetSafeJSString(this.LocalizeString("MoveTop.Action")),
                ["upText"] = Localization.GetSafeJSString(this.LocalizeString("MoveUp.Action")),
                ["downText"] = Localization.GetSafeJSString(this.LocalizeString("MoveDown.Action")),
                ["bottomText"] = Localization.GetSafeJSString(this.LocalizeString("MoveBottom.Action")),
                ["movePaneText"] = Localization.GetSafeJSString(this.LocalizeString("MoveToPane.Action")),
                ["deleteText"] = Localization.GetSafeJSString("DeleteItem.Text", Localization.SharedResourceFile),
                ["yesText"] = Localization.GetSafeJSString("Yes.Text", Localization.SharedResourceFile),
                ["noText"] = Localization.GetSafeJSString("No.Text", Localization.SharedResourceFile),
                ["confirmTitle"] = Localization.GetSafeJSString("Confirm.Text", Localization.SharedResourceFile),
                ["sharedText"] = Localization.GetSafeJSString("ModuleShared.Text", Localization.SharedResourceFile),
                ["rootFolder"] = Globals.ResolveUrl("~/"),
                ["supportsMove"] = this.SupportsMove,
                ["supportsQuickSettings"] = this.SupportsQuickSettings,
                ["displayQuickSettings"] = this.DisplayQuickSettings,
                ["isShared"] = this.IsShared,
                ["moduleTitle"] = Localization.GetSafeJSString(this.ModuleTitle),
            };

            await this.JsRuntime.InvokeVoidAsync("dnn.setUpModuleActions", settings);
        }

        /// <summary>Handles the action post back. Replaces <c>actionButton.Click</c>.</summary>
        /// <returns>A task.</returns>
        private Task HandleActionAsync()
        {
            try
            {
                var posted = this.Request?.Form[ActionField].ToString();
                if (string.IsNullOrEmpty(posted))
                {
                    // The WebForms handler passed __EVENTARGUMENT straight through; an empty
                    // argument resolved to no action.
                    return Task.CompletedTask;
                }

                // Replaces Page.ClientScript.RegisterForEventValidation: only an action that passed
                // the permission filter in ProcessActions may be dispatched. See the remarks.
                if (!int.TryParse(posted, out var postedId) || !this.validIDs.Contains(postedId))
                {
                    return Task.CompletedTask;
                }

                this.ProcessAction(posted);
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }

            return Task.CompletedTask;
        }

        private void ProcessActions()
        {
            try
            {
                if (!this.ActionRoot.Visible)
                {
                    return;
                }

                // Add Menu Items
                foreach (ModuleAction rootAction in this.ActionRoot.Actions)
                {
                    // Process Children
                    var actions = new List<ModuleAction>();
                    foreach (ModuleAction action in rootAction.Actions)
                    {
                        if (action.Visible)
                        {
                            if ((this.EditMode && Globals.IsAdminControl() == false) ||
                                (action.Secure != SecurityAccessLevel.Anonymous && action.Secure != SecurityAccessLevel.View))
                            {
                                if (!action.Icon.Contains("://")
                                        && !action.Icon.StartsWith("/", StringComparison.Ordinal)
                                        && !action.Icon.StartsWith("~/", StringComparison.Ordinal))
                                {
                                    action.Icon = "~/images/" + action.Icon;
                                }

                                if (action.Icon.StartsWith("~/", StringComparison.Ordinal))
                                {
                                    action.Icon = Globals.ResolveUrl(action.Icon);
                                }

                                actions.Add(action);

                                if (string.IsNullOrEmpty(action.Url))
                                {
                                    this.validIDs.Add(action.ID);
                                }
                            }
                        }
                    }

                    if (rootAction.Title == Localization.GetString("ModuleGenericActions.Action", Localization.GlobalResourceFile))
                    {
                        this.AdminActionsJSON = JsonExtensionsWeb.ToJsonString(actions);
                    }
                    else
                    {
                        if (rootAction.Title == Localization.GetString("ModuleSpecificActions.Action", Localization.GlobalResourceFile))
                        {
                            this.CustomActionsJSON = JsonExtensionsWeb.ToJsonString(actions);
                        }
                        else
                        {
                            this.SupportsMove = actions.Count > 0;
                            this.Panes = JsonExtensionsWeb.ToJsonString(this.PortalSettings.ActiveTab.Panes);
                        }
                    }
                }

                this.IsShared = this.ModuleContext.Configuration.AllTabs
                    || PortalGroupController.Instance.IsModuleShared(this.ModuleContext.ModuleId, PortalController.Instance.GetPortal(this.PortalSettings.PortalId))
                    || TabController.Instance.GetTabsByModuleID(this.ModuleContext.ModuleId).Count > 1;
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }
    }
}
