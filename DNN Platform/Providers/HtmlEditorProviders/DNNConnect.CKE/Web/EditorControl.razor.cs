// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DNNConnect.CKEditorProvider.Web
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Text.Encodings.Web;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Serialization;

    using DNNConnect.CKEditorProvider.Constants;
    using DNNConnect.CKEditorProvider.Extensions;
    using DNNConnect.CKEditorProvider.Objects;
    using DNNConnect.CKEditorProvider.Utilities;
    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Framework.Providers;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    using Unit = DotNetNuke.UI.WebControls.Unit;

    /// <summary>The CKEditor control.</summary>
    /// <remarks>
    /// <para>
    /// This was a <c>System.Web.UI.WebControl</c> implementing <c>IPostBackDataHandler</c>. It is
    /// now a Blazor component (through <see cref="DnnComponentBase"/>), which maps the WebForms
    /// concepts it relied upon as follows:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>ViewState["Value"]</c> is the <see cref="Value"/> parameter plus <see cref="ValueChanged"/>, i.e. two-way binding; <c>LoadPostData</c>/<c>RaisePostDataChangedEvent</c> are <see cref="OnTextAreaChangedAsync"/>, which still performs the syntax-highlight / MathJax injection.</description></item>
    /// <item><description><c>Render(HtmlTextWriter)</c> is the markup in <c>EditorControl.razor</c>, which emits the same <c>noscript</c> block, the same <c>textarea</c> attributes and the same administrator "Options" link.</description></item>
    /// <item><description><c>ScriptManager.RegisterClientScriptBlock</c>/<c>RegisterStartupScript</c>/<c>RegisterOnSubmitStatement</c> go to the page's client-script seam (<see cref="DotNetNuke.UI.Utilities.IClientApiPage"/>) when the component renders inside a DNN page, and through the extension's own JS module otherwise, because a Blazor circuit has no page to register a script block on.</description></item>
    /// <item><description><c>HttpContext.Current.Session[...]</c> is <see cref="SessionVariable{T}"/>, the platform's <c>ISession</c> wrapper.</description></item>
    /// <item><description><c>HttpUtility.JavaScriptStringEncode</c> is <see cref="JavaScriptEncoder"/> and <c>Server.HtmlEncode</c> is <see cref="WebUtility.HtmlEncode(string)"/>.</description></item>
    /// </list>
    /// Every settings-driven CKEditor configuration value, toolbar injection and client resource
    /// registration is emitted exactly as before.
    /// </remarks>
    public partial class EditorControl
    {
        private const string ProviderType = "htmlEditor";

        private static readonly SessionVariable<int> SessionTabId = new SessionVariable<int>("CKDNNtabid");
        private static readonly SessionVariable<int> SessionPortalId = new SessionVariable<int>("CKDNNporid");
        private static readonly SessionVariable<bool> SessionIsAuthorized = new SessionVariable<bool>("CKE_DNNIsAuthorized");
        private static readonly SessionVariable<bool> SessionSubDirs = new SessionVariable<bool>("CKDNNSubDirs");
        private static readonly SessionVariable<int> SessionRootDirId = new SessionVariable<int>("CKDNNRootDirId");
        private static readonly SessionVariable<int> SessionRootDirForImgId = new SessionVariable<int>("CKDNNRootDirForImgId");
        private static readonly SessionVariable<int> SessionUpDirId = new SessionVariable<int>("CKDNNUpDirId");
        private static readonly SessionVariable<int> SessionUpDirForImgId = new SessionVariable<int>("CKDNNUpDirForImgId");

        private readonly List<string> pendingScripts = new List<string>();
        private readonly List<string> pendingSubmitScripts = new List<string>();

        private INavigationManager navigationManager;
        private IHostSettings hostSettings;
        private IApplicationStatusInfo appStatus;
        private IEventLogger eventLogger;
        private IClientResourceController clientResourceController;
        private IModuleController moduleController;
        private PortalSettings portalSettings;
        private bool isMerged; // Check if the Settings Collection is Merged with all Settings.
        private NameValueCollection settings;
        private EditorProviderSettings currentEditorSettings = new EditorProviderSettings();
        private string toolBarNameOverride;
        private PortalModuleBase portalModule;
        private int parentModulId;
        private string appliedValue;
        private string valueField = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="EditorControl"/> class.</summary>
        /// <remarks>
        /// Kept because Blazor activates a component through its parameterless constructor; the
        /// services are then taken from the component's own <c>[Inject]</c> properties, so this
        /// does not need an <c>HttpContext</c> the way the net48 overload did.
        /// </remarks>
        public EditorControl()
        {
            this.settings = new NameValueCollection();
        }

        /// <summary>Initializes a new instance of the <see cref="EditorControl"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="moduleController">The module controller.</param>
        public EditorControl(INavigationManager navigationManager, IHostSettings hostSettings, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IClientResourceController clientResourceController, IModuleController moduleController)
        {
            this.navigationManager = navigationManager;
            this.hostSettings = hostSettings;
            this.appStatus = appStatus;
            this.eventLogger = eventLogger;
            this.clientResourceController = clientResourceController;
            this.moduleController = moduleController;
            this.LoadConfigSettings();
        }

        /// <summary>Gets a value indicating whether CKEditor is rendered.</summary>
        public bool IsRendered { get; private set; }

        /// <summary>Gets or sets the width of the editor.</summary>
        /// <remarks>Replaces <c>WebControl.Width</c>; <see cref="Unit"/> is the platform's replacement for <c>System.Web.UI.WebControls.Unit</c>.</remarks>
        [Parameter]
        public Unit Width { get; set; }

        /// <summary>Gets or sets the height of the editor.</summary>
        /// <remarks>Replaces <c>WebControl.Height</c>.</remarks>
        [Parameter]
        public Unit Height { get; set; }

        // BL0007 asks for auto properties. Value and ToolBarName cannot be: Value normalizes null
        // to string.Empty, which is what ViewState["Value"] did and what the provider's Text
        // property contract requires, and ToolBarName keeps the dedicated backing field the
        // settings resolution reads before the parameter has been set.
#pragma warning disable BL0007
        /// <summary>Gets or sets Value.</summary>
        /// <remarks>Replaces <c>ViewState["Value"]</c>.</remarks>
        [Parameter]
        [DefaultValue("")]
        public string Value
        {
            get => this.valueField ?? string.Empty;
            set => this.valueField = value;
        }

        /// <summary>Gets or sets the callback raised when the edited html changed.</summary>
        /// <remarks>Replaces the <c>IPostBackDataHandler</c> round trip that pushed the posted html back onto the consumer.</remarks>
        [Parameter]
        public EventCallback<string> ValueChanged { get; set; }

        /// <summary> Gets or sets The ToolBarName defined in config to override all other Toolbars.</summary>
        [Parameter]
        public string ToolBarName
        {
            get
            {
                return this.toolBarNameOverride;
            }

            set
            {
                this.toolBarNameOverride = value;
            }
        }
#pragma warning restore BL0007

        /// <summary>Gets the editor settings.</summary>
        public NameValueCollection Settings
        {
            get
            {
                if (this.isMerged)
                {
                    return this.settings;
                }

                // Override local settings with attributes
                foreach (var attribute in this.Attributes)
                {
                    this.settings[attribute.Key] = attribute.Value;
                }

                // Inject all Editor Config
                foreach (
                    PropertyInfo info in
                        SettingsUtil.GetEditorConfigProperties())
                {
                    XmlAttributeAttribute xmlAttributeAttribute = null;
                    var settingValue = string.Empty;

                    if (!info.Name.Equals("CodeMirror") && !info.Name.Equals("WordCount"))
                    {
                        if (info.GetValue(this.currentEditorSettings.Config, null) == null)
                        {
                            continue;
                        }

                        var rawValue = info.GetValue(this.currentEditorSettings.Config, null);

                        settingValue = info.PropertyType.Name.Equals("Double")
                                           ? Convert.ToDouble(rawValue)
                                                 .ToString(CultureInfo.InvariantCulture)
                                           : rawValue.ToString();

                        if (string.IsNullOrEmpty(settingValue))
                        {
                            continue;
                        }

                        xmlAttributeAttribute = info.GetCustomAttribute<XmlAttributeAttribute>(true);
                    }

                    if (info.PropertyType.Name == "Boolean")
                    {
                        this.settings[xmlAttributeAttribute.AttributeName] = settingValue.ToLowerInvariant();
                    }
                    else
                    {
                        switch (info.Name)
                        {
                            case "ToolbarLocation":
                                this.settings[xmlAttributeAttribute.AttributeName] = settingValue.ToLowerInvariant();
                                break;
                            case "EnterMode":
                            case "ShiftEnterMode":
                                switch (settingValue)
                                {
                                    case "P":
                                        this.settings[xmlAttributeAttribute.AttributeName] = "1";
                                        break;
                                    case "BR":
                                        this.settings[xmlAttributeAttribute.AttributeName] = "2";
                                        break;
                                    case "DIV":
                                        this.settings[xmlAttributeAttribute.AttributeName] = "3";
                                        break;
                                }

                                break;
                            case "ContentsLangDirection":
                                {
                                    switch (settingValue)
                                    {
                                        case "LeftToRight":
                                            this.settings[xmlAttributeAttribute.AttributeName] = "ltr";
                                            break;
                                        case "RightToLeft":
                                            this.settings[xmlAttributeAttribute.AttributeName] = "rtl";
                                            break;
                                        default:
                                            this.settings[xmlAttributeAttribute.AttributeName] = string.Empty;
                                            break;
                                    }
                                }

                                break;
                            case "CodeMirror":
                                {
                                    var codeMirrorArray = new StringBuilder();

                                    foreach (var codeMirrorInfo in
                                        typeof(CodeMirror).GetProperties())
                                    {
                                        var xmlAttribute =
                                            codeMirrorInfo.GetCustomAttribute<XmlAttributeAttribute>(true);
                                        var rawSettingValue = codeMirrorInfo.GetValue(
                                            this.currentEditorSettings.Config.CodeMirror, null);

                                        var codeMirrorSettingValue = rawSettingValue.ToString();

                                        if (string.IsNullOrEmpty(codeMirrorSettingValue))
                                        {
                                            continue;
                                        }

                                        switch (codeMirrorInfo.PropertyType.Name)
                                        {
                                            case "String":
                                                codeMirrorArray.AppendFormat("{0}: '{1}',", xmlAttribute.AttributeName, JavaScriptStringEncode(codeMirrorSettingValue));
                                                break;
                                            case "Boolean":
                                                codeMirrorArray.AppendFormat("{0}: {1},", xmlAttribute.AttributeName, codeMirrorSettingValue.ToLowerInvariant());
                                                break;
                                        }
                                    }

                                    var codemirrorSettings = codeMirrorArray.ToString();

                                    this.settings["codemirror"] =
                                        $"{{ {codemirrorSettings.Remove(codemirrorSettings.Length - 1, 1)} }}";
                                }

                                break;
                            case "WordCount":
                                {
                                    var wordcountArray = new StringBuilder();

                                    foreach (var wordCountInfo in typeof(WordCountConfig).GetProperties())
                                    {
                                        var xmlAttribute =
                                            wordCountInfo.GetCustomAttribute<XmlAttributeAttribute>(true);

                                        var rawSettingValue =
                                            wordCountInfo.GetValue(this.currentEditorSettings.Config.WordCount, null);

                                        var wordCountSettingValue = rawSettingValue.ToString();

                                        if (string.IsNullOrEmpty(wordCountSettingValue))
                                        {
                                            continue;
                                        }

                                        switch (wordCountInfo.PropertyType.Name)
                                        {
                                            case "String":
                                                wordcountArray.AppendFormat("{0}: '{1}',", xmlAttribute.AttributeName, JavaScriptStringEncode(wordCountSettingValue));
                                                break;
                                            case "Boolean":
                                                wordcountArray.AppendFormat("{0}: {1},", xmlAttribute.AttributeName, wordCountSettingValue.ToLowerInvariant());
                                                break;
                                        }
                                    }

                                    var wordCountSettings = wordcountArray.ToString();

                                    this.settings["wordcount"] =
                                        $"{{ {wordCountSettings.Remove(wordCountSettings.Length - 1, 1)} }}";
                                }

                                break;
                            default:
                                this.settings[xmlAttributeAttribute.AttributeName] = settingValue;
                                break;
                        }
                    }
                }

                try
                {
                    var currentCulture = Thread.CurrentThread.CurrentUICulture;

                    this.settings["language"] = currentCulture.Name.ToLowerInvariant();

                    if (string.IsNullOrEmpty(this.currentEditorSettings.Config.Scayt_sLang))
                    {
                        // 'en-us' is not a language code that is supported, the correct is 'en_US'
                        // https://ckeditor.com/docs/ckeditor4/latest/api/CKEDITOR_config.html#cfg-scayt_sLang
                        this.settings["scayt_sLang"] = currentCulture.Name.Replace("-", "_");
                    }
                }
                catch (Exception)
                {
                    this.settings["language"] = "en";
                }

                if (!string.IsNullOrEmpty(this.currentEditorSettings.Config.CustomConfig))
                {
                    this.settings["customConfig"] = this.FormatUrl(this.currentEditorSettings.Config.CustomConfig);
                }
                else
                {
                    this.settings["customConfig"] = string.Empty;
                }

                if (!string.IsNullOrEmpty(this.currentEditorSettings.Config.Skin))
                {
                    if (this.currentEditorSettings.Config.Skin.Equals("office2003")
                        || this.currentEditorSettings.Config.Skin.Equals("BootstrapCK-Skin")
                        || this.currentEditorSettings.Config.Skin.Equals("chris")
                        || this.currentEditorSettings.Config.Skin.Equals("v2"))
                    {
                        this.settings["skin"] = "moono";
                    }
                    else
                    {
                        this.settings["skin"] = this.currentEditorSettings.Config.Skin;
                    }
                }

                this.settings["linkDefaultProtocol"] = this.currentEditorSettings.DefaultLinkProtocol.ToSettingValue();

                var cssFiles = new List<string>();
                var skinSrc = this.GetSkinSourcePath();
                var containerSrc = this.GetContainerSourcePath();

                cssFiles.Add("~/portals/_default/default.css");
                cssFiles.Add(skinSrc.Replace(skinSrc.Substring(skinSrc.LastIndexOf('/'), skinSrc.Length - skinSrc.Substring(0, skinSrc.LastIndexOf('/')).Length), "/skin.css"));
                cssFiles.Add(containerSrc.Replace(containerSrc.Substring(containerSrc.LastIndexOf('/'), containerSrc.Length - containerSrc.Substring(0, containerSrc.LastIndexOf('/')).Length), "/container.css"));
                if (this.portalModule != null && this.portalModule.ModuleId > -1)
                {
                    cssFiles.Add("~/DesktopModules/" + this.portalModule.ModuleConfiguration.DesktopModule.FolderName + "/module.css");
                }

                cssFiles.Add("~" + this.PortalSettings.HomeDirectory + "portal.css");
                cssFiles.Add("~/Providers/HtmlEditorProviders/DNNConnect.CKE/css/CkEditorContents.css");

                var resolvedCssFiles = cssFiles.Where(cssFile => File.Exists(this.MapPathSecure(cssFile))).Select(Globals.ResolveUrl).ToList();

                if (!string.IsNullOrEmpty(this.currentEditorSettings.Config.ContentsCss))
                {
                    var customCss = Globals.ResolveUrl(this.FormatUrl(this.currentEditorSettings.Config.ContentsCss));
                    resolvedCssFiles.Add(customCss);
                }

                // Replaces System.Web.Script.Serialization.JavaScriptSerializer, which does not
                // exist on ASP.NET Core; Newtonsoft is the host's serializer and produces the same
                // JSON array for a list of strings.
                this.settings["contentsCss"] = Newtonsoft.Json.JsonConvert.SerializeObject(resolvedCssFiles);

                if (!string.IsNullOrEmpty(this.currentEditorSettings.Config.Templates_Files))
                {
                    var templateUrl = this.FormatUrl(this.currentEditorSettings.Config.Templates_Files);

                    var templateFile = templateUrl.EndsWith(".xml") ? $"xml:{templateUrl}" : templateUrl;
                    this.settings["templates_files"] =
                        $"[ '{JavaScriptStringEncode(templateFile)}' ]";
                }

                if (!string.IsNullOrEmpty(this.toolBarNameOverride))
                {
                    this.settings["toolbar"] = this.toolBarNameOverride;
                }
                else
                {
                    var toolbarName = this.SetUserToolbar(this.settings["configFolder"]);

                    var listToolbarSets = ToolbarUtil.GetToolbars(this.PortalSettings.HomeDirectoryMapPath, this.settings["configFolder"]);

                    var toolbarSet = listToolbarSets.FirstOrDefault(toolbar => toolbar.Name.Equals(toolbarName));

                    var toolbarSetString = ToolbarUtil.ConvertToolbarSetToString(toolbarSet, true);

                    this.settings["toolbar"] = $"[{toolbarSetString}]";
                }

                // Easy Image Upload
                if (this.currentEditorSettings.ImageButtonMode == ImageButtonType.EasyImageButton)
                {
                    // replace 'Image' Plugin with 'EasyImage'
                    this.settings["toolbar"] = this.settings["toolbar"].Replace("'Image'", "'EasyImageUpload'");

                    // add the plugin in extraPlugins
                    if (string.IsNullOrEmpty(this.settings["extraPlugins"]) || !this.settings["extraPlugins"].Split(',').Contains("easyimage"))
                    {
                        if (!string.IsNullOrEmpty(this.settings["extraPlugins"]))
                        {
                            this.settings["extraPlugins"] += ",";
                        }

                        this.settings["extraPlugins"] += "easyimage";
                    }

                    // change the easyimage toolbar
                    this.settings["easyimage_toolbar"] = "['EasyImageAlt']";

                    // remove the image plugin in removePlugins
                    if (string.IsNullOrEmpty(this.settings["removePlugins"]) || !this.settings["removePlugins"].Split(',').Contains("image"))
                    {
                        if (!string.IsNullOrEmpty(this.settings["removePlugins"]))
                        {
                            this.settings["removePlugins"] += ",";
                        }

                        this.settings["removePlugins"] += "image";
                    }

                    this.settings.Add("cloudServices_uploadUrl", Globals.ResolveUrl(
                        string.Format(
                            "~/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/Browser.aspx?Command=EasyImageUpload&tabid={0}&PortalID={1}&mid={2}&ckid={3}&mode={4}&lang={5}",
                            this.PortalSettings.ActiveTab.TabID,
                            this.PortalSettings.PortalId,
                            this.parentModulId,
                            this.ID,
                            this.currentEditorSettings.SettingMode,
                            CultureInfo.CurrentCulture.Name)));
                }
                else
                {
                    // remove the easyimage plugin in removePlugins
                    if (string.IsNullOrEmpty(this.settings["removePlugins"]) || !this.settings["removePlugins"].Split(',').Contains("easyimage"))
                    {
                        if (!string.IsNullOrEmpty(this.settings["removePlugins"]))
                        {
                            this.settings["removePlugins"] += ",";
                        }

                        this.settings["removePlugins"] += "easyimage";
                    }
                }

                // cloudservices variables need to be set regardless
                var tokenUrl = ServicesFramework.GetServiceFrameworkRoot() + "API/CKEditorProvider/CloudServices/GetToken";
                this.settings.Add("cloudServices_tokenUrl", tokenUrl);

                // Editor Width
                if (!string.IsNullOrEmpty(this.currentEditorSettings.Config.Width))
                {
                    this.settings["width"] = this.currentEditorSettings.Config.Width;
                }
                else
                {
                    if (this.Width.Value > 0)
                    {
                        this.settings["width"] = this.Width.ToString();
                    }
                }

                // Editor Height
                if (!string.IsNullOrEmpty(this.currentEditorSettings.Config.Height))
                {
                    this.settings["height"] = this.currentEditorSettings.Config.Height;
                }
                else
                {
                    if (this.Height.Value > 0)
                    {
                        this.settings["height"] = this.Height.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(this.settings["extraPlugins"])
                    && this.settings["extraPlugins"].Contains("xmlstyles"))
                {
                    this.settings["extraPlugins"] = this.settings["extraPlugins"].Replace(",xmlstyles", string.Empty);
                }

                // fix oEmbed/oembed issue and other bad settings
                if (!string.IsNullOrEmpty(this.settings["extraPlugins"])
                    && this.settings["extraPlugins"].Contains("oEmbed"))
                {
                    this.settings["extraPlugins"] = this.settings["extraPlugins"].Replace("oEmbed", "oembed");
                }

                if (this.settings["PasteFromWordCleanupFile"] != null
                    && this.settings["PasteFromWordCleanupFile"].Equals("default"))
                {
                    this.settings["PasteFromWordCleanupFile"] = string.Empty;
                }

                if (this.settings["menu_groups"] != null
                    && this.settings["menu_groups"].Equals("clipboard,table,anchor,link,image"))
                {
                    this.settings["menu_groups"] =
                        "clipboard,tablecell,tablecellproperties,tablerow,tablecolumn,table,anchor,link,image,flash,checkbox,radio,textfield,hiddenfield,imagebutton,button,select,textarea,div";
                }

                // Inject maxFileSize
                this.settings["maxFileSize"] = Utility.GetMaxUploadSize().ToString();

                SetSessionValue(SessionTabId, this.PortalSettings.ActiveTab.TabID);
                SetSessionValue(SessionPortalId, this.PortalSettings.PortalId);

                // Add FileBrowser
                switch (this.currentEditorSettings.BrowserMode)
                {
                    case BrowserType.StandardBrowser:
                        {
                            this.settings["filebrowserBrowseUrl"] =
                                Globals.ResolveUrl(
                                    string.Format(
                                        "~/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/Browser.aspx?Type=Link&tabid={0}&PortalID={1}&mid={2}&ckid={3}&mode={4}&lang={5}",
                                        this.PortalSettings.ActiveTab.TabID,
                                        this.PortalSettings.PortalId,
                                        this.parentModulId,
                                        this.ID,
                                        this.currentEditorSettings.SettingMode,
                                        CultureInfo.CurrentCulture.Name));
                            this.settings["filebrowserImageBrowseUrl"] =
                                Globals.ResolveUrl(
                                    string.Format(
                                        "~/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/Browser.aspx?Type=Image&tabid={0}&PortalID={1}&mid={2}&ckid={3}&mode={4}&lang={5}",
                                        this.PortalSettings.ActiveTab.TabID,
                                        this.PortalSettings.PortalId,
                                        this.parentModulId,
                                        this.ID,
                                        this.currentEditorSettings.SettingMode,
                                        CultureInfo.CurrentCulture.Name));
                            this.settings["filebrowserFlashBrowseUrl"] =
                                Globals.ResolveUrl(
                                    string.Format(
                                        "~/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/Browser.aspx?Type=Flash&tabid={0}&PortalID={1}&mid={2}&ckid={3}&mode={4}&lang={5}",
                                        this.PortalSettings.ActiveTab.TabID,
                                        this.PortalSettings.PortalId,
                                        this.parentModulId,
                                        this.ID,
                                        this.currentEditorSettings.SettingMode,
                                        CultureInfo.CurrentCulture.Name));

                            if (this.currentEditorSettings.ImageButtonMode == ImageButtonType.StandardButton && Utility.CheckIfUserHasFolderWriteAccess(this.currentEditorSettings.UploadDirId, this.PortalSettings))
                            {
                                this.settings["filebrowserUploadUrl"] =
                                    Globals.ResolveUrl(
                                        string.Format(
                                            "~/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/Browser.aspx?Command=FileUpload&tabid={0}&PortalID={1}&mid={2}&ckid={3}&mode={4}&lang={5}",
                                            this.PortalSettings.ActiveTab.TabID,
                                            this.PortalSettings.PortalId,
                                            this.parentModulId,
                                            this.ID,
                                            this.currentEditorSettings.SettingMode,
                                            CultureInfo.CurrentCulture.Name));
                                this.settings["filebrowserFlashUploadUrl"] =
                                    Globals.ResolveUrl(
                                        string.Format(
                                            "~/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/Browser.aspx?Command=FlashUpload&tabid={0}&PortalID={1}&mid={2}&ckid={3}&mode={4}&lang={5}",
                                            this.PortalSettings.ActiveTab.TabID,
                                            this.PortalSettings.PortalId,
                                            this.parentModulId,
                                            this.ID,
                                            this.currentEditorSettings.SettingMode,
                                            CultureInfo.CurrentCulture.Name));
                                this.settings["filebrowserImageUploadUrl"] =
                                    Globals.ResolveUrl(
                                        string.Format(
                                            "~/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/Browser.aspx?Command=ImageUpload&tabid={0}&PortalID={1}&mid={2}&ckid={3}&mode={4}&lang={5}",
                                            this.PortalSettings.ActiveTab.TabID,
                                            this.PortalSettings.PortalId,
                                            this.parentModulId,
                                            this.ID,
                                            this.currentEditorSettings.SettingMode,
                                            CultureInfo.CurrentCulture.Name));
                            }

                            this.settings["filebrowserWindowWidth"] = "870";
                            this.settings["filebrowserWindowHeight"] = "800";

                            // Set Browser Authorize
                            const bool isAuthorized = true;

                            SetSessionValue(SessionIsAuthorized, isAuthorized);

                            DataCache.SetCache("CKE_DNNIsAuthorized", isAuthorized);
                        }

                        break;
                    case BrowserType.CKFinder:
                        {
                            this.settings["filebrowserBrowseUrl"] =
                                Globals.ResolveUrl(
                                    string.Format(
                                        "~/Providers/HtmlEditorProviders/DNNConnect.CKE/ckfinder/ckfinder.html?tabid={0}&PortalID={1}",
                                        this.PortalSettings.ActiveTab.TabID,
                                        this.PortalSettings.PortalId));
                            this.settings["filebrowserImageBrowseUrl"] =
                                Globals.ResolveUrl(
                                    string.Format(
                                        "~/Providers/HtmlEditorProviders/DNNConnect.CKE/ckfinder/ckfinder.html?type=Images&tabid={0}&PortalID={1}",
                                        this.PortalSettings.ActiveTab.TabID,
                                        this.PortalSettings.PortalId));
                            this.settings["filebrowserFlashBrowseUrl"] =
                                Globals.ResolveUrl(
                                    string.Format(
                                        "~/Providers/HtmlEditorProviders/DNNConnect.CKE/ckfinder/ckfinder.html?type=Flash&tabid={0}&PortalID={1}",
                                        this.PortalSettings.ActiveTab.TabID,
                                        this.PortalSettings.PortalId));

                            if (Utility.CheckIfUserHasFolderWriteAccess(this.currentEditorSettings.UploadDirId, this.PortalSettings))
                            {
                                this.settings["filebrowserUploadUrl"] =
                                    Globals.ResolveUrl(
                                        string.Format(
                                            "~/Providers/HtmlEditorProviders/DNNConnect.CKE/ckfinder/core/connector/aspx/connector.aspx?command=QuickUpload&type=Files&tabid={0}&PortalID={1}",
                                            this.PortalSettings.ActiveTab.TabID,
                                            this.PortalSettings.PortalId));
                                this.settings["filebrowserFlashUploadUrl"] =
                                    Globals.ResolveUrl(
                                        string.Format(
                                            "~/Providers/HtmlEditorProviders/DNNConnect.CKE/ckfinder/core/connector/aspx/connector.aspx?command=QuickUpload&type=Flash&tabid={0}&PortalID={1}",
                                            this.PortalSettings.ActiveTab.TabID,
                                            this.PortalSettings.PortalId));
                                this.settings["filebrowserImageUploadUrl"] =
                                    Globals.ResolveUrl(
                                        string.Format(
                                            "~/Providers/HtmlEditorProviders/DNNConnect.CKE/ckfinder/core/connector/aspx/connector.aspx?command=QuickUpload&type=Images&tabid={0}&PortalID={1}",
                                            this.PortalSettings.ActiveTab.TabID,
                                            this.PortalSettings.PortalId));
                            }

                            SetSessionValue(SessionSubDirs, this.currentEditorSettings.SubDirs);

                            SetSessionValue(SessionRootDirId, this.currentEditorSettings.BrowserRootDirId);
                            SetSessionValue(SessionRootDirForImgId, this.currentEditorSettings.BrowserRootDirId);
                            SetSessionValue(SessionUpDirId, this.currentEditorSettings.UploadDirId);
                            SetSessionValue(SessionUpDirForImgId, this.currentEditorSettings.UploadDirId);

                            // Set Browser Authorize
                            const bool isAuthorized = true;

                            SetSessionValue(SessionIsAuthorized, isAuthorized);

                            DataCache.SetCache("CKE_DNNIsAuthorized", isAuthorized);
                        }

                        break;
                }

                this.isMerged = true;

                return this.settings;
            }
        }

        /// <summary>Gets the portal settings of the request the editor renders on.</summary>
        /// <remarks>Replaces <c>HttpContextSource.Current.Items["PortalSettings"]</c>, which is what <see cref="PortalSettings.Current"/> reads.</remarks>
        protected PortalSettings PortalSettings => this.portalSettings ??= PortalSettings.Current;

        /// <summary>Gets or sets the JavaScript runtime used to initialize CKEditor outside a DNN page.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets or sets the container of the module the editor is rendered in, when there is one.</summary>
        /// <remarks>
        /// Replaces the <c>FindModuleInstance</c> control-tree walk: the container component
        /// cascades itself under the name <c>ModuleContainer</c>, which is how the platform's own
        /// <c>PortalModuleBase.ContainerControl</c> finds its container.
        /// </remarks>
        [CascadingParameter(Name = "ModuleContainer")]
        protected Microsoft.AspNetCore.Components.IComponent ModuleContainer { get; set; }

        [Inject]
        private INavigationManager InjectedNavigationManager { get; set; }

        [Inject]
        private IHostSettings InjectedHostSettings { get; set; }

        [Inject]
        private IApplicationStatusInfo InjectedAppStatus { get; set; }

        [Inject]
        private IEventLogger InjectedEventLogger { get; set; }

        [Inject]
        private IClientResourceController InjectedClientResourceController { get; set; }

        [Inject]
        private IModuleController InjectedModuleController { get; set; }

        private static string GetResxFileName
        {
            get
            {
                return
                    Globals.ResolveUrl(
                        string.Format("~/Providers/HtmlEditorProviders/DNNConnect.CKE/{0}/Options.resx", Localization.LocalResourceDirectory));
            }
        }

        /// <summary>Gets the rendered client identifier, falling back to the server identifier.</summary>
        private string ClientIdentifier => this.ClientId ?? this.ID ?? string.Empty;

        /// <summary>Gets the identifier the text area and the CKEditor instance are keyed by.</summary>
        /// <remarks>Unchanged: <c>ClientID</c> with the <c>-</c> and <c>.</c> characters removed.</remarks>
        private string EditorFixedId =>
            this.ClientIdentifier.Replace("-", string.Empty).Replace(".", string.Empty);

        /// <summary>Gets the inline style of the wrapper, when the settings configure a width.</summary>
        private string WrapperStyle =>
            !string.IsNullOrEmpty(this.currentEditorSettings.Config.Width)
                ? $"width:{this.currentEditorSettings.Config.Width};"
                : null;

        /// <summary>Gets the html the text area renders with.</summary>
        /// <remarks>Unchanged: the edited html, or the configured blank text when there is none.</remarks>
        private string TextAreaContent =>
            string.IsNullOrEmpty(this.Value) ? this.currentEditorSettings.BlankText ?? string.Empty : this.Value;

        /// <summary>Gets the text shown when scripting is disabled.</summary>
        private string NoJavaText => Localization.GetString("NoJava.Text", GetResxFileName);

        /// <summary>Gets a value indicating whether the administrator's "Options" link renders.</summary>
        /// <remarks>Unchanged: the link rendered for the portal administrators only, and only once a text area had been rendered.</remarks>
        private bool ShowOptionsLink =>
            this.HasRenderedTextArea(this.Page) && PortalSecurity.IsInRoles(this.PortalSettings.AdministratorRoleName);

        /// <summary>Gets the identifier of the administrator's "Options" link.</summary>
        private string OptionsLinkId => $"{this.EditorFixedId}_ckoptions";

        /// <summary>Gets the caption of the administrator's "Options" link.</summary>
        private string OptionsText => Localization.GetString("Options.Text", GetResxFileName);

        /// <summary>Gets the URL of the editor options popup.</summary>
        private string OptionsUrl => this.navigationManager.NavigateURL(
            "CKEditorOptions",
            "ModuleId=" + this.parentModulId,
            "minc=" + this.ID,
            "PortalID=" + this.PortalSettings.PortalId,
            "langCode=" + CultureInfo.CurrentCulture.Name,
            "popUp=true");

        /// <summary>Gets the <c>onclick</c> handler of the administrator's "Options" link.</summary>
        /// <remarks>
        /// Unchanged: the WebForms control wrote this very attribute value, html-attribute-encoding
        /// the JavaScript-encoded and quoted URL. Blazor encodes attribute values itself, so only
        /// the JavaScript quoting is applied here.
        /// </remarks>
        private string OptionsOnClick =>
            $"window.open(\"{JavaScriptStringEncode(this.OptionsUrl)}\",\"Options\", \"width=850,height=750,resizable=yes\")";

        /// <summary>Finds the module instance.</summary>
        /// <param name="editorControl">The editor control.</param>
        /// <returns>The Instances found.</returns>
        /// <remarks>
        /// The WebForms version walked <c>Control.Parent</c> for the closest
        /// <c>PortalModuleBase</c>, preferring one whose <c>TabModuleId</c> was set. The walk is
        /// the same here over <see cref="DotNetNuke.UI.Utilities.IClientApiControl.Parent"/>, the Client API's replacement
        /// for the control tree.
        /// </remarks>
        public static DotNetNuke.UI.Utilities.IClientApiControl FindModuleInstance(DotNetNuke.UI.Utilities.IClientApiControl editorControl)
        {
            DotNetNuke.UI.Utilities.IClientApiControl ctl = editorControl?.Parent;
            DotNetNuke.UI.Utilities.IClientApiControl selectedCtl = null;
            DotNetNuke.UI.Utilities.IClientApiControl possibleCtl = null;

            while (ctl != null)
            {
                var portalModuleBase = ctl as PortalModuleBase;

                if (portalModuleBase != null)
                {
                    if (portalModuleBase.TabModuleId == Null.NullInteger)
                    {
                        possibleCtl = ctl;
                    }
                    else
                    {
                        selectedCtl = ctl;
                        break;
                    }
                }

                ctl = ctl.Parent;
            }

            if (selectedCtl == null & possibleCtl != null)
            {
                selectedCtl = possibleCtl;
            }

            return selectedCtl;
        }

        /// <summary>Checks if the text area was rendered.</summary>
        /// <param name="control">The control to ckeck.</param>
        /// <returns>Returns a value indicating whether the text area has rendered.</returns>
        public bool HasRenderedTextArea(DotNetNuke.UI.Utilities.IClientApiControl control)
        {
            if (control == null)
            {
                return this.IsRendered;
            }

            if (control is EditorControl editor && editor.IsRendered)
            {
                return true;
            }

            return control.HasChildren && control.Children.Any(this.HasRenderedTextArea);
        }

        /// <summary>Loads the post data.</summary>
        /// <param name="postedValue">The html the editor posted.</param>
        /// <returns>A value indicating whether the PostData has loaded.</returns>
        /// <remarks>
        /// This is <c>IPostBackDataHandler.LoadPostData</c>: the syntax-highlight and MathJax
        /// injection it performed on the posted html is preserved verbatim; only the plumbing that
        /// delivers the value changed (an <c>onchange</c> event instead of the post data
        /// collection).
        /// </remarks>
        public bool LoadPostData(string postedValue)
        {
            try
            {
                string currentValue = this.Value;

                if (currentValue == null | !postedValue.Equals(currentValue))
                {
                    if (this.currentEditorSettings.InjectSyntaxJs)
                    {
                        if (postedValue.Contains("<pre class=\"brush:") && !postedValue.Contains("shCore.js"))
                        {
                            // Add Syntax Highlighter Plugin
                            postedValue =
                                string.Format(
                                    "<!-- Injected Syntax Highlight Code --><script type=\"text/javascript\" src=\"{0}plugins/syntaxhighlight/scripts/shCore.js\"></script><link type=\"text/css\" rel=\"stylesheet\" href=\"{0}plugins/syntaxhighlight/styles/shCore.css\"/><script type=\"text/javascript\">SyntaxHighlighter.all();</script>{1}",
                                    Globals.ResolveUrl("~/Providers/HtmlEditorProviders/DNNConnect.CKE/CKEditor/"),
                                    postedValue);
                        }

                        if (postedValue.Contains("<span class=\"math-tex\">") && !postedValue.Contains("MathJax.js"))
                        {
                            // Add MathJax Plugin
                            postedValue =
                                string.Format(
                                    "<!-- Injected MathJax Code --><script type=\"text/javascript\" src=\"//cdn.mathjax.org/mathjax/2.2-latest/MathJax.js?config=TeX-AMS_HTML\"></script>{0}",
                                    postedValue);
                        }
                    }

                    this.Value = postedValue;

                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Replaces the <c>Init</c> handler the constructor hooked up (<c>CKEditorInit</c>): it
        /// resolves the parent module, the file browser mode and every host/portal/page/module
        /// setting, and registers the client resources and the editor load script.
        /// </remarks>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.navigationManager ??= this.InjectedNavigationManager;
            this.hostSettings ??= this.InjectedHostSettings;
            this.appStatus ??= this.InjectedAppStatus;
            this.eventLogger ??= this.InjectedEventLogger;
            this.clientResourceController ??= this.InjectedClientResourceController;
            this.moduleController ??= this.InjectedModuleController;

            if (this.settings == null || this.settings.Count == 0)
            {
                this.LoadConfigSettings();
            }

            this.CKEditorInit();

            // The WebForms Render() set this right after it had written the text area. The
            // component always renders it (see EditorControl.razor), so the flag is set here,
            // where HasRenderedTextArea can already observe it.
            this.IsRendered = true;
        }

        /// <inheritdoc />
        /// <remarks>Keeps <see cref="Value"/> in sync when the consumer assigns new html.</remarks>
        protected override void OnParametersSet()
        {
            base.OnParametersSet();

            if (!string.Equals(this.Value, this.appliedValue, StringComparison.Ordinal))
            {
                this.appliedValue = this.Value;
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Runs the configuration and start-up scripts that could not be registered on a DNN page
        /// (there is no page to register a script block on over a Blazor circuit). They are the
        /// very source the WebForms control emitted, run through the named helper of this
        /// extension's JS module rather than <c>eval</c>, which keeps the component usable under a
        /// CSP that forbids <c>eval</c>.
        /// </remarks>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if ((this.pendingScripts.Count == 0 && this.pendingSubmitScripts.Count == 0) || this.JsRuntime == null)
            {
                return;
            }

            IJSObjectReference module = null;
            try
            {
                module = await this.JsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "/Providers/HtmlEditorProviders/DNNConnect.CKE/js/editorInterop.js");
            }
            catch (JSException)
            {
                // the module is not reachable (e.g. the extension's assets are not deployed yet);
                // the editor degrades to the plain textarea the markup already rendered.
                return;
            }

            foreach (var script in this.pendingScripts.ToList())
            {
                await module.InvokeVoidAsync("runEditorScript", script);
            }

            foreach (var script in this.pendingSubmitScripts.ToList())
            {
                await module.InvokeVoidAsync("registerOnSubmit", script);
            }

            this.pendingScripts.Clear();
            this.pendingSubmitScripts.Clear();
        }

        /// <summary>Takes the html the editor posted through its text area.</summary>
        /// <param name="args">The change event of the text area.</param>
        /// <returns>A task that completes once the consumer has been notified.</returns>
        private async Task OnTextAreaChangedAsync(ChangeEventArgs args)
        {
            var postedValue = args?.Value?.ToString() ?? string.Empty;

            if (this.LoadPostData(postedValue) && this.ValueChanged.HasDelegate)
            {
                await this.ValueChanged.InvokeAsync(this.Value);
            }
        }

        private static string JavaScriptStringEncode(string value)
        {
            // Replaces System.Web.HttpUtility.JavaScriptStringEncode; neither adds surrounding
            // quotes. This is the platform's own convention (see Localization.GetSafeJSString).
            return JavaScriptEncoder.Default.Encode(value ?? string.Empty);
        }

        private static void SetSessionValue<T>(SessionVariable<T> variable, T value)
        {
            var context = HttpContextSource.Current;
            if (context == null
                || context.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>() == null)
            {
                // Session state is only reachable while an HTTP request is in flight, never during
                // interactive (SignalR) rendering. The values below are read back by the file
                // browser, which always runs on its own request.
                return;
            }

            variable.Value = value;
        }

        private string GetContainerSourcePath()
        {
            var containerSource = this.PortalSettings.ActiveTab.ContainerSrc ?? this.PortalSettings.DefaultPortalContainer;
            containerSource = this.ResolveSourcePath(containerSource);
            return containerSource;
        }

        private string GetSkinSourcePath()
        {
            var skinSource = this.PortalSettings.ActiveTab.SkinSrc ?? this.PortalSettings.DefaultPortalSkin;
            skinSource = this.ResolveSourcePath(skinSource);
            return skinSource;
        }

        private string ResolveSourcePath(string source)
        {
            source = "~" + source;
            return source;
        }

        /// <summary>Maps an application relative path onto the file system.</summary>
        /// <param name="virtualPath">The <c>~/</c> relative path.</param>
        /// <returns>The physical path.</returns>
        /// <remarks>Replaces <c>Control.MapPathSecure</c>/<c>Server.MapPath</c>.</remarks>
        private string MapPathSecure(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath))
            {
                return virtualPath;
            }

            var relative = virtualPath.StartsWith("~/", StringComparison.Ordinal)
                ? virtualPath.Substring(2)
                : virtualPath.TrimStart('~', '/');

            // IApplicationStatusInfo.ApplicationMapPath is the injected replacement for the
            // deprecated Globals.ApplicationMapPath; it is resolved before anything maps a path.
            return Path.Combine(
                this.appStatus.ApplicationMapPath, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        private void CKEditorInit()
        {
            this.portalModule = ResolveModule(this.ModuleContainer) ?? FindModuleInstance(this) as PortalModuleBase;

            if (this.portalModule == null || this.portalModule.ModuleId == -1)
            {
                // Get Parent ModuleID From this ClientID
                if (!this.TryParseParentModuleIdFromClientId(out this.parentModulId))
                {
                    // The is no real module, then use the "User Accounts" module (Profile editor)
                    ModuleInfo objm = this.moduleController.GetModuleByDefinition(this.PortalSettings.PortalId, "User Accounts");

                    this.parentModulId = objm.TabModuleID;
                }
            }
            else
            {
                this.parentModulId = this.portalModule.ModuleId;
            }

            this.SetFileBrowserMode();
            this.LoadAllSettings();
            this.RegisterCKEditorLibrary();
            this.GenerateEditorLoadScript();
        }

        /// <summary>Gets the module control of the container cascaded to this component.</summary>
        /// <param name="container">The cascaded container component.</param>
        /// <returns>The module the editor renders in, or <see langword="null"/>.</returns>
        private static PortalModuleBase ResolveModule(Microsoft.AspNetCore.Components.IComponent container)
        {
            return (container as DotNetNuke.UI.Containers.Container)?.ModuleControl as PortalModuleBase;
        }

        /// <summary>Reads the parent module's identifier out of the rendered client identifier.</summary>
        /// <param name="moduleId">The identifier that was parsed.</param>
        /// <returns><see langword="true"/> when the identifier carried a module id.</returns>
        /// <remarks>
        /// The original performed this substring walk over <c>ClientID</c> (DNN names a module
        /// container <c>ctr{ModuleId}</c>). The same walk is kept over
        /// <see cref="DnnComponentBase.ClientId"/>, but it is guarded now: outside a module
        /// container there is no <c>ctr</c> segment, where the WebForms code would have thrown.
        /// </remarks>
        private bool TryParseParentModuleIdFromClientId(out int moduleId)
        {
            moduleId = 0;

            var clientId = this.ClientId ?? string.Empty;
            var ctrIndex = clientId.IndexOf("ctr", StringComparison.Ordinal);
            if (ctrIndex < 0)
            {
                return false;
            }

            var candidate = clientId.Substring(ctrIndex + 3);
            var separatorIndex = candidate.IndexOf("_", StringComparison.Ordinal);
            if (separatorIndex >= 0)
            {
                candidate = candidate.Remove(separatorIndex);
            }

            return int.TryParse(candidate, out moduleId);
        }

        private void SetFileBrowserMode()
        {
            ProviderConfiguration providerConfiguration = ProviderConfiguration.GetProviderConfiguration(ProviderType);
            Provider objProvider = (Provider)providerConfiguration.Providers[providerConfiguration.DefaultProvider];

            if (objProvider == null || string.IsNullOrEmpty(objProvider.Attributes["ck_browser"]))
            {
                return;
            }

            switch (objProvider.Attributes["ck_browser"])
            {
                case "ckfinder":
                    this.currentEditorSettings.BrowserMode = BrowserType.CKFinder;
                    break;
                case "standard":
                    this.currentEditorSettings.BrowserMode = BrowserType.StandardBrowser;
                    break;
                case "none":
                    this.currentEditorSettings.BrowserMode = BrowserType.None;
                    break;
            }
        }

        /// <summary>Load Portal/Page/Module Settings.</summary>
        private void LoadAllSettings()
        {
            var settingsDictionary = EditorController.GetEditorHostSettings(this.hostSettings);
            var portalRoles = RoleController.Instance.GetRoles(this.PortalSettings.PortalId);

            // Load Default Settings
            this.currentEditorSettings = SettingsUtil.GetDefaultSettings(
                this.PortalSettings,
                this.hostSettings,
                this.PortalSettings.HomeDirectoryMapPath,
                this.settings["configFolder"],
                portalRoles);

            // Set Current Mode to Default
            this.currentEditorSettings.SettingMode = SettingsMode.Default;

            var hostKey = SettingConstants.HostKey;
            var portalKey = SettingConstants.PortalKey(this.PortalSettings.PortalId);
            var pageKey = $"DNNCKT#{this.PortalSettings.ActiveTab.TabID}#";
            var moduleKey = $"DNNCKMI#{this.parentModulId}#INS#{this.ID}#";

            // Load Host Settings ?!
            if (SettingsUtil.CheckSettingsExistByKey(settingsDictionary, hostKey))
            {
                var hostPortalRoles = RoleController.Instance.GetRoles(this.hostSettings.HostPortalId);
                this.currentEditorSettings = SettingsUtil.LoadEditorSettingsByKey(
                    this.PortalSettings,
                    this.currentEditorSettings,
                    settingsDictionary,
                    hostKey,
                    hostPortalRoles);

                // Set Current Mode to Host
                this.currentEditorSettings.SettingMode = SettingsMode.Host;

                // reset the roles to the correct portal
                if (this.PortalSettings.PortalId != this.hostSettings.HostPortalId)
                {
                    foreach (var toolbarRole in this.currentEditorSettings.ToolBarRoles)
                    {
                        var roleName = hostPortalRoles.FirstOrDefault(role => role.RoleID == toolbarRole.RoleId)?.RoleName ?? string.Empty;
                        var roleId = portalRoles.FirstOrDefault(role => role.RoleName.Equals(roleName))?.RoleID ?? Null.NullInteger;
                        toolbarRole.RoleId = roleId;
                    }

                    foreach (var uploadRoles in this.currentEditorSettings.UploadSizeRoles)
                    {
                        var roleName = hostPortalRoles.FirstOrDefault(role => role.RoleID == uploadRoles.RoleId)?.RoleName ?? string.Empty;
                        var roleId = portalRoles.FirstOrDefault(role => role.RoleName.Equals(roleName))?.RoleID ?? Null.NullInteger;
                        uploadRoles.RoleId = roleId;
                    }
                }
            }

            // Load Portal Settings ?!
            if (SettingsUtil.CheckSettingsExistByKey(settingsDictionary, portalKey))
            {
                this.currentEditorSettings = SettingsUtil.LoadEditorSettingsByKey(
                    this.PortalSettings,
                    this.currentEditorSettings,
                    settingsDictionary,
                    portalKey,
                    portalRoles);

                // Set Current Mode to Portal
                this.currentEditorSettings.SettingMode = SettingsMode.Portal;
            }

            // Load Page Settings ?!
            if (SettingsUtil.CheckSettingsExistByKey(settingsDictionary, pageKey))
            {
                this.currentEditorSettings = SettingsUtil.LoadEditorSettingsByKey(
                    this.PortalSettings, this.currentEditorSettings, settingsDictionary, pageKey, portalRoles);

                // Set Current Mode to Page
                this.currentEditorSettings.SettingMode = SettingsMode.Page;
            }

            // Load Module Settings ?!
            if (!SettingsUtil.CheckExistsModuleInstanceSettings(moduleKey, this.parentModulId))
            {
                return;
            }

            this.currentEditorSettings = SettingsUtil.LoadModuleSettings(
                this.PortalSettings, this.currentEditorSettings, moduleKey, this.parentModulId, portalRoles);

            // Set Current Mode to Module Instance
            this.currentEditorSettings.SettingMode = SettingsMode.ModuleInstance;
        }

        /// <summary>Format the URL from FileID to File Path URL.</summary>
        /// <param name="inputUrl">The Input URL.</param>
        /// <returns>The formatted URL.</returns>
        private string FormatUrl(string inputUrl)
        {
            var formattedUrl = string.Empty;

            if (string.IsNullOrEmpty(inputUrl))
            {
                return formattedUrl;
            }

            if (inputUrl.StartsWith("http://") || inputUrl.StartsWith("https://") || inputUrl.StartsWith("//", StringComparison.Ordinal))
            {
                formattedUrl = inputUrl;
            }
            else if (inputUrl.StartsWith("FileID="))
            {
                var fileId = int.Parse(inputUrl.Substring(7));

                var objFileInfo = FileManager.Instance.GetFile(fileId);

                formattedUrl = this.PortalSettings.HomeDirectory + objFileInfo.Folder + objFileInfo.FileName;
            }
            else
            {
                formattedUrl = this.PortalSettings.HomeDirectory + inputUrl;
            }

            return formattedUrl;
        }

        /// <summary>Load the Settings from the provider configuration.</summary>
        private void LoadConfigSettings()
        {
            this.settings = new NameValueCollection();

            var providerConfiguration = ProviderConfiguration.GetProviderConfiguration(ProviderType);
            if (providerConfiguration.Providers.ContainsKey(providerConfiguration.DefaultProvider))
            {
                var objProvider = (Provider)providerConfiguration.Providers[providerConfiguration.DefaultProvider];

                foreach (string key in objProvider.Attributes)
                {
                    if (key.StartsWith("ck_", StringComparison.OrdinalIgnoreCase))
                    {
                        string sAdjustedKey = key.Substring(3);

                        // Do not ToLower settingKey, because CKConfig is case-Sensitive, exp: image2_prefillDimension
                        ////.ToLower();

                        if (!string.IsNullOrEmpty(sAdjustedKey))
                        {
                            this.settings[sAdjustedKey] = objProvider.Attributes[key];
                        }
                    }
                }
            }
            else
            {
                // Replaces System.Configuration.ConfigurationErrorsException, which no longer
                // ships with the shared framework; the condition (a provider section whose
                // default provider is missing) and the message are unchanged.
                throw new InvalidOperationException(string.Format(
                    "Configuration error: default provider {0} doesn't exist in {1} providers",
                    providerConfiguration.DefaultProvider,
                    ProviderType));
            }
        }

        /// <summary>Registers a script that runs once the page has loaded.</summary>
        /// <param name="key">The registration key.</param>
        /// <param name="script">The script source.</param>
        /// <param name="addScriptTags">Whether the script needs script tags around it.</param>
        /// <remarks>Replaces <c>ScriptManager.RegisterStartupScript</c>.</remarks>
        private void RegisterStartupScript(string key, string script, bool addScriptTags)
        {
            var page = this.Page;
            if (page != null)
            {
                page.RegisterStartupScript(this.GetType(), key, script, addScriptTags);
                return;
            }

            this.pendingScripts.Add(script);
        }

        /// <summary>Registers a block of script emitted once per page.</summary>
        /// <param name="key">The registration key.</param>
        /// <param name="script">The script source.</param>
        /// <param name="addScriptTags">Whether the script needs script tags around it.</param>
        /// <remarks>Replaces <c>ScriptManager.RegisterClientScriptBlock</c>.</remarks>
        private void RegisterScript(string key, string script, bool addScriptTags)
        {
            var page = this.Page;
            if (page != null)
            {
                page.RegisterClientScriptBlock(this.GetType(), key, addScriptTags ? $"<script type=\"text/javascript\">{script}</script>" : script);
                return;
            }

            this.pendingScripts.Add(script);
        }

        /// <summary>Registers a statement that runs right before the form is submitted.</summary>
        /// <param name="type">The type used as the registration scope.</param>
        /// <param name="key">The registration key.</param>
        /// <param name="script">The statement source.</param>
        /// <remarks>
        /// Replaces <c>ScriptManager.RegisterOnSubmitStatement</c>. There is no WebForms form to
        /// hook on ASP.NET Core, so outside a DNN page the statement is attached to the enclosing
        /// form's <c>submit</c> event by the extension's JS module, which is what the WebForms
        /// submit pipeline did.
        /// </remarks>
        private void RegisterOnSubmitStatement(Type type, string key, string script)
        {
            var page = this.Page;
            if (page != null)
            {
                page.RegisterClientScriptBlock(
                    type,
                    key,
                    $"<script type=\"text/javascript\">document.addEventListener('submit', function (e) {{ {script} }}, true);</script>");
                return;
            }

            this.pendingSubmitScripts.Add(script);
        }

        private string SetUserToolbar(string alternateConfigSubFolder)
        {
            string toolbarName = this.CanUseFullToolbarAsDefault() ? "Full" : "Basic";

            var listToolbarSets = ToolbarUtil.GetToolbars(
                this.PortalSettings.HomeDirectoryMapPath, alternateConfigSubFolder);

            var listUserToolbarSets = new List<ToolbarSet>();

            if (this.currentEditorSettings.ToolBarRoles.Count <= 0)
            {
                return toolbarName;
            }

            foreach (var roleToolbar in this.currentEditorSettings.ToolBarRoles)
            {
                if (roleToolbar.RoleId.Equals(-1) && !this.IsAuthenticated)
                {
                    return roleToolbar.Toolbar;
                }

                if (roleToolbar.RoleId.Equals(-1))
                {
                    continue;
                }

                // Role
                var role = RoleController.Instance.GetRoleById(this.PortalSettings.PortalId, roleToolbar.RoleId);

                if (role == null)
                {
                    continue;
                }

                if (!PortalSecurity.IsInRole(role.RoleName))
                {
                    continue;
                }

                // Handle Different Roles
                if (!listToolbarSets.Any(toolbarSel => toolbarSel.Name.Equals(roleToolbar.Toolbar)))
                {
                    continue;
                }

                var toolbar = listToolbarSets.Find(toolbarSel => toolbarSel.Name.Equals(roleToolbar.Toolbar));

                listUserToolbarSets.Add(toolbar);
            }

            if (listUserToolbarSets.Count <= 0)
            {
                return toolbarName;
            }

            // Compare The User Toolbars if the User is more then One Role, and apply the Toolbar with the Highest Priority
            int iHighestPrio = listUserToolbarSets.Max(toolb => toolb.Priority);

            return ToolbarUtil.FindHighestToolbar(listUserToolbarSets, iHighestPrio).Name;
        }

        /// <summary>Gets a value indicating whether the request the editor renders on is authenticated.</summary>
        /// <remarks>Replaces <c>HttpContext.Current.Request.IsAuthenticated</c>.</remarks>
        private bool IsAuthenticated =>
            this.Request?.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

        private bool CanUseFullToolbarAsDefault()
        {
            if (!this.IsAuthenticated)
            {
                return false;
            }

            var currentUser = UserController.Instance.GetCurrentUserInfo();
            return currentUser.IsSuperUser || PortalSecurity.IsInRole(this.PortalSettings.AdministratorRoleName);
        }

        private void RegisterCKEditorLibrary()
        {
            this.clientResourceController.CreateStylesheet(Globals.ResolveUrl("~/Providers/HtmlEditorProviders/DNNConnect.CKE/css/CkEditorToolBars.css")).Register();
            this.clientResourceController.CreateStylesheet(Globals.ResolveUrl("~/Providers/HtmlEditorProviders/DNNConnect.CKE/css/CKEditorOverride.css")).Register();

            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, CommonJs.jQuery);

            // Replaces ClientScriptManager.RegisterClientScriptInclude; the client resource
            // controller is the ASP.NET Core seam and performs the same "register once" check the
            // IsClientScriptIncludeRegistered guards performed.
            if (File.Exists(this.MapPathSecure("~/Providers/HtmlEditorProviders/DNNConnect.CKE/js/ckeditor/4.18.0/ckeditor.js")))
            {
                this.clientResourceController.CreateScript(
                    Globals.ResolveUrl("~/Providers/HtmlEditorProviders/DNNConnect.CKE/js/ckeditor/4.18.0/ckeditor.js")).Register();
            }

            if (File.Exists(this.MapPathSecure("~/Providers/HtmlEditorProviders/DNNConnect.CKE/ckfinder/ckfinder.js"))
                && this.currentEditorSettings.BrowserMode.Equals(BrowserType.CKFinder))
            {
                this.clientResourceController.CreateScript(
                    Globals.ResolveUrl("~/Providers/HtmlEditorProviders/DNNConnect.CKE/ckfinder/ckfinder.js")).Register();
            }

            this.clientResourceController.CreateScript(Globals.ResolveUrl("~/Providers/HtmlEditorProviders/DNNConnect.CKE/js/editorOverride.js")).Register();

            // Load Custom JS File
            if (!string.IsNullOrEmpty(this.currentEditorSettings.CustomJsFile))
            {
                this.clientResourceController.CreateScript(this.FormatUrl(this.currentEditorSettings.CustomJsFile)).Register();
            }
        }

        private void GenerateEditorLoadScript()
        {
            var editorVar = $"editor{this.ClientIdentifier.Substring(this.ClientIdentifier.LastIndexOf("_", StringComparison.Ordinal) + 1).Replace("-", string.Empty)}";

            var editorFixedId = this.EditorFixedId;

            var postBackScript = string.Format(
                @" if (CKEDITOR && CKEDITOR.instances && CKEDITOR.instances.{0}) {{ CKEDITOR.instances.{0}.updateElement();  if (typeof Page_IsValid !== 'undefined' && !Page_IsValid) return false; CKEDITOR.instances.{0}.destroy(); }}",
                editorFixedId);

            this.RegisterOnSubmitStatement(this.GetType(), $"CKEditor_OnAjaxSubmit_{editorFixedId}", postBackScript);

            var editorScript = new StringBuilder();

            // The ASP.NET AJAX page request manager does not exist on ASP.NET Core, so the loader
            // is defined and then called directly instead of being added to add_pageLoaded. The
            // body of the loader is unchanged.
            editorScript.AppendFormat("function LoadCKEditorInstance_{0}(sender,args) {{", editorFixedId);

            editorScript.AppendFormat(
                @"if (jQuery(""[id*='UpdatePanel']"").length == 0 && CKEDITOR && CKEDITOR.instances && CKEDITOR.instances.{0}) {{ CKEDITOR.instances.{0}.updateElement();}}",
                editorFixedId);

            editorScript.AppendFormat(
                "if (document.getElementById('{0}') == null){{return;}}",
                JavaScriptStringEncode(editorFixedId));

            // Render EditorConfig
            var editorConfigScript = new StringBuilder();
            editorConfigScript.AppendFormat("var editorConfig{0} = {{", editorVar);

            var keysCount = this.Settings.Keys.Count;
            var currentCount = 0;

            // Write options
            foreach (string key in this.Settings.Keys)
            {
                var value = this.Settings[key];

                currentCount++;

                // Is boolean state or string
                if (value.Equals("true", StringComparison.InvariantCultureIgnoreCase)
                    || value.Equals("false", StringComparison.InvariantCultureIgnoreCase) || value.StartsWith("[")
                    || value.StartsWith("{", StringComparison.Ordinal) || Utility.IsNumeric(value))
                {
                    if (value.Equals("True"))
                    {
                        value = "true";
                    }
                    else if (value.Equals("False"))
                    {
                        value = "false";
                    }

                    editorConfigScript.AppendFormat("{0}:{1}", key, value);

                    editorConfigScript.Append(currentCount == keysCount ? "};" : ",");
                }
                else
                {
                    if (key == "browser")
                    {
                        continue;
                    }

                    editorConfigScript.AppendFormat("{0}:'{1}'", key, JavaScriptStringEncode(value));

                    editorConfigScript.Append(currentCount == keysCount ? "};" : ",");
                }
            }

            editorScript.AppendFormat(
                "if (CKEDITOR.instances.{0}){{return;}}",
                editorFixedId);

            // Check if we can use jQuery or $, and if both fail use ckeditor without the adapter
            editorScript.Append("if (jQuery().ckeditor) {");

            editorScript.AppendFormat("var {0} = jQuery('#{1}').ckeditor(editorConfig{0});", editorVar, JavaScriptStringEncode(editorFixedId));

            editorScript.Append("} else if ($.ckeditor) {");

            editorScript.AppendFormat("var {0} = $('#{1}').ckeditor(editorConfig{0});", editorVar, JavaScriptStringEncode(editorFixedId));

            editorScript.Append("} else {");

            editorScript.AppendFormat("var {0} = CKEDITOR.replace( '{1}', editorConfig{0});", editorVar, JavaScriptStringEncode(editorFixedId));

            editorScript.Append('}');

            // firefox maximize fix
            editorScript.Append("CKEDITOR.on('instanceReady', function (ev) {");
            editorScript.Append("ev.editor.on('maximize', function () {");
            editorScript.Append("if (ev.editor.commands.maximize.state == 1) {");
            editorScript.Append("var mainDocument = CKEDITOR.document;");
            editorScript.Append("CKEDITOR.env.gecko && mainDocument.getDocumentElement().setStyle( 'position', 'fixed' );");
            editorScript.Append('}');
            editorScript.Append("});");
            editorScript.Append("});");

            editorScript.Append("if(CKEDITOR && CKEDITOR.config){");
            editorScript.AppendFormat("  CKEDITOR.config.portalId = {0}", this.PortalSettings.PortalId);
            editorScript.Append("};");

            // End of LoadScript
            editorScript.Append('}');

            // and run it (this is what add_pageLoaded did on WebForms).
            editorScript.AppendFormat("LoadCKEditorInstance_{0}(null, null);", editorFixedId);

            this.RegisterScript($@"{editorFixedId}_CKE_Config", editorConfigScript.ToString(), true);
            this.RegisterStartupScript($@"{editorFixedId}_CKE_Startup", editorScript.ToString(), true);
        }
    }
}
