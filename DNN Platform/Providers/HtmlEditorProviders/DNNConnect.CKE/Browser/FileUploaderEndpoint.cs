// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider.Browser;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using DNNConnect.CKEditorProvider.Constants;
using DNNConnect.CKEditorProvider.Objects;
using DNNConnect.CKEditorProvider.Utilities;

using DotNetNuke.Abstractions.Application;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework.Providers;
using DotNetNuke.Security.Roles;
using DotNetNuke.Services.FileSystem;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

/// <summary>
/// The file upload endpoint which replaces the <c>FileUploader.ashx</c> <c>IHttpHandler</c>, used by
/// the jQuery-File-Upload widget of the file browser.
/// </summary>
/// <remarks>
/// The URL is preserved exactly (<c>Browser.aspx</c> emits it into the jQuery-File-Upload
/// configuration) and so is the JSON response shape, which that client parses.
/// </remarks>
public static class FileUploaderEndpoint
{
    /// <summary>The preserved URL of the file upload handler.</summary>
    public const string FileUploaderPath = "/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/FileUploader.ashx";

    /// <summary>Maps the file upload endpoint onto its original URL.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same <paramref name="endpoints"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    public static IEndpointRouteBuilder MapCKEditorFileUploaderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // The handler uploaded on POST and PUT. The deny-anonymous <location> rule the .dnn manifest
        // installed into web.config for this URL becomes RequireAuthorization() here.
        endpoints
            .MapMethods(FileUploaderPath, new[] { HttpMethods.Post, HttpMethods.Put }, HandleUploadAsync)
            .RequireAuthorization()
            .DisableAntiforgery();

        // The handler answered OPTIONS with the list of allowed verbs (jQuery-File-Upload probes it).
        endpoints
            .MapMethods(FileUploaderPath, new[] { HttpMethods.Options }, ReturnOptions)
            .RequireAuthorization();

        // GET/HEAD were an empty (200) response in the handler: the listing code was commented out.
        endpoints
            .MapMethods(FileUploaderPath, new[] { HttpMethods.Get, HttpMethods.Head }, AddNoCacheHeaders)
            .RequireAuthorization();

        return endpoints;
    }

    /// <summary>Reads a request value from the form or the query string, as <c>Request[key]</c> did.</summary>
    /// <param name="request">The request.</param>
    /// <param name="key">The value name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    private static string GetRequestValue(HttpRequest request, string key)
    {
        if (request.HasFormContentType && request.Form.ContainsKey(key))
        {
            return request.Form[key];
        }

        return request.Query.ContainsKey(key) ? request.Query[key].ToString() : null;
    }

    /// <summary>Gets a value indicating whether to override files.</summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when existing files must be overwritten.</returns>
    private static bool GetOverrideFiles(HttpRequest request)
    {
        var overrideFiles = GetRequestValue(request, "overrideFiles");

        return overrideFiles == "1"
               || string.Equals(overrideFiles, "true", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>Gets the storage folder the files are uploaded into.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The storage folder.</returns>
    private static IFolderInfo GetStorageFolder(HttpRequest request)
    {
        return FolderManager.Instance.GetFolder(
            Convert.ToInt32(GetRequestValue(request, "storageFolderID"), CultureInfo.InvariantCulture));
    }

    private static void AddNoCacheHeaders(HttpContext context)
    {
        context.Response.Headers.Append("Pragma", "no-cache");
        context.Response.Headers.Append("Cache-Control", "private, no-cache");
    }

    private static void ReturnOptions(HttpContext context)
    {
        AddNoCacheHeaders(context);
        context.Response.Headers.Append("Allow", "DELETE,GET,HEAD,POST,PUT,OPTIONS");
        context.Response.StatusCode = 200;
    }

    /// <summary>Determines the dimensions the uploaded image must be resized to.</summary>
    /// <param name="width">The width of the uploaded image.</param>
    /// <param name="height">The height of the uploaded image.</param>
    /// <param name="maxWidth">The configured maximum width.</param>
    /// <param name="maxHeight">The configured maximum height.</param>
    /// <returns>The new dimensions.</returns>
    private static (int NewWidth, int NewHeight) DetermineNewDimensions(int width, int height, int maxWidth, int maxHeight)
    {
        // it's too big: we need to resize
        int newWidth, newHeight;

        // which determines the max: height or width?
        var ratioWidth = (double)maxWidth / (double)width;
        var ratioHeight = (double)maxHeight / (double)height;
        if (ratioWidth < ratioHeight)
        {
            // max width needs to be used
            newWidth = maxWidth;
            newHeight = (int)Math.Round(height * ratioWidth);
        }
        else
        {
            // max height needs to be used
            newHeight = maxHeight;
            newWidth = (int)Math.Round(width * ratioHeight);
        }

        return (newWidth, newHeight);
    }

    /// <summary>Resizes the image, keeping the format of the uploaded image.</summary>
    /// <param name="source">The uploaded image stream.</param>
    /// <param name="newWidth">The new width.</param>
    /// <param name="newHeight">The new height.</param>
    /// <returns>A rewound stream holding the resized image.</returns>
    private static Stream ResizeImage(Stream source, int newWidth, int newHeight)
    {
        using var uplImage = Image.Load(source);

        // The format of the uploaded image is preserved (was Image.RawFormat on System.Drawing).
        var imageFormat = uplImage.Metadata.DecodedImageFormat;

        uplImage.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(newWidth, newHeight),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Bicubic,
        }));

        var fileContents = new MemoryStream();

        // Add Compression to Jpeg Images, as the System.Drawing pipeline did (quality 80).
        if (imageFormat is JpegFormat)
        {
            uplImage.Save(fileContents, new JpegEncoder { Quality = 80 });
        }
        else if (imageFormat is not null)
        {
            uplImage.Save(fileContents, imageFormat);
        }
        else
        {
            uplImage.Save(fileContents, new JpegEncoder { Quality = 80 });
        }

        fileContents.Position = 0;
        return fileContents;
    }

    private static EditorProviderSettings GetCurrentSettings(HttpRequest request, IHostSettings hostSettings, out PortalSettings portalSettings)
    {
        // The original read Request["PortalID"] and let Convert fall back to 0 for a missing or
        // non numeric value, which is the host portal; the discard makes that explicit.
        _ = int.TryParse(request.Query["PortalID"], out var portalId);
        portalSettings = new PortalSettings(portalId);

        if (!Enum.TryParse<SettingsMode>(request.Query["mode"], true, out var settingMode))
        {
            settingMode = SettingsMode.Default;
        }

        var providerConfiguration = ProviderConfiguration.GetProviderConfiguration("htmlEditor");
        var objProvider = (Provider)providerConfiguration.Providers[providerConfiguration.DefaultProvider];
        var settingsDictionary = EditorController.GetEditorHostSettings(hostSettings);
        var portalRoles = RoleController.Instance.GetRoles(portalSettings.PortalId);

        var currentPortalSettings = portalSettings;
        return settingMode switch
        {
            SettingsMode.Default =>
                SettingsUtil.GetDefaultSettings(
                    currentPortalSettings,
                    hostSettings,
                    currentPortalSettings.HomeDirectoryMapPath,
                    objProvider.Attributes["ck_configFolder"],
                    portalRoles),
            SettingsMode.Host =>
                SettingsUtil.LoadEditorSettingsByKey(
                    currentPortalSettings,
                    new EditorProviderSettings(),
                    settingsDictionary,
                    SettingConstants.HostKey,
                    portalRoles),
            SettingsMode.Portal =>
                SettingsUtil.LoadEditorSettingsByKey(
                    currentPortalSettings,
                    new EditorProviderSettings(),
                    settingsDictionary,
                    SettingConstants.PortalKey(portalId),
                    portalRoles),
            SettingsMode.Page =>
                SettingsUtil.LoadEditorSettingsByKey(
                    currentPortalSettings,
                    new EditorProviderSettings(),
                    settingsDictionary,
                    $"DNNCKT#{request.Query["tabid"]}#",
                    portalRoles),
            SettingsMode.ModuleInstance =>
                SettingsUtil.LoadModuleSettings(
                    currentPortalSettings,
                    new EditorProviderSettings(),
                    $"DNNCKMI#{request.Query["mid"]}#INS#{request.Query["ckId"]}#",
                    int.Parse(request.Query["mid"], CultureInfo.InvariantCulture),
                    portalRoles),
            _ => new EditorProviderSettings(),
        };
    }

    private static async Task HandleUploadAsync(HttpContext context)
    {
        AddNoCacheHeaders(context);

        var hostSettings = context.RequestServices.GetRequiredService<IHostSettings>();

        var statuses = new List<FilesUploadStatus>();

        UploadWholeFile(context, hostSettings, statuses);

        await WriteJsonIframeSafeAsync(context, statuses);
    }

    /// <summary>Uploads the whole file.</summary>
    /// <param name="context">The context.</param>
    /// <param name="hostSettings">The host settings.</param>
    /// <param name="statuses">The statuses.</param>
    private static void UploadWholeFile(HttpContext context, IHostSettings hostSettings, List<FilesUploadStatus> statuses)
    {
        var request = context.Request;
        if (!request.HasFormContentType)
        {
            return;
        }

        var overrideFiles = GetOverrideFiles(request);
        var storageFolder = GetStorageFolder(request);

        var files = request.Form.Files;
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file is null)
            {
                continue;
            }

            var fileName = Path.GetFileName(file.FileName);

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
                // Was an HttpRequestValidationException, which does not exist on ASP.NET Core; the
                // request is equally invalid, so it is reported as a bad request.
                throw new BadHttpRequestException("File does not have a name");
            }

            if (fileName.Length > 220)
            {
                fileName = fileName.Substring(fileName.Length - 220);
            }

            // file names starting with '\\' may be used for manipulating the filepath and explore vulnerabilities
            fileName = Regex.Replace(fileName, @"^\\+", string.Empty);

            var fileNameNoExtenstion = Path.GetFileNameWithoutExtension(fileName);

            // Rename File if Exists
            if (!overrideFiles)
            {
                var counter = 0;

                while (File.Exists(Path.Combine(storageFolder.PhysicalPath, fileName)))
                {
                    counter++;
                    fileName = string.Format(
                        "{0}_{1}{2}",
                        fileNameNoExtenstion,
                        counter,
                        Path.GetExtension(file.FileName));
                }
            }

            var contentType = FileContentTypeManager.Instance.GetContentType(Path.GetExtension(fileName));
            var userId = UserController.Instance.GetCurrentUserInfo().UserID;

            var disposeStream = false;
            Stream fileContents = null;
            try
            {
                if (!contentType.StartsWith("image", StringComparison.InvariantCultureIgnoreCase))
                {
                    fileContents = file.OpenReadStream();
                    disposeStream = true;
                }
                else
                {
                    // it's an image, so we might need to resize
                    var currentSettings = GetCurrentSettings(request, hostSettings, out _);

                    int maxWidth = currentSettings.ResizeWidthUpload;
                    int maxHeight = currentSettings.ResizeHeightUpload;
                    if (maxWidth <= 0 && maxHeight <= 0)
                    {
                        fileContents = file.OpenReadStream();
                        disposeStream = true;
                    }
                    else
                    {
                        // check if the size of the image is within boundaries
                        ImageInfo imageInfo;
                        using (var probeStream = file.OpenReadStream())
                        {
                            imageInfo = Image.Identify(probeStream);
                        }

                        disposeStream = true;
                        if (imageInfo.Width <= maxWidth && imageInfo.Height <= maxHeight)
                        {
                            // fits within configured maximum dimensions
                            fileContents = file.OpenReadStream();
                        }
                        else
                        {
                            var (newWidth, newHeight) = DetermineNewDimensions(imageInfo.Width, imageInfo.Height, maxWidth, maxHeight);

                            using var sourceStream = file.OpenReadStream();
                            fileContents = ResizeImage(sourceStream, newWidth, newHeight);
                        }
                    }
                }

                FileManager.Instance.AddFile(
                    storageFolder,
                    fileName,
                    fileContents,
                    overrideFiles,
                    true,
                    contentType,
                    userId);
            }
            finally
            {
                if (disposeStream)
                {
                    fileContents?.Dispose();
                }
            }

            var fullName = Path.GetFileName(fileName);
            statuses.Add(new FilesUploadStatus(fullName, (int)file.Length));
        }
    }

    /// <summary>Writes the JSON iFrame safe.</summary>
    /// <param name="context">The context.</param>
    /// <param name="statuses">The statuses.</param>
    /// <returns>A task.</returns>
    private static Task WriteJsonIframeSafeAsync(HttpContext context, List<FilesUploadStatus> statuses)
    {
        context.Response.Headers.Append("Vary", "Accept");
        var accept = context.Request.Headers.Accept.ToString();
        context.Response.ContentType =
            !string.IsNullOrEmpty(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                ? "application/json"
                : "text/plain";

        // The jQuery-File-Upload client depends on this exact shape (the lower-case property names of
        // FilesUploadStatus), which the Newtonsoft serializer reproduces 1:1.
        var jsonObj = JsonConvert.SerializeObject(statuses.ToArray());
        return context.Response.WriteAsync(jsonObj);
    }
}
