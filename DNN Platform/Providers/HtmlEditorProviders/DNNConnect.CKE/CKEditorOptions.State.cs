// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider
{
    using System.Collections.Generic;

    using DNNConnect.CKEditorProvider.Controls;
    using DNNConnect.CKEditorProvider.Objects;

    /// <content>
    /// The component state that replaced the 1169 designer fields of
    /// <c>CKEditorOptions.ascx.designer.cs</c> and the four <c>asp:HiddenField</c>s. Every field
    /// here is bound by <c>CKEditorOptions.razor</c>; the names are the names of the server
    /// controls they replace, so the save path reads exactly as it did.
    /// </content>
    public partial class CKEditorOptions
    {
        /// <summary>The settings currently rendered for the reflection driven editor config UI.</summary>
        private readonly List<EditorConfigSetting> editorConfigSettings = new List<EditorConfigSetting>();

        /// <summary>The editor config settings by property name.</summary>
        /// <remarks>Replaces <c>Utility.FindControl&lt;T&gt;(this.EditorConfigHolder, info.Name)</c>, which was removed.</remarks>
        private readonly Dictionary<string, EditorConfigSetting> editorConfigSettingsByName =
            new Dictionary<string, EditorConfigSetting>(System.StringComparer.Ordinal);

        /// <summary>The items of the "Modify Settings for" radio button list.</summary>
        private readonly List<OptionItem> setModeItems = new List<OptionItem>();

        /// <summary>The items of the skin drop down.</summary>
        private readonly List<OptionItem> ddlSkinItems = new List<OptionItem>();

        /// <summary>The items of the CodeMirror theme drop down.</summary>
        private readonly List<OptionItem> codeMirrorThemeItems = new List<OptionItem>();

        /// <summary>The items of the file browser drop down.</summary>
        private readonly List<OptionItem> ddlBrowserItems = new List<OptionItem>();

        /// <summary>The items of the image button drop down.</summary>
        private readonly List<OptionItem> ddlImageButtonItems = new List<OptionItem>();

        /// <summary>The items of the five folder drop downs.</summary>
        private readonly List<OptionItem> folderItems = new List<OptionItem>();

        /// <summary>The items of the file list view mode drop down.</summary>
        private readonly List<OptionItem> fileListViewModeItems = new List<OptionItem>();

        /// <summary>The items of the default link mode drop down.</summary>
        private readonly List<OptionItem> defaultLinkModeItems = new List<OptionItem>();

        /// <summary>The names of the available toolbar sets.</summary>
        private readonly List<OptionItem> availableToolbarNames = new List<OptionItem>();

        /// <summary>The items of the toolbar priority drop down.</summary>
        private readonly List<OptionItem> toolbarPriorityItems = new List<OptionItem>();

        /// <summary>The file browser security roles (the <c>chblBrowsGr</c> check box list).</summary>
        private readonly List<CheckItem> browserRoles = new List<CheckItem>();

        /// <summary>The rows of the toolbar per role grid (<c>gvToolbars</c>).</summary>
        private readonly List<ToolbarRoleRow> toolbarRoleRows = new List<ToolbarRoleRow>();

        /// <summary>The rows of the upload size limit per role grid (<c>UploadFileLimits</c>).</summary>
        private readonly List<UploadSizeRow> uploadSizeRows = new List<UploadSizeRow>();

        /// <summary>Whether the component already rendered once. Replaces <c>Page.IsPostBack</c>.</summary>
        private bool isReRender;

        /// <summary>The value of the <c>LastTabId</c> hidden field.</summary>
        private string lastTabId = "1";

        /// <summary>The value of the <c>ToolbarSet</c> hidden field.</summary>
        private string toolbarSetValue = string.Empty;

        /// <summary>The identifier of the portal the five URL controls list folders of.</summary>
        private int urlControlPortalId;

        /// <summary>The selected index of the "Modify Settings for" radio button list.</summary>
        private int setModeSelectedIndex;

        // The URL control instances, which the load path assigns ReloadFiles on.
        private UrlControl configUrlControl;
        private UrlControl cssUrlControl;
        private UrlControl templateUrlControl;
        private UrlControl customJsFileControl;
        private UrlControl importFileControl;

        // The values of the five URL controls.
        private string configUrl;
        private string cssUrl;
        private string templateUrl;
        private string customJsFile;
        private string importFileUrl;

        // Visibility of the controls BindOptionsData/LoadSettings toggled.
        private bool lblSetForVisible = true;
        private bool rBlSetModeVisible = true;
        private bool infoTabVisible = true;
        private bool infoTabHolderVisible = true;

        // The text boxes of the main and file browser settings.
        private string txtBlanktext = string.Empty;
        private string txtWidth = string.Empty;
        private string txtHeight = string.Empty;
        private string hostBrowserRootDir = string.Empty;
        private string hostBrowserRootDirForImg = string.Empty;
        private string hostUploadDir = string.Empty;
        private string hostUploadDirForImg = string.Empty;
        private string txtResizeWidthUpload = string.Empty;
        private string txtResizeHeightUpload = string.Empty;
        private string txtResizeWidth = string.Empty;
        private string txtResizeHeight = string.Empty;
        private string fileListPageSize = string.Empty;
        private string exportFileName = string.Empty;
        private string toolbarName = string.Empty;

        // Whether the four host level text boxes are read only (host mode only).
        private bool hostBrowserRootDirReadOnly;
        private bool hostBrowserRootDirForImgReadOnly;
        private bool hostUploadDirReadOnly;
        private bool hostUploadDirForImgReadOnly;

        // The selected values of the drop downs.
        private string ddlSkin = string.Empty;
        private string codeMirrorTheme = string.Empty;
        private string ddlBrowser = string.Empty;
        private string ddlImageButton = string.Empty;
        private string browserRootDir = "-1";
        private string browserRootDirForImg = "-1";
        private string uploadDir = "-1";
        private string uploadDirForImg = "-1";
        private string exportDir = "-1";
        private string fileListViewMode = string.Empty;
        private string defaultLinkMode = string.Empty;
        private string customToolbar = string.Empty;
        private string toolbarPriority = "01";

        // The check boxes.
        private bool injectSyntaxJs = true;
        private bool browAllowFollowPerms = true;
        private bool cbBrowserDirs;
        private bool overrideFileOnUpload;
        private bool useAnchorSelector = true;
        private bool showPageLinksTabFirst;

        // The labels SetLanguage() localizes.
        private string lblSettings;
        private string lblSetFor;
        private string lblMainSet;
        private string lblBrowsSec;
        private string lblCustomToolbars;
        private string lblEditorConfig;
        private string lblHeader;
        private string lblBlanktext;
        private string txtBlanktextToolTip;
        private string lblSkin;
        private string lblWidth;
        private string lblHeight;
        private string lblToolbars;
        private string lblRole;
        private string lblSelToolb;
        private string lblCustomConfig;
        private string codeMirrorLabel;
        private string lblInjectSyntaxJs;
        private string lblCssurl;
        private string lblTemplFiles;
        private string customJsFileLabel;
        private string lblBrowser;
        private string lblImageButton;
        private string lblBrowAllowFollowPerms;
        private string browAllowFollowPermsText = "Allow users with folder permissions";
        private string lblBrowAllow;
        private string hostBrowserRootFolderLabel;
        private string browserRootFolderLabel;
        private string hostBrowserRootFolderForImgLabel;
        private string browserRootFolderForImgLabel;
        private string lblBrowserDirs;
        private string hostUploadFolderLabel;
        private string uploadFolderLabel;
        private string hostUploadFolderLabelForImg;
        private string uploadFolderLabelForImg;
        private string overrideFileOnUploadLabel;
        private string uploadFileLimitLabel;
        private string sizeLimitLabel;
        private string lblResizeWidthUpload;
        private string lblResizeHeightUpload;
        private string lblResizeWidth;
        private string lblResizeHeight;
        private string lblUseAnchorSelector;
        private string lblShowPageLinksTabFirst;
        private string fileListViewModeLabel;
        private string fileListPageSizeLabel;
        private string defaultLinkModeLabel;
        private string lblToolbarList;
        private string lblToolbName;
        private string lblToolbSet;
        private string toolbarGroupsLabel;
        private string lblToolbarPriority;
        private string editorConfigWarning;
        private string waitLabel;
        private string waitMessageLabel;
        private string providerVersion;
        private string lblPortal;
        private string lblPage;
        private string lblModType;
        private string lblModName;
        private string lblModInst;
        private string lblUName;

        /// <summary>Gets or sets a value indicating whether the "delete all settings" link renders.</summary>
        internal bool RemoveAllVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the "delete child settings" link renders.</summary>
        internal bool RemoveChildVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the "copy to child pages" link renders.</summary>
        internal bool CopyToAllChildVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the "add toolbar" image button renders.</summary>
        internal bool AddToolbarVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the "delete settings" link is enabled.</summary>
        internal bool RemoveEnabled { get; set; }

        /// <summary>Gets or sets a value indicating whether the "delete child settings" link is enabled.</summary>
        internal bool RemoveChildEnabled { get; set; }

        /// <summary>Gets or sets a value indicating whether the "copy to child pages" link is enabled.</summary>
        internal bool CopyToAllChildEnabled { get; set; }

        /// <summary>Gets or sets the caption of the import link.</summary>
        internal string ImportText { get; set; }

        /// <summary>Gets or sets the caption of the export link.</summary>
        internal string ExportText { get; set; }

        /// <summary>Gets or sets the caption of the "export now" link.</summary>
        internal string ExportNowText { get; set; }

        /// <summary>Gets or sets the caption of the "import now" link.</summary>
        internal string ImportNowText { get; set; }

        /// <summary>Gets or sets the caption of the OK button.</summary>
        internal string OkText { get; set; }

        /// <summary>Gets or sets the caption of the "delete settings" link.</summary>
        internal string RemoveText { get; set; }

        /// <summary>Gets or sets the tooltip of the "delete settings" link.</summary>
        internal string RemoveToolTip { get; set; }

        /// <summary>Gets or sets the caption of the "delete all settings" link.</summary>
        internal string RemoveAllText { get; set; }

        /// <summary>Gets or sets the tooltip of the "delete all settings" link.</summary>
        internal string RemoveAllToolTip { get; set; }

        /// <summary>Gets or sets the caption of the "delete child settings" link.</summary>
        internal string RemoveChildText { get; set; }

        /// <summary>Gets or sets the tooltip of the "delete child settings" link.</summary>
        internal string RemoveChildToolTip { get; set; }

        /// <summary>Gets or sets the caption of the "copy to child pages" link.</summary>
        internal string CopyToAllChildText { get; set; }

        /// <summary>Gets or sets the tooltip of the "copy to child pages" link.</summary>
        internal string CopyToAllChildToolTip { get; set; }

        /// <summary>Gets or sets the alternate text and tooltip of the "add toolbar" image button.</summary>
        internal string AddToolbarText { get; set; }

        /// <summary>Gets or sets the alternate text and tooltip of the "cancel toolbar" image button.</summary>
        internal string CancelToolbarText { get; set; }

        /// <summary>Gets or sets the alternate text and tooltip of the "edit toolbar" image button.</summary>
        internal string EditToolbarText { get; set; }

        /// <summary>Gets or sets the alternate text and tooltip of the "delete toolbar" image button.</summary>
        internal string DeleteToolbarText { get; set; }

        /// <summary>Gets or sets the available toolbar buttons of the toolbar editor.</summary>
        internal List<ToolbarButton> ListButtons { get; set; }

        /// <summary>Gets or sets every toolbar button known to the toolbar editor.</summary>
        internal List<ToolbarButton> AllListButtons { get; set; }

        /// <summary>Gets or sets the toolbar set currently edited.</summary>
        internal ToolbarSet ToolbarSets { get; set; }

        /// <summary>An item of one of the drop downs or of the radio button list.</summary>
        /// <remarks>Replaces <c>System.Web.UI.WebControls.ListItem</c>.</remarks>
        public sealed class OptionItem
        {
            /// <summary>Initializes a new instance of the <see cref="OptionItem"/> class.</summary>
            /// <param name="text">The display text.</param>
            /// <param name="value">The value.</param>
            public OptionItem(string text, string value)
            {
                this.Text = text;
                this.Value = value;
            }

            /// <summary>Gets or sets the display text of the item.</summary>
            public string Text { get; set; }

            /// <summary>Gets or sets the value of the item.</summary>
            public string Value { get; set; }

            /// <summary>Gets or sets a value indicating whether the item can be selected.</summary>
            public bool Enabled { get; set; } = true;
        }

        /// <summary>An item of the file browser security check box list.</summary>
        public sealed class CheckItem
        {
            /// <summary>Gets or sets the display text of the item.</summary>
            public string Text { get; set; }

            /// <summary>Gets or sets the value of the item.</summary>
            public string Value { get; set; }

            /// <summary>Gets or sets a value indicating whether the item is checked.</summary>
            public bool Selected { get; set; }

            /// <summary>Gets or sets a value indicating whether the item can be checked.</summary>
            public bool Enabled { get; set; } = true;
        }

        /// <summary>A row of the toolbar per role grid.</summary>
        /// <remarks>Replaces a <c>GridViewRow</c> holding the <c>lblRoleName</c> label and the <c>ddlToolbars</c> drop down.</remarks>
        public sealed class ToolbarRoleRow
        {
            /// <summary>Gets or sets the name of the role.</summary>
            public string RoleName { get; set; }

            /// <summary>Gets or sets the name of the toolbar selected for the role.</summary>
            public string SelectedToolbar { get; set; }
        }

        /// <summary>A row of the upload size limit per role grid.</summary>
        /// <remarks>Replaces a <c>GridViewRow</c> holding the <c>lblRoleName</c> label and the <c>SizeLimit</c> text box.</remarks>
        public sealed class UploadSizeRow
        {
            /// <summary>Gets or sets the name of the role.</summary>
            public string RoleName { get; set; }

            /// <summary>Gets or sets the identifier of the role.</summary>
            public int RoleId { get; set; }

            /// <summary>Gets or sets the upload size limit of the role, as it is typed.</summary>
            public string SizeLimit { get; set; }
        }

        /// <summary>One editable property of the editor configuration.</summary>
        /// <remarks>Replaces the label plus input pair <c>RenderEditorConfigSettings</c> created per property.</remarks>
        public sealed class EditorConfigSetting
        {
            /// <summary>Gets or sets the name of the property, which was the control's <c>ID</c>.</summary>
            public string Name { get; set; }

            /// <summary>Gets or sets the label shown next to the input.</summary>
            public string Label { get; set; }

            /// <summary>Gets or sets the tooltip, which came from the property's <c>DescriptionAttribute</c>.</summary>
            public string ToolTip { get; set; }

            /// <summary>Gets or sets the kind of input the property is edited with.</summary>
            public EditorConfigSettingKind Kind { get; set; }

            /// <summary>Gets the items of the drop down, when this is a <see cref="EditorConfigSettingKind.Select"/>.</summary>
            public List<OptionItem> Options { get; } = new List<OptionItem>();

            /// <summary>Gets or sets the value as text, for every kind but <see cref="EditorConfigSettingKind.Boolean"/>.</summary>
            public string StringValue { get; set; } = string.Empty;

            /// <summary>Gets or sets a value indicating whether the flag is set, for <see cref="EditorConfigSettingKind.Boolean"/>.</summary>
            public bool BoolValue { get; set; }
        }
    }
}
