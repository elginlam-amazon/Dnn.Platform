// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.Admin.Modules
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Modules.Definitions;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Security.Permissions.Controls;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.ModuleCache;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.UI.Skins;
    using DotNetNuke.UI.Skins.Controls;
    using DotNetNuke.UI.UserControls;
    using DotNetNuke.Web.UI.WebControls.Internal;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>The ModuleSettings page is used to edit the settings for a module.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>Modulesettings.ascx</c> + <c>Modulesettings.ascx.cs</c> +
    /// <c>Modulesettings.ascx.designer.cs</c> (type <c>ModuleSettingsPage</c>); the markup now lives
    /// in <c>ModuleSettings.razor</c>. The designer fields map as follows:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>specificSettingsTab</c>/<c>hlSpecificSettings</c>/<c>fsSpecific</c>/<c>pnlSpecific</c> become <see cref="SpecificSettingsTabVisible"/>, <see cref="SpecificSettingsTitle"/>, <see cref="SpecificSettingsVisible"/> and the <c>DynamicComponent</c>.</description></item>
    /// <item><description><c>ltModuleId</c>, <c>txtFriendlyName</c>, <c>txtTitle</c>, <c>txtHeader</c>, <c>txtFooter</c>, <c>txtMoniker</c>, <c>txtColor</c>, <c>txtBorder</c>, <c>txtCacheDuration</c>, <c>startDatePicker</c> and <c>endDatePicker</c> become <see cref="ModuleIdText"/>, <see cref="FriendlyName"/>, <see cref="Title"/>, <see cref="Header"/>, <see cref="Footer"/>, <see cref="Moniker"/>, <see cref="Color"/>, <see cref="Border"/>, <see cref="CacheDuration"/>, <see cref="StartDateText"/> and <see cref="EndDateText"/>.</description></item>
    /// <item><description>every <c>asp:CheckBox</c> becomes the matching <c>...Checked</c> property, and the <c>Enabled</c> the code behind set on four of them becomes the matching <c>...Enabled</c> property.</description></item>
    /// <item><description>every <c>runat="server"</c> row (<c>rowAllTabs</c>, <c>trnewPages</c>, <c>allowIndexRow</c>, <c>isShareableRow</c>, <c>permissionsRow</c>, <c>divCacheDuration</c>, <c>cacheWarningRow</c>, <c>rowTab</c>) becomes the matching <c>...Visible</c> property.</description></item>
    /// <item><description><c>cboAlign</c>/<c>cboVisibility</c> become <see cref="AlignValue"/>/<see cref="VisibilityValue"/> over <see cref="AlignOptions"/>/<see cref="VisibilityOptions"/>, which carry the original values and resource keys.</description></item>
    /// <item><description><c>dgOnTabs</c> becomes <see cref="InstalledOnTabs"/> plus the pager; <c>dgOnTabs_PageIndexChanging</c> becomes <see cref="OnTabsPageIndexChanging"/>.</description></item>
    /// <item><description><c>dgPermissions</c>, <c>termsSelector</c>, <c>ctlIcon</c>, <c>moduleContainerCombo</c>, <c>cboCacheProvider</c> and <c>cboTab</c> stay the very same migrated controls, referenced with <c>@@ref</c>.</description></item>
    /// <item><description><c>ctlAudit</c> becomes <see cref="AuditEntity"/>, bound to the <c>ModuleAuditControl</c>'s <c>Entity</c> parameter.</description></item>
    /// <item><description><c>valBorder</c>, <c>valCacheTime</c> and the <c>compareDate</c> <c>CustomValidator</c> become <see cref="Validate"/>, which is what <c>Page.IsValid</c> now means.</description></item>
    /// </list>
    /// <para>
    /// <c>ctlIcon</c> was declared as <c>&lt;dnn:url Src="~/controls/DnnUrlControl.ascx"&gt;</c>, whose
    /// type <see cref="DotNetNuke.Web.UI.WebControls.DnnUrlControl"/> is <see langword="abstract"/>
    /// after the migration (its concrete half is still <c>Website/controls/DnnUrlControl.ascx</c>,
    /// which belongs to another part of the migration). The already migrated
    /// <see cref="UrlControl"/> is used instead: it is the same URL picker with the identical
    /// parameter interface (<c>ShowImages</c>/<c>ShowUrls</c>/<c>ShowTabs</c>/<c>ShowLog</c>/
    /// <c>ShowTrack</c>/<c>Required</c>/<c>ShowNone</c>/<c>FileFilter</c>/<c>Url</c>), so the icon
    /// picker behaves as before.
    /// </para>
    /// <para>
    /// SECURITY - every guard of the original is preserved with identical semantics:
    /// <c>ModulePermissionController.HasModuleAccess(SecurityAccessLevel.Edit, "MANAGE", Module)</c>
    /// with its <c>IsSharedViewOnly() &amp;&amp; TabPermissionController.CanAddContentToPage()</c>
    /// exception and the <c>AccessDeniedURL</c> redirect; the update button's
    /// <c>HasModulePermission(..., "EDIT,MANAGE") || CanAddContentToPage()</c>; the permissions
    /// row's <c>CanAdminModule(Module) || CanAddContentToPage()</c>; the delete button's
    /// <c>CanDeleteModule(Module) || CanAddContentToPage()</c> combined with <c>HideDelete</c>;
    /// <c>PermissionProvider.Instance().IsPortalEditor()</c> for the all-pages row;
    /// <c>PortalSecurity.IsInRole(PortalSettings.AdministratorRoleName)</c> on save;
    /// <c>TabPermissionController.CanAdminPage()</c> for the four page-level checkboxes (both on
    /// load and again on save); and the two
    /// <c>AllowJsInModuleHeaders</c>/<c>AllowJsInModuleFooters</c> JavaScript injection checks,
    /// which still abort the save. The <c>moduleId</c> sentinel stays -1 and every
    /// <c>Null.NullInteger</c> / <c>Null.IsNull</c> test is untouched.
    /// </para>
    /// </remarks>
    public partial class ModuleSettings : PortalModuleBase
    {
        /// <summary>The element id of the delete action, which the interop script attaches dnnConfirm to.</summary>
        internal const string DeleteButtonId = "cmdDelete";

        /// <summary>The number of rows the module-installed-on grid showed per page (was <c>DnnGrid.PageSize</c>).</summary>
        private const int InstalledOnPageSize = 20;

        private readonly INavigationManager navigationManager;
        private readonly IPortalAliasService portalAliasService;
        private readonly IModuleControlPipeline moduleControlPipeline;
        private readonly IHostSettings hostSettings;
        private readonly IJavaScriptLibraryHelper javaScript;
        private readonly IApplicationStatusInfo appStatus;

        private int moduleId = -1;
        private ModuleInfo module;

        // The settings control the pipeline produced. Blazor's renderer creates and owns the
        // instance it renders, so the pipeline's instance supplies the type and the parameter values
        // (DnnComponentParameters.Capture, the same seam ModuleHost uses) and the rendered instance
        // is reached through the DynamicComponent reference.
        private Microsoft.AspNetCore.Components.IComponent pipelineSettingsControl;
        private Type specificSettingsType;
        private IDictionary<string, object> specificSettingsParameters;
        private DynamicComponent specificSettings;

        private ModulePermissionsGrid dgPermissions;
        private TermsSelector termsSelector;
        private UrlControl ctlIcon;
        private DnnSkinComboBox moduleContainerCombo;
        private DnnComboBox cboCacheProvider;
        private DnnComboBox cboTab;

        private IJSObjectReference interopModule;

        private string cancelUrl = string.Empty;
        private bool initialBindDone;
        private bool borderError;
        private bool cacheTimeError;
        private bool endDateError;
        private int installedOnPageIndex;
        private List<TabInfo> installedOnTabs = new List<TabInfo>();

        private bool hideDeleteButton;
        private bool hideCancelButton;
        private bool doNotRedirectOnUpdate;

        /// <summary>Initializes a new instance of the <see cref="ModuleSettings"/> class.</summary>
        /// <remarks>A Blazor component cannot take constructor parameters; the six services are
        /// resolved from the module's dependency provider, which is what the parameterless overload
        /// of the WebForms control did.</remarks>
        public ModuleSettings()
        {
            this.navigationManager = this.DependencyProvider.GetRequiredService<INavigationManager>();
            this.portalAliasService = this.DependencyProvider.GetRequiredService<IPortalAliasService>();
            this.moduleControlPipeline = this.DependencyProvider.GetRequiredService<IModuleControlPipeline>();
            this.hostSettings = this.DependencyProvider.GetRequiredService<IHostSettings>();
            this.javaScript = this.DependencyProvider.GetRequiredService<IJavaScriptLibraryHelper>();
            this.appStatus = this.DependencyProvider.GetRequiredService<IApplicationStatusInfo>();
        }

        /// <summary>Gets the alignment options of <c>cboAlign</c> (resource key, value).</summary>
        protected static IReadOnlyList<KeyValuePair<string, string>> AlignOptions { get; } = new[]
        {
            new KeyValuePair<string, string>("Left", "left"),
            new KeyValuePair<string, string>("Center", "center"),
            new KeyValuePair<string, string>("Right", "right"),
            new KeyValuePair<string, string>("Not_Specified", string.Empty),
        };

        /// <summary>Gets the visibility options of <c>cboVisibility</c> (resource key, value).</summary>
        protected static IReadOnlyList<KeyValuePair<string, string>> VisibilityOptions { get; } = new[]
        {
            new KeyValuePair<string, string>("Maximized", "0"),
            new KeyValuePair<string, string>("Minimized", "1"),
            new KeyValuePair<string, string>("None", "2"),
        };

        /// <summary>Gets or sets the module id text (was <c>ltModuleId.Text</c>).</summary>
        protected string ModuleIdText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the icon url pushed into <c>ctlIcon</c> through its <c>Url</c> parameter
        /// (was the <c>ctlIcon.Url = Module.IconFile</c> assignment).
        /// </summary>
        /// <remarks>
        /// Only the value handed <i>to</i> the picker. The user's current selection is still read
        /// back from the picker itself (<c>ctlIcon?.Url</c>) when the settings are saved.
        /// </remarks>
        protected string IconUrl { get; set; }

        /// <summary>Gets or sets the module's culture (was <c>cultureLanguageLabel.Language</c>).</summary>
        protected string CultureCode { get; set; } = string.Empty;

        /// <summary>Gets or sets the module definition's friendly name (was <c>txtFriendlyName.Text</c>).</summary>
        protected string FriendlyName { get; set; } = string.Empty;

        /// <summary>Gets or sets the module title (was <c>txtTitle.Text</c>).</summary>
        protected string Title { get; set; } = string.Empty;

        /// <summary>Gets or sets the module header (was <c>txtHeader.Text</c>).</summary>
        protected string Header { get; set; } = string.Empty;

        /// <summary>Gets or sets the module footer (was <c>txtFooter.Text</c>).</summary>
        protected string Footer { get; set; } = string.Empty;

        /// <summary>Gets or sets the moniker (was <c>txtMoniker.Text</c>).</summary>
        protected string Moniker { get; set; } = string.Empty;

        /// <summary>Gets or sets the start date (was <c>startDatePicker.Text</c>, format yyyy-MM-dd).</summary>
        protected string StartDateText { get; set; } = string.Empty;

        /// <summary>Gets or sets the end date (was <c>endDatePicker.Text</c>, format yyyy-MM-dd).</summary>
        protected string EndDateText { get; set; } = string.Empty;

        /// <summary>Gets or sets the module colour (was <c>txtColor.Text</c>).</summary>
        protected string Color { get; set; } = string.Empty;

        /// <summary>Gets or sets the module border (was <c>txtBorder.Text</c>).</summary>
        protected string Border { get; set; } = string.Empty;

        /// <summary>Gets or sets the cache duration (was <c>txtCacheDuration.Text</c>).</summary>
        protected string CacheDuration { get; set; } = string.Empty;

        /// <summary>Gets or sets the alignment (was <c>cboAlign.SelectedItem.Value</c>).</summary>
        protected string AlignValue { get; set; } = string.Empty;

        /// <summary>Gets or sets the visibility (was <c>cboVisibility.SelectedItem.Value</c>).</summary>
        protected string VisibilityValue { get; set; } = "0";

        /// <summary>Gets or sets a value indicating whether the module is on all pages (was <c>chkAllTabs.Checked</c>).</summary>
        protected bool AllTabsChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether <c>chkAllTabs</c> is enabled.</summary>
        protected bool AllTabsEnabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the module is added to new pages (was <c>chkNewTabs.Checked</c>).</summary>
        protected bool NewTabsChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether <c>chkNewTabs</c> is enabled.</summary>
        protected bool NewTabsEnabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the module may be indexed (was <c>chkAllowIndex.Checked</c>).</summary>
        protected bool AllowIndexChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether <c>chkAllowIndex</c> is enabled.</summary>
        protected bool AllowIndexEnabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the module is shareable (was <c>isShareableCheckBox.Checked</c>).</summary>
        protected bool IsShareableChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether the module is shareable view only (was <c>isShareableViewOnlyCheckBox.Checked</c>).</summary>
        protected bool IsShareableViewOnlyChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether the admin border is hidden (was <c>chkAdminBorder.Checked</c>).</summary>
        protected bool AdminBorderChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether the title is displayed (was <c>chkDisplayTitle.Checked</c>).</summary>
        protected bool DisplayTitleChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether the print action is displayed (was <c>chkDisplayPrint.Checked</c>).</summary>
        protected bool DisplayPrintChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether the syndicate action is displayed (was <c>chkDisplaySyndicate.Checked</c>).</summary>
        protected bool DisplaySyndicateChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether this is the portal's default module (was <c>chkDefault.Checked</c>).</summary>
        protected bool DefaultChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether <c>chkDefault</c> is enabled.</summary>
        protected bool DefaultEnabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the settings apply to all modules (was <c>chkAllModules.Checked</c>).</summary>
        protected bool AllModulesChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether <c>chkAllModules</c> is enabled.</summary>
        protected bool AllModulesEnabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether view permissions are inherited (was <c>chkInheritPermissions.Checked</c>).</summary>
        protected bool InheritPermissionsChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether <c>chkInheritPermissions</c> is rendered (it declared <c>Visible="false"</c>).</summary>
        protected bool InheritPermissionsVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the all-pages row is rendered (was <c>rowAllTabs.Visible</c>).</summary>
        protected bool RowAllTabsVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the new-pages row is rendered (was <c>trnewPages.Visible</c>).</summary>
        protected bool NewPagesRowVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the allow-index row is rendered (was <c>allowIndexRow.Visible</c>).</summary>
        protected bool AllowIndexRowVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the shareable row is rendered (was <c>isShareableRow.Visible</c>, which declared <c>Visible="False"</c>).</summary>
        protected bool IsShareableRowVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the permissions row is rendered (was <c>permissionsRow.Visible</c>).</summary>
        protected bool PermissionsRowVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the cache duration row is rendered (was <c>divCacheDuration.Visible</c>, which declared <c>visible="false"</c>).</summary>
        protected bool CacheDurationVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the cache warning is rendered (was <c>cacheWarningRow.Visible</c>).</summary>
        protected bool CacheWarningVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the inherited-cache label is rendered (was <c>lblCacheInherited.Visible</c>).</summary>
        protected bool CacheInheritedVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the move-to-page row is rendered (was <c>rowTab.Visible</c>).</summary>
        protected bool RowTabVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether <c>cboTab</c> is enabled.</summary>
        protected bool TabEnabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the specific-settings tab is rendered (was <c>specificSettingsTab.Visible</c>).</summary>
        protected bool SpecificSettingsTabVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the specific-settings fieldset is rendered (was <c>fsSpecific.Visible</c>).</summary>
        protected bool SpecificSettingsVisible { get; set; }

        /// <summary>Gets or sets the title of the specific-settings tab (was <c>hlSpecificSettings.Text</c>).</summary>
        protected string SpecificSettingsTitle { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the update action is rendered (was <c>cmdUpdate.Visible</c>).</summary>
        protected bool CmdUpdateVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the delete action is rendered (was <c>cmdDelete.Visible</c>).</summary>
        protected bool CmdDeleteVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the cancel action is rendered (was <c>cancelHyperLink.Visible</c>).</summary>
        protected bool CancelVisible { get; set; } = true;

        /// <summary>Gets or sets the entity whose audit information is displayed (was <c>ctlAudit.Entity</c>).</summary>
        protected BaseEntityInfo AuditEntity { get; set; }

        /// <summary>Gets the pages this module is installed on (was the <c>dgOnTabs</c> data source).</summary>
        protected IReadOnlyList<TabInfo> InstalledOnTabs => this.installedOnTabs;

        /// <summary>Gets the number of pages of the installed-on grid.</summary>
        protected int InstalledOnPageCount =>
            (this.installedOnTabs.Count + InstalledOnPageSize - 1) / InstalledOnPageSize;

        /// <summary>Gets the current page of the installed-on grid.</summary>
        protected IEnumerable<TabInfo> CurrentInstalledOnPage =>
            this.installedOnTabs.Skip(this.installedOnPageIndex * InstalledOnPageSize).Take(InstalledOnPageSize);

        /// <summary>Gets or sets the navigation manager used for the redirects <c>Response.Redirect</c> issued.</summary>
        [Inject]
        protected Microsoft.AspNetCore.Components.NavigationManager Navigation { get; set; }

        /// <summary>Gets or sets the JavaScript runtime used to wire the theme's jQuery plugins.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        private ModuleInfo Module => this.module ??= ModuleController.Instance.GetModule(this.moduleId, this.TabId, false);

        private ISettingsControl SettingsControl => this.specificSettings?.Instance as ISettingsControl;

        private string ReturnURL => UrlUtils.ValidReturnUrl(this.Request?.GetValue("ReturnURL")) ?? this.navigationManager.NavigateURL();

        /// <summary>Gets the breadcrumb link of a page the module is installed on.</summary>
        /// <param name="dataItem">The page.</param>
        /// <returns>The HTML of the breadcrumb.</returns>
        protected string GetInstalledOnLink(object dataItem)
        {
            var returnValue = new StringBuilder();
            if (dataItem is TabInfo tab)
            {
                var index = 0;
                TabController.Instance.PopulateBreadCrumbs(ref tab);
                var defaultAlias = this.portalAliasService.GetPortalAliasesByPortalId(tab.IsSuperTab ? this.hostSettings.HostPortalId : tab.PortalID)
                                        .OrderByDescending(a => a.IsPrimary)
                                        .FirstOrDefault();
                var portalSettings = new PortalSettings(tab.PortalID)
                {
                    PortalAlias = defaultAlias as PortalAliasInfo,
                };

                var tabUrl = this.navigationManager.NavigateURL(tab.TabID, portalSettings, string.Empty);

                foreach (TabInfo t in tab.BreadCrumbs)
                {
                    if (index > 0)
                    {
                        returnValue.Append(" > ");
                    }

                    if (tab.BreadCrumbs.Count - 1 == index)
                    {
                        returnValue.AppendFormat("<a href=\"{0}\">{1}</a>", tabUrl, t.LocalizedTabName);
                    }
                    else
                    {
                        returnValue.Append(t.LocalizedTabName);
                    }

                    index = index + 1;
                }
            }

            return returnValue.ToString();
        }

        /// <summary>Gets the name of the site a page the module is installed on belongs to.</summary>
        /// <param name="dataItem">The page.</param>
        /// <returns>The portal name.</returns>
        protected string GetInstalledOnSite(object dataItem)
        {
            string returnValue = string.Empty;
            if (dataItem is TabInfo tab)
            {
                var portal = PortalController.Instance.GetPortal(tab.PortalID);
                if (portal != null)
                {
                    returnValue = portal.PortalName;
                }
            }

            return returnValue;
        }

        /// <summary>Gets a value indicating whether this is a shared module the user may only view.</summary>
        /// <returns><see langword="true"/> when the module is shared and shareable view only.</returns>
        protected bool IsSharedViewOnly()
        {
            return this.ModuleContext.Configuration.IsShared && this.ModuleContext.Configuration.IsShareableViewOnly;
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            // The .resx of this control is App_LocalResources/ModuleSettings.ascx.resx; the
            // component rename must not change which resource file is read.
            this.LocalResourceFile = "~/admin/Modules/" + Localization.LocalResourceDirectory + "/ModuleSettings.ascx";

            try
            {
                // The three query-string switches, read while the request is still in flight.
                this.hideDeleteButton = this.GetQueryValue("HideDelete") == "true";
                this.hideCancelButton = this.GetQueryValue("HideCancel") == "true";
                this.doNotRedirectOnUpdate = this.GetQueryValue("NoRedirectOnUpdate") == "true";

                this.javaScript.RequestRegistration(CommonJs.DnnPlugins);

                // get ModuleId
                var moduleIdValue = this.GetQueryValue("ModuleId");
                if (moduleIdValue != null)
                {
                    this.moduleId = int.Parse(moduleIdValue);
                }

                if (this.Module.ContentItemId == Null.NullInteger && this.Module.ModuleID != Null.NullInteger)
                {
                    // This tab does not have a valid ContentItem
                    ModuleController.Instance.CreateContentItem(this.Module);

                    ModuleController.Instance.UpdateModule(this.Module);
                }

                // Verify that the current user has access to edit this module
                if (!ModulePermissionController.HasModuleAccess(SecurityAccessLevel.Edit, "MANAGE", this.Module))
                {
                    if (!(this.IsSharedViewOnly() && TabPermissionController.CanAddContentToPage()))
                    {
                        this.Navigation.NavigateTo(Globals.AccessDeniedURL(), forceLoad: true);
                    }
                }

                if (this.Module != null)
                {
                    // get module
                    this.TabModuleId = this.Module.TabModuleID;

                    // get Settings Control
                    ModuleControlInfo moduleControlInfo = ModuleControlController.GetModuleControlByControlKey(this.hostSettings, "Settings", this.Module.ModuleDefID);

                    if (moduleControlInfo != null)
                    {
                        this.pipelineSettingsControl = this.moduleControlPipeline.LoadSettingsControl(this, this.Module, moduleControlInfo.ControlSrc);

                        if (this.pipelineSettingsControl is ISettingsControl settingsControl)
                        {
                            this.SpecificSettingsTitle = Localization.GetString(
                                "ControlTitle_settings",
                                settingsControl.LocalResourceFile);
                            if (string.IsNullOrEmpty(this.SpecificSettingsTitle))
                            {
                                this.SpecificSettingsTitle =
                                    string.Format(
                                        Localization.GetString("ControlTitle_settings", this.LocalResourceFile),
                                        this.Module.DesktopModule.FriendlyName);
                            }

                            // Was pnlSpecific.Controls.Add(control): the renderer owns the instance
                            // it renders, so it is given the type and the configured parameters.
                            this.specificSettingsType = this.pipelineSettingsControl.GetType();
                            this.specificSettingsParameters = DotNetNuke.UI.DnnComponentParameters.Capture(this.pipelineSettingsControl);
                        }
                    }
                }
            }
            catch (NavigationException)
            {
                throw;
            }
            catch (Exception err)
            {
                Exceptions.ProcessModuleLoadException(this, err);
            }
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                this.cancelUrl = this.ReturnURL;

                if (this.moduleId != -1)
                {
                    this.AuditEntity = this.Module;
                }

                // Was the Page.IsPostBack == false guard: everything below runs once per component
                // instance, which is the same "first time this form is shown" moment. The child
                // controls it also configured are handled in OnPreRender, because a child component
                // only exists once this component has rendered.
                if (!this.initialBindDone)
                {
                    // only Portal Administrators can manage the visibility on all Tabs
                    var isAdmin = PermissionProvider.Instance().IsPortalEditor();
                    this.RowAllTabsVisible = isAdmin;
                    this.AllModulesEnabled = isAdmin;

                    if (this.hideCancelButton)
                    {
                        this.CancelVisible = false;
                    }

                    // tab administrators can only manage their own tab
                    if (!TabPermissionController.CanAdminPage())
                    {
                        this.NewTabsEnabled = false;
                        this.DefaultEnabled = false;
                        this.AllowIndexEnabled = false;
                        this.TabEnabled = false;
                    }

                    if (this.moduleId == -1)
                    {
                        this.IsShareableChecked = true;
                        this.IsShareableViewOnlyChecked = true;
                        this.IsShareableRowVisible = true;

                        this.VisibilityValue = "0"; // maximized
                        this.AllTabsChecked = false;
                        this.CmdDeleteVisible = false;
                    }

                    if (this.Module != null)
                    {
                        this.CmdUpdateVisible = ModulePermissionController.HasModulePermission(this.Module.ModulePermissions, "EDIT,MANAGE") || TabPermissionController.CanAddContentToPage();
                        this.PermissionsRowVisible = ModulePermissionController.CanAdminModule(this.Module) || TabPermissionController.CanAddContentToPage();
                    }

                    // Set visibility of Specific Settings
                    if (this.specificSettingsType != null)
                    {
                        this.SpecificSettingsTabVisible = true;
                        this.SpecificSettingsVisible = true;
                    }
                    else
                    {
                        this.SpecificSettingsTabVisible = false;
                        this.SpecificSettingsVisible = false;
                    }
                }

                if (this.Module != null)
                {
                    this.CultureCode = this.Module.CultureCode;
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// The rest of the WebForms <c>!IsPostBack</c> body: everything that configured a child
        /// control. Child components only exist once this component has rendered, which is this
        /// phase (the WebForms <c>PreRender</c> equivalent).
        /// </remarks>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (this.initialBindDone)
            {
                return;
            }

            this.initialBindDone = true;

            try
            {
                if (this.dgPermissions != null)
                {
                    this.dgPermissions.TabId = this.PortalSettings.ActiveTab.TabID;
                    this.dgPermissions.ModuleID = this.moduleId;
                }

                this.BindModulePages();

                if (this.cboTab != null)
                {
                    this.cboTab.DataSource = TabController.GetPortalTabs(this.hostSettings, this.appStatus, this.PortalId, -1, false, Null.NullString, true, false, true, false, true);
                    this.cboTab.DataBind();

                    // if tab is a  host tab, then add current tab
                    if (Globals.IsHostTab(this.PortalSettings.ActiveTab.TabID))
                    {
                        this.cboTab.InsertItem(0, this.PortalSettings.ActiveTab.LocalizedTabName, this.PortalSettings.ActiveTab.TabID.ToString());
                    }

                    if (this.Module != null)
                    {
                        if (this.cboTab.FindItemByValue(this.Module.TabID.ToString()) == null)
                        {
                            var objTab = TabController.Instance.GetTab(this.Module.TabID, this.Module.PortalID, false);
                            this.cboTab.AddItem(objTab.LocalizedTabName, objTab.TabID.ToString());
                        }
                    }
                }

                if (this.moduleId != -1)
                {
                    this.BindData();
                    this.CmdDeleteVisible = (ModulePermissionController.CanDeleteModule(this.Module) ||
                         TabPermissionController.CanAddContentToPage()) && !this.hideDeleteButton;
                }

                // Get the module settings from the PortalSettings and pass the
                // two settings hashtables to the sub control to process. The async variant is
                // awaited by OnAfterRenderAsync, which replaces Page.RegisterAsyncTask.
                if (this.SettingsControl != null && this.SettingsControl is not IAsyncSettingsControl)
                {
                    this.SettingsControl.LoadSettings();
                }

                if (this.termsSelector != null)
                {
                    this.termsSelector.IncludeTags = false;

                    if (this.Module != null)
                    {
                        this.termsSelector.PortalId = this.Module.PortalID;
                        this.termsSelector.Terms = this.Module.Terms;
                    }

                    this.termsSelector.DataBind();
                }

                this.StateHasChanged();
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Replaces the two <c>Page.RegisterAsyncTask</c> calls for an
        /// <see cref="IAsyncSettingsControl"/> (the load half; the update half is awaited by
        /// <see cref="OnUpdateClick"/>) and imports the theme-plugin interop module, which replaces
        /// the inline <c>&lt;script&gt;</c> block of the <c>.ascx</c>.
        /// </remarks>
        /// <param name="firstRender">Whether this is the first render.</param>
        /// <returns>A task.</returns>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (!firstRender)
            {
                return;
            }

            if (this.SettingsControl is IAsyncSettingsControl asyncSettingsControl)
            {
                try
                {
                    await asyncSettingsControl.LoadSettingsAsync(default);
                }
                catch (Exception exc)
                {
                    Exceptions.ProcessModuleLoadException(this, exc);
                }
            }

            try
            {
                this.interopModule = await this.JsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", Globals.ResolveUrl("~/admin/Modules/moduleSettingsInterop.js"));

                await this.interopModule.InvokeVoidAsync(
                    "setUpDnnModuleSettings",
                    DeleteButtonId,
                    this.moduleContainerCombo?.ID ?? string.Empty,
                    this.ModuleId,
                    this.navigationManager.NavigateURL(this.TabId),
                    new
                    {
                        expandAll = Localization.GetString("ExpandAll", Localization.SharedResourceFile),
                        collapseAll = Localization.GetString("CollapseAll", Localization.SharedResourceFile),
                        deleteItem = Localization.GetString("DeleteItem.Text", Localization.SharedResourceFile),
                        yes = Localization.GetString("Yes.Text", Localization.SharedResourceFile),
                        no = Localization.GetString("No.Text", Localization.SharedResourceFile),
                        confirm = Localization.GetString("Confirm.Text", Localization.SharedResourceFile),
                        previewNoSelection = Localization.GetString("PreviewNoSelectionMessage.Text"),
                        close = Localization.GetString("Close.Text", Localization.SharedResourceFile),
                        ok = Localization.GetString("Ok.Text", Localization.SharedResourceFile),
                    });
            }
            catch (JSException exc)
            {
                // The theme's jQuery plugins are progressive enhancement; a missing plugin must not
                // take the settings form down.
                Exceptions.LogException(exc);
            }
        }

        /// <summary>Shows or hides the new-pages row (was <c>chkAllTabs.CheckedChanged</c>).</summary>
        /// <param name="checked">The new state of the checkbox.</param>
        protected void OnAllTabsCheckChanged(bool @checked)
        {
            this.AllTabsChecked = @checked;
            this.NewPagesRowVisible = this.AllTabsChecked;
        }

        /// <summary>Shows or hides the cache duration row (was <c>cboCacheProvider.TextChanged</c>).</summary>
        /// <param name="e">The event arguments.</param>
        protected void OnCacheProviderIndexChanged(EventArgs e)
        {
            this.ShowCacheRows();
        }

        /// <summary>Deletes this module from the page (was <c>cmdDelete.Click</c>).</summary>
        protected void OnDeleteClick()
        {
            try
            {
                ModuleController.Instance.DeleteTabModule(this.TabId, this.moduleId, true);
                this.Navigation.NavigateTo(this.ReturnURL, forceLoad: true);
            }
            catch (NavigationException)
            {
                throw;
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Applies the inherit-permissions choice to the grid (was <c>chkInheritPermissions.CheckedChanged</c>).</summary>
        /// <param name="checked">The new state of the checkbox.</param>
        protected void OnInheritPermissionsChanged(bool @checked)
        {
            this.InheritPermissionsChecked = @checked;
            if (this.dgPermissions != null)
            {
                this.dgPermissions.InheritViewPermissionsFromTab = this.InheritPermissionsChecked;
            }
        }

        /// <summary>Pages the module-installed-on grid (was <c>dgOnTabs_PageIndexChanging</c>).</summary>
        /// <param name="newPageIndex">The new page index.</param>
        protected void OnTabsPageIndexChanging(int newPageIndex)
        {
            this.installedOnPageIndex = newPageIndex;
            this.BindModulePages();
        }

        /// <summary>Saves the module settings (was <c>cmdUpdate.Click</c>).</summary>
        /// <returns>A task.</returns>
        protected async Task OnUpdateClick()
        {
            try
            {
                // Was Page.IsValid: the two CompareValidators and the date CustomValidator.
                if (!this.Validate())
                {
                    return;
                }

                var allTabsChanged = false;

                // only Portal Administrators can manage the visibility on all Tabs
                var isAdmin = PortalSecurity.IsInRole(this.PortalSettings.AdministratorRoleName);
                this.AllModulesEnabled = isAdmin;

                // tab administrators can only manage their own tab
                if (!TabPermissionController.CanAdminPage())
                {
                    this.AllTabsEnabled = false;
                    this.NewTabsEnabled = false;
                    this.DefaultEnabled = false;
                    this.AllowIndexEnabled = false;
                    this.TabEnabled = false;
                }

                this.Module.ModuleID = this.moduleId;
                this.Module.ModuleTitle = this.Title;
                this.Module.Alignment = this.AlignValue;
                this.Module.Color = this.Color;
                this.Module.Border = this.Border;
                this.Module.IconFile = this.ctlIcon?.Url;
                this.Module.CacheTime = !string.IsNullOrEmpty(this.CacheDuration)
                                        ? int.Parse(this.CacheDuration)
                                        : 0;
                this.Module.CacheMethod = this.cboCacheProvider?.SelectedValue;
                this.Module.TabID = this.TabId;
                if (this.Module.AllTabs != this.AllTabsChecked)
                {
                    allTabsChanged = true;
                }

                this.Module.AllTabs = this.AllTabsChecked;

                // collect these first as any settings update will clear the cache
                var originalChecked = this.Settings["hideadminborder"] != null && bool.Parse(this.Settings["hideadminborder"].ToString());
                var allowIndex = this.GetBooleanSetting("AllowIndex", true);
                var oldMoniker = ((string)this.Settings["Moniker"] ?? string.Empty).TrimToLength(100);
                var newMoniker = this.Moniker.TrimToLength(100);
                if (!oldMoniker.Equals(this.Moniker))
                {
                    var ids = TabModulesController.Instance.GetTabModuleIdsBySetting("Moniker", newMoniker);
                    if (ids != null && ids.Count > 0)
                    {
                        // Warn user - duplicate moniker value
                        Skin.AddModuleMessage(this, Localization.GetString("MonikerExists", this.LocalResourceFile), ModuleMessage.ModuleMessageType.RedError);
                        return;
                    }

                    ModuleController.Instance.UpdateTabModuleSetting(this.Module.TabModuleID, "Moniker", newMoniker);
                }

                if (originalChecked != this.AdminBorderChecked)
                {
                    ModuleController.Instance.UpdateTabModuleSetting(this.Module.TabModuleID, "hideadminborder", this.AdminBorderChecked.ToString());
                }

                // check whether allow index value is changed
                if (allowIndex != this.AllowIndexChecked)
                {
                    ModuleController.Instance.UpdateTabModuleSetting(this.Module.TabModuleID, "AllowIndex", this.AllowIndexChecked.ToString());
                }

                switch (int.Parse(this.VisibilityValue))
                {
                    case 0:
                        this.Module.Visibility = VisibilityState.Maximized;
                        break;
                    case 1:
                        this.Module.Visibility = VisibilityState.Minimized;
                        break;

                    // case 2:
                    default:
                        this.Module.Visibility = VisibilityState.None;
                        break;
                }

                this.Module.IsDeleted = false;

                // If JavaScript is not allowed in module header but changes contain JavaScript, avoid saving the changes
                if (!this.PortalSettings.AllowJsInModuleHeaders && HtmlUtils.ContainsJavaScript(this.Header))
                {
                    string message = Localization.GetString("JavaScriptInHeader", this.LocalResourceFile);
                    Skin.AddModuleMessage(this, message, ModuleMessage.ModuleMessageType.RedError);
                    return;
                }

                // If JavaScript is not allowed in module footer but changes contain JavaScript, avoid saving the changes
                if (!this.PortalSettings.AllowJsInModuleFooters && HtmlUtils.ContainsJavaScript(this.Footer))
                {
                    string message = Localization.GetString("JavaScriptInFooter", this.LocalResourceFile);
                    Skin.AddModuleMessage(this, message, ModuleMessage.ModuleMessageType.RedError);
                    return;
                }

                this.Module.Header = this.Header;
                this.Module.Footer = this.Footer;

                this.Module.StartDate = DateTime.TryParseExact(this.StartDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var startDate) ? startDate : Null.NullDate;
                this.Module.EndDate = DateTime.TryParseExact(this.EndDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var endDate) ? endDate : Null.NullDate;

                this.Module.ContainerSrc = this.moduleContainerCombo?.SelectedValue;
                this.Module.ModulePermissions.Clear();
                if (this.dgPermissions != null)
                {
                    this.Module.ModulePermissions.AddRange(this.dgPermissions.Permissions);
                }

                this.Module.Terms.Clear();
                if (this.termsSelector != null)
                {
                    this.Module.Terms.AddRange(this.termsSelector.Terms);
                }

                if (!this.Module.IsShared)
                {
                    this.Module.InheritViewPermissions = this.InheritPermissionsChecked;
                    this.Module.IsShareable = this.IsShareableChecked;
                    this.Module.IsShareableViewOnly = this.IsShareableViewOnlyChecked;
                }

                this.Module.DisplayTitle = this.DisplayTitleChecked;
                this.Module.DisplayPrint = this.DisplayPrintChecked;
                this.Module.DisplaySyndicate = this.DisplaySyndicateChecked;
                this.Module.IsDefaultModule = this.DefaultChecked;
                this.Module.AllModules = this.AllModulesChecked;
                ModuleController.Instance.UpdateModule(this.Module);

                // Update Custom Settings
                if (this.SettingsControl != null)
                {
                    try
                    {
                        if (this.SettingsControl is IAsyncSettingsControl asyncSettingsControl)
                        {
                            await asyncSettingsControl.UpdateSettingsAsync(default);
                        }
                        else
                        {
                            this.SettingsControl.UpdateSettings();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Was: a ThreadAbortException (raised by Response.Redirect inside a
                        // settings control) was logged and Thread.ResetAbort() called; neither
                        // exists on ASP.NET Core, so every failure now takes the one remaining
                        // path the WebForms code had - log it and carry on to the continuation.
                        Exceptions.LogException(ex);
                    }
                }

                // These Module Copy/Move statements must be
                // at the end of the Update as the Controller code assumes all the
                // Updates to the Module have been carried out.

                // Check if the Module is to be Moved to a new Tab
                if (!this.AllTabsChecked)
                {
                    var newTabId = int.Parse(this.cboTab.SelectedValue);
                    if (this.TabId != newTabId)
                    {
                        // First check if there already is an instance of the module on the target page
                        var tmpModule = ModuleController.Instance.GetModule(this.moduleId, newTabId, false);
                        if (tmpModule == null)
                        {
                            // Move module
                            ModuleController.Instance.MoveModule(this.moduleId, this.TabId, newTabId, Globals.glbDefaultPane);
                        }
                        else
                        {
                            // Warn user
                            Skin.AddModuleMessage(this, Localization.GetString("ModuleExists", this.LocalResourceFile), ModuleMessage.ModuleMessageType.RedError);
                            return;
                        }
                    }
                }

                // Check if Module is to be Added/Removed from all Tabs
                if (allTabsChanged)
                {
                    var listTabs = TabController.GetPortalTabs(this.hostSettings, this.appStatus, this.PortalSettings.PortalId, Null.NullInteger, false, true);
                    if (this.AllTabsChecked)
                    {
                        if (!this.NewTabsChecked)
                        {
                            foreach (var destinationTab in listTabs)
                            {
                                var destinationModule = ModuleController.Instance.GetModule(this.moduleId, destinationTab.TabID, false);
                                if (destinationModule != null)
                                {
                                    if (destinationModule.IsDeleted)
                                    {
                                        ModuleController.Instance.RestoreModule(destinationModule);
                                    }
                                }
                                else
                                {
                                    if (!this.PortalSettings.ContentLocalizationEnabled || (this.Module.CultureCode == destinationTab.CultureCode))
                                    {
                                        ModuleController.Instance.CopyModule(this.Module, destinationTab, this.Module.PaneName, true);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        ModuleController.Instance.DeleteAllModules(this.moduleId, this.TabId, listTabs, true, false, false);
                    }
                }

                if (!this.doNotRedirectOnUpdate)
                {
                    // Navigate back to admin page
                    this.Navigation.NavigateTo(this.ReturnURL, forceLoad: true);
                }
            }
            catch (NavigationException)
            {
                throw;
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        private static IEnumerable GetFilteredProviders<T>(Dictionary<string, T> providerList, string keyFilter)
        {
            var providers = from provider in providerList let filteredkey = provider.Key.Replace(keyFilter, string.Empty) select new { filteredkey, provider.Key };

            return providers;
        }

        /// <summary>
        /// Evaluates the three validators of the form (was <c>Page.IsValid</c>).
        /// </summary>
        /// <returns><see langword="true"/> when the form is valid.</returns>
        /// <remarks>
        /// <para>
        /// <c>valBorder</c> and <c>valCacheTime</c> were <c>CompareValidator</c>s with
        /// <c>Operator="DataTypeCheck" Type="Integer"</c>. A WebForms validator never fails on an
        /// empty value, so an empty box stays valid here too.
        /// </para>
        /// <para>
        /// The end-date validator was a <c>CustomValidator</c> on <c>endDatePicker</c> whose client
        /// function was <c>arg.IsValid = compareTime == null || time &gt; compareTime</c> against
        /// <c>startDatePicker</c>. A <c>CustomValidator</c> does not run on an empty value either
        /// (<c>ValidateEmptyText</c> defaults to false), so an empty end date is valid; otherwise
        /// the end date must be strictly greater than the start date, which the <c>yyyy-MM-dd</c>
        /// format makes an ordinary ordinal comparison, exactly as the script did.
        /// </para>
        /// </remarks>
        private bool Validate()
        {
            this.borderError = !string.IsNullOrEmpty(this.Border) && !int.TryParse(this.Border, out _);
            this.cacheTimeError = !string.IsNullOrEmpty(this.CacheDuration) && !int.TryParse(this.CacheDuration, out _);
            this.endDateError = !string.IsNullOrEmpty(this.EndDateText) &&
                string.CompareOrdinal(this.EndDateText, this.StartDateText ?? string.Empty) <= 0;

            return !this.borderError && !this.cacheTimeError && !this.endDateError;
        }

        private void BindData()
        {
            if (this.Module != null)
            {
                var desktopModule = DesktopModuleController.GetDesktopModule(this.hostSettings, this.Module.DesktopModuleID, this.PortalId);
                if (this.dgPermissions != null)
                {
                    this.dgPermissions.ResourceFile =
                        $"{Globals.ApplicationPath}/DesktopModules/{desktopModule.FolderName}/{Localization.LocalResourceDirectory}/{Localization.LocalSharedResourceFile}";
                }

                if (!this.Module.IsShared)
                {
                    this.InheritPermissionsChecked = this.Module.InheritViewPermissions;
                    if (this.dgPermissions != null)
                    {
                        this.dgPermissions.InheritViewPermissionsFromTab = this.Module.InheritViewPermissions;
                    }
                }

                this.ModuleIdText = this.module.ModuleID.ToString();
                this.FriendlyName = this.Module.DesktopModule.FriendlyName;
                this.Title = this.Module.ModuleTitle;

                // ctlIcon.Url is supplied through the Url parameter declared in the markup (it reads
                // this.IconUrl), so the value flows through SetParametersAsync and the icon picker
                // re-renders with it. Assigning it on the @ref'd instance here would not (BL0005).
                this.IconUrl = this.Module.IconFile;

                if (this.cboTab?.FindItemByValue(this.Module.TabID.ToString()) != null)
                {
                    this.cboTab.FindItemByValue(this.Module.TabID.ToString()).Selected = true;
                }

                this.RowTabVisible = (this.cboTab?.Items.Count ?? 0) != 1;
                this.AllTabsChecked = this.Module.AllTabs;
                this.NewPagesRowVisible = this.AllTabsChecked;
                this.AllowIndexRowVisible = desktopModule.IsSearchable;
                this.AllowIndexChecked = this.GetBooleanSetting("AllowIndex", true);
                this.Moniker = (string)this.Settings["Moniker"] ?? string.Empty;

                this.VisibilityValue = ((int)this.Module.Visibility).ToString(CultureInfo.InvariantCulture);
                this.AdminBorderChecked = this.Settings["hideadminborder"] != null && bool.Parse(this.Settings["hideadminborder"].ToString());

                var objModuleDef = ModuleDefinitionController.GetModuleDefinitionByID(this.Module.ModuleDefID);
                if (objModuleDef.DefaultCacheTime == Null.NullInteger)
                {
                    this.CacheWarningVisible = true;
                    this.CacheDuration = this.Module.CacheTime.ToString();
                }
                else
                {
                    this.CacheWarningVisible = false;
                    this.CacheDuration = this.Module.CacheTime.ToString();
                }

                this.BindModuleCacheProviderList();

                this.ShowCacheRows();

                this.AlignValue = this.Module.Alignment ?? string.Empty;
                this.Color = this.Module.Color;
                this.Border = this.Module.Border;
                this.Header = this.Module.Header;
                this.Footer = this.Module.Footer;

                if (!Null.IsNull(this.Module.StartDate))
                {
                    this.StartDateText = this.Module.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                if (!Null.IsNull(this.Module.EndDate) && this.Module.EndDate <= DateTime.MaxValue)
                {
                    this.EndDateText = this.Module.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                this.BindContainers();

                this.DisplayTitleChecked = this.Module.DisplayTitle;
                this.DisplayPrintChecked = this.Module.DisplayPrint;
                this.DisplaySyndicateChecked = this.Module.DisplaySyndicate;

                if (this.Module.ModuleID == PortalSettings.Current.DefaultModuleId && this.Module.TabID == PortalSettings.Current.DefaultTabId)
                {
                    this.DefaultChecked = true;
                }

                if (!this.Module.IsShared && this.Module.DesktopModule.Shareable != ModuleSharing.Unsupported)
                {
                    this.IsShareableChecked = this.Module.IsShareable;
                    this.IsShareableViewOnlyChecked = this.Module.IsShareableViewOnly;
                    this.IsShareableRowVisible = true;

                    this.InheritPermissionsVisible = true;
                }
            }
        }

        private void BindContainers()
        {
            // PortalId, RootPath, Scope, IncludeNoneSpecificItem and NoneSpecificText are Blazor
            // parameters, bound in the markup with these very values; only the selection is state.
            if (this.moduleContainerCombo != null)
            {
                this.moduleContainerCombo.SelectedValue = this.Module.ContainerSrc;
            }
        }

        private void BindModulePages()
        {
            var tabsByModule = TabController.Instance.GetTabsByModuleID(this.moduleId);
            tabsByModule.Remove(this.TabId);
            this.installedOnTabs = tabsByModule.Values.ToList();
        }

        private void BindModuleCacheProviderList()
        {
            if (this.cboCacheProvider == null)
            {
                return;
            }

            this.cboCacheProvider.DataSource = GetFilteredProviders(ModuleCachingProvider.GetProviderList(), "ModuleCachingProvider");
            this.cboCacheProvider.DataBind();

            this.cboCacheProvider.InsertItem(0, Localization.GetString("None_Specified"), string.Empty);

            if (!string.IsNullOrEmpty(this.Module.GetEffectiveCacheMethod()) && this.cboCacheProvider.FindItemByValue(this.Module.GetEffectiveCacheMethod()) != null)
            {
                this.cboCacheProvider.FindItemByValue(this.Module.GetEffectiveCacheMethod()).Selected = true;
            }
            else
            {
                // select the None Specified value
                this.cboCacheProvider.Items[0].Selected = true;
            }

            this.CacheInheritedVisible = this.Module.CacheMethod != this.Module.GetEffectiveCacheMethod();
        }

        private void ShowCacheRows()
        {
            this.CacheDurationVisible = !string.IsNullOrEmpty(this.cboCacheProvider?.SelectedValue);
        }

        private bool GetBooleanSetting(string settingName, bool defaultValue)
        {
            var value = this.Settings[settingName];

            return value == null
                ? defaultValue
                : bool.Parse(value.ToString());
        }

        /// <summary>Reads a query string value, from the captured request when there is one and from
        /// the current URL otherwise (an interactive circuit has no <c>HttpContext</c>).</summary>
        /// <param name="name">The query string key.</param>
        /// <returns>The value, or <see langword="null"/> when the key is absent - the same
        /// <c>Request.QueryString[key] != null</c> test the WebForms control made.</returns>
        private string GetQueryValue(string name)
        {
            var request = this.Request;
            if (request != null && request.Query.ContainsKey(name))
            {
                return request.Query[name].ToString();
            }

            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(this.Navigation.Uri).Query);
            return query.TryGetValue(name, out var values) ? values.ToString() : null;
        }
    }
}
