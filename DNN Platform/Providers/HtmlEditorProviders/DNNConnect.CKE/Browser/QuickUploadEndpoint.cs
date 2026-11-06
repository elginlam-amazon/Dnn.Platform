// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider.Browser;

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using DNNConnect.CKEditorProvider.Constants;
using DNNConnect.CKEditorProvider.Objects;
using DNNConnect.CKEditorProvider.Utilities;

using DotNetNuke.Abstractions.Application;
using DotNetNuke.Abstractions.Portals;
using DotNetNuke.Abstractions.Security;
using DotNetNuke.Abstractions.Security.Permissions;
using DotNetNuke.Abstractions.Users;
using DotNetNuke.Common.Extensions;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework.Providers;
using DotNetNuke.Security.Roles;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Services.Localization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using SixLabors.ImageSharp;

using Globals = DotNetNuke.Common.Globals;
using Image = SixLabors.ImageSharp.Image;

/// <summary>
/// The CKEditor "quick upload" commands of <c>Browser/Browser.aspx</c>
/// (<c>FileUpload</c>, <c>FlashUpload</c>, <c>ImageUpload</c> and <c>EasyImageUpload</c>).
/// </summary>
/// <remarks>
/// <para>
/// These four commands were never a page render: <c>Browser.aspx.cs</c>'s <c>UploadFile</c> wrote
/// the response with <c>Response.ClearContent()</c> / <c>Response.Write(...)</c> /
/// <c>Response.End()</c>, so the body was EXACTLY the JSON CKEditor's uploader
/// (<c>JSON.parse</c>) reads, or EXACTLY the one <c>&lt;script&gt;</c> statement its hidden upload
/// iframe executes. A Blazor component cannot produce such a body - the host document
/// (<c>Website/Components/App.razor</c>) is rendered around whatever it writes - so the commands
/// are served here, by a <c>MapPost</c> on the component's own URL which short circuits the
/// request and returns.
/// </para>
/// <para>
/// It is mapped from <see cref="CKEditorProviderEndpoints.MapCKEditorProviderEndpoints"/>, which
/// <c>DNN Platform/Website/Program.cs</c> calls BEFORE <c>MapRazorComponents</c>, so this endpoint
/// wins the route for the POST. The permission checks, the folder resolution cascade (shared with
/// the interactive browser through <see cref="BrowserFolderResolver"/>) and the three response
/// shapes are the original ones; the <c>deny users="?"</c> rule the <c>.dnn</c> manifest installed
/// for this URL (which is <c>[Authorize]</c> on <see cref="Browser"/>) is
/// <c>RequireAuthorization()</c> here, so the endpoint is not a hole in it.
/// </para>
/// </remarks>
public static class QuickUploadEndpoint
{
    /// <summary>The preserved URL of the file browser, which the quick upload commands POST to.</summary>
    public const string BrowserPath = "/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/Browser.aspx";

    /// <summary>The <c>Command</c> values this endpoint answers.</summary>
    private static readonly string[] UploadCommands = ["FileUpload", "FlashUpload", "ImageUpload", "EasyImageUpload"];

    /// <summary>Maps the quick upload commands onto the file browser's own URL.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same <paramref name="endpoints"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    public static IEndpointRouteBuilder MapCKEditorQuickUploadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // CKEditor posts the file (multipart/form-data, no antiforgery token - it is a cross
        // document post from the editor's upload iframe, authenticated by the auth cookie).
        endpoints
            .MapPost(BrowserPath, HandleAsync)
            .RequireAuthorization()
            .DisableAntiforgery();

        return endpoints;
    }

    /// <summary>Reads a request value from the query string or the form, as <c>Request[key]</c> did.</summary>
    /// <param name="request">The request.</param>
    /// <param name="key">The value name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    private static string GetRequestValue(HttpRequest request, string key)
    {
        if (request.Query.ContainsKey(key))
        {
            return request.Query[key].ToString();
        }

        return request.HasFormContentType && request.Form.ContainsKey(key) ? request.Form[key].ToString() : null;
    }

    /// <summary>Gets the localized string corresponding to the <paramref name="key"/>.</summary>
    /// <param name="request">The request, whose <c>lang</c> value selects the locale.</param>
    /// <param name="key">The resource key to find.</param>
    /// <returns>The localized text.</returns>
    /// <remarks>This is the page's <c>LocalizeString</c>, over the same <c>Browser.resx</c>.</remarks>
    private static string LocalizeString(HttpRequest request, string key)
    {
        var resXFile = Globals.ResolveUrl(
            $"~/Providers/HtmlEditorProviders/DNNConnect.CKE/{Localization.LocalResourceDirectory}/Browser.resx");
        var language = request.Query["lang"].ToString();
        return Localization.GetString(key, resXFile, string.IsNullOrEmpty(language) ? "en-US" : language);
    }

    /// <summary>JavaScript-encodes a value into a quoted literal.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The quoted, escaped literal.</returns>
    /// <remarks>Replaces <c>HttpUtility.JavaScriptStringEncode(value, addDoubleQuotes: true)</c>.</remarks>
    private static string JsLiteral(string value)
    {
        return HtmlUtils.JavaScriptStringEncode(value, addDoubleQuotes: true).ToString();
    }

    /// <summary>Writes a body which is exactly one <c>&lt;script&gt;</c> block, and nothing else.</summary>
    /// <param name="context">The context.</param>
    /// <param name="statement">The statement inside the block.</param>
    /// <returns>A task.</returns>
    /// <remarks>
    /// Replaces the <c>Response.Write("&lt;script type=\"text/javascript\"&gt;")</c> /
    /// <c>Response.Write(statement)</c> / <c>Response.Write("&lt;/script&gt;")</c> /
    /// <c>Response.End()</c> sequence of the original: the upload iframe executes the statement and
    /// nothing is appended after it.
    /// </remarks>
    private static Task WriteScriptAsync(HttpContext context, string statement)
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        return context.Response.WriteAsync($"<script type=\"text/javascript\">{statement}</script>");
    }

    /// <summary>Writes a body which is exactly the given JSON, and nothing else.</summary>
    /// <param name="context">The context.</param>
    /// <param name="json">The JSON body.</param>
    /// <returns>A task.</returns>
    /// <remarks>
    /// Replaces <c>Response.ClearContent()</c> + <c>ContentType = "application/json"</c> +
    /// <c>Response.Write(json)</c> + <c>Response.End()</c>. CKEditor <c>JSON.parse</c>s this body,
    /// so nothing may trail it.
    /// </remarks>
    private static Task WriteJsonAsync(HttpContext context, string json)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(json);
    }

    /// <summary>Gets the portal settings of the request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="portalAliasService">The portal alias service.</param>
    /// <returns>The portal settings.</returns>
    /// <remarks>This is the page's <c>GetPortalSettings</c>, with the alias resolved off this request.</remarks>
    private static IPortalSettings GetPortalSettings(HttpRequest request, IPortalAliasService portalAliasService)
    {
        int iTabId = 0, iPortalId = 0;

        try
        {
            var tabIdValue = GetRequestValue(request, "tabid");
            if (tabIdValue != null)
            {
                iTabId = int.Parse(tabIdValue, CultureInfo.InvariantCulture);
            }

            var portalIdValue = GetRequestValue(request, "PortalID");
            if (portalIdValue != null)
            {
                iPortalId = int.Parse(portalIdValue, CultureInfo.InvariantCulture);
            }

            var domainName = Globals.GetDomainName(request, true);
            var portalAlias = portalAliasService.GetPortalAliasByPortal(iPortalId, domainName);

            return PortalSettings.Create(iTabId, portalAliasService.GetPortalAlias(portalAlias));
        }
        catch (Exception)
        {
            return PortalSettings.Current;
        }
    }

    /// <summary>Loads the editor provider settings of the request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="hostSettings">The host settings.</param>
    /// <param name="portalSettings">The portal settings.</param>
    /// <param name="allPortalsSettings">The host (all portals) settings.</param>
    /// <returns>The current settings.</returns>
    /// <remarks>
    /// The settings half of <c>Page_Load</c>, line for line: the host settings are loaded first
    /// (they carry <c>HostUploadDir</c>/<c>HostUploadDirForImg</c>, which the upload folder cascade
    /// reads), then the settings of the mode the window was opened with.
    /// </remarks>
    private static EditorProviderSettings LoadSettings(
        HttpRequest request,
        IHostSettings hostSettings,
        IPortalSettings portalSettings,
        out EditorProviderSettings allPortalsSettings)
    {
        var currentSettings = new EditorProviderSettings();

        var mode = GetRequestValue(request, "mode");
        if (!string.IsNullOrEmpty(mode))
        {
            currentSettings.SettingMode = Enum.Parse<SettingsMode>(mode);
        }

        var providerConfiguration = ProviderConfiguration.GetProviderConfiguration("htmlEditor");
        var objProvider = (Provider)providerConfiguration.Providers[providerConfiguration.DefaultProvider];

        var settingsDictionary = EditorController.GetEditorHostSettings(hostSettings);
        var portalRoles = RoleController.Instance.GetRoles(portalSettings.PortalId);

        allPortalsSettings = SettingsUtil.LoadEditorSettingsByKey(
            portalSettings,
            currentSettings,
            settingsDictionary,
            SettingConstants.HostKey,
            null);

        return currentSettings.SettingMode switch
        {
            SettingsMode.Default => SettingsUtil.GetDefaultSettings(
                portalSettings,
                hostSettings,
                portalSettings.HomeDirectoryMapPath,
                objProvider.Attributes["ck_configFolder"],
                portalRoles),
            SettingsMode.Host => SettingsUtil.LoadEditorSettingsByKey(
                portalSettings,
                currentSettings,
                settingsDictionary,
                SettingConstants.HostKey,
                null),
            SettingsMode.Portal => SettingsUtil.LoadEditorSettingsByKey(
                portalSettings,
                currentSettings,
                settingsDictionary,
                SettingConstants.PortalKey(GetRequestValue(request, "PortalID")),
                portalRoles),
            SettingsMode.Page => SettingsUtil.LoadEditorSettingsByKey(
                portalSettings,
                currentSettings,
                settingsDictionary,
                $"DNNCKT#{GetRequestValue(request, "tabid")}#",
                portalRoles),
            SettingsMode.ModuleInstance => SettingsUtil.LoadModuleSettings(
                portalSettings,
                currentSettings,
                $"DNNCKMI#{GetRequestValue(request, "mid")}#INS#{GetRequestValue(request, "ckId")}#",
                int.Parse(GetRequestValue(request, "mid"), CultureInfo.InvariantCulture),
                portalRoles),
            _ => currentSettings,
        };
    }

    /// <summary>Gets the statement which hands the uploaded file's URL back to the editor.</summary>
    /// <param name="request">The request, which carries <c>CKEditorFuncNum</c>.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="fileUrl">The file url.</param>
    /// <returns>The JavaScript statement, as the original composed it.</returns>
    /// <remarks>
    /// This is <c>GetJsUploadCode</c>: the editor lives in <c>window.parent</c> (the upload posts
    /// into a hidden iframe) and the window is NOT closed.
    /// </remarks>
    private static string GetJsUploadCode(HttpRequest request, string fileName, string fileUrl)
    {
        fileUrl = string.Format(
            CultureInfo.InvariantCulture,
            !fileUrl.EndsWith("/", StringComparison.Ordinal) ? "{0}/{1}" : "{0}{1}",
            fileUrl,
            fileName);

        ////var ckEditorName = GetRequestValue(request, "CKEditor");
        // funcNum is null when EasyImageUpload is being used
        var funcNum = GetRequestValue(request, "CKEditorFuncNum") ?? string.Empty;

        var errorMsg = string.Empty;

        funcNum = Regex.Replace(funcNum, @"[^0-9]", string.Empty, RegexOptions.None);

        return string.Format(
            CultureInfo.InvariantCulture,
            "var E = window.parent;E['CKEDITOR'].tools.callFunction({0},'{1}','{2}') ;",
            funcNum,
            HtmlUtils.JavaScriptStringEncode(fileUrl),
            HtmlUtils.JavaScriptStringEncode(errorMsg));
    }

    /// <summary>Handles a quick upload POST.</summary>
    /// <param name="context">The context.</param>
    /// <returns>A task.</returns>
    /// <remarks>
    /// The <c>Page_Load</c> path which led to <c>UploadFile</c>: the standard-browser/easy-image
    /// gate (and the authentication check behind it), then the upload itself.
    /// </remarks>
    private static async Task HandleAsync(HttpContext context)
    {
        var request = context.Request;

        var command = GetRequestValue(request, "Command");
        if (command is null || !UploadCommands.Contains(command, StringComparer.Ordinal))
        {
            // Every other Command value returned from Page_Load without writing anything. Nothing
            // else POSTs to this URL: the WebForms post backs of the browser are circuit messages
            // (/_blazor) now, and the window itself is opened with a GET, which the component
            // still serves.
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var services = context.RequestServices;
        var hostSettings = services.GetRequiredService<IHostSettings>();
        var portalAliasService = services.GetRequiredService<IPortalAliasService>();

        var portalSettings = GetPortalSettings(request, portalAliasService);
        var currentSettings = LoadSettings(request, hostSettings, portalSettings, out var allPortalsSettings);

        // set current Upload file size limit
        currentSettings.UploadFileSizeLimit =
            SettingsUtil.GetCurrentUserUploadSize(currentSettings, portalSettings, request);

        if ((!currentSettings.BrowserMode.Equals(BrowserType.StandardBrowser)
             && !currentSettings.ImageButtonMode.Equals(ImageButtonType.EasyImageButton))
            || !request.IsAuthenticated())
        {
            // The "Error1" branch of Page_Load, whose body was exactly this script.
            await WriteScriptAsync(
                context,
                $"javascript:alert({JsLiteral(LocalizeString(request, "Error1.Text"))});self.close();");
            return;
        }

        var form = await request.ReadFormAsync(context.RequestAborted);
        if (form.Files.Count == 0)
        {
            // The original only called UploadFile when the request carried a file.
            return;
        }

        var uploadState = new UploadState(
            request,
            portalSettings,
            currentSettings,
            allPortalsSettings,
            hostSettings.AllowedExtensionAllowList,
            services.GetRequiredService<IFileManager>(),
            services.GetRequiredService<IFolderManager>(),
            services.GetRequiredService<IFileContentTypeManager>(),
            services.GetRequiredService<IApplicationStatusInfo>(),
            new BrowserFolderResolver(
                portalSettings,
                currentSettings,
                allPortalsSettings,
                GetRequestValue(request, "Type"),
                services.GetRequiredService<IFolderManager>(),
                services.GetRequiredService<IUserController>(),
                services.GetRequiredService<IPermissionDefinitionService>()));

        await UploadFileAsync(context, uploadState, form.Files[0], command);
    }

    /// <summary>Uploads a File.</summary>
    /// <param name="context">The context, whose response is the whole answer of the request.</param>
    /// <param name="state">The resolved settings and services of the request.</param>
    /// <param name="file">The Uploaded File.</param>
    /// <param name="command">The Upload Command Type.</param>
    /// <returns>A task.</returns>
    /// <remarks>
    /// <para>
    /// This is <c>UploadFile</c>. <c>HttpPostedFile</c> is <see cref="IFormFile"/>
    /// (<c>ContentLength</c> is <c>IFormFile.Length</c> and <c>InputStream</c> is
    /// <c>IFormFile.OpenReadStream()</c>), and the <c>System.Drawing</c> "resize when it exceeds
    /// the configured maximum" step runs on ImageSharp (<see cref="BrowserMedia.ResizeImage"/>),
    /// preserving the format of the source image.
    /// </para>
    /// <para>
    /// The three success shapes are the original bodies: <c>{"default": url}</c> for
    /// <c>EasyImageUpload</c>, <c>{"uploaded": 1, ...}</c> when the drag/drop uploader asked for
    /// <c>responseType=json</c>, and the bare <c>CKEditorFuncNum</c> callback script otherwise.
    /// </para>
    /// </remarks>
    private static async Task UploadFileAsync(HttpContext context, UploadState state, IFormFile file, string command)
    {
        var request = state.Request;
        var fileName = Path.GetFileName(file.FileName).Trim();

        if (!string.IsNullOrEmpty(fileName))
        {
            // Convert Unicode Chars
            fileName = Utility.ConvertUnicodeChars(fileName);

            // Replace dots in the name with underscores (only one dot can be there... security issue).
            fileName = Regex.Replace(fileName, @"\.(?![^.]*$)", "_", RegexOptions.None);

            // Check for Illegal Chars
            if (Utility.ValidateFileName(fileName))
            {
                fileName = Utility.CleanFileName(fileName);
            }
        }
        else
        {
            return;
        }

        // Check if file is too big for that user
        if (state.CurrentSettings.UploadFileSizeLimit > 0
            && file.Length > state.CurrentSettings.UploadFileSizeLimit)
        {
            // Was the "errorcloseScript" start-up script followed by Response.End(), which the
            // ended response never rendered.
            // REVIEW: the alert the original intended is emitted as the whole body, so the upload
            // iframe actually shows it - verify the message is acceptable there.
            await WriteScriptAsync(
                context,
                $"javascript:alert({JsLiteral(LocalizeString(request, "FileToBigMessage.Text"))});");

            return;
        }

        if (fileName.Length > 220)
        {
            fileName = fileName.Substring(fileName.Length - 220);
        }

        string sExtension = Path.GetExtension(file.FileName);
        sExtension = sExtension.TrimStart('.');

        bool bAllowUpl = false;
        bool bIsImage = false;

        switch (command)
        {
            case "FlashUpload":
                if (BrowserMedia.AllowedFlashExtensions.Contains(sExtension))
                {
                    bAllowUpl = true;
                }

                break;
            case "EasyImageUpload":
            case "ImageUpload":
                if (BrowserMedia.AllowedImageExtensions.Contains(sExtension))
                {
                    bAllowUpl = true;
                    bIsImage = true;
                }

                break;
            case "FileUpload":
                if (state.ExtensionWhiteList.IsAllowedExtension(sExtension))
                {
                    bAllowUpl = true;
                }

                break;
        }

        if (bAllowUpl)
        {
            string sFileNameNoExt = Path.GetFileNameWithoutExtension(fileName);

            int iCounter = 0;

            var startingDir = state.FolderResolver.StartingDir();

            var uploadPhysicalPath = startingDir.PhysicalPath;

            var currentFolderInfo = state.FolderManager.GetFolder(startingDir.FolderID);

            IFolderInfo uploadFolder = null;
            if (command == "ImageUpload" && !string.IsNullOrEmpty(state.AllPortalsSettings.HostUploadDirForImg) && !state.CurrentSettings.SubDirs)
            {
                uploadFolder = Utility.EnsureGetFolder(state.PortalSettings.PortalId, state.AllPortalsSettings.HostUploadDirForImg);
            }
            else if (command == "ImageUpload" && !state.CurrentSettings.UploadDirForImgId.Equals(-1) && !state.CurrentSettings.SubDirs)
            {
                uploadFolder = state.FolderManager.GetFolder(state.CurrentSettings.UploadDirForImgId);
            }
            else if (!string.IsNullOrEmpty(state.AllPortalsSettings.HostUploadDir) && !state.CurrentSettings.SubDirs)
            {
                uploadFolder = Utility.EnsureGetFolder(state.PortalSettings.PortalId, state.AllPortalsSettings.HostUploadDir);
            }
            else if (!state.CurrentSettings.UploadDirId.Equals(-1) && !state.CurrentSettings.SubDirs)
            {
                uploadFolder = state.FolderManager.GetFolder(state.CurrentSettings.UploadDirId);
            }

            if (uploadFolder != null)
            {
                uploadPhysicalPath = uploadFolder.PhysicalPath;

                currentFolderInfo = uploadFolder;
            }

            string sFilePath = Path.Combine(uploadPhysicalPath, fileName);
            if (File.Exists(sFilePath))
            {
                iCounter++;
                fileName = $"{sFileNameNoExt}_{iCounter}{Path.GetExtension(file.FileName)}";
            }

            var disposeStream = false;
            Stream fileContents = null;
            try
            {
                if (!bIsImage)
                {
                    fileContents = file.OpenReadStream();
                    disposeStream = true;
                }
                else
                {
                    int maxWidth = state.CurrentSettings.ResizeWidthUpload;
                    int maxHeight = state.CurrentSettings.ResizeHeightUpload;
                    if (maxWidth <= 0 && maxHeight <= 0)
                    {
                        fileContents = file.OpenReadStream();
                        disposeStream = true;
                    }
                    else
                    {
                        // check if the size of the image is within boundaries
                        using var uploadStream = file.OpenReadStream();
                        using var uplImage = Image.Load(uploadStream);
                        if (uplImage.Width <= maxWidth && uplImage.Height <= maxHeight)
                        {
                            // fits within configured maximum dimensions
                            fileContents = file.OpenReadStream();
                            disposeStream = true;
                        }
                        else
                        {
                            var (newWidth, newHeight) = BrowserMedia.DetermineNewDimensions(uplImage, maxWidth, maxHeight);

                            disposeStream = true;
                            fileContents = BrowserMedia.ResizeImage(uplImage, newWidth, newHeight);
                        }
                    }
                }

                state.FileManager.AddFile(
                    currentFolderInfo,
                    fileName,
                    fileContents,
                    false,
                    true,
                    state.FileContentTypeManager.GetContentType(Path.GetExtension(fileName)));
            }
            finally
            {
                if (disposeStream)
                {
                    fileContents?.Dispose();
                }
            }

            var mappedUploadUrl = BrowserFolderResolver.MapUrl(uploadPhysicalPath, state.AppStatus.ApplicationMapPath);

            if (command == "EasyImageUpload")
            {
                var fileUrl = string.Format(
                    CultureInfo.InvariantCulture,
                    !mappedUploadUrl.EndsWith("/", StringComparison.Ordinal) ? "{0}/{1}" : "{0}{1}",
                    mappedUploadUrl,
                    fileName);
                await WriteJsonAsync(context, $$"""{"default": {{JsLiteral(fileUrl)}}}""");
            }
            else
            {
                // querystring parameter responseType equals "json" when the request comes from drag/drop
                if (request.Query["responseType"].ToString().Equals("json", StringComparison.InvariantCultureIgnoreCase))
                {
                    var fileUrl = string.Format(
                        CultureInfo.InvariantCulture,
                        !mappedUploadUrl.EndsWith("/", StringComparison.Ordinal) ? "{0}/{1}" : "{0}{1}",
                        mappedUploadUrl,
                        fileName);
                    await WriteJsonAsync(
                        context,
                        $$"""{"uploaded": 1, "fileName": {{JsLiteral(fileName)}}, "url": {{JsLiteral(fileUrl)}}}""");
                }
                else
                {
                    await WriteScriptAsync(context, GetJsUploadCode(request, fileName, mappedUploadUrl));
                }
            }
        }
        else
        {
            // Was the "errorcloseScript" start-up script followed by Response.End().
            // REVIEW: as above, the alert the original intended is the whole body here.
            await WriteScriptAsync(
                context,
                $"javascript:alert({JsLiteral(LocalizeString(request, "Error2.Text"))});");
        }
    }

    /// <summary>The settings and services <see cref="UploadFileAsync"/> works against.</summary>
    /// <remarks>Replaces the fields and injected services of the page the method used to read.</remarks>
    private sealed class UploadState
    {
        /// <summary>Initializes a new instance of the <see cref="UploadState"/> class.</summary>
        /// <param name="request">The request.</param>
        /// <param name="portalSettings">The portal settings.</param>
        /// <param name="currentSettings">The current editor provider settings.</param>
        /// <param name="allPortalsSettings">The host (all portals) editor provider settings.</param>
        /// <param name="extensionWhiteList">The allowed extension list.</param>
        /// <param name="fileManager">The file manager.</param>
        /// <param name="folderManager">The folder manager.</param>
        /// <param name="fileContentTypeManager">The file content type manager.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="folderResolver">The folder resolution cascade.</param>
        public UploadState(
            HttpRequest request,
            IPortalSettings portalSettings,
            EditorProviderSettings currentSettings,
            EditorProviderSettings allPortalsSettings,
            IFileExtensionAllowList extensionWhiteList,
            IFileManager fileManager,
            IFolderManager folderManager,
            IFileContentTypeManager fileContentTypeManager,
            IApplicationStatusInfo appStatus,
            BrowserFolderResolver folderResolver)
        {
            this.Request = request;
            this.PortalSettings = portalSettings;
            this.CurrentSettings = currentSettings;
            this.AllPortalsSettings = allPortalsSettings;
            this.ExtensionWhiteList = extensionWhiteList;
            this.FileManager = fileManager;
            this.FolderManager = folderManager;
            this.FileContentTypeManager = fileContentTypeManager;
            this.AppStatus = appStatus;
            this.FolderResolver = folderResolver;
        }

        /// <summary>Gets the request.</summary>
        public HttpRequest Request { get; }

        /// <summary>Gets the portal settings.</summary>
        public IPortalSettings PortalSettings { get; }

        /// <summary>Gets the current editor provider settings.</summary>
        public EditorProviderSettings CurrentSettings { get; }

        /// <summary>Gets the host (all portals) editor provider settings.</summary>
        public EditorProviderSettings AllPortalsSettings { get; }

        /// <summary>Gets the allowed extension list.</summary>
        public IFileExtensionAllowList ExtensionWhiteList { get; }

        /// <summary>Gets the file manager.</summary>
        public IFileManager FileManager { get; }

        /// <summary>Gets the folder manager.</summary>
        public IFolderManager FolderManager { get; }

        /// <summary>Gets the file content type manager.</summary>
        public IFileContentTypeManager FileContentTypeManager { get; }

        /// <summary>Gets the application status.</summary>
        public IApplicationStatusInfo AppStatus { get; }

        /// <summary>Gets the folder resolution cascade.</summary>
        public BrowserFolderResolver FolderResolver { get; }
    }
}
