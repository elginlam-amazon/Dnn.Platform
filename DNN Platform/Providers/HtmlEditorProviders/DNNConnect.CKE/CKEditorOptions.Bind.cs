// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using DNNConnect.CKEditorProvider.Constants;
    using DNNConnect.CKEditorProvider.Extensions;
    using DNNConnect.CKEditorProvider.Objects;
    using DNNConnect.CKEditorProvider.Utilities;

    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Modules.Definitions;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Framework.Providers;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.Installer.Packages;
    using DotNetNuke.Services.Localization;

    using Globals = DotNetNuke.Common.Globals;

    /// <content>
    /// The load/bind path of <c>CKEditorOptions.ascx.cs</c>: the methods that filled the settings
    /// UI. They are unchanged in behavior; only the controls they wrote to are the component state
    /// of <c>CKEditorOptions.State.cs</c> instead of designer fields.
    /// </content>
    public partial class CKEditorOptions
    {
        /// <summary>Creates the list items the <c>.ascx</c> markup declared inline.</summary>
        /// <remarks>
        /// These were <c>&lt;asp:ListItem&gt;</c> elements of the "Modify Settings for" radio button
        /// list, the browser/image button/file list view mode/default link mode drop downs and the
        /// twenty toolbar priorities.
        /// </remarks>
        private void InitializeStaticItems()
        {
            if (this.setModeItems.Count > 0)
            {
                return;
            }

            this.setModeItems.Add(new OptionItem("Portal", "portal"));
            this.setModeItems.Add(new OptionItem("Page", "page"));
            this.setModeItems.Add(new OptionItem("Module Instance", "minstance"));

            this.ddlBrowserItems.Add(new OptionItem("None", "none"));
            this.ddlBrowserItems.Add(new OptionItem("Standard", "standard"));
            this.ddlBrowserItems.Add(new OptionItem("CKFinder", "ckfinder"));

            this.ddlImageButtonItems.Add(new OptionItem("Standard", "standard"));
            this.ddlImageButtonItems.Add(new OptionItem("Easy Image Upload", "easyimage"));

            this.fileListViewModeItems.Add(new OptionItem("DetailView", "DetailView"));
            this.fileListViewModeItems.Add(new OptionItem("ListView", "ListView"));
            this.fileListViewModeItems.Add(new OptionItem("IconsView", "IconsView"));

            this.defaultLinkModeItems.Add(new OptionItem("RelativeURL", "RelativeURL"));
            this.defaultLinkModeItems.Add(new OptionItem("Absolute URL", "AbsoluteURL"));
            this.defaultLinkModeItems.Add(new OptionItem("RelativeSecuredURL", "RelativeSecuredURL"));
            this.defaultLinkModeItems.Add(new OptionItem("Absolute Secured", "AbsoluteSecuredURL"));

            for (var priority = 1; priority <= 20; priority++)
            {
                var text = priority.ToString("00", CultureInfo.InvariantCulture);
                this.toolbarPriorityItems.Add(new OptionItem(text, text));
            }
        }

        /// <summary>Renders the editor config settings.</summary>
        /// <remarks>
        /// This is <c>RenderEditorConfigSettings</c>: one item per <see cref="EditorConfig"/>
        /// property in the order <c>SettingsUtil.GetEditorConfigProperties()</c> returns them, with
        /// the <c>CodeMirror</c> and <c>WordCount</c> sub settings rendered as their own items and
        /// their parent container skipped, and the <c>DescriptionAttribute</c> as the tooltip.
        /// </remarks>
        private void RenderEditorConfigSettings()
        {
            this.editorConfigSettings.Clear();
            this.editorConfigSettingsByName.Clear();

            foreach (PropertyInfo info in SettingsUtil.GetEditorConfigProperties())
            {
                var description = info.GetCustomAttribute<DescriptionAttribute>(true);

                var isSubSetting = info.Name == "CodeMirror" || info.Name == "WordCount";

                EditorConfigSetting setting = null;

                if (!isSubSetting)
                {
                    setting = new EditorConfigSetting
                    {
                        Name = info.Name,
                        Label = $"{info.Name}:",
                        ToolTip = description?.Description,
                    };
                }

                switch (info.PropertyType.Name)
                {
                    case "String":
                        setting.Kind = EditorConfigSettingKind.MultiLineText;
                        break;
                    case "Decimal":
                    case "Int32":
                        setting.Kind = EditorConfigSettingKind.Numeric;
                        break;
                    case "Boolean":
                        setting.Kind = EditorConfigSettingKind.Boolean;
                        break;
                }

                switch (info.Name)
                {
                    case "ToolbarLocation":
                        this.AddEnumOptions(setting, typeof(ToolBarLocation));
                        break;
                    case "EnterMode":
                    case "ShiftEnterMode":
                        this.AddEnumOptions(setting, typeof(EnterModus));
                        break;
                    case "DefaultLinkType":
                        this.AddEnumOptions(setting, typeof(LinkType));
                        break;
                    case "ContentsLangDirection":
                        this.AddEnumOptions(setting, typeof(LanguageDirection));
                        break;
                    case "CodeMirror":
                        foreach (var codeMirrorInfo in
                            typeof(CodeMirror).GetProperties().Where(property => !property.Name.Equals("Theme", StringComparison.Ordinal)))
                        {
                            this.AddSubSetting(info.Name, codeMirrorInfo);
                        }

                        break;
                    case "WordCount":
                        foreach (var wordCountInfo in typeof(WordCountConfig).GetProperties())
                        {
                            this.AddSubSetting(info.Name, wordCountInfo);
                        }

                        break;
                }

                if (isSubSetting)
                {
                    continue;
                }

                this.editorConfigSettings.Add(setting);
                this.editorConfigSettingsByName[setting.Name] = setting;
            }
        }

        /// <summary>Adds the items of an enumeration to a setting rendered as a drop down.</summary>
        /// <param name="setting">The setting.</param>
        /// <param name="enumType">The type of the enumeration.</param>
        private void AddEnumOptions(EditorConfigSetting setting, Type enumType)
        {
            setting.Kind = EditorConfigSettingKind.Select;

            foreach (var item in Enum.GetValues(enumType))
            {
                setting.Options.Add(new OptionItem(item.ToString(), item.ToString()));
            }
        }

        /// <summary>Adds a <c>CodeMirror</c>/<c>WordCount</c> sub setting to the settings UI.</summary>
        /// <param name="parentName">The name of the parent property.</param>
        /// <param name="info">The property of the sub setting.</param>
        private void AddSubSetting(string parentName, PropertyInfo info)
        {
            var description = info.GetCustomAttribute<DescriptionAttribute>(true);

            var setting = new EditorConfigSetting
            {
                Name = info.Name,
                Label = $"{parentName} - {info.Name}:",
                ToolTip = description?.Description,
                Kind = info.PropertyType.Name.Equals("Boolean", StringComparison.Ordinal)
                    ? EditorConfigSettingKind.Boolean
                    : EditorConfigSettingKind.MultiLineText,
            };

            this.editorConfigSettings.Add(setting);
            this.editorConfigSettingsByName[setting.Name] = setting;
        }

        /// <summary>Assigns the text of an editor config setting, when it is rendered.</summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value.</param>
        /// <remarks>Replaces <c>Utility.FindControl&lt;TextBox&gt;(this.EditorConfigHolder, name).Text = value</c>.</remarks>
        private void SetEditorConfigText(string name, string value)
        {
            if (this.editorConfigSettingsByName.TryGetValue(name, out var setting))
            {
                setting.StringValue = value;
            }
        }

        /// <summary>Assigns the flag of an editor config setting, when it is rendered.</summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value.</param>
        /// <remarks>Replaces <c>Utility.FindControl&lt;CheckBox&gt;(this.EditorConfigHolder, name).Checked = value</c>.</remarks>
        private void SetEditorConfigChecked(string name, bool value)
        {
            if (this.editorConfigSettingsByName.TryGetValue(name, out var setting))
            {
                setting.BoolValue = value;
            }
        }

        /// <summary>Selects the value of an editor config setting rendered as a drop down.</summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// Replaces the <c>DropDownList.Items.FindByValue(...)</c> guarded <c>ClearSelection</c>
        /// plus selection of the original.
        /// </remarks>
        private void SetEditorConfigSelected(string name, string value)
        {
            if (this.editorConfigSettingsByName.TryGetValue(name, out var setting)
                && setting.Options.Any(option => option.Value.Equals(value, StringComparison.Ordinal)))
            {
                setting.StringValue = value;
            }
        }

        /// <summary>Determines whether a drop down holds an item of the given value.</summary>
        /// <param name="items">The items of the drop down.</param>
        /// <param name="value">The value to look for.</param>
        /// <returns><see langword="true"/> when the item exists.</returns>
        /// <remarks>Replaces <c>ListItemCollection.FindByValue(value) != null</c>.</remarks>
        private static bool HasOption(IEnumerable<OptionItem> items, string value) =>
            items.Any(item => string.Equals(item.Value, value, StringComparison.Ordinal));

        /// <summary>Load Default Host Settings from 'web.config'.</summary>
        private void LoadDefaultSettings()
        {
            var ckeditorProvider = (Provider)this.provConfig.Providers[this.provConfig.DefaultProvider];

            if (ckeditorProvider == null)
            {
                return;
            }

            // Skin
            if (ckeditorProvider.Attributes["ck_skin"] != string.Empty
                && HasOption(this.ddlSkinItems, ckeditorProvider.Attributes["ck_skin"]))
            {
                this.ddlSkin = ckeditorProvider.Attributes["ck_skin"];
            }

            // FileBrowser
            if (ckeditorProvider.Attributes["ck_Browser"] != string.Empty)
            {
                this.ddlBrowser = ckeditorProvider.Attributes["ck_Browser"];
            }

            // ImageButton
            if (ckeditorProvider.Attributes["ck_ImageButton"] != string.Empty)
            {
                this.ddlImageButton = ckeditorProvider.Attributes["ck_ImageButton"];
            }

            if (ckeditorProvider.Attributes["ck_contentsCss"] != string.Empty)
            {
                this.cssUrl = ckeditorProvider.Attributes["ck_contentsCss"];
            }

            if (ckeditorProvider.Attributes["ck_templates_files"] != string.Empty)
            {
                this.templateUrl = ckeditorProvider.Attributes["ck_templates_files"];
            }

            if (ckeditorProvider.Attributes["ck_customConfig"] != string.Empty)
            {
                this.configUrl = ckeditorProvider.Attributes["ck_customConfig"];
            }
        }

        /// <summary>Load All Editor Settings.</summary>
        /// <param name="currentMode">The current mode.</param>
        /// <param name="changeMode">if set to <see langword="true"/> [change mode].</param>
        private void LoadSettings(int currentMode, bool changeMode = true)
        {
            this.moduleInstanceName = this.GetQueryValue("minc");
            this.CurrentSettingsMode = Enum.Parse<SettingsMode>(currentMode.ToString(CultureInfo.InvariantCulture));

            this.RemoveAllVisible = !currentMode.Equals(0);
            this.RemoveChildVisible = !currentMode.Equals(0);
            this.CopyToAllChildVisible = !currentMode.Equals(0);

            var modeText = currentMode != Null.NullInteger ? this.setModeItems[currentMode].Text : "Host";
            this.RemoveText = string.Format(
                Localization.GetString("Remove.Text", this.ResXFile, this.LangCode),
                modeText);
            this.RemoveAllText = string.Format(
                Localization.GetString("RemoveAll.Text", this.ResXFile, this.LangCode),
                modeText);

            this.RemoveToolTip = string.Format(
                Localization.GetString("Remove.Help", this.ResXFile, this.LangCode),
                modeText);
            this.RemoveAllToolTip = string.Format(
                Localization.GetString("RemoveAll.Help", this.ResXFile, this.LangCode),
                modeText);

            this.LoadDefaultSettings();

            var settingsDictionary = EditorController.GetEditorHostSettings(this.HostSettingsService);
            var portalRoles = RoleController.Instance.GetRoles(this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId);

            var hostKey = SettingConstants.HostKey;
            var portalKey = SettingConstants.PortalKey(this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId);
            var pageKey = $"DNNCKT#{this.CurrentOrSelectedTabId}#";
            var moduleKey = $"DNNCKMI#{this.ModuleId}#INS#{this.moduleInstanceName}#";

            var providerConfiguration = ProviderConfiguration.GetProviderConfiguration("htmlEditor");
            var objProvider = (Provider)providerConfiguration.Providers[providerConfiguration.DefaultProvider];

            var homeDirectory = this.portalSettings?.HomeDirectoryMapPath ?? Globals.HostMapPath;

            this.currentSettings = SettingsUtil.GetDefaultSettings(
                this.portalSettings,
                this.HostSettingsService,
                homeDirectory,
                objProvider.Attributes["ck_configFolder"],
                portalRoles);

            this.currentSettings.UploadSizeRoles = this.GetDefaultUploadFileSettings(portalRoles);

            switch (this.CurrentSettingsMode)
            {
                case SettingsMode.Host:
                    {
                        // Load Host Settings ?!
                        if (SettingsUtil.CheckSettingsExistByKey(settingsDictionary, hostKey))
                        {
                            this.currentSettings = new EditorProviderSettings();

                            this.currentSettings = SettingsUtil.LoadEditorSettingsByKey(
                                this.portalSettings,
                                this.currentSettings,
                                settingsDictionary,
                                hostKey,
                                portalRoles);

                            this.currentSettings.SettingMode = SettingsMode.Host;

                            this.RemoveEnabled = true;
                        }
                        else
                        {
                            this.RemoveEnabled = false;
                        }
                    }

                    break;
                case SettingsMode.Portal:
                    {
                        // Load Portal Settings ?!
                        if (SettingsUtil.CheckSettingsExistByKey(settingsDictionary, portalKey))
                        {
                            this.currentSettings = new EditorProviderSettings();

                            this.currentSettings = SettingsUtil.LoadEditorSettingsByKey(
                                this.portalSettings, this.currentSettings, settingsDictionary, portalKey, portalRoles);

                            // check if UploadSizeLimits have been set
                            if (this.currentSettings.UploadSizeRoles == null || this.currentSettings.UploadSizeRoles.Count == 0)
                            {
                                this.currentSettings.UploadSizeRoles = this.GetDefaultUploadFileSettings(portalRoles);
                            }

                            // Set Current Mode to Portal
                            this.currentSettings.SettingMode = SettingsMode.Portal;

                            this.RemoveEnabled = true;
                        }
                        else
                        {
                            this.RemoveEnabled = false;
                        }
                    }

                    break;
                case SettingsMode.Page:
                    {
                        // Load Page Settings ?!
                        if (SettingsUtil.CheckSettingsExistByKey(settingsDictionary, pageKey))
                        {
                            this.currentSettings = new EditorProviderSettings();

                            this.currentSettings = SettingsUtil.LoadEditorSettingsByKey(
                                this.portalSettings, this.currentSettings, settingsDictionary, pageKey, portalRoles);

                            // Set Current Mode to Page
                            this.currentSettings.SettingMode = SettingsMode.Page;

                            this.RemoveEnabled = true;
                        }
                        else
                        {
                            this.RemoveEnabled = false;
                        }

                        var currentTab = this.TabControllerService.GetTab(
                            this.CurrentOrSelectedTabId, this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId, false);

                        this.RemoveChildEnabled = currentTab.HasChildren;

                        this.RemoveChildText = Localization.GetString("RemovePageChild.Text", this.ResXFile, this.LangCode);
                        this.RemoveChildToolTip = Localization.GetString("RemovePageChild.Help", this.ResXFile, this.LangCode);

                        this.CopyToAllChildEnabled = currentTab.HasChildren;

                        this.CopyToAllChildText = Localization.GetString("CopyPageChild.Text", this.ResXFile, this.LangCode);
                        this.CopyToAllChildToolTip = Localization.GetString("CopyPageChild.Help", this.ResXFile, this.LangCode);
                    }

                    break;
                case SettingsMode.ModuleInstance:
                    {
                        // Load Module Settings ?!
                        if (SettingsUtil.CheckExistsModuleInstanceSettings(moduleKey, this.ModuleId))
                        {
                            this.currentSettings = new EditorProviderSettings();

                            this.currentSettings = SettingsUtil.LoadModuleSettings(
                                this.portalSettings, this.currentSettings, moduleKey, this.ModuleId, portalRoles);

                            this.currentSettings.SettingMode = SettingsMode.ModuleInstance;

                            this.RemoveEnabled = true;
                        }
                        else
                        {
                            this.RemoveEnabled = false;
                        }

                        this.RemoveChildEnabled = true;

                        this.RemoveChildText = Localization.GetString("RemoveModuleChild.Text", this.ResXFile, this.LangCode);
                        this.RemoveChildToolTip = Localization.GetString("RemoveModuleChild.Help", this.ResXFile, this.LangCode);
                    }

                    break;
            }

            if (this.currentSettings != null)
            {
                this.FillSettings(this.currentSettings, changeMode);
            }
        }

        /// <summary>Gets default upload size limits for each existent role.</summary>
        /// <param name="portalRoles">A list of roles.</param>
        /// <returns>A list of <see cref="UploadSizeRoles"/> instances.</returns>
        private List<UploadSizeRoles> GetDefaultUploadFileSettings(IList<RoleInfo> portalRoles)
        {
            return portalRoles.Select(role => new UploadSizeRoles()
            {
                RoleId = role.RoleID,
                RoleName = role.RoleName,
                UploadFileLimit = -1,
            }).ToList();
        }

        /// <summary>Fill file upload size limit controls.</summary>
        /// <param name="imporUploadSizeRoles">A list of <see cref="UploadSizeRoles"/>.</param>
        private void FillFileUploadSettings(List<UploadSizeRoles> imporUploadSizeRoles)
        {
            this.uploadSizeRows.Clear();

            foreach (var uploadSizeRole in imporUploadSizeRoles)
            {
                this.uploadSizeRows.Add(new UploadSizeRow
                {
                    RoleId = uploadSizeRole.RoleId,
                    RoleName = uploadSizeRole.RoleName,
                    SizeLimit = uploadSizeRole.UploadFileLimit.ToString(CultureInfo.InvariantCulture),
                });
            }

            // Load Upload Size Setting for Each Portal Role
            foreach (var uploadSizeRole in imporUploadSizeRoles)
            {
                if (uploadSizeRole.RoleId.Equals(-1))
                {
                    foreach (var row in this.uploadSizeRows.Where(row => row.RoleName == UnauthenticatedUsersRoleName))
                    {
                        row.SizeLimit = uploadSizeRole.UploadFileLimit.ToString(CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    RoleInfo objRole = RoleController.Instance.GetRoleById(
                        this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId, uploadSizeRole.RoleId);

                    if (objRole == null)
                    {
                        continue;
                    }

                    foreach (var row in this.uploadSizeRows.Where(row => row.RoleName == objRole.RoleName))
                    {
                        row.SizeLimit = uploadSizeRole.UploadFileLimit.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        /// <summary>Bind User Groups to GridView.</summary>
        /// <remarks>
        /// This is <c>BindUserGroupsGridView</c> together with <c>On_gvToolbars_RowDataBound</c>,
        /// which ran per row: the toolbar drop down of a row holds every toolbar set and selects
        /// the one the current settings assign to the row's role.
        /// </remarks>
        private void BindUserGroupsGridView()
        {
            var portalId = this.portalSettings?.PortalId != Null.NullInteger
                ? this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId
                : this.HostSettingsService.HostPortalId;

            var roleNames = RoleController.Instance.GetRoles(portalId)
                .Select(objRole => objRole.RoleName)
                .ToList();

            roleNames.Add(UnauthenticatedUsersRoleName);

            this.InsertToolbars();

            this.toolbarRoleRows.Clear();

            foreach (var roleName in roleNames)
            {
                var objRole = RoleController.Instance.GetRoleByName(portalId, roleName);

                if (objRole == null && roleName != UnauthenticatedUsersRoleName)
                {
                    continue;
                }

                var row = new ToolbarRoleRow { RoleName = roleName };

                if (this.currentSettings == null)
                {
                    var settingsDictionary = EditorController.GetEditorHostSettings(this.HostSettingsService);
                    var pageKey = $"DNNCKT#{this.CurrentOrSelectedTabId}#";
                    this.LoadSettings(SettingsUtil.CheckSettingsExistByKey(settingsDictionary, pageKey) ? 1 : 0);
                }

                var objRoleId = roleName != UnauthenticatedUsersRoleName ? objRole.RoleID : -1;
                var currentToolbarSettings = this.currentSettings.ToolBarRoles.FirstOrDefault(o => o.RoleId == objRoleId);

                if (currentToolbarSettings != null
                    && HasOption(this.availableToolbarNames, currentToolbarSettings.Toolbar))
                {
                    row.SelectedToolbar = currentToolbarSettings.Toolbar;
                }

                this.toolbarRoleRows.Add(row);
            }

            this.lblRole = Localization.GetString("lblRole.Text", this.ResXFile, this.LangCode);
            this.lblSelToolb = Localization.GetString("lblSelToolb.Text", this.ResXFile, this.LangCode);
        }

        /// <summary>Insert Toolbar Names from Serialized Xml File.</summary>
        /// <remarks>
        /// This is <c>InsertToolbars</c>: the toolbar name list, the priority exclusion and the
        /// reset of the two toolbar editor repeaters (<see cref="FillEmptyToolbarEditor"/>).
        /// </remarks>
        private void InsertToolbars()
        {
            this.availableToolbarNames.Clear();

            foreach (var toolbarSet in this.listToolbars)
            {
                this.availableToolbarNames.Add(new OptionItem(toolbarSet.Name, toolbarSet.Name));

                // Exclude used Prioritys from the DropDown
                var priority = this.toolbarPriorityItems.FirstOrDefault(
                    item => item.Text == toolbarSet.Priority.ToString(CultureInfo.InvariantCulture));

                if (priority != null)
                {
                    priority.Enabled = false;
                }
            }

            this.HideAddToolbar();

            this.FillEmptyToolbarEditor();
        }

        /// <summary>Hide Add Toolbar Button if all Priorities are used.</summary>
        private void HideAddToolbar()
        {
            var bHideAll = !this.toolbarPriorityItems.Any(item => item.Enabled);

            if (bHideAll)
            {
                this.AddToolbarVisible = false;
            }
        }

        /// <summary>Write Information.</summary>
        private void FillInformations()
        {
            var ckEditorPackage = PackageController.Instance.GetExtensionPackage(
                Null.NullInteger, p => p.Name.Equals("DotNetNuke.CKHtmlEditorProvider", StringComparison.OrdinalIgnoreCase));

            if (ckEditorPackage != null)
            {
                this.providerVersion += ckEditorPackage.Version;
            }

            this.lblPortal += !this.IsAllInstances ? this.portalSettings.PortalName : "Host";

            ModuleDefinitionInfo moduleDefinitionInfo = null;
            var portalId = this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId;
            var moduleInfo = this.ModuleControllerService.GetModuleByDefinition(portalId, "User Accounts");

            if (this.CurrentModule != null)
            {
                moduleDefinitionInfo = ModuleDefinitionController.GetModuleDefinitionByID(this.CurrentModule.ModuleDefID);
            }

            if (moduleDefinitionInfo == null)
            {
                moduleDefinitionInfo = ModuleDefinitionController.GetModuleDefinitionByFriendlyName(
                    "User Accounts", moduleInfo.DesktopModuleID);
            }

            try
            {
                var tabName = this.TabControllerService.GetTab(this.CurrentOrSelectedTabId, portalId, false).TabName;
                this.lblPage += $"{tabName} - TabID {this.CurrentOrSelectedTabId}";
            }
            catch (Exception)
            {
                this.lblPage = string.Empty;
            }

            if (moduleDefinitionInfo != null)
            {
                this.lblModType += moduleDefinitionInfo.FriendlyName;
                if (!this.IsHostMode && moduleDefinitionInfo.FriendlyName.Equals("User Accounts", StringComparison.Ordinal))
                {
                    this.setModeItems.RemoveAt(2);
                }
            }
            else
            {
                this.lblModType = string.Empty;
            }

            this.lblModName += this.CurrentModule?.ModuleTitle ?? moduleInfo.ModuleTitle;

            if (this.GetQueryValue("minc") != null)
            {
                this.lblModInst += this.GetQueryValue("minc");
                this.moduleInstanceName = this.GetQueryValue("minc");
            }

            if (this.UserInfo != null)
            {
                this.lblUName += this.UserInfo.Username;
            }
            else
            {
                this.lblUName = string.Empty;
            }
        }

        /// <summary>Loads all DNN Roles.</summary>
        private void FillRoles()
        {
            this.browserRoles.Clear();

            var portalId = this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId;

            foreach (var objRole in this.GetRoles(portalId))
            {
                var isAdmin = objRole.RoleName.Equals(this.PortalSettings.AdministratorRoleName, StringComparison.Ordinal);

                this.browserRoles.Add(new CheckItem
                {
                    Text = objRole.RoleName,
                    Value = objRole.RoleID.ToString(CultureInfo.InvariantCulture),
                    Selected = isAdmin || this.GetActiveRolesIds().Contains(objRole.RoleID),
                    Enabled = !isAdmin,
                });
            }
        }

        /// <summary>Gets a sequence of currently active file browser roles.</summary>
        /// <returns>A sequence of role IDs.</returns>
        private IEnumerable<int> GetActiveRolesIds()
        {
            if (this.currentSettings?.BrowserRoles != null)
            {
                var rolesIds = this.currentSettings.BrowserRoles.Split(';');
                foreach (string roleId in rolesIds)
                {
                    int actualRoleId;

                    if (!int.TryParse(roleId, out actualRoleId))
                    {
                        var role = RoleController.Instance.GetRoleByName(this.PortalId, roleId);
                        actualRoleId = role != null ? role.RoleID : -1;
                    }

                    yield return actualRoleId;
                }
            }
            else
            {
                yield break;
            }
        }

        /// <summary>Gets the roles.</summary>
        /// <param name="portalId">The portal ID.</param>
        /// <returns>A sequence of <see cref="RoleInfo"/> instances.</returns>
        private IEnumerable<RoleInfo> GetRoles(int portalId)
        {
            portalId = portalId == Null.NullInteger ? this.HostSettingsService.HostPortalId : portalId;
            var roles = RoleController.Instance.GetRoles(portalId);

            if (!this.IsAllInstances)
            {
                return roles;
            }

            return (from role in roles
                    let isCommon = PortalController.Instance.GetPortals().Cast<IPortalInfo>().All(
                        portal => RoleController.Instance.GetRoles(portal.PortalId).Any(r => r.RoleName == role.RoleName))
                    where isCommon
                    select role).ToList();
        }

        /// <summary>Loads the List of available Skins.</summary>
        private void FillSkinList()
        {
            this.ddlSkinItems.Clear();

            DirectoryInfo objDir = new DirectoryInfo(
                this.AppStatus.ApplicationMapPath + "/Providers/HtmlEditorProviders/DNNConnect.CKE/js/ckeditor/4.18.0/skins");

            foreach (var skinItem in
                objDir.GetDirectories().Select(objSubFolder => new OptionItem(objSubFolder.Name, objSubFolder.Name)))
            {
                this.ddlSkinItems.Add(skinItem);
            }

            this.ddlSkin = this.currentSettings?.Config?.Skin ?? string.Empty;

            // CodeMirror Themes
            this.codeMirrorThemeItems.Clear();

            var themesPath = this.AppStatus.ApplicationMapPath
                + "/Providers/HtmlEditorProviders/DNNConnect.CKE/js/ckeditor/4.18.0/plugins/codemirror/theme";
            if (Directory.Exists(themesPath))
            {
                var themesFolder = new DirectoryInfo(themesPath);

                // add default theme
                this.codeMirrorThemeItems.Add(new OptionItem("default", "default"));

                foreach (var skinItem in
                    themesFolder.GetFiles("*.css")
                        .Select(themeCssFile => themeCssFile.Name.Replace(themeCssFile.Extension, string.Empty))
                        .Select(themeName => new OptionItem(themeName, themeName)))
                {
                    this.codeMirrorThemeItems.Add(skinItem);
                }
            }
        }

        /// <summary>Loads the List of available Folders.</summary>
        /// <remarks>
        /// The five folder drop downs (<c>UploadDir</c>, <c>UploadDirForImg</c>,
        /// <c>BrowserRootDir</c>, <c>BrowserRootDirForImg</c>, <c>ExportDir</c>) were filled with
        /// the very same items, so they share one item list here.
        /// </remarks>
        private void FillFolders()
        {
            this.folderItems.Clear();

            var portalId = this.portalSettings?.PortalId ?? Null.NullInteger;
            foreach (var folder in FolderManager.Instance.GetFolders(portalId))
            {
                string text, value;

                if (folder.FolderPath == Null.NullString)
                {
                    text = "Standard";
                    value = "-1";
                }
                else
                {
                    text = folder.FolderPath;
                    value = folder.FolderID.ToString(CultureInfo.InvariantCulture);
                }

                if (!FolderPermissionController.CanViewFolder(folder as FolderInfo))
                {
                    continue;
                }

                this.folderItems.Add(new OptionItem(text, value));
            }

            this.uploadDir = "-1";
            this.uploadDirForImg = "-1";
            this.browserRootDir = "-1";
            this.browserRootDirForImg = "-1";
            this.exportDir = "-1";
        }
    }
}
