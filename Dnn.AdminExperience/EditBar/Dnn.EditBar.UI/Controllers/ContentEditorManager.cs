// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.EditBar.UI.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Collections;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Modules.Definitions;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Personalization;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Containers;
    using DotNetNuke.UI.Skins;
    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.Client.ClientResourceManagement;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    using Newtonsoft.Json;

    using FileOrder = DotNetNuke.Abstractions.ClientResources.FileOrder;
    using Globals = DotNetNuke.Common.Globals;

    /// <summary>Content Editor Manager.</summary>
    /// <remarks>
    /// This was a <c>System.Web.UI</c> server control added to the skin's <c>&lt;form
    /// runat="server"&gt;</c>. The platform's skinning engine is a Blazor component tree now, so the
    /// base type is <see cref="DnnComponentBase"/>, which preserves the control surface this class
    /// is written against (<c>ID</c>/<c>Visible</c>/<c>Attributes</c>/<c>Controls</c>/
    /// <c>FindControl</c>/<c>OnInit</c>/<c>OnPreRender</c>/<c>CreateChildControls</c>/
    /// <c>EnsureChildControls</c>/<c>Request</c>/<c>Response</c>) and exposes the page as an
    /// <see cref="IClientApiPage"/>. It stays a code-only component: it renders nothing of its own,
    /// its whole job is registering client resources and restructuring the skin's pane tree.
    /// </remarks>
    public class ContentEditorManager : DnnComponentBase
    {
        /// <summary>The folder that contains the control.</summary>
        public const string ControlFolder = "~/DesktopModules/admin/Dnn.EditBar/Resources";

        /// <summary>
        /// The per-request <c>Items</c> key under which the <see cref="ProxyPage"/> is published.
        /// </summary>
        /// <remarks>
        /// Deliberately distinct from <c>typeof(ProxyPage)</c>: that key is an external contract which
        /// always carried an <c>IClientApiControl</c> (the module control) and must keep that single meaning.
        /// </remarks>
        private const string ProxyPageItemKey = "Dnn.EditBar.UI.ProxyPage";

        private const string ContentEditorManagerKey = "ContentEditorManager";

        private const FileOrder.Css CssFileOrder = (FileOrder.Css)40;

        private readonly IClientResourceController clientResourceController;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;
        private readonly IPortalController portalController;
        private readonly IHostSettings hostSettings;
        private readonly IUserController userController;
        private readonly IHostSettingsService hostSettingsService;
        private readonly IServicesFramework servicesFramework;
        private bool supportAjax = true;

        /// <summary>Initializes a new instance of the <see cref="ContentEditorManager"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public ContentEditorManager()
            : this(null, null, null, null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ContentEditorManager"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="userController">The user controller.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        public ContentEditorManager(IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IPortalController portalController, IHostSettings hostSettings, IUserController userController, IHostSettingsService hostSettingsService, IServicesFramework servicesFramework)
        {
            this.clientResourceController = clientResourceController ?? HttpContextSource.Current.GetScope().ServiceProvider.GetRequiredService<IClientResourceController>();
            this.appStatus = appStatus ?? HttpContextSource.Current.GetScope().ServiceProvider.GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? HttpContextSource.Current.GetScope().ServiceProvider.GetRequiredService<IEventLogger>();
            this.portalController = portalController ?? HttpContextSource.Current.GetScope().ServiceProvider.GetRequiredService<IPortalController>();
            this.hostSettings = hostSettings ?? HttpContextSource.Current.GetScope().ServiceProvider.GetRequiredService<IHostSettings>();
            this.userController = userController ?? HttpContextSource.Current.GetScope().ServiceProvider.GetRequiredService<IUserController>();
            this.hostSettingsService = hostSettingsService ?? HttpContextSource.Current.GetScope().ServiceProvider.GetRequiredService<IHostSettingsService>();
            this.servicesFramework = servicesFramework ?? HttpContextSource.Current.GetScope().ServiceProvider.GetRequiredService<IServicesFramework>();
        }

        /// <summary>Gets or sets the skin.</summary>
        /// <remarks>
        /// This MUST be a <see cref="ParameterAttribute"/>. <c>DnnComponentBase.RenderControl</c> renders a
        /// child by opening a component of this type and copying over only <c>[Parameter]</c> values
        /// (<c>DnnComponentParameters.Capture</c>), so the instance <c>EditBarModule</c> constructs and
        /// configures is discarded and the renderer owns the instance whose lifecycle actually runs.
        /// Without the attribute that instance would see <c>Skin == null</c> and every pane-dependent
        /// behaviour (pane wrapping, pane client ids, empty-pane class, module lookup) would silently no-op.
        /// This matches the platform convention for <c>DnnComponentBase.ID</c>/<c>Visible</c>/<c>CssClass</c>
        /// and <c>DnnGenericControl.Tag</c>/<c>InnerHtml</c>.
        /// </remarks>
        [Parameter]
        public Skin Skin { get; set; }

        /// <summary>Gets the portal settings of the current request.</summary>
        /// <remarks>Replaces <c>UserControlBase.PortalSettings</c>, which <c>DnnComponentBase</c> does not carry.</remarks>
        public PortalSettings PortalSettings => PortalSettings.Current;

        private static string LocalResourcesFile => Path.Combine(ControlFolder, "ContentEditorManager/App_LocalResources/SharedResources.resx");

        /// <summary>
        /// Gets the client API page of the current request.
        /// </summary>
        /// <remarks>
        /// <c>DnnComponentBase.Page</c> walks up the component tree, which is not yet attached while
        /// the skin builds itself, so the request-scoped registration is the fallback. This is the
        /// same pattern <c>DotNetNuke.UI.Skins.Skin</c> uses to register its own startup scripts.
        /// </remarks>
        private IClientApiPage ClientPage =>
            this.Page ?? HttpContextSource.Current?.RequestServices?.GetService<IClientApiPage>();

        private bool SupportAjax
        {
            get => AJAX.GetScriptManager(HttpContextSource.Current) && this.supportAjax;
            set => this.supportAjax = value;
        }

        /// <summary>Determines whether the specified permission is allowed for the tab.</summary>
        /// <param name="permissionKey">The permission key to check.</param>
        /// <returns>A value indicating whether the user has the specified permission for the tab.</returns>
        public static bool HasTabPermission(string permissionKey)
        {
            var principal = Thread.CurrentPrincipal;
            if (!principal.Identity.IsAuthenticated)
            {
                return false;
            }

            var currentPortal = PortalController.Instance.GetCurrentSettings();
            var currentUser = UserController.Instance.GetCurrentUserInfo();
            bool isAdminUser = currentUser.IsSuperUser || PortalSecurity.IsInRole(currentPortal.AdministratorRoleName);
            if (isAdminUser)
            {
                return true;
            }

            return TabPermissionController.HasTabPermission(permissionKey);
        }

        /// <summary>
        /// Gets the current instance of the ContentEditorManager.
        /// </summary>
        /// <param name="page">The page to check.</param>
        /// <returns><see cref="ContentEditorManager"/>.</returns>
        /// <remarks>
        /// The per-request singleton was stored in <c>Page.Items["ContentEditorManager"]</c>. The key
        /// is unchanged; the bag is the page's item dictionary when there is one, falling back to
        /// <c>HttpContextSource.Current.Items</c> (which is the same bag the client API page wraps)
        /// so the lookup works before the component tree is attached.
        /// </remarks>
        internal static ContentEditorManager GetCurrent(IClientApiPage page)
        {
            var items = page?.Items ?? HttpContextSource.Current?.Items;
            if (items != null && items.TryGetValue(ContentEditorManagerKey, out var instance))
            {
                return instance as ContentEditorManager;
            }

            return null;
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            var page = this.ClientPage;

            if (GetCurrent(page) != null)
            {
                throw new InvalidOperationException("Instance has already initialized");
            }

            this.AutoSetUserMode();

            var user = this.PortalSettings.UserInfo;

            if (user.UserID > 0)
            {
                ClientAPI.RegisterClientVariable(page, "dnn_current_userid", this.PortalSettings.UserInfo.UserID.ToString(CultureInfo.InvariantCulture), true);
            }

            var isInViewMode = Personalization.GetUserMode() != PortalSettings.Mode.Edit;
            var menuItemsCount = EditBarController.Instance.GetMenuItems().Count;
            if (isInViewMode || menuItemsCount == 0)
            {
                // Was: this.Parent.Controls.Remove(this). The parent is a DNN component collection now.
                if (this.Parent is IDnnControl parent)
                {
                    parent.Controls.Remove(this);
                }

                this.Visible = false;
                return;
            }

            this.RegisterClientResources();

            this.RegisterEditBarResources();

            var items = page?.Items ?? HttpContextSource.Current?.Items;
            items?.Add(ContentEditorManagerKey, this);

            // if there is pending work cookie, then reset it
            this.CheckPendingData();

            // if there is callback data cookie, then process the module for drag.
            this.CheckCallbackData();

            this.EnsureChildControls();
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            this.RemoveEmptyPaneClass();
            this.RegisterInitScripts();
        }

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// Was: wrap every pane of the skin in an <c>UpdatePanel</c> (conditional update mode,
        /// children as triggers) so that a module added/removed through the Edit Bar refreshed only
        /// its pane. There is no <c>UpdatePanel</c> on ASP.NET Core — Blazor diffs the DOM, so every
        /// render is already a partial update — but the markup contract the Edit Bar's JavaScript and
        /// stylesheets depend on is reproduced exactly: a wrapper <c>div</c> per pane carrying
        /// <c>class="DnnAjaxPanel &lt;original pane class&gt;"</c> and the <c>_SyncPanel</c> id
        /// suffix, with the pane's own class cleared and the pane moved inside the wrapper.
        /// </para>
        /// <para>
        /// AJAX support for the request is declared through <see cref="AJAX.RegisterScriptManager"/>
        /// (the WebForms code relied on the skin's <c>ScriptManager</c> instance being present); if
        /// the restructuring fails, <c>SupportAjax</c> is turned off exactly as before, which makes
        /// the client-side init fall back to full page loads.
        /// </para>
        /// </remarks>
        protected override void CreateChildControls()
        {
            base.CreateChildControls();

            AJAX.RegisterScriptManager();

            foreach (string paneId in this.PortalSettings.ActiveTab.Panes)
            {
                var pane = this.Skin?.FindControl(paneId) as IDnnControl;
                if (pane == null)
                {
                    continue;
                }

                // create the pane wrapper that replaces the update panel.
                // The pane is handed over through the [Parameter] ChildContent fragment rather than
                // through Controls: the renderer creates and owns the DnnGenericControl instance it
                // renders and carries over only [Parameter] values, so children added to the local
                // instance's Controls collection would never be rendered and the pane - along with
                // every module in it - would vanish from the page.
                var wrappedPane = pane;
                var updatePanel = new DnnGenericControl("div")
                {
                    ID = pane.Id + "_SyncPanel",
                    ChildContent = builder => RenderControl(builder, 0, wrappedPane),
                };

                try
                {
                    if (pane.Parent is not IDnnControl paneParent)
                    {
                        continue;
                    }

                    var paneIndex = paneParent.Controls.IndexOf(pane);
                    paneParent.Controls.AddAt(paneIndex, updatePanel);

                    // remove the pane from its original parent so it is not rendered twice: it is
                    // now rendered by the wrapper's ChildContent.
                    paneParent.Controls.Remove(pane);
                }
                catch (Exception)
                {
                    this.SupportAjax = false;
                    return;
                }

                updatePanel.Attributes["class"] = $"DnnAjaxPanel {GetAttribute(pane, "class")}";
                pane.Attributes["class"] = string.Empty;

                // Was: ScriptManager.GetCurrent(Page) is { IsInAsyncPostBack: true } plus the posted
                // __EVENTTARGET/__EVENTARGUMENT fields. ASP.NET Core has no async postback, but the
                // Edit Bar's client code still posts the very same two form fields when it asks the
                // server to re-render a pane, so the branch keys off the posted values themselves.
                var eventTarget = this.GetFormValue("__EVENTTARGET");
                var eventArgument = this.GetFormValue("__EVENTARGUMENT");
                if (AJAX.IsEnabled()
                    && updatePanel.ClientId == eventTarget
                    && !string.IsNullOrEmpty(eventArgument)
                    && !eventArgument.Equals("undefined", StringComparison.OrdinalIgnoreCase)
                    && eventArgument.StartsWith("module-", StringComparison.OrdinalIgnoreCase))
                {
                    var moduleId = Convert.ToInt32(eventArgument.Substring(7), CultureInfo.InvariantCulture);

                    var moduleContainer = this.FindModuleContainer(moduleId);
                    if (moduleContainer != null)
                    {
                        var moduleControl = this.FindModuleControl(moduleId);
                        var moduleInfo = this.FindModuleInfo(moduleId);

                        if (moduleControl != null && moduleInfo != null && moduleContainer.Parent is IDnnControl control)
                        {
                            control.Attributes["data-module-title"] = moduleInfo.ModuleTitle;

                            var page = this.ClientPage;
                            if (page != null)
                            {
                                if (this.HaveContentLayoutModuleOnPage())
                                {
                                    page.Items[typeof(ProxyPage)] = moduleControl;
                                }
                                else
                                {
                                    // Was: moduleControl.Page = new ProxyPage(...). IClientApiControl.Page
                                    // is read-only (the component tree owns it), so the proxy page is
                                    // published under its OWN per-request key instead; because ProxyPage
                                    // itself forwards every script/variable registration to the real page,
                                    // anything a module registers through it still reaches the page - which
                                    // is what the former Render() re-registration pass existed to do.
                                    // It deliberately does NOT reuse Items[typeof(ProxyPage)]: that key is
                                    // an external contract (read by the out-of-repo Content Layout module)
                                    // which always held an IClientApiControl, and must not now hold a ProxyPage.
                                    page.Items[ProxyPageItemKey] = new ProxyPage(this.portalController, this.appStatus, this.hostSettings, this.userController, this.hostSettingsService, page);
                                }
                            }

                            this.ProcessDragTipShown(moduleContainer);
                        }
                    }
                }
            }
        }

        private static string GetAttribute(IClientApiControl control, string name)
        {
            return control != null && control.Attributes.TryGetValue(name, out var value) ? value : string.Empty;
        }

        private static void FindControlRecursive(IClientApiControl rootControl, string controlId, ICollection<IClientApiControl> foundControls)
        {
            if (rootControl == null)
            {
                return;
            }

            if (rootControl.Id == controlId)
            {
                foundControls.Add(rootControl);
            }

            foreach (var subControl in rootControl.Children.ToList())
            {
                FindControlRecursive(subControl, controlId, foundControls);
            }
        }

        private static string GetPanesClientIds(IEnumerable<IEnumerable<string>> panelClientIdCollection)
        {
            return string.Join(";", panelClientIdCollection.Select(x => string.Join(",", x)));
        }

        private string GetFormValue(string key)
        {
            var request = this.Request;
            if (request == null || !request.HasFormContentType)
            {
                return null;
            }

            // Request.Form values are StringValues on ASP.NET Core.
            return request.Form[key].ToString();
        }

        private void RegisterClientResources()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            ClientResourceManager.EnableAsyncPostBackHandler();
#pragma warning restore CS0618 // Type or member is obsolete

            // register drop down list required resources
            this.clientResourceController.RegisterStylesheet("~/Resources/Shared/components/DropDownList/dnn.DropDownList.css", FileOrder.Css.ResourceCss);
            this.clientResourceController.RegisterStylesheet("~/Resources/Shared/scripts/jquery/dnn.jScrollBar.css", FileOrder.Css.ResourceCss);

            this.clientResourceController.RegisterScript("~/Resources/Shared/scripts/dnn.extensions.js");
            this.clientResourceController.RegisterScript("~/Resources/Shared/scripts/dnn.jquery.extensions.js");
            this.clientResourceController.RegisterScript("~/Resources/Shared/scripts/dnn.DataStructures.js");
            this.clientResourceController.RegisterScript("~/Resources/Shared/scripts/jquery/jquery.mousewheel.js");
            this.clientResourceController.RegisterScript("~/Resources/Shared/scripts/jquery/dnn.jScrollBar.js");
            this.clientResourceController.RegisterScript("~/Resources/Shared/scripts/TreeView/dnn.TreeView.js");
            this.clientResourceController.RegisterScript("~/Resources/Shared/scripts/TreeView/dnn.DynamicTreeView.js");
            this.clientResourceController.RegisterScript("~/Resources/Shared/Components/DropDownList/dnn.DropDownList.js");

            this.clientResourceController.RegisterScript(Path.Combine(ControlFolder, "ContentEditorManager/Js/ModuleManager.js"));
            this.clientResourceController.RegisterScript(Path.Combine(ControlFolder, "ContentEditorManager/Js/ModuleDialog.js"));
            this.clientResourceController.RegisterScript(Path.Combine(ControlFolder, "ContentEditorManager/Js/ExistingModuleDialog.js"));
            this.clientResourceController.RegisterScript(Path.Combine(ControlFolder, "ContentEditorManager/Js/ModuleService.js"));
            this.clientResourceController.RegisterScript(Path.Combine(ControlFolder, "ContentEditorManager/Js/ContentEditor.js"));
            this.clientResourceController.RegisterStylesheet(Path.Combine(ControlFolder, "ContentEditorManager/Styles/ContentEditor.css"), (FileOrder.Css)CssFileOrder);
            this.servicesFramework.RequestAjaxScriptSupport();

            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, CommonJs.DnnPlugins);

            // We need to add the Dnn JQuery plugins because the Edit Bar removes the Control Panel from the page
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, CommonJs.KnockoutMapping);

            this.clientResourceController.RegisterScript("~/Resources/Shared/Components/Tokeninput/jquery.tokeninput.js");
            this.clientResourceController.RegisterStylesheet("~/Resources/Shared/Components/Tokeninput/Themes/token-input-facebook.css");
        }

        private void RegisterEditBarResources()
        {
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, CommonJs.jQuery);
            this.servicesFramework.RequestAjaxAntiForgerySupport();

            var page = this.ClientPage;

            ClientAPI.RegisterClientVariable(page, "editbar_isAdmin", this.IsAdmin().ToString(), true);

            var settings = EditBarController.Instance.GetConfigurations(this.PortalSettings.PortalId);
            var settingsScript = "window.editBarSettings = " + JsonConvert.SerializeObject(settings) + ";";

            // Was: Page.ClientScript.RegisterClientScriptBlock(type, key, script, addScriptTags: true).
            // IClientApiPage.RegisterClientScriptBlock emits the block verbatim, so the script tags
            // that the addScriptTags flag used to add are written here.
            page?.RegisterClientScriptBlock(page.GetType(), "EditBarSettings", $"<script type=\"text/javascript\">{settingsScript}</script>");

            this.clientResourceController.RegisterScript("~/DesktopModules/admin/Dnn.EditBar/scripts/editBarContainer.js");

            this.clientResourceController.RegisterStylesheet("~/DesktopModules/admin/Dnn.EditBar/css/editBarContainer.css");
        }

        private List<List<string>> GetPaneClientIdCollection()
        {
            var panelClientIds = new List<List<string>>(this.PortalSettings.ActiveTab.Panes.Count);

            try
            {
                // Was: Page.FindControl("SkinPlaceHolder").Controls[0], the placeholder Default.aspx
                // loaded the skin user control into. The skin is a component in the DNN component
                // tree now, so the walk starts from it directly.
                IClientApiControl skinControl = this.Skin;

                foreach (var pane in this.PortalSettings.ActiveTab.Panes.Cast<string>())
                {
                    var foundControls = new List<IClientApiControl>();
                    FindControlRecursive(skinControl, pane, foundControls);
                    panelClientIds.Add((from control in foundControls select control.ClientId).ToList());
                }
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
            }

            return panelClientIds;
        }

        /// <summary>
        /// remove default empty pane class as some skin have special style on it
        /// and it may caught style issues with content editor feature, then we use a
        /// new style to cover it.
        /// </summary>
        private void RemoveEmptyPaneClass()
        {
            foreach (string paneId in this.PortalSettings.ActiveTab.Panes)
            {
                var paneControl = this.Skin?.FindControl(paneId) as IDnnControl;
                if (paneControl != null
                        && !string.IsNullOrEmpty(GetAttribute(paneControl, "class"))
                        && GetAttribute(paneControl, "class").Contains("DNNEmptyPane"))
                {
                    paneControl.Attributes["class"] = $"{GetAttribute(paneControl, "class")} EditBarEmptyPane";
                }
            }
        }

        private void RegisterInitScripts()
        {
            this.RegisterLocalResources();

            var page = this.ClientPage;

            // Was: HttpContext.Current.Request.RawUrl. On ASP.NET Core the raw URL is rebuilt by the
            // platform's HttpRequestExtensions.GetRawUrl().
            var rawUrl = HttpContextSource.Current?.Request.GetRawUrl();
            ClientAPI.RegisterClientVariable(page, "cem_loginurl", Globals.LoginURL(rawUrl, false), true);

            var panes = string.Join(",", this.PortalSettings.ActiveTab.Panes.Cast<string>());
            var panesClientIds = GetPanesClientIds(this.GetPaneClientIdCollection());
            var script = string.Format(
                CultureInfo.InvariantCulture,
                "dnn.ContentEditorManager.init({{type: 'moduleManager', panes: '{0}', panesClientIds: '{2}', supportAjax: {1}}});",
                HtmlUtils.JavaScriptStringEncode(panes),
                this.SupportAjax ? "true" : "false",
                HtmlUtils.JavaScriptStringEncode(panesClientIds));

            // Was: ScriptManager.RegisterStartupScript when MS AJAX was present, otherwise
            // Page.ClientScript.RegisterStartupScript. There is a single startup-script path now.
            page?.RegisterStartupScript(this.GetType(), "ContentEditorManager", script, true);
        }

        private void RegisterLocalResources()
        {
            var script = $$"""
                           dnn.ContentEditorManagerResources = {
                               title: '{{Localization.GetSafeJSString("AddModule.Text", LocalResourcesFile)}}',
                               nomodules: '{{Localization.GetSafeJSString("NoModules.Text", LocalResourcesFile)}}',
                               dragtip: '{{Localization.GetSafeJSString("DragTip.Text", LocalResourcesFile)}}',
                               pendingsave: '{{Localization.GetSafeJSString("PendingSave.Text", LocalResourcesFile)}}',
                               confirmTitle: '{{Localization.GetSafeJSString("ConfirmTitle.Text", LocalResourcesFile)}}',
                               confirmYes: '{{Localization.GetSafeJSString("ConfirmYes.Text", LocalResourcesFile)}}',
                               confirmNo: '{{Localization.GetSafeJSString("ConfirmNo.Text", LocalResourcesFile)}}',
                               cancelConfirm: '{{Localization.GetSafeJSString("CancelConfirm.Text", LocalResourcesFile)}}',
                               deleteModuleConfirm: '{{Localization.GetSafeJSString("DeleteModuleConfirm.Text", LocalResourcesFile)}}',
                               cancel: '{{Localization.GetSafeJSString("Cancel.Text", LocalResourcesFile)}}',
                               searchPlaceHolder: '{{Localization.GetSafeJSString("SearchPlaceHolder.Text", LocalResourcesFile)}}',
                               categoryRecommended: '{{Localization.GetSafeJSString("Category_Recommended.Text", LocalResourcesFile)}}',
                               categoryAll: '{{Localization.GetSafeJSString("Category_All.Text", LocalResourcesFile)}}',
                               pagePicker_clearButtonTooltip: '{{Localization.GetSafeJSString("pagePicker_clearButtonTooltip.Text", LocalResourcesFile)}}',
                               pagePicker_loadingResultText: '{{Localization.GetSafeJSString("pagePicker_loadingResultText.Text", LocalResourcesFile)}}',
                               pagePicker_resultsText: '{{Localization.GetSafeJSString("pagePicker_resultsText.Text", LocalResourcesFile)}}',
                               pagePicker_searchButtonTooltip: '{{Localization.GetSafeJSString("pagePicker_searchButtonTooltip.Text", LocalResourcesFile)}}',
                               pagePicker_searchInputPlaceHolder: '{{Localization.GetSafeJSString("pagePicker_searchInputPlaceHolder.Text", LocalResourcesFile)}}',
                               pagePicker_selectedItemCollapseTooltip: '{{Localization.GetSafeJSString("pagePicker_selectedItemCollapseTooltip.Text", LocalResourcesFile)}}',
                               pagePicker_selectedItemExpandTooltip: '{{Localization.GetSafeJSString("pagePicker_selectedItemExpandTooltip.Text", LocalResourcesFile)}}',
                               pagePicker_selectItemDefaultText: '{{Localization.GetSafeJSString("pagePicker_selectItemDefaultText.Text", LocalResourcesFile)}}',
                               pagePicker_sortAscendingButtonTitle: '{{Localization.GetSafeJSString("pagePicker_sortAscendingButtonTitle.Text", LocalResourcesFile)}}',
                               pagePicker_sortAscendingButtonTooltip: '{{Localization.GetSafeJSString("pagePicker_sortAscendingButtonTooltip.Text", LocalResourcesFile)}}',
                               pagePicker_sortDescendingButtonTooltip: '{{Localization.GetSafeJSString("pagePicker_sortDescendingButtonTooltip.Text", LocalResourcesFile)}}',
                               pagePicker_unsortedOrderButtonTooltip: '{{Localization.GetSafeJSString("pagePicker_unsortedOrderButtonTooltip.Text", LocalResourcesFile)}}',
                               site: '{{Localization.GetSafeJSString("Site.Text", LocalResourcesFile)}}',
                               page: '{{Localization.GetSafeJSString("Page.Text", LocalResourcesFile)}}',
                               addExistingModule: '{{Localization.GetSafeJSString("AddExistingModule.Text", LocalResourcesFile)}}',
                               makeCopy: '{{Localization.GetSafeJSString("MakeCopy.Text", LocalResourcesFile)}}'
                           };
                           """;

            this.ClientPage?.RegisterStartupScript(this.GetType(), "ContentEditorManagerResources", script, true);
        }

        private void CheckPendingData()
        {
            var pendingData = this.Request?.Cookies["cem_pending"];
            if (pendingData != null)
            {
                if (!string.IsNullOrEmpty(pendingData))
                {
                    var tabId = this.PortalSettings.ActiveTab.TabID;
                    if (pendingData.StartsWith("module-", StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(pendingData.AsSpan(7), out var moduleId))
                    {
                        var module = ModuleController.Instance.GetModule(moduleId, tabId, false);
                        if (module != null)
                        {
                            this.RemoveTabModule(tabId, moduleId);

                            // remove related modules
                            ModuleController.Instance.GetTabModules(tabId).Values
                                .Where(m => m.CreatedOnDate > module.CreatedOnDate && m.CreatedByUserID == module.CreatedByUserID)
                                .ForEach(m =>
                                {
                                    this.RemoveTabModule(tabId, m.ModuleID);
                                });
                        }
                    }
                }

                this.DeleteCookie("cem_pending");
            }
        }

        /// <summary>Deletes a cookie by re-issuing it with an expiry in the past.</summary>
        /// <param name="name">The name of the cookie.</param>
        /// <remarks>
        /// Was: read the cookie from <c>Request.Cookies</c>, set <c>Expires</c> in the past and add
        /// it back to <c>Response.Cookies</c>. On ASP.NET Core request cookies are read-only strings,
        /// so the expired cookie is appended to the response instead, which emits the same
        /// <c>Set-Cookie</c> header the browser deletes on.
        /// </remarks>
        private void DeleteCookie(string name)
        {
            this.Response?.Cookies.Append(name, string.Empty, new CookieOptions { Expires = DateTimeOffset.Now.AddDays(-1), });
        }

        private void RemoveTabModule(int tabId, int moduleId)
        {
            ModuleController.Instance.DeleteTabModule(tabId, moduleId, false);

            // remove that module control
            var moduleControl = ControlUtilities.FindFirstDescendent<Container>(
                this.Skin,
                c => c.Id == "ctr" + moduleId);

            if (moduleControl?.Parent?.Parent is IDnnControl grandParent)
            {
                grandParent.Controls.Remove(moduleControl.Parent);
            }

            // Parent is only wired by DnnControlCollection.Adopt, so it can be null on a
            // renderer-created instance, in which case the removal above is a no-op. Hiding the
            // container as well guarantees the deleted module stops rendering for this request
            // either way (Visible is honoured by DnnComponentBase.BuildRenderTree), which is the
            // behaviour the WebForms tree removal had.
            if (moduleControl != null)
            {
                moduleControl.Visible = false;
            }
        }

        private void CheckCallbackData()
        {
            var callbackData = this.Request?.Cookies["CEM_CallbackData"];
            if (callbackData != null)
            {
                if (!string.IsNullOrEmpty(callbackData) && callbackData.StartsWith("module-", StringComparison.OrdinalIgnoreCase))
                {
                    var moduleId = Convert.ToInt32(callbackData.Substring(7), CultureInfo.InvariantCulture);

                    var moduleContainer = this.FindModuleContainer(moduleId);
                    var moduleInfo = this.FindModuleInfo(moduleId);
                    if (moduleContainer != null && moduleInfo != null && moduleContainer.Parent is IDnnControl parent)
                    {
                        parent.Attributes["data-module-title"] = moduleInfo.ModuleTitle;
                        this.ProcessDragTipShown(moduleContainer);
                    }
                }
            }
        }

        private void ProcessDragTipShown(Container moduleContainer)
        {
            var dragTipShown = Convert.ToString(Personalization.GetProfile("Usability", "DragTipShown" + this.PortalSettings.PortalId), CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(dragTipShown) && moduleContainer.Parent is IDnnControl parent && this.Request?.Cookies["noFloat"] == null)
            {
                Personalization.SetProfile("Usability", "DragTipShown" + this.PortalSettings.PortalId, "true");
                parent.Attributes["class"] = GetAttribute(parent, "class") + " dragtip";
            }
        }

        private Container FindModuleContainer(int moduleId)
        {
            return ControlUtilities.FindFirstDescendent<Container>(this.Skin, c => c.Id == "ctr" + moduleId);
        }

        private IClientApiControl FindModuleControl(int moduleId)
        {
            var moduleContainer = this.FindModuleContainer(moduleId);
            if (moduleContainer != null)
            {
                var moduleInfo = this.FindModuleInfo(moduleId);
                if (moduleInfo != null)
                {
                    var controlId = Path.GetFileNameWithoutExtension(moduleInfo.ModuleControl.ControlSrc);
                    return ControlUtilities.FindFirstDescendent<IClientApiControl>(moduleContainer, c => c.Id == controlId);
                }
            }

            return null;
        }

        private ModuleInfo FindModuleInfo(int moduleId)
        {
            return this.PortalSettings.ActiveTab.Modules.Cast<ModuleInfo>()
                        .FirstOrDefault(m => m.ModuleID == moduleId);
        }

        // The former UpdatePanelUnloadEvent handler (and GetUpdatePanelsInPane/IsListControl, which
        // existed only to find the panels to hook it onto) is gone. It reflected over
        // ScriptManager's System.Web.UI.IScriptManagerInternal.RegisterUpdatePanel to re-register
        // update panels that had already unloaded - a workaround for CONTENT-4039, where a pane's
        // partial update rendered stale markup. Blazor diffs the DOM against the server-side render
        // tree on every change, so there is no update-panel registry to repair and the workaround
        // has no target; removing it changes no other behaviour.
        private bool HaveContentLayoutModuleOnPage()
        {
            var moduleDefinition =
                ModuleDefinitionController.GetModuleDefinitions(this.hostSettings).Values
                    .FirstOrDefault(m => m.DefinitionName == "Content Layout");
            if (moduleDefinition != null)
            {
                return this.PortalSettings.ActiveTab.Modules.Cast<ModuleInfo>().Any(m => m.ModuleDefID == moduleDefinition.ModuleDefID);
            }

            return false;
        }

        private void SetLastPageHistory(string pageId)
        {
            this.Response?.Cookies.Append(
                "LastPageId",
                pageId,
                new CookieOptions { Path = !string.IsNullOrEmpty(Globals.ApplicationPath) ? Globals.ApplicationPath : "/", });
        }

        private string GetLastPageHistory()
        {
            var cookie = this.Request?.Cookies["LastPageId"];
            if (cookie != null)
            {
                return cookie;
            }

            return "NEW";
        }

        private void SetUserMode(string userMode)
        {
            Personalization.SetProfile("Usability", "UserMode" + this.PortalSettings.PortalId, userMode.ToUpperInvariant());
        }

        private void AutoSetUserMode()
        {
            int tabId = this.PortalSettings.ActiveTab.TabID;
            int portalId = PortalSettings.Current.PortalId;
            string pageId = $"{portalId}:{tabId}";

            var request = this.Request;
            var stayInEditMode = request?.Cookies["StayInEditMode"];
            if (stayInEditMode == "YES")
            {
                if (Personalization.GetUserMode() != PortalSettings.Mode.Edit)
                {
                    this.SetUserMode("EDIT");
                    this.SetLastPageHistory(pageId);

                    // Was: Response.Redirect(Request.RawUrl, endResponse: true). Globals.Redirect is
                    // the platform's Core equivalent: it writes the redirect (which ends the response
                    // for this handler) and swallows the former thread abort.
                    Globals.Redirect(request.GetRawUrl(), true);
                }

                return;
            }

            string lastPageId = this.GetLastPageHistory();
            var isShowAsCustomError = request != null && request.Query.ContainsKey("aspxerrorpath");

            if (lastPageId != pageId && !isShowAsCustomError)
            {
                // navigate between pages
                if (Personalization.GetUserMode() != PortalSettings.Mode.View)
                {
                    this.SetUserMode("VIEW");
                    this.SetLastPageHistory(pageId);
                    Globals.Redirect(request.GetRawUrl(), true);
                }
            }

            if (!isShowAsCustomError)
            {
                this.SetLastPageHistory(pageId);
            }
        }

        private bool IsAdmin()
        {
            var user = this.PortalSettings.UserInfo;
            return user.IsSuperUser || PortalSecurity.IsInRole(this.PortalSettings.AdministratorRoleName);
        }

        /// <summary>
        /// A page that stands in for the real page while a module control is rendered on its own.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The WebForms version reflected into <c>System.Web.UI.Page</c>'s private fields
        /// (<c>_header</c>, <c>_request</c>, <c>_response</c>, <c>_context</c>, <c>_application</c>,
        /// <c>_cache</c>) and its non-public <c>TemplateControlVirtualDirectory</c> property to make a
        /// second <c>Page</c> instance behave like the one being served. Those fields do not exist on
        /// ASP.NET Core, so there is nothing to reflect over - and nothing needs to be: the request,
        /// response, context and cache are ambient (<c>HttpContextSource.Current</c>) rather than page
        /// state, and the <c>&lt;head&gt;</c> is owned by the request-scoped page service.
        /// </para>
        /// <para>
        /// What the proxy still has to do is behave like the page for the code that asks it to find a
        /// control or to register client script, so it forwards both to the page it proxies (and
        /// copies the per-request <c>Items</c> entries as before). Because the registrations are
        /// forwarded at the moment they happen, they reach the real page directly; the former
        /// <c>Render</c> override, which re-registered the proxy's startup scripts onto the real page
        /// after the fact, is therefore no longer needed and is gone.
        /// </para>
        /// </remarks>
        public class ProxyPage : CDefault, IClientApiPage
        {
            private readonly IClientApiPage originalPage;
            private readonly Dictionary<object, object> items = new Dictionary<object, object>();
            private readonly Dictionary<string, string> attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Initializes a new instance of the <see cref="ProxyPage"/> class.</summary>
            /// <param name="originalPage">The page being proxied.</param>
            [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IPortalController. Scheduled removal in v12.0.0.")]
            public ProxyPage(IClientApiPage originalPage)
                : this(null, null, null, null, null, originalPage)
            {
            }

            /// <summary>Initializes a new instance of the <see cref="ProxyPage"/> class.</summary>
            /// <param name="portalController">The portal controller.</param>
            /// <param name="appStatus">The application status.</param>
            /// <param name="hostSettings">The host settings.</param>
            /// <param name="userController">The user controller.</param>
            /// <param name="hostSettingsService">The host settings service.</param>
            /// <param name="originalPage">The page being proxied.</param>
            public ProxyPage(IPortalController portalController, IApplicationStatusInfo appStatus, IHostSettings hostSettings, IUserController userController, IHostSettingsService hostSettingsService, IClientApiPage originalPage)
                : base(portalController, appStatus, hostSettings, userController, hostSettingsService)
            {
                this.originalPage = originalPage;

                if (originalPage?.Items != null)
                {
                    foreach (var entry in originalPage.Items)
                    {
                        this.items[entry.Key] = entry.Value;
                    }
                }
            }

            /// <inheritdoc />
            public event EventHandler PreRender
            {
                add
                {
                    if (this.originalPage != null)
                    {
                        this.originalPage.PreRender += value;
                    }
                }

                remove
                {
                    if (this.originalPage != null)
                    {
                        this.originalPage.PreRender -= value;
                    }
                }
            }

            /// <inheritdoc />
            public string Id
            {
                get => this.originalPage?.Id;
                set
                {
                    if (this.originalPage != null)
                    {
                        this.originalPage.Id = value;
                    }
                }
            }

            /// <inheritdoc />
            public string ClientId => this.originalPage?.ClientId;

            /// <inheritdoc />
            public string UniqueId => this.originalPage?.UniqueId;

            /// <inheritdoc />
            public IDictionary<string, string> Attributes => this.originalPage?.Attributes ?? this.attributes;

            /// <inheritdoc />
            public IClientApiControl Parent => this.originalPage?.Parent;

            /// <inheritdoc />
            public IClientApiControl NamingContainer => this.originalPage?.NamingContainer;

            /// <inheritdoc />
            public IList<IClientApiControl> Children => this.originalPage?.Children ?? new List<IClientApiControl>();

            /// <inheritdoc />
            public bool HasChildren => this.originalPage?.HasChildren == true;

            /// <inheritdoc />
            IClientApiPage IClientApiControl.Page => this;

            /// <inheritdoc />
            public Microsoft.AspNetCore.Http.HttpContext HttpContext => this.originalPage?.HttpContext ?? HttpContextSource.Current;

            /// <inheritdoc />
            public IDictionary<object, object> Items => this.items;

            /// <inheritdoc />
            public IServiceProvider RequestServices => this.originalPage?.RequestServices ?? HttpContextSource.Current?.RequestServices;

            /// <inheritdoc />
            public IClientApiControl Form => this.originalPage?.Form;

            /// <inheritdoc />
            public IClientApiControl FindControl(string id)
            {
                return this.originalPage?.FindControl(id);
            }

            /// <inheritdoc />
            public string GetRequestValue(string key) => this.originalPage?.GetRequestValue(key);

            /// <inheritdoc />
            public bool IsClientScriptBlockRegistered(Type type, string key) => this.originalPage?.IsClientScriptBlockRegistered(type, key) == true;

            /// <inheritdoc />
            public bool IsClientScriptBlockRegistered(string key) => this.originalPage?.IsClientScriptBlockRegistered(key) == true;

            /// <inheritdoc />
            public void RegisterClientScriptBlock(Type type, string key, string script) => this.originalPage?.RegisterClientScriptBlock(type, key, script);

            /// <inheritdoc />
            public void RegisterClientScriptInclude(string key, string url) => this.originalPage?.RegisterClientScriptInclude(key, url);

            /// <inheritdoc />
            public void RegisterClientScriptResource(Type type, string resourceName) => this.originalPage?.RegisterClientScriptResource(type, resourceName);

            /// <inheritdoc />
            public void RegisterStartupScript(Type type, string key, string script, bool addScriptTags) => this.originalPage?.RegisterStartupScript(type, key, script, addScriptTags);

            /// <inheritdoc />
            public void RegisterHiddenField(string name, string value) => this.originalPage?.RegisterHiddenField(name, value);

            /// <inheritdoc />
            public string GetPostBackEventReference(IClientApiControl control, string argument) => this.originalPage?.GetPostBackEventReference(control, argument);

            /// <inheritdoc />
            public string GetWebResourceUrl(Type type, string resourceName) => this.originalPage?.GetWebResourceUrl(type, resourceName);

            /// <inheritdoc />
            public void SetRenderMethodDelegate(Action<IClientApiHtmlWriter, IClientApiControl> renderMethod) => this.originalPage?.SetRenderMethodDelegate(renderMethod);
        }
    }
}
