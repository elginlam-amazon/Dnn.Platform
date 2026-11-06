// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DNNConnect.CKEditorProvider.Browser;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using DNNConnect.CKEditorProvider.Constants;
using DNNConnect.CKEditorProvider.Helper;
using DNNConnect.CKEditorProvider.Objects;
using DNNConnect.CKEditorProvider.Utilities;

using DotNetNuke.Abstractions.Application;
using DotNetNuke.Abstractions.ClientResources;
using DotNetNuke.Abstractions.Logging;
using DotNetNuke.Abstractions.Portals;
using DotNetNuke.Abstractions.Security;
using DotNetNuke.Abstractions.Security.Permissions;
using DotNetNuke.Abstractions.Users;
using DotNetNuke.Common.Extensions;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Data;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Tabs;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Framework.Providers;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Security.Roles;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Services.Installer.Packages;
using DotNetNuke.Services.Localization;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.JSInterop;

using Convert = System.Convert;
using Globals = DotNetNuke.Common.Globals;

/// <summary>The file browser of the CKEditor provider.</summary>
/// <remarks>
/// <para>
/// This was <c>Browser/Browser.aspx</c> + <c>Browser.aspx.cs</c> (a <c>PageBase</c> WebForms
/// page). It is now a routable Blazor component served from the very same URL, so every
/// <c>window.open(".../Browser/Browser.aspx?...")</c> call site inside CKEditor keeps working.
/// The WebForms concepts it relied upon map as follows:
/// </para>
/// <list type="bullet">
/// <item><description>The four overloaded constructors that resolved their services off
/// <c>HttpContextSource.Current.GetScope()</c> are gone: Blazor activates a component through its
/// parameterless constructor, so every service is an <c>[Inject]</c> property.</description></item>
/// <item><description>The 15 <c>ViewState</c> keys are fields of the component, which lives for
/// as long as the circuit (the equivalent of the post back chain the view state spanned).</description></item>
/// <item><description>Every server control is component state: a panel's <c>Visible</c> is a
/// <see langword="bool"/> field, a label's <c>Text</c> is an entry of <see cref="localizedText"/>
/// or a dedicated field, the two <c>TreeView</c>s are <c>Controls/DnnTreeView.razor</c> over
/// <see cref="DnnTreeNode"/> graphs, the <c>Repeater</c> is an <c>@foreach</c> and the
/// <c>wnet:Pager</c> is <c>Controls/Pager.razor</c>.</description></item>
/// <item><description><c>Page.ClientScript.RegisterStartupScript</c> is
/// <c>Browser/js/browserInterop.js</c>, invoked from <c>OnAfterRenderAsync</c>.</description></item>
/// <item><description><c>HttpContext.Current.Request.QueryString</c> is
/// <see cref="GetQueryValue"/> (the URL is reachable over the circuit, the request is not), and
/// the few request bound facts that the circuit cannot re-read (the resolved portal alias and the
/// role based upload size limit) are handed from the pre-render to the circuit through
/// <see cref="PersistentComponentState"/>.</description></item>
/// </list>
/// <para>
/// The page-link mode, the CKEditor callback, the upload and the image editing halves live in
/// <c>Browser.razor.Editing.cs</c>; the seam between the two is the set of modifier-less partial
/// methods declared at the end of this file.
/// </para>
/// </remarks>
public partial class Browser
{
    /// <summary>The virtual path of the extension's browser folder.</summary>
    private const string BrowserRoot = "~/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/";

    /// <summary>The key the pre-rendered request state is persisted under.</summary>
    private const string PersistedStateKey = "DNNConnect.CKEditorProvider.Browser.RequestState";

    /// <summary>The Image or Link that is selected inside the Editor.</summary>
    private static string ckFileUrl;

    /// <summary>The localized text of every label, button and link of the page, keyed by its original control ID.</summary>
    /// <remarks>Replaces the <c>Text</c> property of the 35 labels, 14 buttons and 12 link buttons <c>SetLanguage</c> assigned.</remarks>
    private readonly Dictionary<string, string> localizedText = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The localized tool tip of every control of the page, keyed by its original control ID.</summary>
    /// <remarks>Replaces the <c>ToolTip</c> property <c>SetLanguage</c> assigned.</remarks>
    private readonly Dictionary<string, string> localizedToolTip = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Current Settings Base.</summary>
    private EditorProviderSettings currentSettings = new EditorProviderSettings();

    /// <summary>Settings Base for All Portals.</summary>
    private EditorProviderSettings allPortalsSettings = new EditorProviderSettings();

    /// <summary>The portal settings.</summary>
    private IPortalSettings portalSettings;

    /// <summary>The extension white list.</summary>
    private IFileExtensionAllowList extensionWhiteList;

    /// <summary>The browser modus.</summary>
    private string browserModus;

    /// <summary>The request bound state handed from the pre-render to the circuit.</summary>
    private PersistedRequestState persistedState = new PersistedRequestState();

    /// <summary>The subscription that persists <see cref="persistedState"/> at the end of the pre-render.</summary>
    private PersistingComponentStateSubscription persistingSubscription;

    /// <summary>The header link of the portal's favicon.</summary>
    /// <remarks>Replaces the <c>favicon</c> place holder <c>LoadFavIcon</c> filled.</remarks>
    private string faviconHeaderLink = string.Empty;

    /// <summary>The parsed query string of the page's URL.</summary>
    private Dictionary<string, Microsoft.Extensions.Primitives.StringValues> queryString;

    // ---- the 15 view state keys -------------------------------------------------------------

    /// <summary>Replaces <c>ViewState["AcceptFileTypes"]</c>.</summary>
    private string acceptFileTypes = ".*";

    /// <summary>Replaces <c>ViewState["CurrentFolderId"]</c>.</summary>
    private int? currentFolderId;

    /// <summary>Replaces <c>ViewState["FilesTable"]</c>.</summary>
    private IEnumerable<BrowserFile> filesTable;

    /// <summary>Replaces <c>ViewState["SortFilesAscending"]</c>.</summary>
    private bool sortFilesAscending;

    /// <summary>Replaces <c>ViewState["SortFilesDescending"]</c>.</summary>
    private bool sortFilesDescending;

    /// <summary>Replaces <c>ViewState["SortFilesDateAscending"]</c>.</summary>
    private bool sortFilesDateAscending;

    /// <summary>Replaces <c>ViewState["SortFilesDateDescending"]</c>.</summary>
    private bool sortFilesDateDescending;

    // ---- the control tree, as component state -----------------------------------------------

    /// <summary>The title of the window, replaces the <c>title</c> HTML control.</summary>
    private string titleText = "DNNConnect.CKEditorProvider.FileBrowser";

    /// <summary>Replaces <c>lblModus.Text</c>.</summary>
    private string browserModusText = string.Empty;

    /// <summary>Replaces <c>lblCurrentDir.Text</c>.</summary>
    private string currentDirectoryText = string.Empty;

    /// <summary>Replaces <c>FileSpaceUsedLabel.Text</c>.</summary>
    private string fileSpaceUsedText = string.Empty;

    /// <summary>Replaces the <c>ListViewState</c> hidden field, which <c>Browser.js</c> reads and writes.</summary>
    private string listViewState = string.Empty;

    /// <summary>Replaces <c>FileId.Text</c> (the hidden label carrying the selected file ID).</summary>
    private string selectedFileId;

    /// <summary>Replaces <c>lblFileName.Text</c> (the hidden label carrying the selected file name).</summary>
    private string selectedFileName;

    /// <summary>The nodes of the folder tree, replaces <c>FoldersTree.Nodes</c>.</summary>
    private List<DnnTreeNode> folderTreeNodes = new List<DnnTreeNode>();

    /// <summary>The value of the selected folder tree node, replaces <c>FoldersTree.SelectedValue</c>.</summary>
    private string selectedFolderNodeValue;

    /// <summary>The files of the current page of the file list, replaces the <c>FilesList</c> repeater's data source.</summary>
    private List<BrowserFile> pagedFiles = new List<BrowserFile>();

    /// <summary>The number of pages of the file list, replaces <c>PagerFileLinks.PageCount</c>.</summary>
    private int pagerPageCount;

    /// <summary>The current page of the file list, replaces <c>PagerFileLinks.CurrentPageIndex</c>.</summary>
    private int pagerCurrentPageIndex;

    /// <summary>Whether the pager renders, replaces <c>PagerFileLinks.Visible</c>.</summary>
    private bool pagerVisible;

    /// <summary>The CSS class of the four sort buttons, replaces their <c>CssClass</c>.</summary>
    private string sortAscendingCssClass = "ButtonNormal";

    /// <summary>The CSS class of the sort descending button.</summary>
    private string sortDescendingCssClass = "ButtonNormal";

    /// <summary>The CSS class of the sort by date ascending button.</summary>
    private string sortByDateAscendingCssClass = "ButtonNormal";

    /// <summary>The CSS class of the sort by date descending button.</summary>
    private string sortByDateDescendingCssClass = "ButtonNormal";

    /// <summary>The element to scroll into view after the next render, replaces <c>ScrollToSelectedFile</c>'s start-up script.</summary>
    private string pendingScrollElementId;

    /// <summary>The message to alert (and close the window) after the next render.</summary>
    private string pendingAlertAndCloseMessage;

    /// <summary>The message to alert after the next render.</summary>
    private string pendingAlertMessage;

    /// <summary>Whether the page denied the request, replaces the <c>Response.End()</c> of the "Error1" branch.</summary>
    private bool accessDenied;

    /// <summary>Whether the page already ran its (once per circuit) load.</summary>
    private bool isLoaded;

    // ---- state shared with the page-link/upload/image-editing half (Browser.razor.Editing.cs) --

    /// <summary>Replaces <c>panLinkMode.Visible</c>.</summary>
    private bool panLinkModeVisible = true;

    /// <summary>Replaces <c>panPageMode.Visible</c>.</summary>
    private bool panPageModeVisible = true;

    /// <summary>Replaces <c>panInfo.Visible</c>.</summary>
    private bool panInfoVisible = true;

    /// <summary>Replaces <c>panCreate.Visible</c>.</summary>
    private bool panCreateVisible;

    /// <summary>Replaces <c>panUploadDiv.Visible</c>.</summary>
    private bool panUploadDivVisible;

    /// <summary>Replaces <c>panThumb.Visible</c>.</summary>
    private bool panThumbVisible;

    /// <summary>Replaces <c>panImageEdHead.Visible</c>.</summary>
    private bool panImageEdHeadVisible;

    /// <summary>Replaces <c>panImageEditor.Visible</c>.</summary>
    private bool panImageEditorVisible;

    /// <summary>Replaces <c>panImagePreview.Visible</c>.</summary>
    private bool panImagePreviewVisible;

    /// <summary>Replaces <c>panelLoading.Visible</c>.</summary>
    private bool panelLoadingVisible;

    /// <summary>Replaces <c>cmdClose.Visible</c>.</summary>
    private bool cmdCloseVisible = true;

    /// <summary>Replaces <c>BrowserMode.Visible</c>.</summary>
    private bool browserModeVisible;

    /// <summary>Replaces <c>BrowserMode.SelectedValue</c>.</summary>
    private string browserModeSelectedValue = "file";

    /// <summary>Replaces the two items of the <c>BrowserMode</c> radio button list.</summary>
    private List<BrowserListItem> browserModeItems =
    [
        new BrowserListItem { Text = "File Link", Value = "file" },
        new BrowserListItem { Text = "Page Link", Value = "page" },
    ];

    /// <summary>Replaces the two items of the <c>rblLinkType</c> radio button list.</summary>
    private List<BrowserListItem> linkTypeItems =
    [
        new BrowserListItem { Text = "Relative URL", Value = "relLnk" },
        new BrowserListItem { Text = "Absolute URL", Value = "absLnk" },
    ];

    /// <summary>Replaces <c>rblLinkType.SelectedValue</c>.</summary>
    private string linkTypeSelectedValue = "relLnk";

    /// <summary>Replaces the items of the <c>LanguageList</c> drop down (its "None" item included).</summary>
    private List<BrowserListItem> languageListItems =
    [
        new BrowserListItem { Text = "None", Value = string.Empty },
    ];

    /// <summary>Replaces <c>LanguageList.SelectedValue</c>.</summary>
    private string languageListSelectedValue = string.Empty;

    /// <summary>Replaces <c>LanguageRow.Visible</c>.</summary>
    private bool languageRowVisible = true;

    /// <summary>Replaces the items of the <c>AnchorList</c> drop down (its "None" item included).</summary>
    private List<BrowserListItem> anchorListItems =
    [
        new BrowserListItem { Text = "None", Value = string.Empty },
    ];

    /// <summary>Replaces <c>AnchorList.SelectedValue</c>.</summary>
    private string anchorListSelectedValue = string.Empty;

    /// <summary>Replaces <c>AnchorList.Visible</c>.</summary>
    private bool anchorListVisible;

    /// <summary>Replaces <c>LabelAnchor.Visible</c>.</summary>
    private bool labelAnchorVisible;

    /// <summary>Replaces <c>chkHumanFriendy.Checked</c>.</summary>
    private bool humanFriendlyChecked = true;

    /// <summary>Replaces <c>OverrideFile.Checked</c>.</summary>
    private bool overrideFileChecked;

    /// <summary>The nodes of the page tree, replaces <c>dnntreeTabs.Nodes</c>.</summary>
    private List<DnnTreeNode> tabTreeNodes = new List<DnnTreeNode>();

    /// <summary>The value of the selected page tree node, replaces <c>dnntreeTabs.SelectedValue</c>.</summary>
    private string selectedTabNodeValue;

    /// <summary>The confirmation prompt of the delete command, replaces <c>ClientAPI.AddButtonConfirm</c>.</summary>
    private string deleteConfirmMessage;

    /// <summary>Replaces <c>cmdUpload.Enabled</c>.</summary>
    private bool cmdUploadEnabled;

    /// <summary>Replaces <c>cmdCreate.Enabled</c>.</summary>
    private bool cmdCreateEnabled;

    /// <summary>Replaces <c>Syncronize.Enabled</c>.</summary>
    private bool syncronizeEnabled;

    /// <summary>Replaces <c>cmdDelete.Enabled</c>.</summary>
    private bool cmdDeleteEnabled;

    /// <summary>Replaces <c>cmdResizer.Enabled</c>.</summary>
    private bool cmdResizerEnabled;

    /// <summary>Replaces <c>cmdDownload.Enabled</c>.</summary>
    private bool cmdDownloadEnabled;

    /// <summary>Replaces <c>cmdUpload.CssClass</c>.</summary>
    private string cmdUploadCssClass = "LinkDisabled";

    /// <summary>Replaces <c>cmdCreate.CssClass</c>.</summary>
    private string cmdCreateCssClass = "LinkDisabled";

    /// <summary>Replaces <c>Syncronize.CssClass</c>.</summary>
    private string syncronizeCssClass = "LinkDisabled";

    /// <summary>Replaces <c>cmdDelete.CssClass</c>.</summary>
    private string cmdDeleteCssClass = "LinkDisabled";

    /// <summary>Replaces <c>cmdResizer.CssClass</c>.</summary>
    private string cmdResizerCssClass = "LinkDisabled";

    /// <summary>Replaces <c>cmdDownload.CssClass</c>.</summary>
    private string cmdDownloadCssClass = "LinkDisabled";

    /// <summary>Initializes a new instance of the <see cref="Browser"/> class.</summary>
    public Browser()
    {
    }

    /// <summary>Gets or sets the accept file types.</summary>
    /// <value>The accept file types.</value>
    public string AcceptFileTypes
    {
        get => this.acceptFileTypes ?? ".*";
        set => this.acceptFileTypes = value;
    }

    /// <summary>Gets Current Language from Url.</summary>
    protected string LanguageCode
    {
        get
        {
            var language = this.GetQueryValue("lang");
            return !string.IsNullOrEmpty(language) ? language : "en-US";
        }
    }

    /// <summary>Gets the Name for the Current Resource file name.</summary>
    /// <value>The resource executable file.</value>
    /// <remarks>
    /// Replaces the <c>Request.ServerVariables["SCRIPT_NAME"]</c> derived path of the original
    /// (there are no server variables on ASP.NET Core): the resource file is named after the page
    /// this component replaces, which keeps every key and every portal level override.
    /// </remarks>
    protected string ResXFile =>
        Globals.ResolveUrl($"~/Providers/HtmlEditorProviders/DNNConnect.CKE/{Localization.LocalResourceDirectory}/Browser.resx");

    /// <summary>Gets the maximum size of the upload.</summary>
    /// <value>The maximum size of the upload.</value>
    protected long MaxUploadSize
    {
        get
        {
            return this.currentSettings.UploadFileSizeLimit > 0
                   && this.currentSettings.UploadFileSizeLimit <= Utility.GetMaxUploadSize()
                       ? this.currentSettings.UploadFileSizeLimit
                       : Utility.GetMaxUploadSize();
        }
    }

    /// <summary>Gets or sets the current folder ID.</summary>
    protected int CurrentFolderId
    {
        get => this.currentFolderId ?? this.StartingDir().FolderID;

        set => this.currentFolderId = value;
    }

    /// <summary>Gets or sets the JavaScript runtime the page's client side seam is called through.</summary>
    [Inject]
    protected IJSRuntime JsRuntime { get; set; }

    /// <summary>Gets or sets the navigation manager the query string is read from.</summary>
    [Inject]
    protected NavigationManager Navigation { get; set; }

    /// <summary>Gets or sets the state handed from the pre-render to the circuit.</summary>
    [Inject]
    protected PersistentComponentState ComponentState { get; set; }

    /// <summary>Gets or sets the host settings.</summary>
    [Inject]
    protected IHostSettings HostSettings { get; set; }

    /// <summary>Gets or sets the application status.</summary>
    [Inject]
    protected IApplicationStatusInfo AppStatus { get; set; }

    /// <summary>Gets or sets the user controller.</summary>
    [Inject]
    protected IUserController UserController { get; set; }

    /// <summary>Gets or sets the portal controller.</summary>
    [Inject]
    protected IPortalController PortalController { get; set; }

    /// <summary>Gets or sets the host settings service.</summary>
    [Inject]
    protected IHostSettingsService HostSettingsService { get; set; }

    /// <summary>Gets or sets the event logger.</summary>
    [Inject]
    protected IEventLogger EventLogger { get; set; }

    /// <summary>Gets or sets the permission definition service.</summary>
    [Inject]
    protected IPermissionDefinitionService PermissionDefinitionService { get; set; }

    /// <summary>Gets or sets the portal alias service.</summary>
    [Inject]
    protected IPortalAliasService PortalAliasService { get; set; }

    /// <summary>Gets or sets the file manager.</summary>
    [Inject]
    protected IFileManager FileManager { get; set; }

    /// <summary>Gets or sets the folder manager.</summary>
    [Inject]
    protected IFolderManager FolderManager { get; set; }

    /// <summary>Gets or sets the file content type manager.</summary>
    [Inject]
    protected IFileContentTypeManager FileContentTypeManager { get; set; }

    /// <summary>Gets or sets the data provider.</summary>
    [Inject]
    protected DataProvider DataProviderInstance { get; set; }

    /// <summary>Gets or sets the package controller.</summary>
    [Inject]
    protected IPackageController PackageController { get; set; }

    /// <summary>Gets or sets the tab controller.</summary>
    [Inject]
    protected ITabController TabController { get; set; }

    /// <summary>Gets or sets the client resource controller.</summary>
    [Inject]
    protected IClientResourceController ClientResourceController { get; set; }

    /// <summary>Set the file url from JavaScript to code.</summary>
    /// <param name="fileUrl">The file url.</param>
    /// <remarks>
    /// Was an ASP.NET AJAX page method ([ScriptService]/[WebMethod]), which does not exist on
    /// ASP.NET Core. It is now called by <see cref="SetFileEndpoint"/>, which is mapped onto a real
    /// endpoint URL; this method keeps the behaviour (and the static state the page reads back).
    /// </remarks>
    public static void SetFile(string fileUrl)
    {
        ckFileUrl = fileUrl;
    }

    /// <summary>Get all Files and Put them in a DataTable for the GridView.</summary>
    /// <param name="currentFolderInfo">The current folder info.</param>
    /// <returns>The Files.</returns>
    public List<BrowserFile> GetFiles(IFolderInfo currentFolderInfo)
    {
        var sizeResx = this.LocalizeString("Size.Text");
        var createdResx = this.LocalizeString("Created.Text");

        var type = this.GetQueryValue("Type");
        if (string.IsNullOrEmpty(type))
        {
            type = "Link";
        }

        // Get the files
        var files = this.FolderManager.GetFiles(currentFolderInfo).ToList();

        if (this.sortFilesAscending)
        {
            Utility.SortAscending(files, item => item.FileName);
        }

        if (this.sortFilesDescending)
        {
            Utility.SortDescending(files, item => item.FileName);
        }

        if (this.sortFilesDateAscending)
        {
            Utility.SortAscending(files, item => item.CreatedOnDate);
        }

        if (this.sortFilesDateDescending)
        {
            Utility.SortDescending(files, item => item.CreatedOnDate);
        }

        return files.Select(fileItem =>
        {
            var name = fileItem.FileName;
            var extension = fileItem.Extension;

            if (currentFolderInfo.IsProtected)
            {
                name = GetFileNameCleaned(name);
                extension = Path.GetExtension(name);
            }

            var infoHtml =
                $"""
                 <span class="FileName">{WebUtility.HtmlEncode(name)}</span><br />
                 <span class="FileInfo">{WebUtility.HtmlEncode(sizeResx)}: {fileItem.Size}</span><br />
                 <span class="FileInfo">{WebUtility.HtmlEncode(createdResx)}: {fileItem.LastModificationTime}</span>
                 """;
            switch (type)
            {
                case "Image":
                    {
                        if (BrowserMedia.AllowedImageExtensions.Contains(extension))
                        {
                            return new BrowserFile
                            {
                                PictureUrl = this.FileManager.GetUrl(fileItem),
                                FileName = name,
                                FileId = fileItem.FileId,
                                InfoHtml = infoHtml,
                            };
                        }
                    }

                    return null;
                case "Flash":
                    {
                        if (BrowserMedia.AllowedFlashExtensions.Contains(extension))
                        {
                            return new BrowserFile
                            {
                                PictureUrl = "images/types/swf.png",
                                FileName = name,
                                FileId = fileItem.FileId,
                                InfoHtml = infoHtml,
                            };
                        }
                    }

                    return null;
                default:
                    if (extension.StartsWith(".", StringComparison.Ordinal))
                    {
                        extension = extension.Replace(".", string.Empty);
                    }

                    if (extension.Length <= 1 || !this.extensionWhiteList.IsAllowedExtension(extension))
                    {
                        return null;
                    }

                    string pictureUrl;
                    if (BrowserMedia.AllowedImageExtensions.Any(sAllowImgExt => name.EndsWith(sAllowImgExt, StringComparison.OrdinalIgnoreCase)))
                    {
                        pictureUrl = this.FileManager.GetUrl(fileItem);
                    }
                    else
                    {
                        pictureUrl = $"images/types/{extension}.png";
                        if (!File.Exists(this.MapPath(pictureUrl)))
                        {
                            pictureUrl = "images/types/unknown.png";
                        }
                    }

                    return new BrowserFile
                    {
                        PictureUrl = pictureUrl,
                        FileName = name,
                        FileId = fileItem.FileId,
                        InfoHtml = infoHtml,
                    };
            }
        }).Where(file => file is not null)
        .ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// This is <c>OnInit</c> (which called <c>InitializeComponent</c>), <c>LoadFavIcon</c> and
    /// <c>Page_Load</c>: there is no <c>IsPostBack</c> on a Blazor circuit, the "not a post back"
    /// work is simply the work this one-time initialization does.
    /// </remarks>
    protected override void OnInitialized()
    {
        base.OnInitialized();

        this.persistingSubscription =
            this.ComponentState.RegisterOnPersisting(this.PersistRequestStateAsync);

        if (this.ComponentState.TryTakeFromJson<PersistedRequestState>(PersistedStateKey, out var restored)
            && restored is not null)
        {
            this.persistedState = restored;
        }

        // InitializeComponent(): the event wiring is the markup's @onclick/@bind; what is left of
        // it is the portal settings resolution.
        this.portalSettings = this.GetPortalSettings();

        // LoadFavIcon()
        this.faviconHeaderLink = DotNetNuke.UI.Internals.FavIcon.GetHeaderLink(
            this.HostSettings, this.portalSettings?.PortalId ?? this.HostSettings.HostPortalId);

        if (!this.isLoaded)
        {
            this.isLoaded = true;

            this.PageLoad();

            // OnPreRender(): the client resource registrations.
            this.RegisterClientResources();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Runs the start-up scripts the WebForms page registered on
    /// <c>Page.ClientScript</c>/wrote into the response: the "scroll the selected file into view"
    /// statement of <c>ScrollToSelectedFile</c> and the <c>alert()</c>/<c>self.close()</c>
    /// statements of the error branches.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (this.pendingScrollElementId is null
            && this.pendingAlertMessage is null
            && this.pendingAlertAndCloseMessage is null
            && this.pendingInteropCalls.Count == 0)
        {
            return;
        }

        IJSObjectReference module;
        try
        {
            module = await this.JsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/js/browserInterop.js");
        }
        catch (JSException)
        {
            // the extension's assets are not reachable; the page degrades to plain markup.
            return;
        }

        if (this.pendingAlertAndCloseMessage is not null)
        {
            var message = this.pendingAlertAndCloseMessage;
            this.pendingAlertAndCloseMessage = null;
            await module.InvokeVoidAsync("alertAndClose", message);
            return;
        }

        if (this.pendingAlertMessage is not null)
        {
            var message = this.pendingAlertMessage;
            this.pendingAlertMessage = null;
            await module.InvokeVoidAsync("showAlert", message);
        }

        if (this.pendingScrollElementId is not null)
        {
            var elementId = this.pendingScrollElementId;
            this.pendingScrollElementId = null;
            await module.InvokeVoidAsync("scrollToElement", elementId);
        }

        // The start-up scripts of the page-link/upload/image-editing half (Browser.razor.Editing.cs).
        await this.FlushPendingInteropCallsAsync(module);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.persistingSubscription.Dispose();
            this.DisposeEditing();
        }

        base.Dispose(disposing);
    }

    /// <summary>Gets the localized string corresponding to the <paramref name="key"/>.</summary>
    /// <param name="key">The resource key to find.</param>
    /// <returns>The localized Text.</returns>
    protected string LocalizeString(string key)
    {
        return Localization.GetString(key, this.ResXFile, this.LanguageCode);
    }

    /// <summary>Shows the files in directory.</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="pagerChanged">if set to <see langword="true"/> [pager changed].</param>
    protected void ShowFilesIn(string directory, bool pagerChanged = false)
    {
        var currentFolderInfo = Utility.ConvertFilePathToFolderInfo(directory, this.portalSettings);

        this.ShowFilesIn(currentFolderInfo, pagerChanged);
    }

    /// <summary>Gets the text of a control of the page, as <c>SetLanguage</c> localized it.</summary>
    /// <param name="controlId">The ID the control had on <c>Browser.aspx</c>.</param>
    /// <returns>The localized text, or an empty string when the control has none (yet).</returns>
    protected string Text(string controlId)
    {
        return this.localizedText.TryGetValue(controlId, out var text) ? text : string.Empty;
    }

    /// <summary>Gets the tool tip of a control of the page, as <c>SetLanguage</c> localized it.</summary>
    /// <param name="controlId">The ID the control had on <c>Browser.aspx</c>.</param>
    /// <returns>The localized tool tip, or <see langword="null"/> when the control has none.</returns>
    protected string ToolTip(string controlId)
    {
        return this.localizedToolTip.TryGetValue(controlId, out var toolTip) ? toolTip : null;
    }

    /// <summary>Formats a MapPath into relative MapUrl.</summary>
    /// <param name="sPath">MapPath Input string.</param>
    /// <param name="applicationMapPath">The physical path of the application root.</param>
    /// <returns>The output URL string.</returns>
    /// <remarks>
    /// Replaces <c>Server.MapPath("~")</c> with the injected application map path and
    /// <c>Request.ApplicationPath</c> with the application path <c>Globals.ApplicationPath</c>
    /// carries, which is what the request value resolved to.
    /// </remarks>
    private static string MapUrl(string sPath, string applicationMapPath)
    {
        return Globals.ApplicationPath +
               sPath.Replace(applicationMapPath, string.Empty).Replace(@"\", "/");
    }

    /// <summary>Get File Name without .resources extension.</summary>
    /// <param name="fileName">File Name.</param>
    /// <returns>Cleaned File Name.</returns>
    private static string GetFileNameCleaned(string fileName)
    {
        return fileName.Replace(".resources", string.Empty);
    }

    /// <summary>Get Folder Icon.</summary>
    /// <param name="folderInfo">The folder info.</param>
    /// <returns>The URL of the folder's icon.</returns>
    private static string GetFolderIcon(IFolderInfo folderInfo)
    {
        if (folderInfo == null || string.IsNullOrEmpty(folderInfo.FolderPath))
        {
            return "Images/folder.gif";
        }

        switch (folderInfo.StorageLocation)
        {
            case (int)FolderController.StorageLocationTypes.InsecureFileSystem:
                return "Images/folder.gif";
            case (int)FolderController.StorageLocationTypes.SecureFileSystem:
                return "Images/folderLocked.gif";
            case (int)FolderController.StorageLocationTypes.DatabaseSecure:
                return "Images/folderdb.gif";
            default:
                var folderMapping = FolderMappingController.Instance.GetFolderMapping(folderInfo.PortalID, folderInfo.FolderMappingID);
                if (folderMapping != null)
                {
                    return folderMapping.ImageUrl;
                }

                return "Images/folder.gif";
        }
    }

    /// <summary>Gets the ID of the element of a file of the list.</summary>
    /// <param name="index">The zero based index of the file in the rendered page of the list.</param>
    /// <returns>The element ID.</returns>
    /// <remarks>Keeps the <c>ClientID</c> the <c>FilesList</c> repeater gave its <c>FileListItem</c> link button, which the scroll script addressed.</remarks>
    private static string FileListItemElementId(int index)
    {
        return $"FilesList_FileListItem_{index}";
    }

    /// <summary>Gets the ID of the row element of a file of the list.</summary>
    /// <param name="index">The zero based index of the file in the rendered page of the list.</param>
    /// <returns>The element ID.</returns>
    private static string ListRowElementId(int index)
    {
        return $"FilesList_ListRow_{index}";
    }

    /// <summary>Gets the <c>title</c> attribute of a file's row.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The file name part of the file's picture URL, which is what the row carried.</returns>
    private static string ListRowTitle(BrowserFile file)
    {
        return file.PictureUrl.Substring(file.PictureUrl.LastIndexOf("/", StringComparison.Ordinal) + 1);
    }

    /// <summary>Handles the Load event of the Page control.</summary>
    /// <remarks>This is <c>Page_Load</c>, line for line; the <c>!IsPostBack</c> branches run because this is the one load of the page.</remarks>
    private void PageLoad()
    {
        JavaScript.RequestRegistration(this.AppStatus, this.EventLogger, this.portalSettings, CommonJs.jQuery);

        this.SetSortButtonClasses();

        this.extensionWhiteList = this.HostSettings.AllowedExtensionAllowList;

        var mode = this.GetQueryValue("mode");
        if (!string.IsNullOrEmpty(mode))
        {
            this.currentSettings.SettingMode = Enum.Parse<SettingsMode>(mode);
        }

        ProviderConfiguration providerConfiguration = ProviderConfiguration.GetProviderConfiguration("htmlEditor");
        Provider objProvider = (Provider)providerConfiguration.Providers[providerConfiguration.DefaultProvider];

        var settingsDictionary = EditorController.GetEditorHostSettings(this.HostSettings);
        var portalRoles = RoleController.Instance.GetRoles(this.portalSettings.PortalId);

        this.allPortalsSettings = SettingsUtil.LoadEditorSettingsByKey(
            this.portalSettings,
            this.currentSettings,
            settingsDictionary,
            SettingConstants.HostKey,
            null);

        this.currentSettings = this.currentSettings.SettingMode switch
        {
            SettingsMode.Default => SettingsUtil.GetDefaultSettings(
                this.portalSettings,
                this.HostSettings,
                this.portalSettings.HomeDirectoryMapPath,
                objProvider.Attributes["ck_configFolder"],
                portalRoles),
            SettingsMode.Host => SettingsUtil.LoadEditorSettingsByKey(
                this.portalSettings,
                this.currentSettings,
                settingsDictionary,
                SettingConstants.HostKey,
                null),
            SettingsMode.Portal => SettingsUtil.LoadEditorSettingsByKey(
                this.portalSettings,
                this.currentSettings,
                settingsDictionary,
                SettingConstants.PortalKey(this.GetQueryValue("PortalID")),
                portalRoles),
            SettingsMode.Page => SettingsUtil.LoadEditorSettingsByKey(
                this.portalSettings,
                this.currentSettings,
                settingsDictionary,
                $"DNNCKT#{this.GetQueryValue("tabid")}#",
                portalRoles),
            SettingsMode.ModuleInstance => SettingsUtil.LoadModuleSettings(
                this.portalSettings,
                this.currentSettings,
                $"DNNCKMI#{this.GetQueryValue("mid")}#INS#{this.GetQueryValue("ckId")}#",
                int.Parse(this.GetQueryValue("mid"), CultureInfo.InvariantCulture),
                portalRoles),
            _ => this.currentSettings,
        };

        // set current Upload file size limit
        this.currentSettings.UploadFileSizeLimit = this.GetCurrentUserUploadSize();

        if ((this.currentSettings.BrowserMode.Equals(BrowserType.StandardBrowser) || this.currentSettings.ImageButtonMode.Equals(ImageButtonType.EasyImageButton))
            && this.IsAuthenticated())
        {
            string command = null;

            try
            {
                if (this.GetQueryValue("Command") != null)
                {
                    command = this.GetQueryValue("Command");
                }
            }
            catch (Exception)
            {
                command = null;
            }

            try
            {
                if (this.GetQueryValue("Type") != null)
                {
                    this.browserModus = this.GetQueryValue("Type");
                    var browserModusText = this.LocalizeString("lblBrowserModus.Text");
                    var browserModusTypeKey = $"BrowserModus.{this.browserModus}.Text";
                    var browserModusTypeText = this.LocalizeString(browserModusTypeKey);
                    this.browserModusText = string.Format(CultureInfo.CurrentCulture, browserModusText, browserModusTypeText);

                    this.AcceptFileTypes = this.GetAcceptedFileTypes();

                    this.titleText = $"{this.browserModusText} - DNNConnect.CKEditorProvider.FileBrowser";

                    this.anchorListVisible = this.currentSettings.UseAnchorSelector;
                    this.labelAnchorVisible = this.currentSettings.UseAnchorSelector;

                    this.listViewState = this.currentSettings.FileListViewMode.ToString();

                    // Set default link mode
                    this.linkTypeSelectedValue = this.currentSettings.DefaultLinkMode switch
                    {
                        LinkMode.RelativeURL => "relLink",
                        LinkMode.AbsoluteURL => "absLnk",
                        _ => this.linkTypeSelectedValue,
                    };

                    switch (this.browserModus)
                    {
                        case "Link":
                            this.browserModeVisible = true;

                            if (this.currentSettings.ShowPageLinksTabFirst)
                            {
                                this.browserModeSelectedValue = "page";
                                this.panLinkModeVisible = false;
                                this.panPageModeVisible = true;

                                this.browserModusText = string.Format(
                                    CultureInfo.CurrentCulture,
                                    this.LocalizeString("BrowserModus.Text"),
                                    $"Page {this.browserModus}");
                                this.titleText = $"{this.browserModusText} - DNNConnect.CKEditorProvider.FileBrowser";

                                this.RenderTabs();
                            }
                            else
                            {
                                this.browserModeSelectedValue = "file";
                                this.panPageModeVisible = false;
                            }

                            break;
                        case "Image":
                        case "Flash":
                        default:
                            this.browserModeVisible = false;
                            this.panPageModeVisible = false;
                            break;
                    }
                }
            }
            catch (Exception)
            {
                this.browserModus = null;
            }

            if (command != null)
            {
                // The CKEditor "quick upload" commands (FileUpload/FlashUpload/ImageUpload/
                // EasyImageUpload) POST the file to this very URL and expect a response which is
                // *only* the JSON (or *only* the one <script> statement) the original wrote with
                // Response.ClearContent()/Write()/End(). A component cannot produce that - the host
                // document is rendered around it - so those four commands are served by
                // QuickUploadEndpoint, a MapPost on this URL which is mapped before
                // MapRazorComponents and therefore wins the route. Every other Command value did
                // nothing here, exactly as it does now.
                return;
            }
            else
            {
                this.overrideFileChecked = this.currentSettings.OverrideFileOnUpload;

                this.SetLanguage();

                this.GetLanguageList();

                var startFolder = this.StartingDir();

                this.FillFolderTree(startFolder);

                bool folderSelected = false;

                if (!string.IsNullOrEmpty(ckFileUrl))
                {
                    try
                    {
                        folderSelected = this.SelectFolderFile(ckFileUrl);
                        ckFileUrl = null;
                    }
                    catch (Exception)
                    {
                        folderSelected = false;
                        ckFileUrl = null;
                    }
                }

                if (!folderSelected)
                {
                    var folderName = !string.IsNullOrEmpty(startFolder.FolderPath) ?
                        startFolder.FolderPath : this.LocalizeString("RootFolder.Text");
                    this.currentDirectoryText = folderName;

                    this.ShowFilesIn(startFolder);
                }

                this.FillQualityPercentages();
            }
        }
        else
        {
            // Replaces Response.Write("<script>javascript:alert(...);self.close();</script>") +
            // Response.End(): the page renders nothing and the statement runs after the render.
            this.accessDenied = true;
            this.pendingAlertAndCloseMessage = this.LocalizeString("Error1.Text");
        }
    }

    /// <summary>Registers JavaScripts and CSS.</summary>
    /// <remarks>
    /// This is the <c>OnPreRender</c> override, unchanged, except that the page relative URLs are
    /// resolved from the extension's browser folder (there is no <c>Control.ResolveUrl</c>).
    /// </remarks>
    private void RegisterClientResources()
    {
        JavaScript.RequestRegistration(this.AppStatus, this.EventLogger, this.portalSettings, CommonJs.jQuery);
        JavaScript.RequestRegistration(this.AppStatus, this.EventLogger, this.portalSettings, CommonJs.jQueryUI);
        this.ClientResourceController.CreateScript(Globals.ResolveUrl($"{BrowserRoot}js/Browser.js")).Register();
        this.ClientResourceController.CreateScript(Globals.ResolveUrl($"{BrowserRoot}js/jquery.ImageSlider.js")).Register();
        this.ClientResourceController.CreateScript(Globals.ResolveUrl($"{BrowserRoot}js/jquery.cropzoom.js")).Register();
        this.ClientResourceController.CreateScript(Globals.ResolveUrl($"{BrowserRoot}js/jquery.ImageResizer.js")).Register();
        this.ClientResourceController.CreateScript(Globals.ResolveUrl($"{BrowserRoot}js/jquery.pagemethod.js")).Register();
        this.ClientResourceController.CreateScript(Globals.ResolveUrl($"{BrowserRoot}js/jquery.fileupload.comb.min.js")).Register();
        this.ClientResourceController.CreateStylesheet("https://ajax.googleapis.com/ajax/libs/jqueryui/1/themes/blitzer/jquery-ui.css").Register();
        this.ClientResourceController.CreateStylesheet(Globals.ResolveUrl($"{BrowserRoot}Browser.comb.min.css")).Register();

        this.GetSelectedImageOrLink();
    }

    /// <summary>Persists the request bound facts so the interactive instance can read them back.</summary>
    /// <returns>A completed task.</returns>
    private Task PersistRequestStateAsync()
    {
        this.ComponentState.PersistAsJson(PersistedStateKey, this.persistedState);
        return Task.CompletedTask;
    }

    /// <summary>Gets a value indicating whether the user of the request is authenticated.</summary>
    /// <returns><see langword="true"/> when the user is authenticated.</returns>
    /// <remarks>
    /// Replaces <c>HttpContext.Current.Request.IsAuthenticated</c>. The route carries
    /// <c>[Authorize]</c> (which replaces the <c>.dnn</c> manifest's <c>deny users="?"</c> rule for
    /// this URL), so an anonymous visitor never reaches the component; the check is kept because it
    /// also gates the "standard browser"/"easy image" branch.
    /// </remarks>
    private bool IsAuthenticated()
    {
        var request = this.Request;
        if (request is not null)
        {
            return request.IsAuthenticated();
        }

        // over the circuit there is no request; the user got past [Authorize] to open it.
        return true;
    }

    /// <summary>Computes the current user's upload file size limit.</summary>
    /// <returns>The limit, in bytes.</returns>
    /// <remarks>
    /// <c>SettingsUtil.GetCurrentUserUploadSize</c> needs the request to tell an anonymous visitor
    /// apart from an authenticated one. It is computed while the page pre-renders (where the
    /// request exists) and handed to the circuit, so the role based limit is the same in both.
    /// </remarks>
    private int GetCurrentUserUploadSize()
    {
        var request = this.Request;
        if (request is not null)
        {
            this.persistedState.UploadFileSizeLimit =
                SettingsUtil.GetCurrentUserUploadSize(this.currentSettings, this.portalSettings, request);
        }

        return this.persistedState.UploadFileSizeLimit >= 0
            ? this.persistedState.UploadFileSizeLimit
            : this.currentSettings.UploadFileSizeLimit;
    }

    /// <summary>Reads a value off the query string of the page's URL.</summary>
    /// <param name="name">The name of the query string parameter.</param>
    /// <returns>The value, or <see langword="null"/> when it was not supplied.</returns>
    /// <remarks>Replaces <c>HttpContext.Current.Request.QueryString[...]</c>, which is not reachable over a Blazor circuit.</remarks>
    private string GetQueryValue(string name)
    {
        this.queryString ??= QueryHelpers.ParseQuery(new Uri(this.Navigation.Uri).Query);
        return this.queryString.TryGetValue(name, out var values) ? values.ToString() : null;
    }

    /// <summary>Maps a path of the extension's browser folder onto the file system.</summary>
    /// <param name="path">An application (<c>~/</c> or <c>/</c>) or page relative path.</param>
    /// <returns>The physical path.</returns>
    /// <remarks>
    /// Replaces <c>Control.MapPath</c>, which resolved a relative path against the page's own
    /// directory and an application relative one against the application root.
    /// </remarks>
    private string MapPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        var applicationPath = Globals.ApplicationPath;
        var relative = path.Replace('\\', '/');

        if (relative.StartsWith("~/", StringComparison.Ordinal))
        {
            relative = relative.Substring(2);
        }
        else if (!string.IsNullOrEmpty(applicationPath)
                 && relative.StartsWith($"{applicationPath}/", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative.Substring(applicationPath.Length + 1);
        }
        else if (relative.StartsWith("/", StringComparison.Ordinal))
        {
            relative = relative.Substring(1);
        }
        else
        {
            // page relative, i.e. relative to Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/
            relative = $"Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/{relative}";
        }

        return Path.Combine(
            this.AppStatus.ApplicationMapPath, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>Builds the absolute URL of a file URL.</summary>
    /// <param name="fileUrl">The file URL.</param>
    /// <returns>The absolute URL.</returns>
    /// <remarks>Replaces <c>HttpContext.Current.Request.Url</c> with the URL of the page, which the navigation manager carries in both render passes.</remarks>
    private string BuildAbsoluteUrl(string fileUrl)
    {
        if (fileUrl.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
        {
            return fileUrl;
        }

        var requestUrl = new Uri(this.Navigation.Uri);
        return $"{requestUrl.Scheme}://{requestUrl.Authority}{fileUrl}";
    }

    /// <summary>Gets the folder resolution cascade of the page, over the page's current state.</summary>
    /// <returns>The resolver.</returns>
    /// <remarks>
    /// <c>StartingDir</c>, <c>GetUserFolderInfo</c> and the folder permission helpers live on
    /// <see cref="BrowserFolderResolver"/>, because <see cref="QuickUploadEndpoint"/> - which
    /// serves the CKEditor <c>*Upload</c> commands of this very URL - resolves the upload folder
    /// with the same cascade.
    /// </remarks>
    private BrowserFolderResolver GetFolderResolver()
    {
        return new BrowserFolderResolver(
            this.portalSettings,
            this.currentSettings,
            this.allPortalsSettings,
            this.browserModus,
            this.FolderManager,
            this.UserController,
            this.PermissionDefinitionService);
    }

    /// <summary>Set Folder Permission.</summary>
    /// <param name="folderId">The Folder ID.</param>
    private void SetFolderPermission(int folderId)
    {
        this.GetFolderResolver().SetFolderPermission(folderId);
    }

    /// <summary>Hide Create Items if User has no write access to the Current Folder.</summary>
    /// <param name="folderId">The folder id to check.</param>
    /// <param name="isFileSelected">if set to <see langword="true"/> [is file selected].</param>
    private void CheckFolderAccess(int folderId, bool isFileSelected)
    {
        var hasWriteAccess = Utility.CheckIfUserHasFolderWriteAccess(folderId, this.portalSettings);

        this.cmdUploadEnabled = hasWriteAccess;
        this.cmdCreateEnabled = hasWriteAccess;
        this.syncronizeEnabled = hasWriteAccess;
        this.cmdDeleteEnabled = hasWriteAccess && isFileSelected;
        this.cmdResizerEnabled = hasWriteAccess && isFileSelected;
        this.cmdDownloadEnabled = isFileSelected;

        this.cmdUploadCssClass = hasWriteAccess ? "LinkNormal" : "LinkDisabled";
        this.cmdCreateCssClass = hasWriteAccess ? "LinkNormal" : "LinkDisabled";
        this.syncronizeCssClass = hasWriteAccess ? "LinkNormal" : "LinkDisabled";
        this.cmdDeleteCssClass = hasWriteAccess && isFileSelected ? "LinkNormal" : "LinkDisabled";
        this.cmdResizerCssClass = hasWriteAccess && isFileSelected ? "LinkNormal" : "LinkDisabled";
        this.cmdDownloadCssClass = isFileSelected ? "LinkNormal" : "LinkDisabled";
    }

    /// <summary>The get portal settings.</summary>
    /// <returns>Current Portal Settings.</returns>
    /// <remarks>
    /// The alias resolution of the original, with the query string read off the URL. The resolved
    /// alias is remembered, because <c>Globals.GetDomainName(Request)</c> cannot run over the
    /// circuit; the settings are then rebuilt from the very same alias and tab.
    /// </remarks>
    private IPortalSettings GetPortalSettings()
    {
        int iTabId = 0, iPortalId = 0;

        try
        {
            var tabIdValue = this.GetQueryValue("tabid");
            if (tabIdValue != null)
            {
                iTabId = int.Parse(tabIdValue, CultureInfo.InvariantCulture);
            }

            var portalIdValue = this.GetQueryValue("PortalID");
            if (portalIdValue != null)
            {
                iPortalId = int.Parse(portalIdValue, CultureInfo.InvariantCulture);
            }

            string sPortalAlias;
            if (this.Request is not null)
            {
                string sDomainName = Globals.GetDomainName(this.Request, true);

                sPortalAlias = this.PortalAliasService.GetPortalAliasByPortal(iPortalId, sDomainName);

                this.persistedState.PortalAlias = sPortalAlias;
                this.persistedState.DomainName = sDomainName;
                this.persistedState.TabId = iTabId;
            }
            else
            {
                sPortalAlias = this.persistedState.PortalAlias;
                iTabId = this.persistedState.TabId;
            }

            var objPortalAliasInfo = this.PortalAliasService.GetPortalAlias(sPortalAlias);
            return PortalSettings.Create(iTabId, objPortalAliasInfo);
        }
        catch (Exception)
        {
            return PortalSettings.Current;
        }
    }

    /// <summary>Get the Current Starting Directory.</summary>
    /// <returns>Returns the Starting Directory.</returns>
    private IFolderInfo StartingDir()
    {
        return this.GetFolderResolver().StartingDir();
    }

    /// <summary>Gets the current folder.</summary>
    /// <returns>The current folder.</returns>
    private IFolderInfo GetCurrentFolder()
    {
        return this.FolderManager.GetFolder(this.CurrentFolderId);
    }

    /// <summary>Gets the language list, and sets the default locale if Content Localization is Enabled.</summary>
    private void GetLanguageList()
    {
        var languageListItems =
            new LocaleController(this.HostSettings, this.AppStatus, this.DataProviderInstance, this.PortalController, this.TabController, this.UserController, this.PackageController).GetLocales(this.portalSettings.PortalId)
                .Values.Select(language => new BrowserListItem { Text = language.Text, Value = language.Code });
        foreach (var languageListItem in languageListItems)
        {
            this.languageListItems.Add(languageListItem);
        }

        if (this.languageListItems.Count.Equals(1))
        {
            this.languageRowVisible = false;
        }
        else
        {
            // Set default locale and remove no locale if Content Localization is Enabled
            if (!this.portalSettings.ContentLocalizationEnabled)
            {
                return;
            }

            var currentTab = this.TabController.GetTab(
                int.Parse(this.GetQueryValue("tabid"), CultureInfo.InvariantCulture), this.portalSettings.PortalId, false);

            if (currentTab == null || string.IsNullOrEmpty(currentTab.CultureCode))
            {
                return;
            }

            this.languageListItems.RemoveAt(0);

            var currentTabCultureItem =
                this.languageListItems.FirstOrDefault(item => string.Equals(item.Value, currentTab.CultureCode, StringComparison.Ordinal));

            if (currentTabCultureItem != null)
            {
                this.languageListSelectedValue = currentTabCultureItem.Value;
            }
        }
    }

    /// <summary>Gets the disk space used.</summary>
    private void GetDiskSpaceUsed()
    {
        var spaceAvailable = this.portalSettings.HostSpace.Equals(0)
                                 ? this.LocalizeString("UnlimitedSpace.Text")
                                 : $"{this.portalSettings.HostSpace}MB";

        var spaceUsed = this.PortalController.GetPortalSpaceUsedBytes(this.portalSettings.PortalId);

        string[] suffix = ["B", "KB", "MB", "GB", "TB"];

        var index = 0;

        double spaceUsedDouble = spaceUsed;

        if (spaceUsed > 1024)
        {
            for (index = 0; (spaceUsed / 1024) > 0; index++, spaceUsed /= 1024)
            {
                spaceUsedDouble = spaceUsed / 1024.0;
            }
        }

        var usedSpace = $"{spaceUsedDouble:0.##}{suffix[index]}";

        this.fileSpaceUsedText =
            string.Format(
                CultureInfo.CurrentCulture,
                this.LocalizeString("SpaceUsed.Text"),
                usedSpace,
                spaceAvailable);
    }

    /// <summary>Gets the accepted file types.</summary>
    /// <returns>The accepted file types.</returns>
    private string GetAcceptedFileTypes()
    {
        return this.browserModus switch
        {
            "Flash" => string.Join("|", BrowserMedia.AllowedFlashExtensions),
            "Image" => string.Join("|", BrowserMedia.AllowedImageExtensions),
            _ => this.extensionWhiteList.ToStorageString().Replace(",", "|"),
        };
    }

    /// <summary>Set Language for all Controls on this Page.</summary>
    /// <remarks>
    /// Assigns the very same resource keys to the very same controls; the target is the
    /// <see cref="localizedText"/>/<see cref="localizedToolTip"/> entry keyed by the control's
    /// original ID instead of its <c>Text</c>/<c>ToolTip</c> property.
    /// </remarks>
    private void SetLanguage()
    {
        // Buttons
        this.localizedText["cmdResizeCancel"] = this.LocalizeString("cmdResizeCancel.Text");
        this.localizedText["cmdResizeNow"] = this.LocalizeString("cmdResizeNow.Text");
        this.localizedText["cmdUploadCancel"] = this.LocalizeString("cmdUploadCancel.Text");
        this.localizedText["cmdCancel"] = this.LocalizeString("cmdCancel.Text");
        this.localizedText["cmdClose"] = this.LocalizeString("cmdClose.Text");
        this.localizedText["cmdCreateFolder"] = this.LocalizeString("cmdCreateFolder.Text");
        this.localizedText["cmdCreateCancel"] = this.LocalizeString("cmdCreateCancel.Text");
        this.localizedText["cmdCrop"] = this.LocalizeString("cmdCrop.Text");
        this.localizedText["cmdZoom"] = this.LocalizeString("cmdZoom.Text");
        this.localizedText["cmdRotate"] = this.LocalizeString("cmdRotate.Text");
        this.localizedText["cmdResize2"] = this.LocalizeString("cmdResize2.Text");
        this.localizedText["cmdCropNow"] = this.LocalizeString("cmdCropNow.Text");
        this.localizedText["cmdCropCancel"] = this.LocalizeString("cmdCropCancel.Text");

        // Labels
        this.localizedText["lblConFiles"] = this.LocalizeString("lblConFiles.Text");
        this.localizedText["lblCurrent"] = this.LocalizeString("lblCurrent.Text");
        this.localizedText["lblSubDirs"] = this.LocalizeString("lblSubDirs.Text");
        this.localizedText["lblUrlType"] = this.LocalizeString("lblUrlType.Text");
        this.localizedToolTip["rblLinkType"] = this.LocalizeString("lblUrlType.Text");
        this.localizedText["lblChoosetab"] = this.LocalizeString("lblChoosetab.Text");
        this.localizedText["lblHeight"] = this.LocalizeString("lblHeight.Text");
        this.localizedText["lblWidth"] = this.LocalizeString("lblWidth.Text");
        this.localizedText["lblThumbName"] = this.LocalizeString("lblThumbName.Text");
        this.localizedText["lblImgQuality"] = this.LocalizeString("lblImgQuality.Text");
        this.localizedText["lblResizeHeader"] = this.LocalizeString("lblResizeHeader.Text");
        this.localizedText["lblOtherTools"] = this.LocalizeString("lblOtherTools.Text");
        this.localizedText["lblCropImageName"] = this.LocalizeString("lblThumbName.Text");
        this.localizedText["lblCropInfo"] = this.LocalizeString("lblCropInfo.Text");
        this.localizedText["lblShowPreview"] = this.LocalizeString("lblShowPreview.Text");
        this.localizedText["lblClearPreview"] = this.LocalizeString("lblClearPreview.Text");
        this.localizedText["lblOriginal"] = this.LocalizeString("lblOriginal.Text");
        this.localizedText["lblPreview"] = this.LocalizeString("lblPreview.Text");
        this.localizedText["lblNewFoldName"] = this.LocalizeString("lblNewFoldName.Text");
        this.localizedText["LabelAnchor"] = this.LocalizeString("LabelAnchor.Text");
        this.localizedText["NewFolderTitle"] = this.LocalizeString("cmdCreate.Text");
        this.localizedText["UploadTitle"] = this.LocalizeString("cmdUpload.Text");
        this.localizedText["AddFiles"] = this.LocalizeString("AddFiles.Text");
        this.localizedText["Wait"] = this.LocalizeString("Wait.Text");
        this.localizedText["WaitMessage"] = this.LocalizeString("WaitMessage.Text");
        this.localizedText["ExtraTabOptions"] = this.LocalizeString("ExtraTabOptions.Text");
        this.localizedText["LabelTabLanguage"] = this.LocalizeString("LabelTabLanguage.Text");

        this.localizedText["MaximumUploadSizeInfo"] =
            string.Format(
                CultureInfo.CurrentCulture,
                this.LocalizeString("FileSizeRestriction"),
                this.MaxUploadSize / (1024 * 1024),
                this.AcceptFileTypes.Replace("|", ","));

        // RadioButtonList
        this.browserModeItems[0].Text = this.LocalizeString("FileLink.Text");
        this.browserModeItems[1].Text = this.LocalizeString("PageLink.Text");

        // DropDowns
        this.languageListItems[0].Text = this.LocalizeString("None.Text");
        this.anchorListItems[0].Text = this.LocalizeString("None.Text");

        // CheckBoxes
        this.localizedText["chkAspect"] = this.LocalizeString("chkAspect.Text");
        this.localizedText["chkHumanFriendy"] = this.LocalizeString("chkHumanFriendy.Text");
        this.localizedText["OverrideFile"] = this.LocalizeString("OverrideFile.Text");

        // LinkButtons (with Image)
        this.localizedText["Syncronize"] =
            $"""<img src="Images/SyncFolder.png" alt="{WebUtility.HtmlEncode(this.LocalizeString("Syncronize.Text"))}" title="{WebUtility.HtmlEncode(this.LocalizeString("Syncronize.Help"))}" />""";
        this.localizedToolTip["Syncronize"] = this.LocalizeString("Syncronize.Help");

        this.localizedText["cmdCreate"] =
            $"""<img src="Images/CreateFolder.png" alt="{WebUtility.HtmlEncode(this.LocalizeString("cmdCreate.Text"))}" title="{WebUtility.HtmlEncode(this.LocalizeString("cmdCreate.Help"))}" />""";
        this.localizedToolTip["cmdCreate"] = this.LocalizeString("cmdCreate.Help");

        this.localizedText["cmdDownload"] =
            $"""<img src="Images/DownloadButton.png" alt="{WebUtility.HtmlEncode(this.LocalizeString("cmdDownload.Text"))}" title="{WebUtility.HtmlEncode(this.LocalizeString("cmdDownload.Help"))}" />""";
        this.localizedToolTip["cmdDownload"] = this.LocalizeString("cmdDownload.Help");

        this.localizedText["cmdUpload"] =
            $"""<img src="Images/UploadButton.png" alt="{WebUtility.HtmlEncode(this.LocalizeString("cmdUpload.Text"))}" title="{WebUtility.HtmlEncode(this.LocalizeString("cmdUpload.Help"))}" />""";
        this.localizedToolTip["cmdUpload"] = this.LocalizeString("cmdUpload.Help");

        this.localizedText["cmdDelete"] =
            $"""<img src="Images/DeleteFile.png" alt="{WebUtility.HtmlEncode(this.LocalizeString("cmdDelete.Text"))}" title="{WebUtility.HtmlEncode(this.LocalizeString("cmdDelete.Help"))}" />""";
        this.localizedToolTip["cmdDelete"] = this.LocalizeString("cmdDelete.Help");

        this.localizedText["cmdResizer"] =
            $"""<img src="Images/ResizeImage.png" alt="{WebUtility.HtmlEncode(this.LocalizeString("cmdResizer.Text"))}" title="{WebUtility.HtmlEncode(this.LocalizeString("cmdResizer.Help"))}" />""";
        this.localizedToolTip["cmdResizer"] = this.LocalizeString("cmdResizer.Help");

        const string SwitchContent =
            "<a class=\"Switch{0}\" onclick=\"javascript: SwitchView('{0}');\" href=\"javascript:void(0)\"><img src=\"Images/{0}.png\" alt=\"{1}\" title=\"{2}\" />{1}</a>";

        this.localizedText["SwitchDetailView"] = string.Format(
            CultureInfo.InvariantCulture,
            SwitchContent,
            "DetailView",
            WebUtility.HtmlEncode(this.LocalizeString("DetailView.Text")),
            WebUtility.HtmlEncode(this.LocalizeString("DetailViewTitle.Text")));
        this.localizedToolTip["SwitchDetailView"] = this.LocalizeString("DetailViewTitle.Text");

        this.localizedText["SwitchListView"] = string.Format(
            CultureInfo.InvariantCulture,
            SwitchContent,
            "ListView",
            WebUtility.HtmlEncode(this.LocalizeString("ListView.Text")),
            WebUtility.HtmlEncode(this.LocalizeString("ListViewTitle.Text")));
        this.localizedToolTip["SwitchListView"] = this.LocalizeString("ListViewTitle.Text");

        this.localizedText["SwitchIconsView"] = string.Format(
            CultureInfo.InvariantCulture,
            SwitchContent,
            "IconsView",
            WebUtility.HtmlEncode(this.LocalizeString("IconsView.Text")),
            WebUtility.HtmlEncode(this.LocalizeString("IconsViewTitle.Text")));
        this.localizedToolTip["SwitchIconsView"] = this.LocalizeString("IconsViewTitle.Text");

        this.localizedText["SortAscending"] =
            $"""<img src="Images/SortAscending.png" alt="{WebUtility.HtmlEncode(this.LocalizeString("SortAscending.Text"))}" title="{WebUtility.HtmlEncode(this.LocalizeString("SortAscending.Help"))}" />""";
        this.localizedToolTip["SortAscending"] = this.LocalizeString("SortAscending.Help");

        this.localizedText["SortDescending"] =
            $"""<img src="Images/SortDescending.png" alt="{WebUtility.HtmlEncode(this.LocalizeString("SortDescending.Text"))}" title="{WebUtility.HtmlEncode(this.LocalizeString("SortDescending.Help"))}" />""";
        this.localizedToolTip["SortDescending"] = this.LocalizeString("SortDescending.Help");

        this.localizedText["SortByDateAscending"] =
            $"""<img src="Images/AscendingArrow.png" alt="{WebUtility.HtmlEncode(this.LocalizeString("SortByDateAscending.Text"))}" title="{WebUtility.HtmlEncode(this.LocalizeString("SortByDateAscending.Help"))}" />{WebUtility.HtmlEncode(this.LocalizeString("SortByDate.Text"))}""";
        this.localizedToolTip["SortByDateAscending"] = this.LocalizeString("SortByDateAscending.Help");

        this.localizedText["SortByDateDescending"] =
            $"""<img src="Images/DescendingArrow.png" alt="{WebUtility.HtmlEncode(this.LocalizeString("SortByDateDescending.Text"))}" title="{WebUtility.HtmlEncode(this.LocalizeString("SortByDateDescending.Help"))}" />{WebUtility.HtmlEncode(this.LocalizeString("SortByDate.Text"))}""";
        this.localizedToolTip["SortByDateDescending"] = this.LocalizeString("SortByDateDescending.Help");

        // Replaces ClientAPI.AddButtonConfirm(this.cmdDelete, ...), which rendered an inline
        // onclick="return confirm(...)": a Blazor element cannot carry both an inline onclick and
        // a component handler, so the prompt is issued by the handler through JS interop - the
        // same thing the platform's own DNNDataGrid does now.
        this.deleteConfirmMessage = this.LocalizeString("AreYouSure.Text");

        this.SetDefaultLinkTypeText();
    }

    /// <summary>Sets the default link type text.</summary>
    private void SetDefaultLinkTypeText()
    {
        this.linkTypeItems[0].Text = this.LocalizeString("relLnk.Text");
        this.linkTypeItems[1].Text = this.LocalizeString("absLnk.Text");
    }

    /// <summary>Sets the sort button classes.</summary>
    private void SetSortButtonClasses()
    {
        this.sortAscendingCssClass = !this.sortFilesAscending ? "ButtonNormal" : "ButtonSelected";
        this.sortDescendingCssClass = !this.sortFilesDescending ? "ButtonNormal" : "ButtonSelected";
        this.sortByDateAscendingCssClass = !this.sortFilesDateAscending ? "ButtonNormal" : "ButtonSelected";
        this.sortByDateDescendingCssClass = !this.sortFilesDateDescending ? "ButtonNormal" : "ButtonSelected";
    }

    /// <summary>Fill the Folder TreeView with all (Sub)Directories.</summary>
    /// <param name="currentFolderInfo">The current folder information.</param>
    private void FillFolderTree(IFolderInfo currentFolderInfo)
    {
        this.folderTreeNodes.Clear();

        var folderName = !string.IsNullOrEmpty(currentFolderInfo.FolderPath) ?
            currentFolderInfo.FolderPath : this.LocalizeString("RootFolder.Text");

        var folderNode = new DnnTreeNode
        {
            Text = folderName,
            Value = currentFolderInfo.FolderID.ToString(CultureInfo.InvariantCulture),
            ImageUrl = GetFolderIcon(currentFolderInfo),
        };

        this.folderTreeNodes.AddChild(null, folderNode);

        // gets the list of folders the specified user has read permissions and transform into dictionary:
        //      Key = parent folder ID
        //      Value = list of child folders
        // this will let us possible to create folders tree much faster on a recursion below
        var readableFolders =
            this.FolderManager.GetFolders(this.UserController.GetCurrentUserInfo())
                .GroupBy(folder => folder.ParentID)
                .ToDictionary(
                    key => key.Key,
                    value => value?.Select(folder => folder) ?? []);

        // get all folders where parent folder is current one
        if (!readableFolders.TryGetValue(currentFolderInfo.FolderID, out var folders))
        {
            return;
        }

        foreach (var node in folders.Cast<FolderInfo>().Select(folder => this.RenderFolder(folder, readableFolders)).Where(node => node != null))
        {
            folderNode.ChildNodes.AddChild(folderNode, node);
        }
    }

    /// <summary>Render all Directories and subdirectories recursive.</summary>
    /// <param name="folderInfo">The folder Info.</param>
    /// <param name="readableFolders">The list of folders that the current user has READ access.</param>
    /// <returns>TreeNode List.</returns>
    private DnnTreeNode RenderFolder(FolderInfo folderInfo, IDictionary<int, IEnumerable<IFolderInfo>> readableFolders)
    {
        var tnFolder = new DnnTreeNode
        {
            Text = folderInfo.FolderName,
            Value = folderInfo.FolderID.ToString(CultureInfo.InvariantCulture),
            ImageUrl = GetFolderIcon(folderInfo),
        };

        if (!readableFolders.TryGetValue(folderInfo.FolderID, out var folders))
        {
            return tnFolder;
        }

        foreach (var node in folders.Cast<FolderInfo>().Select(folder => this.RenderFolder(folder, readableFolders)).Where(node => node != null))
        {
            tnFolder.ChildNodes.AddChild(tnFolder, node);
        }

        return tnFolder;
    }

    /// <summary>Finds a node of a tree by its value.</summary>
    /// <param name="nodes">The nodes of the tree.</param>
    /// <param name="value">The value to look for.</param>
    /// <returns>The node, or <see langword="null"/> when there is none.</returns>
    private DnnTreeNode FindNodeByValue(IList<DnnTreeNode> nodes, string value)
    {
        return this.FindNodeByValue((IEnumerable<DnnTreeNode>)nodes, value);
    }

    /// <summary>Finds a node by its value, searching the whole sub tree.</summary>
    /// <param name="nodes">The nodes to search.</param>
    /// <param name="value">The value to look for.</param>
    /// <returns>The node, or <see langword="null"/> when there is none.</returns>
    private DnnTreeNode FindNodeByValue(IEnumerable<DnnTreeNode> nodes, string value)
    {
        return nodes.FindByValue(value);
    }

    /// <summary>Shows the files in directory.</summary>
    /// <param name="currentFolderInfo">The current folder information.</param>
    /// <param name="pagerChanged">if set to <see langword="true"/> [pager changed].</param>
    /// <remarks>
    /// The <c>PagedDataSource</c> the repeater was bound to is a <c>Skip</c>/<c>Take</c> over the
    /// file list: the same page size, the same page count (and therefore the same pager) and the
    /// same "keep the current page when only the pager changed" behaviour.
    /// </remarks>
    private void ShowFilesIn(IFolderInfo currentFolderInfo, bool pagerChanged = false)
    {
        this.CheckFolderAccess(currentFolderInfo.FolderID, false);

        if (!pagerChanged)
        {
            this.filesTable = this.GetFiles(currentFolderInfo);

            this.GetDiskSpaceUsed();
        }
        else
        {
            if (this.filesTable == null)
            {
                this.filesTable = this.GetFiles(currentFolderInfo);
            }
        }

        var allFiles = this.filesTable?.ToList() ?? [];

        if (this.currentSettings.FileListPageSize > 0)
        {
            var pageSize = this.currentSettings.FileListPageSize;

            this.pagerPageCount = (int)Math.Ceiling(allFiles.Count / (double)pageSize);
            this.pagerCurrentPageIndex = pagerChanged ? this.pagerCurrentPageIndex : 0;

            this.pagedFiles = allFiles.Skip(this.pagerCurrentPageIndex * pageSize).Take(pageSize).ToList();
        }
        else
        {
            // PagedDataSource.PageCount is 1 when paging is not allowed.
            this.pagerPageCount = 1;
            this.pagerCurrentPageIndex = 0;
            this.pagedFiles = allFiles;
        }

        this.pagerVisible = this.pagerPageCount > 1;
    }

    /// <summary>Load Files of Selected Folder.</summary>
    /// <param name="node">The clicked folder node.</param>
    private void FoldersTree_NodeClick(DnnTreeNode node)
    {
        this.folderTreeNodes.ClearSelection();
        node.Selected = true;
        this.selectedFolderNodeValue = node.Value;

        var folderId = Convert.ToInt32(node.Value, CultureInfo.InvariantCulture);
        var folder = this.FolderManager.GetFolder(folderId);

        var folderName = !string.IsNullOrEmpty(folder.FolderPath) ?
            folder.FolderPath : this.LocalizeString("RootFolder.Text");
        this.currentDirectoryText = folderName;

        this.CurrentFolderId = folderId;

        this.ShowFilesIn(folder);

        // Reset selected file
        this.SetDefaultLinkTypeText();

        this.selectedFileId = null;
        this.selectedFileName = null;

        // Expand Sub folders (if) exists
        node.Expanded = true;
    }

    /// <summary>Show Info for Selected File.</summary>
    /// <param name="file">The clicked file.</param>
    /// <remarks>This is <c>FilesList_ItemCommand</c>: the repeater's item command is the click of the rendered list item.</remarks>
    private void FilesList_ItemCommand(BrowserFile file)
    {
        // the "reset every row's class, then mark the clicked one as Selected" loop is the
        // selectedFileId the markup compares each row against.
        this.selectedFileId = file.FileId.ToString(CultureInfo.InvariantCulture);

        var currentFile = this.FileManager.GetFile(file.FileId);

        this.ShowFileHelpUrl(currentFile.FileName, currentFile);

        this.ScrollToSelectedFile(FileListItemElementId(this.pagedFiles.IndexOf(file)));
    }

    /// <summary>Handles the Page Changed event of the Pager FileLinks control.</summary>
    /// <param name="pageIndex">The new, zero based page index the pager reports.</param>
    private void PagerFileLinks_PageChanged(int pageIndex)
    {
        this.pagerCurrentPageIndex = pageIndex;

        this.ShowFilesIn(this.GetCurrentFolder(), true);

        // Reset selected file
        this.SetDefaultLinkTypeText();

        this.selectedFileId = null;
        this.selectedFileName = null;
    }

    /// <summary>Sorts the Files in ascending order.</summary>
    private void SortAscendingClick()
    {
        this.SortFiles(ascending: true, descending: false, dateAscending: false, dateDescending: false);
    }

    /// <summary>Sorts the Files in descending order.</summary>
    private void SortDescendingClick()
    {
        this.SortFiles(ascending: false, descending: true, dateAscending: false, dateDescending: false);
    }

    /// <summary>Sorts the Files by Date in ascending order.</summary>
    private void SortByDateAscendingClick()
    {
        this.SortFiles(ascending: false, descending: false, dateAscending: true, dateDescending: false);
    }

    /// <summary>Sorts the Files by Date in descending order.</summary>
    private void SortByDateDescendingClick()
    {
        this.SortFiles(ascending: false, descending: false, dateAscending: false, dateDescending: true);
    }

    /// <summary>Applies one of the four sort orders.</summary>
    /// <param name="ascending">Sort by name, ascending.</param>
    /// <param name="descending">Sort by name, descending.</param>
    /// <param name="dateAscending">Sort by date, ascending.</param>
    /// <param name="dateDescending">Sort by date, descending.</param>
    /// <remarks>
    /// The body of the four identical <c>Sort*Click</c> handlers; setting any of the four view
    /// state keys dropped the cached file list, which the explicit reset below replaces.
    /// </remarks>
    private void SortFiles(bool ascending, bool descending, bool dateAscending, bool dateDescending)
    {
        this.sortFilesAscending = ascending;
        this.sortFilesDescending = descending;
        this.sortFilesDateAscending = dateAscending;
        this.sortFilesDateDescending = dateDescending;
        this.filesTable = null;

        this.SetSortButtonClasses();

        this.ShowFilesIn(this.GetCurrentFolder(), true);

        // Reset selected file
        this.SetDefaultLinkTypeText();

        this.selectedFileId = null;
        this.selectedFileName = null;
    }

    /// <summary>Scroll to a Selected File or Uploaded File.</summary>
    /// <param name="elementId">The element ID.</param>
    /// <remarks>Replaces the <c>scrollIntoView()</c> start-up script; it runs from <see cref="OnAfterRenderAsync"/>.</remarks>
    private void ScrollToSelectedFile(string elementId)
    {
        this.pendingScrollElementId = elementId;
    }

    /// <summary>Select a folder and the file inside the Browser.</summary>
    /// <param name="fileUrl">The file URL.</param>
    /// <returns>if folder was selected.</returns>
    private bool SelectFolderFile(string fileUrl)
    {
        var fileName = fileUrl.Substring(fileUrl.LastIndexOf("/", StringComparison.Ordinal) + 1);

        if (fileName.StartsWith("LinkClick", StringComparison.Ordinal) ||
            fileUrl.StartsWith("http:", StringComparison.Ordinal) ||
            fileUrl.StartsWith("https:", StringComparison.Ordinal) ||
            fileUrl.StartsWith("mailto:", StringComparison.Ordinal))
        {
            ckFileUrl = null;
            return false;
        }

        var selectedDir = this.MapPath(fileUrl).Replace(fileName, string.Empty);

        if (!Directory.Exists(selectedDir))
        {
            ckFileUrl = null;
            return false;
        }

        this.currentDirectoryText = selectedDir;

        var newDir = this.currentDirectoryText;

        var tnNewFolder = this.FindNodeByValue(this.folderTreeNodes, newDir);
        if (tnNewFolder != null)
        {
            this.folderTreeNodes.ClearSelection();
            tnNewFolder.Selected = true;
            this.selectedFolderNodeValue = tnNewFolder.Value;
            tnNewFolder.Expanded = true;
        }

        this.ShowFilesIn(newDir);

        this.GoToSelectedFile(fileName);

        return true;
    }

    /// <summary>Goes to selected file.</summary>
    /// <param name="fileName">Name of the file.</param>
    private void GoToSelectedFile(string fileName)
    {
        // Find the File inside the rendered list
        for (var index = 0; index < this.pagedFiles.Count; index++)
        {
            var item = this.pagedFiles[index];

            if (!string.Equals(ListRowTitle(item), fileName, StringComparison.Ordinal))
            {
                continue;
            }

            this.selectedFileId = item.FileId.ToString(CultureInfo.InvariantCulture);

            var fileInfo = this.FileManager.GetFile(item.FileId);

            this.ShowFileHelpUrl(fileInfo.FileName, fileInfo);

            this.ScrollToSelectedFile(FileListItemElementId(index));
        }
    }

    /// <summary>Show Preview for the URLs.</summary>
    /// <param name="fileName">Selected FileName.</param>
    /// <param name="fileInfo">The file Info.</param>
    private void ShowFileHelpUrl(string fileName, IFileInfo fileInfo)
    {
        try
        {
            this.SetDefaultLinkTypeText();

            // Enable Buttons
            this.CheckFolderAccess(fileInfo.FolderId, true);

            this.linkTypeSelectedValue = this.linkTypeItems[0].Value;

            var isAllowedExtension = BrowserMedia.AllowedImageExtensions.Contains(Path.GetExtension(fileName).TrimStart('.'));

            this.cmdResizerEnabled = this.cmdResizerEnabled && isAllowedExtension;
            this.cmdResizerCssClass = this.cmdResizerEnabled ? "LinkNormal" : "LinkDisabled";

            this.selectedFileId = fileInfo.FileId.ToString(CultureInfo.InvariantCulture);
            this.selectedFileName = fileName;
            var providerFileUrl = this.FileManager.GetUrl(fileInfo);

            // Relative Url  (Or provider default)
            this.linkTypeItems[0].Text = Regex.Replace(this.linkTypeItems[0].Text, "/Images/MyImage.jpg", providerFileUrl, RegexOptions.IgnoreCase);

            // Absolute Url
            this.linkTypeItems[1].Text = Regex.Replace(this.linkTypeItems[1].Text, "http://www.MyWebsite.com/Images/MyImage.jpg", this.BuildAbsoluteUrl(providerFileUrl), RegexOptions.IgnoreCase);
        }
        catch (Exception)
        {
            this.SetDefaultLinkTypeText();
        }
    }

    /// <summary>Formats a MapPath into relative MapUrl.</summary>
    /// <param name="sPath">MapPath Input string.</param>
    /// <returns>The output URL string.</returns>
    private string MapUrl(string sPath)
    {
        return MapUrl(sPath, this.AppStatus.ApplicationMapPath);
    }

    /// <summary>Deletes the selected file, after the confirmation prompt the original attached to the button.</summary>
    /// <returns>A task.</returns>
    /// <remarks>
    /// Replaces <c>ClientAPI.AddButtonConfirm(this.cmdDelete, ...)</c>: the prompt is issued
    /// through JS interop and the delete itself is the handler of the upload/file-command half.
    /// </remarks>
    private async Task DeleteWithConfirmAsync()
    {
        if (!string.IsNullOrEmpty(this.deleteConfirmMessage) && this.JsRuntime != null)
        {
            var confirmed = await this.JsRuntime.InvokeAsync<bool>("confirm", this.deleteConfirmMessage);
            if (!confirmed)
            {
                return;
            }
        }

        this.Delete_Click();
    }

    // ---- the seam of the page-link / CKEditor-callback / upload / image-editing half ----------
    // These are C# partial methods without an accessibility modifier, which is the language's
    // declaration-only seam: the implementing declarations live in the second partial of this
    // component (Browser.razor.Editing.cs), together with the markup of the panels they drive.
#pragma warning disable SA1400 // a modifier-less partial method is what makes the declaration-only seam legal

    /// <summary>Show Create New Folder Panel.</summary>
    partial void Create_Click();

    /// <summary>Synchronize Current Folder With the Database.</summary>
    partial void Syncronize_Click();

    /// <summary>Show Upload Controls.</summary>
    partial void Upload_Click();

    /// <summary>Download selected File.</summary>
    partial void Download_Click();

    /// <summary>Delete Selected File.</summary>
    partial void Delete_Click();

    /// <summary>Opens the Re-sizing Panel.</summary>
    partial void Resizer_Click();

    /// <summary>Switch Browser in Link Modus between Link and Page Mode.</summary>
    /// <param name="selectedValue">The selected value of the <c>BrowserMode</c> list.</param>
    partial void BrowserMode_SelectedIndexChanged(string selectedValue);

    /// <summary>Load the Portal Tabs for the Page Links TreeView Selector.</summary>
    partial void RenderTabs();

    /// <summary>Fill Quality Values 1-100 %.</summary>
    partial void FillQualityPercentages();

    /// <summary>JS Code that gets the selected File Url.</summary>
    partial void GetSelectedImageOrLink();

    /// <summary>Releases the JavaScript callback reference the editing half handed to the client.</summary>
    partial void DisposeEditing();
#pragma warning restore SA1400

    /// <summary>Runs the start-up scripts the editing half queued during this render.</summary>
    /// <param name="module">The imported <c>browserInterop.js</c> module.</param>
    /// <returns>A task.</returns>
    private partial Task FlushPendingInteropCallsAsync(IJSObjectReference module);

    /// <summary>An item of one of the page's drop downs or radio button lists.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.ListItem</c>, which does not exist on ASP.NET Core.</remarks>
    public sealed class BrowserListItem
    {
        /// <summary>Gets or sets the display text of the item.</summary>
        public string Text { get; set; }

        /// <summary>Gets or sets the value of the item.</summary>
        public string Value { get; set; }
    }

    /// <summary>Information about a file to display in the file browser.</summary>
    public class BrowserFile
    {
        /// <summary>Gets or sets the name of the file.</summary>
        public string FileName { get; set; }

        /// <summary>Gets or sets the URL to the picture for the file.</summary>
        public string PictureUrl { get; set; }

        /// <summary>Gets the information summary for the file.</summary>
        /// <remarks>Replaces the <c>IHtmlString</c>/<c>HtmlString</c> of the original, which does not exist on ASP.NET Core.</remarks>
        public MarkupString Info => new MarkupString(this.InfoHtml);

        /// <summary>Gets or sets the HTML of the information summary for the file.</summary>
        public string InfoHtml { get; set; }

        /// <summary>Gets or sets the ID of the file.</summary>
        public int FileId { get; set; }
    }

    /// <summary>The request bound facts the circuit cannot re-read, handed over by the pre-render.</summary>
    /// <remarks>
    /// <c>HttpContext</c> is only available while the component pre-renders inside the HTTP
    /// request; the interactive instance that follows runs over SignalR. These two values are the
    /// only ones the page derives from the request itself (the portal alias, which
    /// <c>Globals.GetDomainName(Request)</c> resolved, and the role based upload size limit, which
    /// needs <c>HttpRequest.IsAuthenticated()</c>), so they travel through persistent component
    /// state instead of being recomputed without a request.
    /// </remarks>
    public sealed class PersistedRequestState
    {
        /// <summary>Gets or sets the resolved portal alias of the request.</summary>
        public string PortalAlias { get; set; }

        /// <summary>Gets or sets the domain name of the request, as <c>Globals.GetDomainName(Request, true)</c> resolved it.</summary>
        /// <remarks>
        /// The page-link half builds absolute page URLs from it; <c>Globals.GetDomainName</c> needs
        /// a request, which the circuit does not have.
        /// </remarks>
        public string DomainName { get; set; }

        /// <summary>Gets or sets the tab ID of the request.</summary>
        public int TabId { get; set; }

        /// <summary>Gets or sets the current user's upload file size limit, or <c>-1</c> when it was not computed yet.</summary>
        public int UploadFileSizeLimit { get; set; } = -1;
    }
}
