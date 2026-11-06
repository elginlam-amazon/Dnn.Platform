// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.InternalServices;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using DotNetNuke.Abstractions.Application;
using DotNetNuke.Abstractions.Portals;
using DotNetNuke.Abstractions.Security;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Icons;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;
using DotNetNuke.Instrumentation;
using DotNetNuke.Security;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Services.Localization;
using DotNetNuke.Web.Api;
using DotNetNuke.Web.Api.Internal;
using DotNetNuke.Web.Common.Internal;
using DotNetNuke.Web.UI.WebControls.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using FileInfo = DotNetNuke.Services.FileSystem.FileInfo;

/// <summary>A web API for uploading files.</summary>
[DnnAuthorize]
public class FileUploadController : DnnApiController
{
    private static readonly ILogger Logger = DnnLoggingController.GetLogger<FileUploadController>();
    private static readonly Regex UserFolderEx = new Regex(@"users/\d+/\d+/(\d+)/", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly List<string> ImageExtensions = Globals.ImageFileTypes.Split(',').ToList();

    private readonly IHostSettings hostSettings;
    private readonly ICryptographyProvider cryptographyProvider;
    private readonly IPortalController portalController;
    private readonly IApplicationStatusInfo appStatus;
    private readonly IPortalGroupController portalGroupController;

    /// <summary>Initializes a new instance of the <see cref="FileUploadController"/> class.</summary>
    [Obsolete("Deprecated in DotNetNuke 10.2.2. Use overload with ICryptographyProvider. Scheduled for removal in v12.0.0.")]
    public FileUploadController()
        : this(null, null, null, null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FileUploadController"/> class.</summary>
    /// <param name="hostSettings">The host settings.</param>
    [Obsolete("Deprecated in DotNetNuke 10.2.2. Use overload with ICryptographyProvider. Scheduled for removal in v12.0.0.")]
    public FileUploadController(IHostSettings hostSettings)
        : this(hostSettings, null, null, null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FileUploadController"/> class.</summary>
    /// <param name="hostSettings">The host settings.</param>
    /// <param name="cryptographyProvider">The cryptography provider.</param>
    /// <param name="portalController">The portal controller.</param>
    /// <param name="appStatus">The application status.</param>
    /// <param name="portalGroupController">The portal group controller.</param>
    public FileUploadController(IHostSettings hostSettings, ICryptographyProvider cryptographyProvider, IPortalController portalController, IApplicationStatusInfo appStatus, IPortalGroupController portalGroupController)
    {
        this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
        this.cryptographyProvider = cryptographyProvider ?? Globals.GetCurrentServiceProvider().GetRequiredService<ICryptographyProvider>();
        this.portalController = portalController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>();
        this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
        this.portalGroupController = portalGroupController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalGroupController>();
    }

    /// <summary>Gets the URL for a file.</summary>
    /// <param name="fileId">The ID of the file.</param>
    /// <returns>The URL.</returns>
    public static string GetUrl(int fileId)
    {
        var file = FileManager.Instance.GetFile(fileId, true);
        return FileManager.Instance.GetUrl(file);
    }

    /// <summary>Gets the files in the folder.</summary>
    /// <param name="folderItem">Information about the folder.</param>
    /// <returns>A response with a list of <see cref="FileItem"/> objects.</returns>
    [HttpPost]
    public IActionResult LoadFiles(FolderItemDTO folderItem)
    {
        var effectivePortalId = this.PortalSettings.PortalId;

        if (folderItem.FolderId <= 0)
        {
            return DnnApiResults.Response(HttpStatusCode.BadRequest);
        }

        var folder = FolderManager.Instance.GetFolder(folderItem.FolderId);
        if (folder == null)
        {
            return DnnApiResults.Response(HttpStatusCode.BadRequest);
        }

        if (IsUserFolder(folder.FolderPath, out var userId))
        {
            var user = UserController.GetUserById(this.hostSettings, effectivePortalId, userId);
            if (user is { IsSuperUser: true, })
            {
                effectivePortalId = Null.NullInteger;
            }
            else
            {
                effectivePortalId = PortalController.GetEffectivePortalId(this.portalController, this.appStatus, this.portalGroupController, effectivePortalId);
            }
        }

        var list = Globals.GetFileList(effectivePortalId, folderItem.FileFilter, !folderItem.Required, folder.FolderPath);
        var fileItems = list.OfType<FileItem>().ToList();

        return DnnApiResults.Response(HttpStatusCode.OK, fileItems);
    }

    // A routable MVC action must stay a public instance method; a static method is not discovered as an action.
    #pragma warning disable CA1822 // Mark members as static
    /// <summary>Gets the URL of an image file.</summary>
    /// <param name="fileId">The file ID.</param>
    /// <returns>A response with either <c>null</c> or the image URL.</returns>
    [HttpGet]
    public IActionResult LoadImage(string fileId)
    {
        if (string.IsNullOrEmpty(fileId) || !int.TryParse(fileId, out var file))
        {
            return DnnApiResults.Response(HttpStatusCode.InternalServerError);
        }

        var imageUrl = ShowImage(file);
        return imageUrl == null
            ? DnnApiResults.Response(HttpStatusCode.NotFound)
            : DnnApiResults.Response(HttpStatusCode.OK, imageUrl);
    }
    #pragma warning restore CA1822 // Mark members as static

    /// <summary>Uploads an image.</summary>
    /// <returns>A response with an <see cref="SavedFileDTO"/> object.</returns>
    /// <remarks>
    /// Was a <c>MultipartMemoryStreamProvider</c>-based Web API 2 action. On ASP.NET Core the multipart
    /// body is read through <see cref="IFormCollection"/>/<see cref="IFormFile"/>, but every form field
    /// name, status code and response body (including the <c>text/plain</c> content type the
    /// <c>DnnFileUpload</c> iframe transport requires) is unchanged.
    /// </remarks>
    [HttpPost]
    [IFrameSupportedValidateAntiForgeryToken]
    public async Task<IActionResult> PostFile()
    {
        if (!IsMultipartRequest(this.Request))
        {
            return DnnApiResults.Response(HttpStatusCode.UnsupportedMediaType);
        }

        var form = await this.Request.ReadFormAsync();

        var folder = GetFormValue(form, "folder");
        var filter = GetFormValue(form, "filter");
        if (!bool.TryParse(GetFormValue(form, "overwrite"), out var overwrite))
        {
            overwrite = false;
        }

        if (!bool.TryParse(GetFormValue(form, "ishostmenu"), out var isHostMenu))
        {
            isHostMenu = false;
        }

        if (!bool.TryParse(GetFormValue(form, "extract"), out var extract))
        {
            extract = false;
        }

        var fileName = string.Empty;
        var postedFile = GetPostedFile(form);
        if (postedFile != null)
        {
            fileName = GetPostedFileName(postedFile);
        }

        var errorMessage = string.Empty;
        var alreadyExists = false;
        var returnFileDto = new SavedFileDTO();
        if (!string.IsNullOrEmpty(fileName) && postedFile != null)
        {
            // Everything ready
            using var stream = postedFile.OpenReadStream();
            returnFileDto = SaveFile(stream, this.portalController, this.appStatus, this.portalGroupController, this.hostSettings, this.PortalSettings, this.UserInfo, folder, filter, fileName, overwrite, isHostMenu, extract, out alreadyExists, out errorMessage);
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            return PlainTextJson(
                HttpStatusCode.BadRequest,
                new
                {
                    AlreadyExists = alreadyExists,
                    Message = string.Format(
                        CultureInfo.CurrentCulture,
                        CompositeFormatCache.Get(GetLocalizedString("ErrorMessage")),
                        fileName,
                        errorMessage),
                });
        }

        return PlainTextJson(HttpStatusCode.OK, returnFileDto);
    }

    /// <summary>Uploads a file to the current portal.</summary>
    /// <returns>A response with a <see cref="FileUploadDto"/> object.</returns>
    [HttpPost]
    [IFrameSupportedValidateAntiForgeryToken]
    [AllowAnonymous]
    public Task<IActionResult> UploadFromLocal()
    {
        return this.UploadFromLocal(this.PortalSettings.PortalId);
    }

    /// <summary>Uploads a file.</summary>
    /// <param name="portalId">The ID of the portal to which to upload it.</param>
    /// <returns>A response with a <see cref="FileUploadDto"/> object.</returns>
    [HttpPost]
    [IFrameSupportedValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> UploadFromLocal(int portalId)
    {
        if (!IsMultipartRequest(this.Request))
        {
            return DnnApiResults.Response(HttpStatusCode.UnsupportedMediaType);
        }

        if (portalId > -1)
        {
            if (!this.IsPortalIdValid(portalId))
            {
                return DnnApiResults.Response(HttpStatusCode.Unauthorized);
            }
        }
        else
        {
            portalId = this.PortalSettings.PortalId;
        }

        var form = await this.Request.ReadFormAsync();
        var userInfo = this.UserInfo;

        var folder = GetFormValue(form, "folder");
        var filter = GetFormValue(form, "filter");
        var validationCode = GetFormValue(form, "validationcode");
        if (!bool.TryParse(GetFormValue(form, "overwrite"), out var overwrite))
        {
            overwrite = false;
        }

        if (!bool.TryParse(GetFormValue(form, "ishostportal"), out var isHostPortal))
        {
            isHostPortal = false;
        }

        if (!bool.TryParse(GetFormValue(form, "extract"), out var extract))
        {
            extract = false;
        }

        if (userInfo.IsSuperUser && form.ContainsKey("portalid"))
        {
            if (int.TryParse(GetFormValue(form, "portalid"), out var requestedPortalId))
            {
                portalId = requestedPortalId;
            }
        }

        FileUploadDto result = null;
        var postedFile = GetPostedFile(form);
        if (postedFile != null)
        {
            var fileName = GetPostedFileName(postedFile);
            if (!string.IsNullOrEmpty(fileName) && !Globals.FileEscapingRegex.IsMatch(fileName))
            {
                using var stream = postedFile.OpenReadStream();
                result = UploadFile(this.cryptographyProvider, this.hostSettings, stream, portalId, userInfo, folder, filter, fileName, overwrite, isHostPortal, extract, validationCode);
            }
        }

        /* Response Content Type cannot be application/json
         * because IE9 with iframe-transport manages the response
         * as a file download
         */
        return PlainTextJson(HttpStatusCode.OK, result);
    }

    /// <summary>Determines whether the request body is a multipart MIME payload.</summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> if the request is multipart, otherwise <see langword="false"/>.</returns>
    private static bool IsMultipartRequest(HttpRequest request)
    {
        return request.HasFormContentType
            && request.ContentType?.Contains("multipart/", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>Reads a form field by name, matching case-insensitively as the legacy code did.</summary>
    /// <param name="form">The posted form.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or <see cref="string.Empty"/>.</returns>
    private static string GetFormValue(IFormCollection form, string name)
    {
        foreach (var key in form.Keys)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return form[key].ToString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    /// <summary>Gets the posted file, which the clients send in the <c>postfile</c> field.</summary>
    /// <param name="form">The posted form.</param>
    /// <returns>The file, or <see langword="null"/>.</returns>
    private static IFormFile GetPostedFile(IFormCollection form)
    {
        if (form.Files is null || form.Files.Count == 0)
        {
            return null;
        }

        return form.Files.FirstOrDefault(f => string.Equals(f.Name, "postfile", StringComparison.OrdinalIgnoreCase))
            ?? form.Files[0];
    }

    /// <summary>Gets the file name of the posted file, applying the legacy quote/path stripping.</summary>
    /// <param name="postedFile">The posted file.</param>
    /// <returns>The file name.</returns>
    private static string GetPostedFileName(IFormFile postedFile)
    {
        var fileName = (postedFile.FileName ?? string.Empty).Replace("\"", string.Empty);
        if (fileName.Contains('\\'))
        {
            fileName = Path.GetFileName(fileName);
        }

        return fileName;
    }

    /// <summary>
    /// Writes <paramref name="value"/> as JSON with a <c>text/plain</c> content type, which is what the
    /// legacy <c>JsonMediaTypeFormatter</c> + <c>text/plain</c> media type did.
    /// </summary>
    /// <remarks>
    /// The response content type cannot be <c>application/json</c>, because IE9 with the iframe transport
    /// manages such a response as a file download. Newtonsoft is used (rather than
    /// <c>System.Text.Json</c>) so that the <see cref="DataMemberAttribute"/> names on
    /// <see cref="FileUploadDto"/> keep producing exactly the same JSON keys as on .NET Framework.
    /// </remarks>
    /// <param name="statusCode">The status code.</param>
    /// <param name="value">The value to serialize.</param>
    /// <returns>A <see cref="ContentResult"/>.</returns>
    private static ContentResult PlainTextJson(HttpStatusCode statusCode, object value)
    {
        return new ContentResult
        {
            Content = JsonConvert.SerializeObject(value),
            ContentType = "text/plain",
            StatusCode = (int)statusCode,
        };
    }

    // CA1859 suggests the concrete PortalSettings type here, but the only PortalSecurity.IsInRoles
    // overload taking it is obsolete; the interface keeps this helper on the supported API.
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static SavedFileDTO SaveFile(
        Stream stream,
        IPortalController portalController,
        IApplicationStatusInfo appStatus,
        IPortalGroupController portalGroupController,
        IHostSettings hostSettings,
        IPortalSettings portalSettings,
        UserInfo userInfo,
        string folder,
        string filter,
        string fileName,
        bool overwrite,
        bool isHostMenu,
        bool extract,
        out bool alreadyExists,
        out string errorMessage)
    {
        alreadyExists = false;
        var savedFileDto = new SavedFileDTO();
        try
        {
            var extension = Path.GetExtension(fileName).ValueOrEmpty().Replace(".", string.Empty);
            if (!string.IsNullOrEmpty(filter) && !filter.Contains(extension, StringComparison.InvariantCultureIgnoreCase))
            {
                errorMessage = GetLocalizedString("ExtensionNotAllowed");
                return savedFileDto;
            }

            var folderManager = FolderManager.Instance;

            // Check if this is a User Folder
            var effectivePortalId = isHostMenu ? Null.NullInteger : PortalController.GetEffectivePortalId(portalController, appStatus, portalGroupController, portalSettings.PortalId);
            var folderInfo = folderManager.GetFolder(effectivePortalId, folder);
            if (IsUserFolder(folder, out var userId))
            {
                var user = UserController.GetUserById(hostSettings, effectivePortalId, userId);
                if (user != null)
                {
                    folderInfo = folderManager.GetUserFolder(user);
                }
            }

            if (!PortalSecurity.IsInRoles(userInfo, portalSettings, folderInfo.FolderPermissions.ToString("WRITE"))
                && !PortalSecurity.IsInRoles(userInfo, portalSettings, folderInfo.FolderPermissions.ToString("ADD")))
            {
                errorMessage = GetLocalizedString("NoPermission");
                return savedFileDto;
            }

            const bool AlreadyCheckedPermissions = true;
            if (!overwrite && FileManager.Instance.FileExists(folderInfo, fileName, true))
            {
                errorMessage = GetLocalizedString("AlreadyExists");
                alreadyExists = true;
                savedFileDto.FilePath = Path.Combine(folderInfo.PhysicalPath, fileName);
                return savedFileDto;
            }

            var contentType = FileContentTypeManager.Instance.GetContentType(Path.GetExtension(fileName));
            var file = FileManager.Instance.AddFile(folderInfo, fileName, stream, true, !AlreadyCheckedPermissions, contentType, userInfo.UserID);

            if (extract && extension.Equals("zip", StringComparison.OrdinalIgnoreCase))
            {
                FileManager.Instance.UnzipFile(file);
                FileManager.Instance.DeleteFile(file);
            }

            errorMessage = string.Empty;
            savedFileDto.FileId = file.FileId.ToString(CultureInfo.InvariantCulture);
            savedFileDto.FilePath = FileManager.Instance.GetUrl(file);
            return savedFileDto;
        }
        catch (InvalidFileExtensionException)
        {
            errorMessage = GetLocalizedString("ExtensionNotAllowed");
            return savedFileDto;
        }
        catch (Exception ex)
        {
            Logger.FileUploadControllerSaveFileException(ex);
            errorMessage = ex.Message;
            return savedFileDto;
        }
    }
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

    private static string GetLocalizedString(string key)
    {
        return Localization.GetString(key, resourceFileRoot: "/App_GlobalResources/FileUpload.resx");
    }

    private static bool IsUserFolder(string folderPath, out int userId)
    {
        var match = UserFolderEx.Match(folderPath);
        userId = match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : Null.NullInteger;

        return match.Success;
    }

    private static string ShowImage(int fileId)
    {
        var image = (FileInfo)FileManager.Instance.GetFile(fileId);

        if (image != null && IsImageExtension(image.Extension))
        {
            var folder = FolderManager.Instance.GetFolder(image.FolderId);
            if (folder == null || !FolderPermissionController.CanViewFolder((FolderInfo)folder))
            {
                return null;
            }

            var imageUrl = FileManager.Instance.GetUrl(image);
            return imageUrl;
        }

        return null;
    }

    private static bool IsImageExtension(string extension)
    {
        return ImageExtensions.Any(e => e.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsImage(string fileName)
    {
        return ImageExtensions.Any(extension => fileName.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase));
    }

    private static FileUploadDto UploadFile(
        ICryptographyProvider cryptographyProvider,
        IHostSettings hostSettings,
        Stream stream,
        int portalId,
        UserInfo userInfo,
        string folder,
        string filter,
        string fileName,
        bool overwrite,
        bool isHostPortal,
        bool extract,
        string validationCode)
    {
        var result = new FileUploadDto();
        BinaryReader reader = null;
        Stream fileContent = null;
        try
        {
            var extensionList = new List<string>();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                extensionList = filter.Split(',').Select(i => i.Trim()).ToList();
            }

            var validateParams = new List<object> { extensionList, userInfo.UserID };
            if (!userInfo.IsSuperUser)
            {
                validateParams.Add(portalId);
            }

            if (!ValidationUtils.ValidationCodeMatched(cryptographyProvider, hostSettings, validateParams, validationCode))
            {
                throw new InvalidOperationException("Bad Request");
            }

            var extension = Path.GetExtension(fileName).ValueOrEmpty().Replace(".", string.Empty);
            result.FileIconUrl = IconController.GetFileIconUrl(extension);

            if (!string.IsNullOrEmpty(filter) && !filter.Contains(extension, StringComparison.InvariantCultureIgnoreCase))
            {
                result.Message = GetLocalizedString("ExtensionNotAllowed");
                return result;
            }

            var folderManager = FolderManager.Instance;
            var effectivePortalId = isHostPortal ? Null.NullInteger : portalId;
            var folderInfo = folderManager.GetFolder(effectivePortalId, folder);

            if (folderInfo == null && IsUserFolder(folder, out var userId))
            {
                var user = UserController.GetUserById(hostSettings, effectivePortalId, userId);
                if (user != null)
                {
                    folderInfo = folderManager.GetUserFolder(user);
                }
            }

            if (!FolderPermissionController.HasFolderPermission(portalId, folder, "WRITE")
                && !FolderPermissionController.HasFolderPermission(portalId, folder, "ADD"))
            {
                result.Message = GetLocalizedString("NoPermission");
                return result;
            }

            const bool AlreadyCheckedPermissions = true;
            IFileInfo file;
            if (!overwrite && FileManager.Instance.FileExists(folderInfo, fileName, true))
            {
                result.Message = GetLocalizedString("AlreadyExists");
                result.AlreadyExists = true;
                file = FileManager.Instance.GetFile(folderInfo, fileName, true);
                result.FileId = file.FileId;
            }
            else
            {
                file = FileManager.Instance.AddFile(folderInfo, fileName, stream, true, !AlreadyCheckedPermissions, FileContentTypeManager.Instance.GetContentType(Path.GetExtension(fileName)), userInfo.UserID);
                if (extract && extension.Equals("zip", StringComparison.OrdinalIgnoreCase))
                {
                    var destinationFolder = FolderManager.Instance.GetFolder(file.FolderId);
                    var invalidFiles = new List<string>();
                    var filesCount = FileManager.Instance.UnzipFile(file, destinationFolder, invalidFiles);

                    var invalidFilesJson = invalidFiles.Count > 0
                        ? string.Join(",", invalidFiles.Select(invalidFile => JsonConvert.ToString(invalidFile)))
                        : string.Empty;
                    result.Prompt = $"{{\"invalidFiles\":[{invalidFilesJson}], \"totalCount\": {filesCount}}}";
                }

                result.FileId = file.FileId;
            }

            fileContent = FileManager.Instance.GetFileContent(file);

            var path = GetUrl(result.FileId);
            using (reader = new BinaryReader(fileContent))
            {
                // Read the dimensions without binding to a specific Size type: ImageHeader is being ported
                // from System.Drawing to ImageSharp, and the orientation rule (wider than tall is
                // horizontal, otherwise vertical) is identical either way.
                int width;
                int height;
                if (IsImage(fileName))
                {
                    try
                    {
                        var dimensions = ImageHeader.GetDimensions(reader);
                        width = dimensions.Width;
                        height = dimensions.Height;
                    }
                    catch (ArgumentException exc)
                    {
                        Logger.FileUploadControllerUnableToGetImageDimensions(exc);
                        width = 32;
                        height = 32;
                    }
                }
                else
                {
                    width = 32;
                    height = 32;
                }

                result.Orientation = width > height ? DnnOrientation.Horizontal : DnnOrientation.Vertical;
            }

            result.Path = result.FileId > 0 ? path : string.Empty;
            result.FileName = fileName;

            if (extract && extension.Equals("zip", StringComparison.OrdinalIgnoreCase))
            {
                FileManager.Instance.DeleteFile(file);
            }

            return result;
        }
        catch (InvalidFileExtensionException)
        {
            result.Message = GetLocalizedString("ExtensionNotAllowed");
            return result;
        }
        catch (Exception exe)
        {
            Logger.FileUploadControllerUploadFileException(exe);
            result.Message = exe.Message;
            return result;
        }
        finally
        {
            if (reader != null)
            {
                reader.Close();
                reader.Dispose();
            }

            if (fileContent != null)
            {
                fileContent.Close();
                fileContent.Dispose();
            }
        }
    }

    private static IPortalInfo[] GetMyPortalGroup()
    {
        return (
            from @group in PortalGroupController.Instance.GetPortalGroups().ToArray()
            select PortalGroupController.Instance.GetPortalsByGroup(@group.PortalGroupId) into portals
            where portals.Any((IPortalInfo x) => x.PortalId == PortalSettings.Current.PortalId)
            select portals.Cast<IPortalInfo>().ToArray())
            .FirstOrDefault();
    }

    private bool IsPortalIdValid(int portalId)
    {
        if (this.UserInfo.IsSuperUser)
        {
            return true;
        }

        if (this.PortalSettings.PortalId == portalId)
        {
            return true;
        }

        var isAdminUser = PortalSecurity.IsInRole(this.PortalSettings.AdministratorRoleName);
        if (!isAdminUser)
        {
            return false;
        }

        var myGroup = GetMyPortalGroup();
        return myGroup != null && myGroup.Any(p => p.PortalId == portalId);
    }

    /// <summary>A data transfer object with information about a folder.</summary>
    public class FolderItemDTO
    {
        /// <summary>Gets or sets the folder ID.</summary>
        public int FolderId { get; set; }

        /// <summary>Gets or sets the file filter.</summary>
        public string FileFilter { get; set; }

        /// <summary>Gets or sets a value indicating whether to include an entry for an unspecified file.</summary>
        public bool Required { get; set; }
    }

    /// <summary>A data transfer object with information about a file that has been saved.</summary>
    public class SavedFileDTO
    {
        /// <summary>Gets or sets the ID of the file.</summary>
        public string FileId { get; set; }

        /// <summary>Gets or sets the path of the file.</summary>
        public string FilePath { get; set; }
    }

    /// <summary>A data transfer object with information about an upload by URL request.</summary>
    public class UploadByUrlDto
    {
        /// <summary>Gets or sets the URL.</summary>
        public string Url { get; set; }

        /// <summary>Gets or sets the destination folder.</summary>
        public string Folder { get; set; }

        /// <summary>Gets or sets a value indicating whether to overwrite an existing file.</summary>
        public bool Overwrite { get; set; }

        /// <summary>Gets or sets a value indicating whether to unzip the resulting file.</summary>
        public bool Unzip { get; set; }

        /// <summary>Gets or sets the filter.</summary>
        public string Filter { get; set; }

        /// <summary>Gets or sets a value indicating whether the request is from the host menu.</summary>
        public bool IsHostMenu { get; set; }

        /// <summary>Gets or sets the portal ID.</summary>
        public int PortalId { get; set; } = -1;

        /// <summary>Gets or sets the validation code.</summary>
        public string ValidationCode { get; set; }
    }

    /// <summary>A data transfer object with information about a file upload.</summary>
    [DataContract]
    public class FileUploadDto
    {
        /// <summary>Gets or sets the file path.</summary>
        [DataMember(Name = "path")]
        public string Path { get; set; }

        /// <summary>Gets or sets the image orientation.</summary>
        [DataMember(Name = "orientation")]
        public DnnOrientation Orientation { get; set; }

        /// <summary>Gets or sets a value indicating whether the file already exists.</summary>
        [DataMember(Name = "alreadyExists")]
        public bool AlreadyExists { get; set; }

        /// <summary>Gets or sets an error message.</summary>
        [DataMember(Name = "message")]
        public string Message { get; set; }

        /// <summary>Gets or sets the URL of the file type icon.</summary>
        [DataMember(Name = "fileIconUrl")]
        public string FileIconUrl { get; set; }

        /// <summary>Gets or sets the ID of the file.</summary>
        [DataMember(Name = "fileId")]
        public int FileId { get; set; }

        /// <summary>Gets or sets the name of the file.</summary>
        [DataMember(Name = "fileName")]
        public string FileName { get; set; }

        /// <summary>Gets or sets a JSON string with <c>invalidFiles</c> and <c>totalCount</c> fields.</summary>
        [DataMember(Name = "prompt")]
        public string Prompt { get; set; }
    }
}
