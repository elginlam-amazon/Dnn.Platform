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

    using DNNConnect.CKEditorProvider.Constants;
    using DNNConnect.CKEditorProvider.Objects;
    using DNNConnect.CKEditorProvider.Utilities;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Localization;

    using Globals = DotNetNuke.Common.Globals;

    /// <content>
    /// <c>FillSettings</c> and <c>SetLanguage</c>: the two methods that pushed the loaded settings
    /// and the localized captions onto the UI.
    /// </content>
    public partial class CKEditorOptions
    {
        /// <summary>Fills the setting controls with the loaded Setting Values.</summary>
        /// <param name="importedSettings">The imported settings.</param>
        /// <param name="changeMode">if set to <see langword="true"/> [change mode].</param>
        private void FillSettings(EditorProviderSettings importedSettings, bool changeMode = true)
        {
            // Editor config settings
            foreach (PropertyInfo info in SettingsUtil.GetEditorConfigProperties())
            {
                object value = null;

                if (!info.Name.Equals("CodeMirror", StringComparison.Ordinal) && !info.Name.Equals("WordCount", StringComparison.Ordinal))
                {
                    value = info.GetValue(importedSettings.Config, null);

                    if (value == null)
                    {
                        continue;
                    }
                }

                switch (info.PropertyType.Name)
                {
                    case "Decimal":
                    case "Int32":
                    case "String":
                        this.SetEditorConfigText(info.Name, value?.ToString());
                        break;
                    case "Boolean":
                        this.SetEditorConfigChecked(info.Name, (bool)value);
                        break;
                }

                switch (info.Name)
                {
                    case "ContentsLangDirection":
                    case "EnterMode":
                    case "ShiftEnterMode":
                    case "ToolbarLocation":
                    case "DefaultLinkType":
                        this.SetEditorConfigSelected(info.Name, value.ToString());
                        break;
                    case "CodeMirror":
                        foreach (var codeMirrorInfo in
                            typeof(CodeMirror).GetProperties().Where(property => !property.Name.Equals("Theme", StringComparison.Ordinal)))
                        {
                            value = codeMirrorInfo.GetValue(importedSettings.Config.CodeMirror, null);

                            if (value == null)
                            {
                                continue;
                            }

                            switch (codeMirrorInfo.PropertyType.Name)
                            {
                                case "String":
                                    this.SetEditorConfigText(codeMirrorInfo.Name, value.ToString());
                                    break;

                                case "Boolean":
                                    this.SetEditorConfigChecked(codeMirrorInfo.Name, (bool)value);
                                    break;
                            }
                        }

                        break;
                    case "WordCount":
                        foreach (var wordCountInfo in typeof(WordCountConfig).GetProperties())
                        {
                            value = wordCountInfo.GetValue(importedSettings.Config.WordCount, null);

                            if (value == null)
                            {
                                continue;
                            }

                            switch (wordCountInfo.PropertyType.Name)
                            {
                                case "String":
                                    this.SetEditorConfigText(wordCountInfo.Name, value.ToString());
                                    break;

                                case "Boolean":
                                    this.SetEditorConfigChecked(wordCountInfo.Name, (bool)value);
                                    break;
                            }
                        }

                        break;
                }
            }

            ///////////////////
            if (!string.IsNullOrEmpty(importedSettings.Config.Skin)
                && HasOption(this.ddlSkinItems, importedSettings.Config.Skin))
            {
                this.ddlSkin = importedSettings.Config.Skin;
            }

            if (!string.IsNullOrEmpty(importedSettings.Config.CodeMirror.Theme)
                && HasOption(this.codeMirrorThemeItems, importedSettings.Config.CodeMirror.Theme))
            {
                this.codeMirrorTheme = importedSettings.Config.CodeMirror.Theme;
            }

            if (!string.IsNullOrEmpty(importedSettings.Browser)
                && HasOption(this.ddlBrowserItems, importedSettings.Browser))
            {
                this.ddlBrowser = importedSettings.Browser;
            }

            if (!string.IsNullOrEmpty(importedSettings.ImageButton)
                && HasOption(this.ddlImageButtonItems, importedSettings.ImageButton))
            {
                this.ddlImageButton = importedSettings.ImageButton;
            }

            this.fileListPageSize = importedSettings.FileListPageSize.ToString(CultureInfo.InvariantCulture);

            this.fileListViewMode = importedSettings.FileListViewMode.ToString();
            this.defaultLinkMode = importedSettings.DefaultLinkMode.ToString();
            this.useAnchorSelector = importedSettings.UseAnchorSelector;
            this.showPageLinksTabFirst = importedSettings.ShowPageLinksTabFirst;

            this.cbBrowserDirs = importedSettings.SubDirs;

            this.overrideFileOnUpload = importedSettings.OverrideFileOnUpload;

            // get the all-sites settings to be able to show overridden values
            // when we're not in all-sites mode
            var allPortalsSettings = SettingsUtil.LoadEditorSettingsByKey(
                this.portalSettings,
                this.currentSettings,
                EditorController.GetEditorHostSettings(this.HostSettingsService),
                SettingConstants.HostKey,
                new List<RoleInfo>());

            this.hostBrowserRootDirReadOnly = !this.IsHostMode || this.CurrentPortalOnly;
            this.hostBrowserRootDir = this.hostBrowserRootDirReadOnly ? allPortalsSettings.HostBrowserRootDir : importedSettings.HostBrowserRootDir;

            this.browserRootDir =
                HasOption(this.folderItems, importedSettings.BrowserRootDirId.ToString(CultureInfo.InvariantCulture))
                    ? importedSettings.BrowserRootDirId.ToString(CultureInfo.InvariantCulture)
                    : "-1";

            this.hostBrowserRootDirForImgReadOnly = !this.IsHostMode || this.CurrentPortalOnly;
            this.hostBrowserRootDirForImg = this.hostBrowserRootDirForImgReadOnly ? allPortalsSettings.HostBrowserRootDirForImg : importedSettings.HostBrowserRootDirForImg;

            this.browserRootDirForImg =
                HasOption(this.folderItems, importedSettings.BrowserRootDirForImgId.ToString(CultureInfo.InvariantCulture))
                    ? importedSettings.BrowserRootDirForImgId.ToString(CultureInfo.InvariantCulture)
                    : "-1";

            this.hostUploadDirReadOnly = !this.IsHostMode || this.CurrentPortalOnly;
            this.hostUploadDir = this.hostUploadDirReadOnly ? allPortalsSettings.HostUploadDir : importedSettings.HostUploadDir;

            this.uploadDir = HasOption(this.folderItems, importedSettings.UploadDirId.ToString(CultureInfo.InvariantCulture))
                ? importedSettings.UploadDirId.ToString(CultureInfo.InvariantCulture)
                : "-1";

            this.hostUploadDirForImgReadOnly = !this.IsHostMode || this.CurrentPortalOnly;
            this.hostUploadDirForImg = this.hostUploadDirForImgReadOnly ? allPortalsSettings.HostUploadDirForImg : importedSettings.HostUploadDirForImg;

            this.uploadDirForImg = HasOption(this.folderItems, importedSettings.UploadDirForImgId.ToString(CultureInfo.InvariantCulture))
                ? importedSettings.UploadDirForImgId.ToString(CultureInfo.InvariantCulture)
                : "-1";

            var homeDirectory = this.portalSettings?.HomeDirectoryMapPath ?? Globals.HostMapPath;
            var configFolderInfo =
                Utility.ConvertFilePathToFolderInfo(
                    !string.IsNullOrEmpty(this.configFolder)
                        ? Path.Combine(homeDirectory, this.configFolder)
                        : homeDirectory,
                    this.portalSettings);

            this.exportDir = configFolderInfo != null
                             && HasOption(this.folderItems, configFolderInfo.FolderID.ToString(CultureInfo.InvariantCulture))
                ? configFolderInfo.FolderID.ToString(CultureInfo.InvariantCulture)
                : "-1";

            this.exportFileName = $"CKEditorSettings-{importedSettings.SettingMode}.xml";

            switch (importedSettings.SettingMode)
            {
                case SettingsMode.Host:
                    this.exportFileName = "CKEditorSettings-Host.xml";
                    break;
                case SettingsMode.Portal:
                    this.exportFileName = $"CKEditorSettings-{importedSettings.SettingMode}-{this.portalSettings?.PortalId ?? Null.NullInteger}.xml";
                    break;
                case SettingsMode.Page:
                    this.exportFileName = $"CKEditorSettings-{importedSettings.SettingMode}-{this.CurrentOrSelectedTabId}.xml";
                    break;
                case SettingsMode.ModuleInstance:
                    this.exportFileName = $"CKEditorSettings-{importedSettings.SettingMode}-{this.ModuleId}.xml";
                    break;
            }

            this.txtResizeWidthUpload = importedSettings.ResizeWidthUpload.ToString(CultureInfo.InvariantCulture);

            this.txtResizeHeightUpload = importedSettings.ResizeHeightUpload.ToString(CultureInfo.InvariantCulture);

            this.txtResizeWidth = importedSettings.ResizeWidth.ToString(CultureInfo.InvariantCulture);

            this.txtResizeHeight = importedSettings.ResizeHeight.ToString(CultureInfo.InvariantCulture);

            this.injectSyntaxJs = importedSettings.InjectSyntaxJs;

            if (Utility.IsUnit(importedSettings.Config.Width))
            {
                this.txtWidth = importedSettings.Config.Width;
            }

            if (Utility.IsUnit(importedSettings.Config.Height))
            {
                this.txtHeight = importedSettings.Config.Height;
            }

            this.txtBlanktext = Convert.ToString(importedSettings.BlankText, CultureInfo.InvariantCulture);

            this.FillFileUploadSettings(importedSettings.UploadSizeRoles);

            if (!string.IsNullOrEmpty(importedSettings.Config.ContentsCss))
            {
                this.cssUrl = this.ReFormatURL(importedSettings.Config.ContentsCss);
            }

            if (!string.IsNullOrEmpty(importedSettings.Config.Templates_Files))
            {
                this.templateUrl = this.ReFormatURL(importedSettings.Config.Templates_Files);
            }

            if (!string.IsNullOrEmpty(importedSettings.CustomJsFile))
            {
                this.customJsFile = this.ReFormatURL(importedSettings.CustomJsFile);
            }

            if (!string.IsNullOrEmpty(importedSettings.Config.CustomConfig))
            {
                this.configUrl = this.ReFormatURL(importedSettings.Config.CustomConfig);
            }

            this.browAllowFollowPerms = importedSettings.BrowserAllowFollowFolderPerms;

            if (!string.IsNullOrEmpty(importedSettings.BrowserRoles))
            {
                string sRoles = importedSettings.BrowserRoles;

                if (sRoles.Length >= 1 && sRoles.Contains(';'))
                {
                    string[] roles = sRoles.Split(';');

                    foreach (string sRoleName in roles)
                    {
                        if (Utility.IsNumeric(sRoleName))
                        {
                            var roleItem = this.browserRoles.FirstOrDefault(
                                item => string.Equals(item.Value, sRoleName, StringComparison.Ordinal));

                            if (roleItem != null)
                            {
                                roleItem.Selected = true;
                            }
                        }
                        else
                        {
                            var roleItem = this.browserRoles.FirstOrDefault(
                                item => string.Equals(item.Text, sRoleName, StringComparison.Ordinal));

                            if (roleItem != null)
                            {
                                roleItem.Selected = true;
                            }
                        }
                    }
                }
            }

            if (!changeMode)
            {
                return;
            }

            switch (importedSettings.SettingMode)
            {
                case SettingsMode.Portal:
                    this.setModeSelectedIndex = 0;
                    break;
                case SettingsMode.Page:
                    this.setModeSelectedIndex = 1;
                    break;
                case SettingsMode.ModuleInstance:
                    this.setModeSelectedIndex = 2;
                    break;
            }
        }

        /// <summary>Localizes every caption of the settings UI.</summary>
        /// <remarks>This is <c>SetLanguage</c>, key for key.</remarks>
        private void SetLanguage()
        {
            this.lblHeader = Localization.GetString("lblHeader.Text", this.ResXFile, this.LangCode);

            this.providerVersion = "<strong>DNN Connect CKEditor™ Provider</strong> ";

            this.lblPortal = $"<strong>{Localization.GetString("lblPortal.Text", this.ResXFile, this.LangCode)}</strong> ";
            this.lblPage = $"<strong>{Localization.GetString("lblPage.Text", this.ResXFile, this.LangCode)}</strong> ";
            this.lblModType = $"<strong>{Localization.GetString("lblModType.Text", this.ResXFile, this.LangCode)}</strong> ";
            this.lblModName = $"<strong>{Localization.GetString("lblModName.Text", this.ResXFile, this.LangCode)}</strong> ";
            this.lblModInst = $"<strong>{Localization.GetString("lblModInst.Text", this.ResXFile, this.LangCode)}</strong> ";
            this.lblUName = $"<strong>{Localization.GetString("lblUName.Text", this.ResXFile, this.LangCode)}</strong> ";
            this.lblMainSet = Localization.GetString("lblMainSet.Text", this.ResXFile, this.LangCode);
            this.lblSettings = Localization.GetString("lblSettings.Text", this.ResXFile, this.LangCode);
            this.lblSetFor = Localization.GetString("lblSetFor.Text", this.ResXFile, this.LangCode);
            this.lblBrowser = Localization.GetString("lblBrowser.Text", this.ResXFile, this.LangCode);

            this.lblBlanktext = Localization.GetString("lblBlanktext.Text", this.ResXFile, this.LangCode);
            this.txtBlanktextToolTip = Localization.GetString("BlanktextTT.Text", this.ResXFile, this.LangCode);

            this.lblBrowsSec = Localization.GetString("lblBrowsSec.Text", this.ResXFile, this.LangCode);
            this.lblBrowAllow = Localization.GetString("lblBrowAllow.Text", this.ResXFile, this.LangCode);

            // The nine captions below were the <asp:label> elements whose default text was written
            // inline in CKEditorOptions.ascx and which DNN's automatic control localization
            // replaced from the "<control id>.Text" key of the resource file. A Blazor component
            // has no control tree to localize, so the same keys are read here explicitly, keeping
            // the markup's text as the fallback for the two keys the resource file does not carry
            // (lblImageButton.Text and UploadFolderLabelForImg.Text), exactly as before.
            this.lblImageButton = this.LocalizeOrDefault("lblImageButton.Text", "Image Button:");
            this.lblBrowAllowFollowPerms = this.LocalizeOrDefault("lblBrowAllowFollowPerms.Text", "Allow folder users");
            this.hostBrowserRootFolderLabel = this.LocalizeOrDefault("HostBrowserRootFolder.Text", "Browser Root Folder Name for all portals (overrides setting below)");
            this.hostBrowserRootFolderForImgLabel = this.LocalizeOrDefault("HostBrowserRootFolderForImg.Text", "Browser Images Root Folder Name for all portals (overrides setting below)");
            this.browserRootFolderForImgLabel = this.LocalizeOrDefault("BrowserRootFolderForImg.Text", "Browser Root Folder for images");
            this.hostUploadFolderLabel = this.LocalizeOrDefault("HostUploadFolderLabel.Text", "Default Upload Folder for all portals (overrides setting below)");
            this.hostUploadFolderLabelForImg = this.LocalizeOrDefault("HostUploadFolderLabelForImg.Text", "Default Upload Folder for images for all portals (overrides setting below)");
            this.uploadFolderLabelForImg = this.LocalizeOrDefault("UploadFolderLabelForImg.Text", "Default Upload Folder for images");
            this.sizeLimitLabel = this.LocalizeOrDefault("SizeLimitLabel.Text", string.Empty);

            this.browserRootFolderLabel = Localization.GetString("BrowserRootFolder.Text", this.ResXFile, this.LangCode);
            this.overrideFileOnUploadLabel = Localization.GetString("OverrideFileOnUploadLabel.Text", this.ResXFile, this.LangCode);
            this.lblBrowserDirs = Localization.GetString("lblBrowserDirs.Text", this.ResXFile, this.LangCode);
            this.uploadFolderLabel = Localization.GetString("UploadFolderLabel.Text", this.ResXFile, this.LangCode);
            this.lblCustomConfig = Localization.GetString("lblCustomConfig.Text", this.ResXFile, this.LangCode);
            this.lblInjectSyntaxJs = Localization.GetString("lblInjectSyntaxJs.Text", this.ResXFile, this.LangCode);
            this.lblWidth = Localization.GetString("lblWidth.Text", this.ResXFile, this.LangCode);
            this.lblHeight = Localization.GetString("lblHeight.Text", this.ResXFile, this.LangCode);
            this.lblEditorConfig = Localization.GetString("lblEditorConfig.Text", this.ResXFile, this.LangCode);
            this.lblCssurl = Localization.GetString("lblCSSURL.Text", this.ResXFile, this.LangCode);
            this.lblToolbars = Localization.GetString("lblToolbars.Text", this.ResXFile, this.LangCode);
            this.uploadFileLimitLabel = Localization.GetString("UploadFileLimitLabel.Text", this.ResXFile, this.LangCode);
            this.lblTemplFiles = Localization.GetString("lblTemplFiles.Text", this.ResXFile, this.LangCode);
            this.customJsFileLabel = Localization.GetString("CustomJsFileLabel.Text", this.ResXFile, this.LangCode);
            this.lblCustomToolbars = Localization.GetString("lblCustomToolbars.Text", this.ResXFile, this.LangCode);
            this.lblToolbarList = Localization.GetString("lblToolbarList.Text", this.ResXFile, this.LangCode);
            this.lblToolbName = Localization.GetString("lblToolbName.Text", this.ResXFile, this.LangCode);
            this.lblToolbSet = Localization.GetString("lblToolbSet.Text", this.ResXFile, this.LangCode);
            this.lblResizeWidthUpload = Localization.GetString("lblResizeWidthUpload.Text", this.ResXFile, this.LangCode);
            this.lblResizeHeightUpload = Localization.GetString("lblResizeHeightUpload.Text", this.ResXFile, this.LangCode);
            this.lblResizeWidth = Localization.GetString("lblResizeWidth.Text", this.ResXFile, this.LangCode);
            this.lblResizeHeight = Localization.GetString("lblResizeHeight.Text", this.ResXFile, this.LangCode);
            this.ImportText = Localization.GetString("lnkImport.Text", this.ResXFile, this.LangCode);
            this.lblToolbarPriority = Localization.GetString("lblToolbarPriority.Text", this.ResXFile, this.LangCode);
            this.toolbarGroupsLabel = Localization.GetString("ToolbarGroupsLabel.Text", this.ResXFile, this.LangCode);
            this.lblSkin = Localization.GetString("lblSkin.Text", this.ResXFile, this.LangCode);
            this.codeMirrorLabel = Localization.GetString("CodeMirrorLabel.Text", this.ResXFile, this.LangCode);
            this.waitLabel = Localization.GetString("Wait.Text", this.ResXFile, this.LangCode);
            this.waitMessageLabel = Localization.GetString("WaitMessage.Text", this.ResXFile, this.LangCode);
            this.editorConfigWarning = Localization.GetString("EditorConfigWarning.Text", this.ResXFile, this.LangCode);

            this.fileListPageSizeLabel = Localization.GetString("FileListPageSizeLabel.Text", this.ResXFile, this.LangCode);
            this.fileListViewModeLabel = Localization.GetString("FileListViewModeLabel.Text", this.ResXFile, this.LangCode);
            this.lblUseAnchorSelector = Localization.GetString("lblUseAnchorSelector.Text", this.ResXFile, this.LangCode);
            this.lblShowPageLinksTabFirst = Localization.GetString("lblShowPageLinksTabFirst.Text", this.ResXFile, this.LangCode);

            this.AddToolbarText = Localization.GetString("AddToolbar.Text", this.ResXFile, this.LangCode);
            this.CancelToolbarText = Localization.GetString("CancelToolbar.Text", this.ResXFile, this.LangCode);
            this.EditToolbarText = Localization.GetString("EditToolbar.Text", this.ResXFile, this.LangCode);
            this.DeleteToolbarText = Localization.GetString("DeleteToolbar.Text", this.ResXFile, this.LangCode);

            this.ExportText = Localization.GetString("lnkExport.Text", this.ResXFile, this.LangCode);

            this.ExportNowText = Localization.GetString("ExportNow.Text", this.ResXFile, this.LangCode);
            this.ImportNowText = Localization.GetString("ImportNow.Text", this.ResXFile, this.LangCode);

            this.OkText = Localization.GetString("btnOK.Text", this.ResXFile, this.LangCode);

            this.setModeItems[0].Text = Localization.GetString("Portal.Text", this.ResXFile, this.LangCode);
            this.setModeItems[1].Text = Localization.GetString("Page.Text", this.ResXFile, this.LangCode);

            if (this.setModeItems.Count.Equals(3))
            {
                this.setModeItems[2].Text = Localization.GetString("ModuleInstance.Text", this.ResXFile, this.LangCode);
            }

            this.fileListViewModeItems[0].Text = Localization.GetString("DetailView.Text", this.ResXFile, this.LangCode);
            this.fileListViewModeItems[1].Text = Localization.GetString("ListView.Text", this.ResXFile, this.LangCode);
            this.fileListViewModeItems[2].Text = Localization.GetString("IconsView.Text", this.ResXFile, this.LangCode);

            this.defaultLinkModeLabel = Localization.GetString("DefaultLinkModeLabel.Text", this.ResXFile, this.LangCode);

            this.defaultLinkModeItems[0].Text = Localization.GetString("DefaultLinkMode0.Text", this.ResXFile, this.LangCode);
            this.defaultLinkModeItems[1].Text = Localization.GetString("DefaultLinkMode1.Text", this.ResXFile, this.LangCode);
            this.defaultLinkModeItems[2].Text = Localization.GetString("DefaultLinkMode2.Text", this.ResXFile, this.LangCode);
            this.defaultLinkModeItems[3].Text = Localization.GetString("DefaultLinkMode3.Text", this.ResXFile, this.LangCode);
        }
    }
}
