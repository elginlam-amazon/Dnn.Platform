// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using DNNConnect.CKEditorProvider.Constants;
    using DNNConnect.CKEditorProvider.Controls;
    using DNNConnect.CKEditorProvider.Objects;
    using DNNConnect.CKEditorProvider.Utilities;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Framework.Providers;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.JSInterop;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>The kind of input a single <see cref="EditorConfig"/> property is edited with.</summary>
    /// <remarks>
    /// Replaces the control type <c>RenderEditorConfigSettings</c> instantiated per property
    /// (<c>TextBox</c> multi line, <c>TextBox</c> numeric, <c>CheckBox</c>, <c>DropDownList</c>).
    /// </remarks>
    public enum EditorConfigSettingKind
    {
        /// <summary>A multi line text input (the original 5 row <c>TextBox</c>).</summary>
        MultiLineText = 0,

        /// <summary>A single line numeric text input.</summary>
        Numeric = 1,

        /// <summary>A check box.</summary>
        Boolean = 2,

        /// <summary>A drop down of the values of an enumeration.</summary>
        Select = 3,
    }

    /// <summary>The CKEditor options settings control.</summary>
    /// <remarks>
    /// <para>
    /// This was <c>CKEditorOptions.ascx</c>/<c>.ascx.cs</c>, a <c>PortalModuleBase</c> hosted by
    /// <c>Options.aspx</c> through <c>LoadControl</c>. It is now a Blazor component rendered by
    /// <c>Options.razor</c> with <see cref="ModuleConfiguration"/> as its parameter, and by the
    /// Editor Config Manager settings control.
    /// </para>
    /// <para>The WebForms concepts it relied upon map as follows:</para>
    /// <list type="bullet">
    /// <item><description>The ten <c>ViewState[...]</c> keys (<c>CurrentTabId</c>, <c>CurrentPortalId</c>, <c>DefaultHostLoadMode</c>, <c>IsHostMode</c>, <c>CurrentPortalOnly</c>, <c>CurrentSettingsMode</c> and the four hidden fields) are component state: parameters for the values the host assigns, fields for the rest.</description></item>
    /// <item><description>The designer's server controls are the fields bound by <c>CKEditorOptions.razor</c>; <c>DataSource</c>/<c>DataBind()</c> pairs are the row model lists the markup iterates.</description></item>
    /// <item><description><c>Utility.FindControl&lt;T&gt;(EditorConfigHolder, info.Name)</c> has no Blazor analogue: the reflection driven settings UI is the <see cref="editorConfigSettings"/> list, keyed by the property name in <see cref="editorConfigSettingsByName"/>.</description></item>
    /// <item><description><c>ScriptManager.RegisterStartupScript</c> is <see cref="ShowNotification"/> through js/optionsInterop.js, and <c>HttpUtility.JavaScriptStringEncode</c> is <see cref="DotNetNuke.Common.Utilities.HtmlUtils.JavaScriptStringEncode(string, bool)"/>.</description></item>
    /// </list>
    /// </remarks>
    public partial class CKEditorOptions
    {
        /// <summary>The provider type.</summary>
        private const string ProviderType = "htmlEditor";

        /// <summary>The name of the role every unauthenticated user belongs to.</summary>
        private const string UnauthenticatedUsersRoleName = "Unauthenticated Users";

        /// <summary>The identifier meaning "no portal".</summary>
        private const int NoPortal = -1;

        /// <summary>The provider config.</summary>
        private readonly ProviderConfiguration provConfig = ProviderConfiguration.GetProviderConfiguration(ProviderType);

        /// <summary>The notifications waiting to be shown on the client.</summary>
        private readonly List<(string Message, string Type)> pendingNotifications = new List<(string Message, string Type)>();

        /// <summary>The portal settings.</summary>
        private IPortalSettings portalSettings;

        /// <summary>Override Default Config Folder from Web.config.</summary>
        private string configFolder = string.Empty;

        /// <summary>The list toolbars.</summary>
        private List<ToolbarSet> listToolbars;

        /// <summary>The settings currently shown.</summary>
        private EditorProviderSettings currentSettings;

        /// <summary>The module instance name (the <c>minc</c> query string value).</summary>
        private string moduleInstanceName;

        /// <summary>The current module.</summary>
        private ModuleInfo currentModule;

        /// <summary>The message alerted before the popup closes, when the user may not edit the settings.</summary>
        private string accessDeniedMessage;

        /// <summary>Gets or sets the module the settings are edited for.</summary>
        /// <remarks>Replaces <c>PortalModuleBase.ModuleConfiguration</c>, which <c>Options.aspx</c> assigned after <c>LoadControl</c>.</remarks>
        [Parameter]
        public ModuleInfo ModuleConfiguration { get; set; }

        /// <summary>Gets or sets a value indicating whether this instance is host mode.</summary>
        /// <remarks>Replaces <c>ViewState["IsHostMode"]</c>.</remarks>
        [Parameter]
        public bool IsHostMode { get; set; }

        /// <summary>Gets or sets a value indicating whether [current portal only].</summary>
        [Parameter]
        public bool CurrentPortalOnly { get; set; }

        /// <summary>Gets or sets the Current or selected Tab ID.</summary>
        /// <remarks>Replaces <c>ViewState["CurrentTabId"]</c>, whose unset value was -1.</remarks>
        [Parameter]
        public int CurrentOrSelectedTabId { get; set; } = NoPortal;

        /// <summary>Gets or sets the Current or selected Portal ID.</summary>
        /// <remarks>Replaces <c>ViewState["CurrentPortalId"]</c>.</remarks>
        [Parameter]
        public int CurrentOrSelectedPortalId { get; set; }

        /// <summary>Gets or sets the default host load mode.</summary>
        /// <remarks>Replaces <c>ViewState["DefaultHostLoadMode"]</c>.</remarks>
        [Parameter]
        public int DefaultHostLoadMode { get; set; }

        /// <summary>Gets or sets the local resource file of this control.</summary>
        /// <remarks>Replaces <c>PortalModuleBase.LocalResourceFile</c>.</remarks>
        public string LocalResourceFile { get; set; }

        /// <summary>Gets or sets the JavaScript runtime the client side seam is called through.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets or sets the navigation manager the query string is read from.</summary>
        [Inject]
        protected NavigationManager Navigation { get; set; }

        /// <summary>Gets Current Language from Url.</summary>
        protected string LangCode => !string.IsNullOrEmpty(this.GetQueryValue("langCode"))
                                         ? this.GetQueryValue("langCode")
                                         : CultureInfo.CurrentCulture.Name;

        /// <summary>Gets the Name for the Current Resource file name.</summary>
        protected string ResXFile =>
            Globals.ResolveUrl($"~/Providers/HtmlEditorProviders/DNNConnect.CKE/{Localization.LocalResourceDirectory}/Options.resx");

        /// <summary>Gets the portal settings of the request this control renders on.</summary>
        /// <remarks>Replaces <c>PortalModuleBase.PortalSettings</c>, which read the request's portal settings.</remarks>
        protected DotNetNuke.Entities.Portals.PortalSettings PortalSettings =>
            DotNetNuke.Entities.Portals.PortalSettings.Current;

        /// <summary>Gets the identifier of the portal this control renders on.</summary>
        /// <remarks>Replaces <c>PortalModuleBase.PortalId</c>.</remarks>
        protected int PortalId => this.PortalSettings?.PortalId ?? this.HostSettingsService.HostPortalId;

        /// <summary>Gets the user this control renders for.</summary>
        /// <remarks>Replaces <c>PortalModuleBase.UserInfo</c>.</remarks>
        protected UserInfo UserInfo => UserController.Instance.GetCurrentUserInfo();

        /// <summary>Gets the identifier of the module the settings are edited for.</summary>
        /// <remarks>Replaces <c>PortalModuleBase.ModuleId</c>.</remarks>
        protected int ModuleId =>
            this.ModuleConfiguration != null && !Null.IsNull(this.ModuleConfiguration.ModuleID)
                ? this.ModuleConfiguration.ModuleID
                : this.GetQueryValueAsInt("ModuleId", Null.NullInteger);

        /// <summary>Gets the identifier of the page the module lives on.</summary>
        /// <remarks>Replaces <c>PortalModuleBase.TabId</c>.</remarks>
        protected int TabId =>
            this.ModuleConfiguration != null && !Null.IsNull(this.ModuleConfiguration.TabID)
                ? this.ModuleConfiguration.TabID
                : this.PortalSettings?.ActiveTab?.TabID ?? Null.NullInteger;

        [Inject]
        private IHostSettings HostSettingsService { get; set; }

        [Inject]
        private IApplicationStatusInfo AppStatus { get; set; }

        [Inject]
        private IEventLogger EventLogger { get; set; }

        [Inject]
        private IClientResourceController ClientResourceController { get; set; }

        [Inject]
        private IPortalAliasService PortalAliasService { get; set; }

        [Inject]
        private IModuleController ModuleControllerService { get; set; }

        [Inject]
        private ITabController TabControllerService { get; set; }

        /// <summary>Gets or sets the current settings mode.</summary>
        /// <remarks>Replaces <c>ViewState["CurrentSettingsMode"]</c>.</remarks>
        private SettingsMode CurrentSettingsMode { get; set; }

        /// <summary>Gets the current module.</summary>
        private ModuleInfo CurrentModule
        {
            get
            {
                if (this.currentModule != null)
                {
                    return this.currentModule;
                }

                if (this.ModuleConfiguration != null && !Null.IsNull(this.ModuleConfiguration.ModuleID))
                {
                    this.currentModule = this.ModuleConfiguration;
                    return this.currentModule;
                }

                this.currentModule = this.ModuleControllerService.GetModule(
                    this.GetQueryValueAsInt("ModuleId", -1),
                    this.TabId,
                    false);

                return this.currentModule;
            }
        }

        /// <summary>Gets a value indicating whether this instance is all instances.</summary>
        private bool IsAllInstances => this.IsHostMode && !this.CurrentPortalOnly;

        /// <summary>Gets the home directory.</summary>
        private string HomeDirectory => this.portalSettings?.HomeDirectoryMapPath ?? Globals.HostMapPath;

        /// <summary>Localizes a key out of this control's resource file.</summary>
        /// <param name="key">The resource key.</param>
        /// <returns>The localized text.</returns>
        /// <remarks>Replaces <c>PortalModuleBase.LocalizeString</c>, which read <c>LocalResourceFile</c>.</remarks>
        public string LocalizeString(string key) =>
            Localization.GetString(key, this.LocalResourceFile ?? this.ResXFile, this.LangCode);

        /// <summary>Localizes a key, falling back to the text the original markup carried inline.</summary>
        /// <param name="key">The resource key.</param>
        /// <param name="fallback">The default text the <c>.ascx</c> element declared.</param>
        /// <returns>The localized text, or <paramref name="fallback"/> when the key is not in the resource file.</returns>
        /// <remarks>
        /// Replaces DNN's automatic control localization, which left an <c>&lt;asp:label&gt;</c>'s
        /// inline text in place when the resource file carried no <c>&lt;id&gt;.Text</c> key.
        /// </remarks>
        internal string LocalizeOrDefault(string key, string fallback)
        {
            var localized = Localization.GetString(key, this.ResXFile, this.LangCode);
            return string.IsNullOrEmpty(localized) ? fallback : localized;
        }

        /// <summary>Binds the options data.</summary>
        /// <param name="reloadOptionsfromModule">if set to <see langword="true"/> [reload options from module].</param>
        internal void BindOptionsData(bool reloadOptionsfromModule = false)
        {
            // Check if Options Window is in Host Page
            if (this.IsHostMode)
            {
                if (!this.isReRender)
                {
                    this.lastTabId = "0";
                }

                this.portalSettings = this.GetPortalSettings();

                this.listToolbars = ToolbarUtil.GetToolbars(this.HomeDirectory, this.configFolder);

                this.RenderUrlControls(true);

                this.FillRoles();

                this.BindUserGroupsGridView();

                this.lblSetForVisible = false;
                this.rBlSetModeVisible = false;
                this.RemoveAllVisible = false;
                this.infoTabVisible = false;
                this.infoTabHolderVisible = false;

                if (this.DefaultHostLoadMode.Equals(0))
                {
                    var currentPortalLabel = string.Empty;
                    if (this.CurrentOrSelectedPortalId > NoPortal)
                    {
                        currentPortalLabel = string.Format(
                            "- <em>{0} {1} : - Portal ID: {2}</em>",
                            Localization.GetString("lblPortal.Text", this.ResXFile, this.LangCode),
                            this.portalSettings?.PortalName ?? "Host",
                            this.CurrentOrSelectedPortalId);
                    }

                    this.lblSettings = $"{Localization.GetString("lblSettings.Text", this.ResXFile, this.LangCode)} {currentPortalLabel}";
                }
                else if (this.DefaultHostLoadMode.Equals(1))
                {
                    var currentTabName = this.TabControllerService.GetTab(this.CurrentOrSelectedTabId, this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId, false).TabName;
                    var pageLabel = Localization.GetString("lblPage.Text", this.ResXFile, this.LangCode);
                    var settingsLabel = Localization.GetString("lblSettings.Text", this.ResXFile, this.LangCode);

                    this.lblSettings = $"{settingsLabel} - <em>{pageLabel} {currentTabName} - TabID: {this.CurrentOrSelectedTabId}</em>";
                }
                else
                {
                    this.lblSettings = Localization.GetString("lblSettings.Text", this.ResXFile, this.LangCode);
                }

                this.LoadSettings(this.DefaultHostLoadMode);
            }
            else
            {
                var pageKey = $"DNNCKT#{this.CurrentOrSelectedTabId}#";
                var moduleKey = $"DNNCKMI#{this.ModuleId}#INS#{this.moduleInstanceName}#";

                if (SettingsUtil.CheckExistsModuleInstanceSettings(moduleKey, this.ModuleId))
                {
                    this.LoadSettings(2);
                }
                else
                {
                    var settingsDictionary = EditorController.GetEditorHostSettings(this.HostSettingsService);

                    this.LoadSettings(SettingsUtil.CheckSettingsExistByKey(settingsDictionary, pageKey) ? 1 : 0);
                }
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Replaces <c>OnInit</c> (the module lookup and <c>InitializeComponent</c>) and
        /// <c>Page_Load</c>, whose <c>!IsPostBack</c> branch ran the whole load path once.
        /// </remarks>
        protected override void OnInitialized()
        {
            base.OnInitialized();

            // the <asp:ListItem> elements the .ascx markup declared inline.
            this.InitializeStaticItems();

            // OnInit: pick up the module the query string names when the host did not assign one.
            var moduleId = this.GetQueryValueAsInt("ModuleId", Null.NullInteger);
            if (moduleId != Null.NullInteger && this.ModuleConfiguration == null)
            {
                var module = ModuleController.Instance.GetModule(moduleId, this.TabId, false);
                if (module != null)
                {
                    this.ModuleConfiguration = module;
                }
            }

            this.InitializeComponent();

            // Page_Load
            this.AddJavaScripts();
            this.LocalResourceFile = this.ResXFile;
            this.portalSettings = this.GetPortalSettings();

            if (this.IsAllInstances
                    ? UserController.Instance.GetCurrentUserInfo().IsSuperUser
                    : Utility.IsInRoles(this.portalSettings.AdministratorRoleName, this.portalSettings))
            {
                this.listToolbars = ToolbarUtil.GetToolbars(this.HomeDirectory, this.configFolder);

                this.BindUserGroupsGridView();

                this.FillFolders();

                this.SetLanguage();

                this.BindOptionsData();

                this.FillInformations();

                // Load Skin List
                this.FillSkinList();

                this.RenderUrlControls();

                this.FillRoles();

                // Remove CKFinder from the Browser list if not installed
                if (!System.IO.File.Exists(this.AppStatus.ApplicationMapPath + "/Providers/HtmlEditorProviders/DNNConnect.CKE/ckfinder/ckfinder.js"))
                {
                    this.ddlBrowserItems.RemoveAt(2);
                }
            }
            else
            {
                this.Visible = false;

                this.accessDeniedMessage = Localization.GetString("Error1.Text", this.ResXFile, this.LangCode);
            }

            this.isReRender = true;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Runs the client side pieces the control registered through <c>ScriptManager</c>: the
        /// jQuery tabs/panels set-up, the queued notifications and the "no permission" alert that
        /// closed the popup.
        /// </remarks>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            IJSObjectReference module;
            try
            {
                module = await this.JsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "/Providers/HtmlEditorProviders/DNNConnect.CKE/js/optionsInterop.js");
            }
            catch (JSException)
            {
                // the extension's assets are not reachable; the markup stays usable unstyled.
                return;
            }

            if (this.accessDeniedMessage != null)
            {
                var message = this.accessDeniedMessage;
                this.accessDeniedMessage = null;
                await module.InvokeVoidAsync("alertAndClose", message);
                return;
            }

            await module.InvokeVoidAsync("initOptionsPage", "CKEditor_Options_pnlEditor");

            if (this.pendingNotifications.Count == 0)
            {
                return;
            }

            var imagePath = Globals.ResolveUrl("~/Providers/HtmlEditorProviders/DNNConnect.CKE/images/");
            foreach (var notification in this.pendingNotifications.ToList())
            {
                await module.InvokeVoidAsync("showNotificationBar", notification.Message, notification.Type, imagePath);
            }

            this.pendingNotifications.Clear();
        }

        /// <summary>Adds the JavaScripts.</summary>
        private void AddJavaScripts()
        {
            this.ClientResourceController.CreateStylesheet(Globals.ResolveUrl("~/Providers/HtmlEditorProviders/DNNConnect.CKE/css/jquery.notification.css")).Register();
            this.ClientResourceController.CreateStylesheet(Globals.ResolveUrl("~/Providers/HtmlEditorProviders/DNNConnect.CKE/css/Options.css")).Register();

            JavaScript.RequestRegistration(this.AppStatus, this.EventLogger, this.PortalSettings, CommonJs.jQuery);
            JavaScript.RequestRegistration(this.AppStatus, this.EventLogger, this.PortalSettings, CommonJs.jQueryMigrate);
            JavaScript.RequestRegistration(this.AppStatus, this.EventLogger, this.PortalSettings, CommonJs.jQueryUI);
            JavaScript.RequestRegistration(this.AppStatus, this.EventLogger, this.PortalSettings, CommonJs.DnnPlugins);

            // ScriptManager.RegisterClientScriptInclude is the client resource seam on ASP.NET Core.
            this.ClientResourceController.CreateScript(Globals.ResolveUrl("~/Providers/HtmlEditorProviders/DNNConnect.CKE/js/jquery.notification.js")).Register();
            this.ClientResourceController.CreateScript(Globals.ResolveUrl("~/Providers/HtmlEditorProviders/DNNConnect.CKE/js/Options.js")).Register();
        }

        /// <summary>
        /// Wires up the control: the provider's config folder, the toolbar sets and the
        /// reflection driven editor config settings UI.
        /// </summary>
        /// <remarks>
        /// This is <c>InitializeComponent</c> without the <c>Click</c>/<c>SelectedIndexChanged</c>
        /// handler attachments, which are the <c>@onclick</c>/<c>@onchange</c> bindings of the
        /// markup now.
        /// </remarks>
        private void InitializeComponent()
        {
            try
            {
                var providerConfiguration = ProviderConfiguration.GetProviderConfiguration(ProviderType);
                var objProvider = (Provider)providerConfiguration.Providers[providerConfiguration.DefaultProvider];

                if (!string.IsNullOrEmpty(objProvider?.Attributes["ck_configFolder"]))
                {
                    this.configFolder = objProvider.Attributes["ck_configFolder"];
                }

                this.listToolbars = ToolbarUtil.GetToolbars(this.HomeDirectory, this.configFolder);

                this.RenderEditorConfigSettings();
            }
            catch (Exception ex)
            {
                this.ShowNotification(ex.Message, "error");
            }
        }

        /// <summary>Reloaded the Settings of the Selected Mode.</summary>
        /// <param name="index">The index of the selected mode.</param>
        /// <remarks>Replaces <c>SetMode_SelectedIndexChanged</c>, which the auto-postback radio button list raised.</remarks>
        private void SetModeSelectedIndexChanged(int index)
        {
            this.setModeSelectedIndex = index;

            this.LoadSettings(index, false);
            this.BindUserGroupsGridView();
        }

        /// <summary>Shows the info notification.</summary>
        /// <param name="message">The message.</param>
        /// <param name="type">The type.</param>
        /// <remarks>
        /// Replaces <c>ScriptManager.RegisterStartupScript(..., "ShowNotificationBar(...)")</c>;
        /// the call is made from <see cref="OnAfterRenderAsync"/> through js/optionsInterop.js, so
        /// the message no longer needs to be JavaScript encoded into a script literal.
        /// </remarks>
        private void ShowNotification(string message, string type)
        {
            this.pendingNotifications.Add((message, type));
        }

        /// <summary>Renders the URL controls.</summary>
        /// <param name="reloadControls">if set to <see langword="true"/> [reload controls].</param>
        private void RenderUrlControls(bool reloadControls = false)
        {
            // Assign Url Controls on the Page the Correct Portal Id
            this.urlControlPortalId = this.portalSettings?.PortalId ?? Null.NullInteger;

            if (!reloadControls)
            {
                return;
            }

            foreach (var urlControl in new[]
            {
                this.templateUrlControl,
                this.configUrlControl,
                this.customJsFileControl,
                this.cssUrlControl,
                this.importFileControl,
            })
            {
                if (urlControl != null)
                {
                    urlControl.ReloadFiles = true;
                }
            }
        }

        /// <summary>Re-Formats Url from the Url Control.</summary>
        /// <param name="inputUrl">The input Url.</param>
        /// <returns>Returns the Formatted Url.</returns>
        private string ReFormatURL(string inputUrl)
        {
            if (inputUrl.StartsWith("http://", StringComparison.Ordinal) || inputUrl.StartsWith("FileID=", StringComparison.Ordinal))
            {
                return inputUrl;
            }

            return string.Format(
                "FileID={0}",
                Utility.ConvertFilePathToFileId(inputUrl, this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId));
        }

        /// <summary>Gets the portal settings.</summary>
        /// <returns>Returns the Current Portal Settings.</returns>
        private IPortalSettings GetPortalSettings()
        {
            try
            {
                if (this.IsHostMode && this.CurrentPortalOnly)
                {
                    return this.PortalSettings;
                }

                if (this.IsHostMode)
                {
                    return new DotNetNuke.Entities.Portals.PortalSettings(this.CurrentOrSelectedPortalId);
                }

                if (!this.IsHostMode)
                {
                    var tabIdString = this.GetQueryValue("tid");
                    this.CurrentOrSelectedTabId = !string.IsNullOrWhiteSpace(tabIdString)
                        ? int.Parse(tabIdString, CultureInfo.InvariantCulture)
                        : this.PortalSettings.ActiveTab.TabID;
                }

                if (!this.IsHostMode && this.GetQueryValue("PortalID") != null)
                {
                    this.CurrentOrSelectedPortalId = int.Parse(this.GetQueryValue("PortalID"), CultureInfo.InvariantCulture);
                }

                var domainName = Globals.GetDomainName(this.Request, true);

                var portalAlias = this.PortalAliasService.GetPortalAliasByPortal(this.CurrentOrSelectedPortalId, domainName);

                return DotNetNuke.Entities.Portals.PortalSettings.Create(
                    this.CurrentOrSelectedTabId, this.PortalAliasService.GetPortalAlias(portalAlias));
            }
            catch (Exception)
            {
                return DotNetNuke.Entities.Portals.PortalSettings.Current;
            }
        }

        /// <summary>Reads a value off the query string of the page's URL.</summary>
        /// <param name="name">The name of the query string parameter.</param>
        /// <returns>The value, or <see langword="null"/> when it was not supplied.</returns>
        /// <remarks>Replaces <c>HttpContext.Current.Request.QueryString[...]</c>.</remarks>
        private string GetQueryValue(string name)
        {
            var query = QueryHelpers.ParseQuery(new Uri(this.Navigation.Uri).Query);
            return query.TryGetValue(name, out var values) ? values.ToString() : null;
        }

        /// <summary>Reads an integer value off the query string of the page's URL.</summary>
        /// <param name="name">The name of the query string parameter.</param>
        /// <param name="defaultValue">The value returned when the parameter is absent or not a number.</param>
        /// <returns>The value.</returns>
        /// <remarks>Replaces <c>Request.QueryString.GetValueOrDefault(name, defaultValue)</c>.</remarks>
        private int GetQueryValueAsInt(string name, int defaultValue)
        {
            var value = this.GetQueryValue(name);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : defaultValue;
        }
    }
}
