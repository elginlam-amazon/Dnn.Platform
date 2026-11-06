// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Xml.Serialization;

    using DNNConnect.CKEditorProvider.Constants;
    using DNNConnect.CKEditorProvider.Objects;
    using DNNConnect.CKEditorProvider.Utilities;

    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules.Definitions;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.Localization;

    using Globals = DotNetNuke.Common.Globals;

    /// <content>
    /// The command and save path of <c>CKEditorOptions.ascx.cs</c>: the OK button, the four delete
    /// and copy link buttons, the three save methods, the import and export of the settings XML and
    /// the three role scoped setting writers.
    /// </content>
    /// <remarks>
    /// <para>
    /// Every method is ported verbatim in behavior. Two WebForms seams had to be replaced:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>Utility.FindControl&lt;TextBox|CheckBox|DropDownList&gt;(EditorConfigHolder, name)</c> is the <see cref="editorConfigSettingsByName"/> lookup; a control the original did not find is a key the dictionary does not hold, so the same settings are skipped.</description></item>
    /// <item><description>The two <c>GridView.Rows</c> loops read the <see cref="toolbarRoleRows"/>/<see cref="uploadSizeRows"/> row models, whose <c>RoleName</c> is the <c>lblRoleName</c> label text and whose value is the <c>ddlToolbars</c>/<c>SizeLimit</c> control value.</description></item>
    /// </list>
    /// </remarks>
    public partial class CKEditorOptions
    {
        /// <summary>Saves all Settings and Close Options.</summary>
        /// <returns>A task.</returns>
        /// <remarks>Replaces <c>OK_Click</c>.</remarks>
        private async Task OkClick()
        {
            this.SaveSettings();

            // check if toolbar set editor is not correctly saved
            var serializedToolbar = await this.ReadToolbarSetValueAsync();

            if (this.isToolbarEditMode && !string.IsNullOrEmpty(serializedToolbar))
            {
                var modifiedSet = ToolbarUtil.ConvertStringToToolbarSet(serializedToolbar);
                var toolbarEdit = this.listToolbars.Find(
                    toolbarSel => toolbarSel.Name.Equals(this.toolbarName, StringComparison.Ordinal));

                toolbarEdit.ToolbarGroups = modifiedSet.ToolbarGroups;
                toolbarEdit.Priority = int.Parse(this.toolbarPriority, CultureInfo.InvariantCulture);
                var homeDirectory = this.portalSettings?.HomeDirectoryMapPath ?? Globals.HostMapPath;
                var homeDirPath = !string.IsNullOrEmpty(this.configFolder)
                                      ? Path.Combine(homeDirectory, this.configFolder)
                                      : homeDirectory;
                ToolbarUtil.SaveToolbarSets(this.listToolbars, homeDirPath);
            }

            this.ShowNotification(Localization.GetString("lblInfo.Text", this.ResXFile, this.LangCode), "success");

            this.BindOptionsData(true);
        }

        /// <summary>Remove Current selected Settings.</summary>
        /// <remarks>Replaces <c>Remove_Click</c>.</remarks>
        private void RemoveClick()
        {
            switch (this.CurrentSettingsMode)
            {
                case SettingsMode.Host:
                    EditorController.DeleteAllHostSettings();
                    break;
                case SettingsMode.Portal:
                    EditorController.DeleteAllPortalSettings(this.portalSettings?.PortalId ?? Null.NullInteger);
                    break;
                case SettingsMode.Page:
                    EditorController.DeleteCurrentPageSettings(this.CurrentOrSelectedTabId);
                    break;
                case SettingsMode.ModuleInstance:
                    this.DelModuleSettings();
                    break;
            }

            this.ShowNotification(Localization.GetString("lblInfoDel.Text", this.ResXFile, this.LangCode), "success");
        }

        /// <summary>Remove selected all Settings.</summary>
        /// <remarks>Replaces <c>RemoveAll_Click</c>.</remarks>
        private void RemoveAllClick()
        {
            switch (this.CurrentSettingsMode)
            {
                case SettingsMode.Host:
                    EditorController.DeleteAllHostSettings();
                    break;
                case SettingsMode.Portal:
                    EditorController.DeleteAllPortalSettings(this.portalSettings?.PortalId ?? Null.NullInteger);
                    break;
                case SettingsMode.Page:
                    EditorController.DeleteAllPageSettings(this.portalSettings?.PortalId ?? Null.NullInteger);
                    break;
                case SettingsMode.ModuleInstance:
                    EditorController.DeleteAllModuleSettings(this.portalSettings?.PortalId ?? Null.NullInteger);
                    break;
            }

            this.ShowNotification(Localization.GetString("lblInfoDel.Text", this.ResXFile, this.LangCode), "success");
        }

        /// <summary>Removes the settings of the child pages or module instances.</summary>
        /// <remarks>Replaces <c>RemoveChild_Click</c>.</remarks>
        private void RemoveChildClick()
        {
            switch (this.CurrentSettingsMode)
            {
                case SettingsMode.Page:
                    {
                        // Delete all Page Setting for all Child Tabs
                        EditorController.DeleteAllChildPageSettings(this.CurrentOrSelectedTabId);
                    }

                    break;
                case SettingsMode.ModuleInstance:
                    {
                        // Delete all Module Instance Settings for the Current Tab
                        EditorController.DeleteAllModuleSettingsById(this.CurrentOrSelectedTabId);
                    }

                    break;
                default:
                    return;
            }

            this.ShowNotification(Localization.GetString("lblInfoDel.Text", this.ResXFile, this.LangCode), "success");
        }

        /// <summary>Copies the current Page Settings to all Child Pages.</summary>
        /// <remarks>Replaces <c>CopyToAllChild_Click</c>.</remarks>
        private void CopyToAllChildClick()
        {
            var childTabs = TabController.GetTabsByParent(this.CurrentOrSelectedTabId, this.CurrentOrSelectedPortalId);

            foreach (var tab in childTabs)
            {
                // Sa Settings to tab
                this.SaveSettingsByKey($"DNNCKT#{tab.TabID}#");
            }

            // Finally Clear Cache
            EditorController.ClearEditorCache();

            this.ShowNotification(Localization.GetString("lblInfoCopyAll.Text", this.ResXFile, this.LangCode), "success");
        }

        /// <summary>Import Current Settings.</summary>
        /// <remarks>Replaces <c>Import_Click</c>; <c>upOptions.Update()</c> is the component re-render.</remarks>
        private void ImportClick()
        {
            if (string.IsNullOrEmpty(this.importFileUrl))
            {
                return;
            }

            var sXmlImport = this.importFileUrl;

            // RESET Dialog
            this.importFileUrl = null;

            int imageFileId = int.Parse(sXmlImport.AsSpan(7), CultureInfo.InvariantCulture);

            var objFileInfo = FileManager.Instance.GetFile(imageFileId);

            var homeDirectory = this.HomeDirectory;
            sXmlImport = homeDirectory + objFileInfo.Folder + objFileInfo.FileName;

            try
            {
                this.ImportXmlFile(sXmlImport);

                this.ShowNotification(Localization.GetString("Imported.Text", this.ResXFile, this.LangCode), "success");
            }
            catch (Exception)
            {
                this.ShowNotification(
                    Localization.GetString("BadImportXml.Text", this.ResXFile, this.LangCode), "error");
            }
        }

        /// <summary>Export Current Settings.</summary>
        /// <remarks>Replaces <c>Export_Click</c>.</remarks>
        private void ExportClick()
        {
            var exportSettings = this.ExportSettings();

            // Save XML file
            try
            {
                var serializer = new XmlSerializer(typeof(EditorProviderSettings));

                var xmlFileName = !string.IsNullOrEmpty((this.exportFileName ?? string.Empty).Trim())
                                      ? this.exportFileName
                                      : $"CKEditorSettings-{exportSettings.SettingMode}.xml";

                if (!xmlFileName.EndsWith(".xml", StringComparison.Ordinal))
                {
                    xmlFileName += ".xml";
                }

                var homeDirectory = this.portalSettings?.HomeDirectoryMapPath ?? Globals.HostMapPath;

                string targetPath;
                if (this.exportDir.Equals("-1", StringComparison.Ordinal))
                {
                    targetPath = Path.Combine(homeDirectory, xmlFileName);
                }
                else
                {
                    var exportFolderInfo = FolderManager.Instance.GetFolder(
                        Convert.ToInt32(this.exportDir, CultureInfo.InvariantCulture));

                    targetPath = Path.Combine(exportFolderInfo.PhysicalPath, xmlFileName);
                }

                using (var textWriter = new StreamWriter(targetPath))
                {
                    serializer.Serialize(textWriter, exportSettings);
                }

                this.ShowNotification(Localization.GetString("Export.Text", this.ResXFile, this.LangCode), "success");
            }
            catch (Exception exception)
            {
                this.ShowNotification(exception.Message, "error");
            }
        }

        /// <summary>Imports the XML file.</summary>
        /// <param name="xmlFilePath">The XML file path.</param>
        /// <param name="changeMode">if set to <see langword="true"/> [change mode].</param>
        private void ImportXmlFile(string xmlFilePath, bool changeMode = true)
        {
            var serializer = new XmlSerializer(typeof(EditorProviderSettings));

            EditorProviderSettings importedSettings;

            using (var textReader = new StreamReader(xmlFilePath))
            {
                importedSettings = (EditorProviderSettings)SafeXml.Deserialize(serializer, textReader);
            }

            if (!string.IsNullOrEmpty(importedSettings.EditorWidth))
            {
                importedSettings.Config.Width = importedSettings.EditorWidth;
            }

            if (!string.IsNullOrEmpty(importedSettings.EditorHeight))
            {
                importedSettings.Config.Height = importedSettings.EditorHeight;
            }

            this.FillSettings(importedSettings, changeMode);
        }

        /// <summary>Delete Settings only for this Module Instance.</summary>
        private void DelModuleSettings()
        {
            this.moduleInstanceName = this.GetQueryValue("minc");
            string moduleKey = $"DNNCKMI#{this.ModuleId}#INS#{this.moduleInstanceName}#";

            foreach (PropertyInfo info in SettingsUtil.GetEditorConfigProperties())
            {
                this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{info.Name}");
            }

            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.SKIN}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.CODEMIRRORTHEME}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.BROWSER}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.FILELISTPAGESIZE}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.FILELISTVIEWMODE}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.DEFAULTLINKMODE}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.USEANCHORSELECTOR}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.SHOWPAGELINKSTABFIRST}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.OVERRIDEFILEONUPLOAD}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.SUBDIRS}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.BROWSERROOTDIRID}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.BROWSERROOTDIRFORIMGID}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.UPLOADDIRID}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.UPLOADDIRFORIMGID}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.INJECTJS}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.WIDTH}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.HEIGHT}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.BLANKTEXT}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.CSS}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.TEMPLATEFILES}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.CUSTOMJSFILE}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.CONFIG}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.ROLES}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.RESIZEHEIGHTUPLOAD}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.RESIZEWIDTHUPLOAD}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.RESIZEHEIGHT}");
            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}{SettingConstants.RESIZEWIDTH}");

            foreach (var objRole in RoleController.Instance.GetRoles(
                this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId))
            {
                this.ModuleControllerService.DeleteModuleSetting(
                    this.ModuleId, $"{moduleKey}{objRole.RoleID}#{SettingConstants.TOOLB}");
            }

            this.ModuleControllerService.DeleteModuleSetting(this.ModuleId, $"{moduleKey}-1#{SettingConstants.TOOLB}");

            // Finally Clear Cache
            EditorController.ClearEditorCache();
        }

        /// <summary>Save all Settings for the Current Selected Mode.</summary>
        private void SaveSettings()
        {
            var moduleInfo = this.ModuleControllerService.GetModuleByDefinition(
                this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId, "User Accounts");

            ModuleDefinitionInfo definition;
            try
            {
                definition = ModuleDefinitionController.GetModuleDefinitionByID(this.CurrentModule.ModuleDefID);
            }
            catch (Exception)
            {
                definition = ModuleDefinitionController.GetModuleDefinitionByFriendlyName(
                    "User Accounts", moduleInfo.DesktopModuleID);
            }

            switch (this.CurrentSettingsMode)
            {
                case SettingsMode.Host:
                    this.SaveSettingsByKey(SettingConstants.HostKey);
                    break;
                case SettingsMode.Portal:
                    this.SaveSettingsByKey(
                        SettingConstants.PortalKey(this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId));
                    break;
                case SettingsMode.Page:
                    this.SaveSettingsByKey($"DNNCKT#{this.CurrentOrSelectedTabId}#");
                    break;
                default:
                    if (this.CurrentSettingsMode.Equals(SettingsMode.ModuleInstance)
                        && !definition.FriendlyName.Equals("User Accounts", StringComparison.Ordinal))
                    {
                        this.SaveModuleSettings();
                    }

                    break;
            }

            // Finally Clear Cache
            EditorController.ClearEditorCache();
        }

        /// <summary>Save Settings for this Module Instance.</summary>
        private void SaveModuleSettings()
        {
            this.moduleInstanceName = this.GetQueryValue("minc");
            string key = $"DNNCKMI#{this.ModuleId}#INS#{this.moduleInstanceName}#";

            // Editor config settings
            this.WriteEditorConfigSettings((settingName, value) =>
                this.ModuleControllerService.UpdateModuleSetting(this.ModuleId, $"{key}{settingName}", value));

            ///////////////////
            void Update(string settingName, string value) =>
                this.ModuleControllerService.UpdateModuleSetting(this.ModuleId, $"{key}{settingName}", value);

            Update(SettingConstants.SKIN, this.ddlSkin);
            Update(SettingConstants.CODEMIRRORTHEME, this.codeMirrorTheme);
            Update(SettingConstants.BROWSER, this.ddlBrowser);
            Update(SettingConstants.IMAGEBUTTON, this.ddlImageButton);
            Update(SettingConstants.FILELISTVIEWMODE, this.fileListViewMode);
            Update(SettingConstants.DEFAULTLINKMODE, this.defaultLinkMode);
            Update(SettingConstants.USEANCHORSELECTOR, this.useAnchorSelector.ToString());
            Update(SettingConstants.SHOWPAGELINKSTABFIRST, this.showPageLinksTabFirst.ToString());
            Update(SettingConstants.OVERRIDEFILEONUPLOAD, this.overrideFileOnUpload.ToString());
            Update(SettingConstants.SUBDIRS, this.cbBrowserDirs.ToString());
            Update(SettingConstants.BROWSERROOTDIRID, this.browserRootDir);
            Update(SettingConstants.BROWSERROOTDIRFORIMGID, this.browserRootDirForImg);
            Update(SettingConstants.UPLOADDIRID, this.uploadDir);
            Update(SettingConstants.UPLOADDIRFORIMGID, this.uploadDirForImg);

            if (Utility.IsNumeric(this.fileListPageSize))
            {
                Update(SettingConstants.FILELISTPAGESIZE, this.fileListPageSize);
            }

            if (Utility.IsNumeric(this.txtResizeWidthUpload))
            {
                Update(SettingConstants.RESIZEWIDTHUPLOAD, this.txtResizeWidthUpload);
            }

            if (Utility.IsNumeric(this.txtResizeHeightUpload))
            {
                Update(SettingConstants.RESIZEHEIGHTUPLOAD, this.txtResizeHeightUpload);
            }

            if (Utility.IsNumeric(this.txtResizeWidth))
            {
                Update(SettingConstants.RESIZEWIDTH, this.txtResizeWidth);
            }

            if (Utility.IsNumeric(this.txtResizeHeight))
            {
                Update(SettingConstants.RESIZEHEIGHT, this.txtResizeHeight);
            }

            Update(SettingConstants.INJECTJS, this.injectSyntaxJs.ToString());

            if (Utility.IsUnit(this.txtWidth))
            {
                Update(SettingConstants.WIDTH, this.txtWidth);
            }

            if (Utility.IsUnit(this.txtHeight))
            {
                // REVIEW: the original wrote txtWidth into the HEIGHT module setting; preserved verbatim.
                Update(SettingConstants.HEIGHT, this.txtWidth);
            }

            Update(SettingConstants.BROWSERALLOWFOLLOWFOLDERPERMS, this.browAllowFollowPerms.ToString());

            Update(SettingConstants.BLANKTEXT, this.txtBlanktext);
            Update(SettingConstants.CSS, this.cssUrl);
            Update(SettingConstants.TEMPLATEFILES, this.templateUrl);
            Update(SettingConstants.CUSTOMJSFILE, this.customJsFile);
            Update(SettingConstants.CONFIG, this.configUrl);

            string sRoles = this.browserRoles.Where(item => item.Selected).Aggregate(
                string.Empty, (current, item) => current + (item.Value + ";"));

            if (sRoles != string.Empty)
            {
                Update(SettingConstants.ROLES, sRoles);
            }

            // Save Toolbar Setting for every Role
            foreach (var row in this.toolbarRoleRows)
            {
                if (row.RoleName == null)
                {
                    continue;
                }

                if (row.RoleName.Equals(UnauthenticatedUsersRoleName, StringComparison.Ordinal))
                {
                    this.ModuleControllerService.UpdateModuleSetting(
                        this.ModuleId,
                        string.Format(CultureInfo.InvariantCulture, "{0}{2}#{1}", key, "-1", SettingConstants.TOOLB),
                        row.SelectedToolbar);
                }
                else
                {
                    var objRole = RoleController.Instance.GetRoleByName(
                        this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId, row.RoleName);

                    this.ModuleControllerService.UpdateModuleSetting(
                        this.ModuleId,
                        string.Format(CultureInfo.InvariantCulture, "{0}{2}#{1}", key, objRole.RoleID, SettingConstants.TOOLB),
                        row.SelectedToolbar);
                }
            }

            // Save Upload File Limit Setting for every Role
            foreach (var row in this.uploadSizeRows)
            {
                if (row.RoleName == null || string.IsNullOrEmpty(row.SizeLimit))
                {
                    continue;
                }

                if (row.RoleName.Equals(UnauthenticatedUsersRoleName, StringComparison.Ordinal))
                {
                    this.ModuleControllerService.UpdateModuleSetting(
                        this.ModuleId,
                        $"{key}-1{SettingConstants.UPLOADFILELIMITS}",
                        row.SizeLimit);
                }
                else
                {
                    var objRole = RoleController.Instance.GetRoleByName(
                        this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId, row.RoleName);

                    this.ModuleControllerService.UpdateModuleSetting(
                        this.ModuleId,
                        string.Format(CultureInfo.InvariantCulture, "{0}{1}#{2}", key, objRole.RoleID, SettingConstants.UPLOADFILELIMITS),
                        row.SizeLimit);
                }
            }
        }

        /// <summary>Save Settings for this Page Or Portal.</summary>
        /// <param name="key">The key.</param>
        private void SaveSettingsByKey(string key)
        {
            // Editor config settings
            this.WriteEditorConfigSettings((settingName, value) =>
                EditorController.AddOrUpdateEditorHostSetting($"{key}{settingName}", value));

            ///////////////////
            void Update(string settingName, string value) =>
                EditorController.AddOrUpdateEditorHostSetting($"{key}{settingName}", value);

            Update(SettingConstants.SKIN, this.ddlSkin);
            Update(SettingConstants.CODEMIRRORTHEME, this.codeMirrorTheme);
            Update(SettingConstants.BROWSER, this.ddlBrowser);
            Update(SettingConstants.IMAGEBUTTON, this.ddlImageButton);
            Update(SettingConstants.FILELISTVIEWMODE, this.fileListViewMode);
            Update(SettingConstants.DEFAULTLINKMODE, this.defaultLinkMode);
            Update(SettingConstants.USEANCHORSELECTOR, this.useAnchorSelector.ToString());
            Update(SettingConstants.SHOWPAGELINKSTABFIRST, this.showPageLinksTabFirst.ToString());
            Update(SettingConstants.OVERRIDEFILEONUPLOAD, this.overrideFileOnUpload.ToString());
            Update(SettingConstants.SUBDIRS, this.cbBrowserDirs.ToString());
            Update(SettingConstants.HOSTBROWSERROOTDIR, this.hostBrowserRootDir);
            Update(SettingConstants.BROWSERROOTDIRID, this.browserRootDir);
            Update(SettingConstants.HOSTBROWSERROOTDIRFORIMG, this.hostBrowserRootDirForImg);
            Update(SettingConstants.BROWSERROOTDIRFORIMGID, this.browserRootDirForImg);
            Update(SettingConstants.HOSTUPLOADDIR, this.hostUploadDir);
            Update(SettingConstants.UPLOADDIRID, this.uploadDir);
            Update(SettingConstants.HOSTUPLOADDIRFORIMG, this.hostUploadDirForImg);
            Update(SettingConstants.UPLOADDIRFORIMGID, this.uploadDirForImg);

            if (Utility.IsNumeric(this.fileListPageSize))
            {
                Update(SettingConstants.FILELISTPAGESIZE, this.fileListPageSize);
            }

            if (Utility.IsNumeric(this.txtResizeWidthUpload))
            {
                Update(SettingConstants.RESIZEWIDTHUPLOAD, this.txtResizeWidthUpload);
            }

            if (Utility.IsNumeric(this.txtResizeHeightUpload))
            {
                Update(SettingConstants.RESIZEHEIGHTUPLOAD, this.txtResizeHeightUpload);
            }

            if (Utility.IsNumeric(this.txtResizeWidth))
            {
                Update(SettingConstants.RESIZEWIDTH, this.txtResizeWidth);
            }

            if (Utility.IsNumeric(this.txtResizeHeight))
            {
                Update(SettingConstants.RESIZEHEIGHT, this.txtResizeHeight);
            }

            Update(SettingConstants.INJECTJS, this.injectSyntaxJs.ToString());

            if (Utility.IsUnit(this.txtWidth))
            {
                Update(SettingConstants.WIDTH, this.txtWidth);
            }

            if (Utility.IsUnit(this.txtHeight))
            {
                Update(SettingConstants.HEIGHT, this.txtHeight);
            }

            Update(SettingConstants.BLANKTEXT, this.txtBlanktext);
            Update(SettingConstants.CSS, this.cssUrl);
            Update(SettingConstants.TEMPLATEFILES, this.templateUrl);
            Update(SettingConstants.CUSTOMJSFILE, this.customJsFile);
            Update(SettingConstants.CONFIG, this.configUrl);

            Update(SettingConstants.BROWSERALLOWFOLLOWFOLDERPERMS, this.browAllowFollowPerms.ToString());

            var sRoles = this.browserRoles.Where(item => item.Selected)
                .Aggregate(string.Empty, (current, item) => current + (item.Value + ";"));

            if (sRoles != string.Empty)
            {
                Update(SettingConstants.ROLES, sRoles);
            }

            var settingsPortalId = this.portalSettings?.PortalId != Null.NullInteger
                ? this.portalSettings?.PortalId ?? this.HostSettingsService.HostPortalId
                : this.HostSettingsService.HostPortalId;

            // Save Toolbar Setting for every Role
            foreach (var row in this.toolbarRoleRows)
            {
                if (row.RoleName == null)
                {
                    continue;
                }

                if (row.RoleName.Equals(UnauthenticatedUsersRoleName, StringComparison.Ordinal))
                {
                    EditorController.AddOrUpdateEditorHostSetting($"{key}toolb#{"-1"}", row.SelectedToolbar);
                }
                else
                {
                    RoleInfo objRole = RoleController.Instance.GetRoleByName(settingsPortalId, row.RoleName);

                    EditorController.AddOrUpdateEditorHostSetting($"{key}toolb#{objRole.RoleID}", row.SelectedToolbar);
                }
            }

            // Save Upload File Limit Setting for every Role
            foreach (var row in this.uploadSizeRows)
            {
                if (row.RoleName == null || string.IsNullOrEmpty(row.SizeLimit))
                {
                    continue;
                }

                if (row.RoleName.Equals(UnauthenticatedUsersRoleName, StringComparison.Ordinal))
                {
                    EditorController.AddOrUpdateEditorHostSetting(
                        $"{key}-1{SettingConstants.UPLOADFILELIMITS}", row.SizeLimit);
                }
                else
                {
                    RoleInfo objRole = RoleController.Instance.GetRoleByName(settingsPortalId, row.RoleName);

                    EditorController.AddOrUpdateEditorHostSetting(
                        $"{key}{objRole.RoleID}#{SettingConstants.UPLOADFILELIMITS}", row.SizeLimit);
                }
            }
        }

        /// <summary>Writes every rendered editor config setting through a sink.</summary>
        /// <param name="write">Receives the setting name (the property name) and the value.</param>
        /// <remarks>
        /// The <c>foreach (PropertyInfo ...)</c> block <c>SaveModuleSettings</c> and
        /// <c>SaveSettingsByKey</c> held identically; the only difference was where the value went,
        /// which is the <paramref name="write"/> sink. A setting that is not rendered is skipped,
        /// just as <c>Utility.FindControl</c> returning <see langword="null"/> skipped it.
        /// </remarks>
        private void WriteEditorConfigSettings(Action<string, string> write)
        {
            foreach (PropertyInfo info in SettingsUtil.GetEditorConfigProperties())
            {
                switch (info.PropertyType.Name)
                {
                    case "Decimal":
                    case "Int32":
                    case "String":
                        if (this.editorConfigSettingsByName.TryGetValue(info.Name, out var textSetting))
                        {
                            write(info.Name, textSetting.StringValue);
                        }

                        break;
                    case "Boolean":
                        if (this.editorConfigSettingsByName.TryGetValue(info.Name, out var boolSetting))
                        {
                            write(info.Name, boolSetting.BoolValue.ToString());
                        }

                        break;
                }

                switch (info.Name)
                {
                    case "ContentsLangDirection":
                    case "EnterMode":
                    case "ShiftEnterMode":
                    case "ToolbarLocation":
                    case "DefaultLinkType":
                        if (this.editorConfigSettingsByName.TryGetValue(info.Name, out var selectSetting)
                            && !string.IsNullOrEmpty(selectSetting.StringValue))
                        {
                            write(info.Name, selectSetting.StringValue);
                        }

                        break;
                    case "CodeMirror":
                        foreach (var codeMirrorInfo in typeof(CodeMirror).GetProperties()
                            .Where(codeMirrorInfo => !codeMirrorInfo.Name.Equals("Theme", StringComparison.Ordinal)))
                        {
                            this.WriteSubSetting(codeMirrorInfo, write);
                        }

                        break;
                    case "WordCount":
                        foreach (var wordCountInfo in typeof(WordCountConfig).GetProperties())
                        {
                            this.WriteSubSetting(wordCountInfo, write);
                        }

                        break;
                }
            }
        }

        /// <summary>Writes one <c>CodeMirror</c>/<c>WordCount</c> sub setting through a sink.</summary>
        /// <param name="info">The property of the sub setting.</param>
        /// <param name="write">Receives the setting name and the value.</param>
        private void WriteSubSetting(PropertyInfo info, Action<string, string> write)
        {
            switch (info.PropertyType.Name)
            {
                case "String":
                    if (this.editorConfigSettingsByName.TryGetValue(info.Name, out var textSetting))
                    {
                        write(info.Name, textSetting.StringValue);
                    }

                    break;

                case "Boolean":
                    if (this.editorConfigSettingsByName.TryGetValue(info.Name, out var boolSetting))
                    {
                        write(info.Name, boolSetting.BoolValue.ToString());
                    }

                    break;
            }
        }

        /// <summary>Exports the settings.</summary>
        /// <returns>Returns the exported EditorProviderSettings.</returns>
        private EditorProviderSettings ExportSettings()
        {
            var exportSettings = new EditorProviderSettings { SettingMode = SettingsMode.Default };

            exportSettings.SettingMode = this.CurrentSettingsMode;

            // Export all Editor config settings
            foreach (var info in SettingsUtil.GetEditorConfigProperties())
            {
                this.editorConfigSettingsByName.TryGetValue(info.Name, out var setting);

                switch (info.PropertyType.Name)
                {
                    case "String":
                        if (setting != null && !string.IsNullOrEmpty(setting.StringValue))
                        {
                            info.SetValue(exportSettings.Config, setting.StringValue, null);
                        }

                        break;
                    case "Int32":
                        if (setting != null && !string.IsNullOrEmpty(setting.StringValue))
                        {
                            info.SetValue(exportSettings.Config, int.Parse(setting.StringValue, CultureInfo.InvariantCulture), null);
                        }

                        break;
                    case "Decimal":
                        if (setting != null && !string.IsNullOrEmpty(setting.StringValue))
                        {
                            info.SetValue(exportSettings.Config, decimal.Parse(setting.StringValue, CultureInfo.InvariantCulture), null);
                        }

                        break;
                    case "Boolean":
                        if (setting != null)
                        {
                            info.SetValue(exportSettings.Config, setting.BoolValue, null);
                        }

                        break;
                }

                switch (info.Name)
                {
                    case "ToolbarLocation":
                        if (setting != null && !string.IsNullOrEmpty(setting.StringValue))
                        {
                            info.SetValue(
                                exportSettings.Config,
                                Enum.Parse<ToolBarLocation>(setting.StringValue),
                                null);
                        }

                        break;
                    case "DefaultLinkType":
                        if (setting != null && !string.IsNullOrEmpty(setting.StringValue))
                        {
                            info.SetValue(
                                exportSettings.Config,
                                Enum.Parse<LinkType>(setting.StringValue),
                                null);
                        }

                        break;
                    case "EnterMode":
                    case "ShiftEnterMode":
                        if (setting != null && !string.IsNullOrEmpty(setting.StringValue))
                        {
                            info.SetValue(
                                exportSettings.Config,
                                Enum.Parse<EnterModus>(setting.StringValue),
                                null);
                        }

                        break;
                    case "ContentsLangDirection":
                        if (setting != null && !string.IsNullOrEmpty(setting.StringValue))
                        {
                            info.SetValue(
                                exportSettings.Config,
                                Enum.Parse<LanguageDirection>(setting.StringValue),
                                null);
                        }

                        break;
                    case "CodeMirror":
                        foreach (var codeMirrorInfo in typeof(CodeMirror).GetProperties()
                            .Where(codeMirrorInfo => !codeMirrorInfo.Name.Equals("Theme", StringComparison.Ordinal)))
                        {
                            this.ExportSubSetting(codeMirrorInfo, exportSettings.Config.CodeMirror);
                        }

                        break;
                    case "WordCount":
                        foreach (var wordCountInfo in typeof(WordCountConfig).GetProperties())
                        {
                            this.ExportSubSetting(wordCountInfo, exportSettings.Config.WordCount);
                        }

                        break;
                }
            }

            ///////////////////
            exportSettings.Config.Skin = this.ddlSkin;
            exportSettings.Config.CodeMirror.Theme = this.codeMirrorTheme;
            exportSettings.Browser = this.ddlBrowser;
            exportSettings.BrowserAllowFollowFolderPerms = this.browAllowFollowPerms;
            exportSettings.ImageButton = this.ddlImageButton;
            exportSettings.FileListViewMode =
                Enum.Parse<FileListView>(this.fileListViewMode);
            exportSettings.DefaultLinkMode = Enum.Parse<LinkMode>(this.defaultLinkMode);
            exportSettings.UseAnchorSelector = this.useAnchorSelector;
            exportSettings.ShowPageLinksTabFirst = this.showPageLinksTabFirst;
            exportSettings.OverrideFileOnUpload = this.overrideFileOnUpload;
            exportSettings.SubDirs = this.cbBrowserDirs;
            exportSettings.HostBrowserRootDir = this.hostBrowserRootDir;
            exportSettings.BrowserRootDirId = int.Parse(this.browserRootDir, CultureInfo.InvariantCulture);
            exportSettings.HostBrowserRootDirForImg = this.hostBrowserRootDirForImg;
            exportSettings.BrowserRootDirForImgId = int.Parse(this.browserRootDirForImg, CultureInfo.InvariantCulture);
            exportSettings.HostUploadDir = this.hostUploadDir;
            exportSettings.UploadDirId = int.Parse(this.uploadDir, CultureInfo.InvariantCulture);
            exportSettings.HostUploadDirForImg = this.hostUploadDirForImg;
            exportSettings.UploadDirForImgId = int.Parse(this.uploadDirForImg, CultureInfo.InvariantCulture);

            if (Utility.IsNumeric(this.fileListPageSize))
            {
                exportSettings.FileListPageSize = int.Parse(this.fileListPageSize, CultureInfo.InvariantCulture);
            }

            if (Utility.IsNumeric(this.txtResizeWidthUpload))
            {
                exportSettings.ResizeWidthUpload = int.Parse(this.txtResizeWidthUpload, CultureInfo.InvariantCulture);
            }

            if (Utility.IsNumeric(this.txtResizeHeightUpload))
            {
                exportSettings.ResizeHeightUpload = int.Parse(this.txtResizeHeightUpload, CultureInfo.InvariantCulture);
            }

            if (Utility.IsNumeric(this.txtResizeWidth))
            {
                exportSettings.ResizeWidth = int.Parse(this.txtResizeWidth, CultureInfo.InvariantCulture);
            }

            if (Utility.IsNumeric(this.txtResizeHeight))
            {
                exportSettings.ResizeHeight = int.Parse(this.txtResizeHeight, CultureInfo.InvariantCulture);
            }

            exportSettings.InjectSyntaxJs = this.injectSyntaxJs;

            if (Utility.IsUnit(this.txtWidth))
            {
                exportSettings.EditorWidth = this.txtWidth;
            }

            if (Utility.IsUnit(this.txtHeight))
            {
                exportSettings.EditorHeight = this.txtHeight;
            }

            exportSettings.BlankText = this.txtBlanktext;
            exportSettings.Config.ContentsCss = this.cssUrl;
            exportSettings.Config.Templates_Files = this.templateUrl;
            exportSettings.CustomJsFile = this.customJsFile;
            exportSettings.Config.CustomConfig = this.configUrl;

            this.SetBrowserRoles(exportSettings);
            this.SetToolbarRoles(exportSettings);
            this.SetUploadSizeRoles(exportSettings);

            return exportSettings;
        }

        /// <summary>Exports one <c>CodeMirror</c>/<c>WordCount</c> sub setting onto its config object.</summary>
        /// <param name="info">The property of the sub setting.</param>
        /// <param name="target">The <c>CodeMirror</c>/<c>WordCount</c> configuration the value is written to.</param>
        private void ExportSubSetting(PropertyInfo info, object target)
        {
            if (!this.editorConfigSettingsByName.TryGetValue(info.Name, out var setting))
            {
                return;
            }

            switch (info.PropertyType.Name)
            {
                case "String":
                    if (!string.IsNullOrEmpty(setting.StringValue))
                    {
                        info.SetValue(target, setting.StringValue, null);
                    }

                    break;

                case "Boolean":
                    info.SetValue(target, setting.BoolValue, null);
                    break;
            }
        }

        /// <summary>Sets the browser roles.</summary>
        /// <param name="exportSettings">The export settings.</param>
        private void SetBrowserRoles(EditorProviderSettings exportSettings)
        {
            var sRoles = this.browserRoles.Where(item => item.Selected)
                                    .Aggregate(string.Empty, (current, item) => current + (item.Value + ";"));

            if (sRoles != string.Empty)
            {
                exportSettings.BrowserRoles = sRoles;
            }
        }

        /// <summary>Sets the toolbar roles.</summary>
        /// <param name="exportSettings">The export settings.</param>
        private void SetToolbarRoles(EditorProviderSettings exportSettings)
        {
            var listToolbarRoles = new List<ToolbarRoles>();

            // Save Toolbar Setting for every Role
            foreach (var row in this.toolbarRoleRows)
            {
                if (row.RoleName == null)
                {
                    continue;
                }

                if (row.RoleName.Equals(UnauthenticatedUsersRoleName, StringComparison.Ordinal))
                {
                    listToolbarRoles.Add(new ToolbarRoles { RoleId = -1, Toolbar = row.SelectedToolbar });
                }
                else
                {
                    this.AddOtherToolbarRoles(listToolbarRoles, row.RoleName, row.SelectedToolbar);
                }
            }

            exportSettings.ToolBarRoles = listToolbarRoles;
        }

        /// <summary>Sets the upload size roles.</summary>
        /// <param name="exportSettings">The export settings.</param>
        private void SetUploadSizeRoles(EditorProviderSettings exportSettings)
        {
            var listUploadSizeRoles = new List<UploadSizeRoles>();

            // Save Upload File Limit Setting for every Role
            foreach (var row in this.uploadSizeRows)
            {
                if (row.RoleName == null || string.IsNullOrEmpty(row.SizeLimit))
                {
                    continue;
                }

                if (row.RoleName.Equals(UnauthenticatedUsersRoleName, StringComparison.Ordinal))
                {
                    listUploadSizeRoles.Add(new UploadSizeRoles
                    {
                        RoleId = -1,
                        UploadFileLimit = Convert.ToInt32(row.SizeLimit, CultureInfo.InvariantCulture),
                    });
                }
                else
                {
                    this.AddOtherUploadRoles(row.RoleName, listUploadSizeRoles, row.SizeLimit);
                }
            }

            exportSettings.UploadSizeRoles = listUploadSizeRoles;
        }

        /// <summary>Adds the other upload roles.</summary>
        /// <param name="roleName">Name of the role.</param>
        /// <param name="listUploadSizeRoles">The list upload size roles.</param>
        /// <param name="sizeLimit">The size limit.</param>
        private void AddOtherUploadRoles(string roleName, List<UploadSizeRoles> listUploadSizeRoles, string sizeLimit)
        {
            if (this.IsAllInstances)
            {
                listUploadSizeRoles.AddRange(
                    from IPortalInfo portal in PortalController.Instance.GetPortals()
                    select RoleController.Instance.GetRoleByName(portal.PortalId, roleName)
                    into objRole
                    select new UploadSizeRoles()
                    {
                        RoleId = objRole.RoleID,
                        UploadFileLimit = Convert.ToInt32(sizeLimit, CultureInfo.InvariantCulture),
                    });
            }
            else
            {
                var objRole = RoleController.Instance.GetRoleByName(this.portalSettings.PortalId, roleName);
                listUploadSizeRoles.Add(new UploadSizeRoles
                {
                    RoleId = objRole.RoleID,
                    UploadFileLimit = Convert.ToInt32(sizeLimit, CultureInfo.InvariantCulture),
                });
            }
        }

        /// <summary>Adds the other toolbar roles.</summary>
        /// <param name="listToolbarRoles">The list toolbar roles.</param>
        /// <param name="roleName">Name of the role.</param>
        /// <param name="value">The value.</param>
        private void AddOtherToolbarRoles(List<ToolbarRoles> listToolbarRoles, string roleName, string value)
        {
            if (this.IsAllInstances)
            {
                listToolbarRoles.AddRange(
                    from IPortalInfo portal in PortalController.Instance.GetPortals()
                    select RoleController.Instance.GetRoleByName(portal.PortalId, roleName)
                    into objRole
                    select new ToolbarRoles { RoleId = objRole.RoleID, Toolbar = value });
            }
            else
            {
                var objRole = RoleController.Instance.GetRoleByName(this.portalSettings.PortalId, roleName);
                listToolbarRoles.Add(new ToolbarRoles { RoleId = objRole.RoleID, Toolbar = value });
            }
        }
    }
}
