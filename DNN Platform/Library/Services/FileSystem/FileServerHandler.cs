// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.FileSystem
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Services.FileSystem.EventArgs;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;
    using Microsoft.Net.Http.Headers;

    /// <summary>
    /// Serves secure file downloads and tracked links. This is the ASP.NET Core replacement for
    /// the <c>LinkClickHandler</c> <c>IHttpHandler</c> which was registered in <c>web.config</c>
    /// for <c>path="LinkClick.aspx" verb="*"</c>.
    /// </summary>
    public static class FileServerHandler
    {
        /// <summary>The preserved LinkClick URL, so existing links and e-mails keep working.</summary>
        public const string LinkClickPath = "/LinkClick.aspx";

        /// <summary>
        /// The preserved LinkClick URL when it appears below a portal/tab folder, e.g.
        /// <c>/Home/LinkClick.aspx?fileticket=...</c>. The IIS handler mapping matched the file
        /// name at any folder depth, so both shapes are routed.
        /// </summary>
        public const string NestedLinkClickPath = "/{**dnnLinkClickPrefix}/LinkClick.aspx";

        private const int StreamBufferSize = 64 * 1024;

        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(FileServerHandler));

        /// <summary>Maps the file serving endpoint onto its original URL(s).</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        public static IEndpointRouteBuilder MapDnnFileServer(this IEndpointRouteBuilder endpoints)
        {
            // Map (rather than MapGet): the handler was registered with verb="*". It must stay
            // anonymous — the handler itself redirects to the Access Denied page when the current
            // (possibly anonymous) user may not see the file.
            endpoints.Map(LinkClickPath, ProcessRequestAsync);
            endpoints.Map(NestedLinkClickPath, ProcessRequestAsync);
            return endpoints;
        }

        /// <summary>
        /// This handles requests for LinkClick.aspx, but only those specific to file serving.
        /// </summary>
        /// <param name="context">The HTTP context of the request.</param>
        /// <returns>A task which completes once the file (or redirect) has been written.</returns>
        public static async Task ProcessRequestAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var request = context.Request;
            var rawUrl = request.GetRawUrl();
            var portalSettings = PortalController.Instance.GetCurrentSettings();
            var tabId = -1;
            var moduleId = -1;
            try
            {
                // get TabId
                if (request.Query.TryGetValue("tabid", out var rawTabId))
                {
                    if (!int.TryParse(rawTabId.ToString(), out tabId))
                    {
                        tabId = -1;
                    }
                }

                // get ModuleId
                if (request.Query.TryGetValue("mid", out var rawModuleId))
                {
                    if (!int.TryParse(rawModuleId.ToString(), out moduleId))
                    {
                        moduleId = -1;
                    }
                }
            }
            catch (Exception)
            {
                // The TabId or ModuleId are incorrectly formatted (potential DOS)
                await Handle404ExceptionAsync(context, rawUrl).ConfigureAwait(false);
            }

            // get Language
            string language = portalSettings.DefaultLanguage;
            if (request.Query.TryGetValue("language", out var rawLanguage))
            {
                language = rawLanguage.ToString();
            }
            else
            {
                // Core cookies are plain strings, there is no HttpCookie.Value.
                if (request.Cookies.TryGetValue("language", out var languageCookie))
                {
                    language = languageCookie;
                }
            }

            if (LocaleController.Instance.IsEnabled(ref language, portalSettings.PortalId))
            {
                Localization.SetThreadCultures(new CultureInfo(language), portalSettings);
                Localization.SetLanguage(language);
            }

            // get the URL
            string url = string.Empty;
            if (request.Query.ContainsKey("fileticket"))
            {
                url = "FileID=" + FileLinkClickController.Instance.GetFileIdFromLinkClick(request.Query.ToNameValueCollection());
            }

            if (request.Query.TryGetValue("userticket", out var userTicket))
            {
                url = "UserId=" + UrlUtils.DecryptParameter(userTicket.ToString());
            }

            if (request.Query.TryGetValue("link", out var rawLink))
            {
                url = rawLink.ToString();
                if (url.StartsWith("fileid=", StringComparison.InvariantCultureIgnoreCase))
                {
                    url = string.Empty; // restrict direct access by FileID
                }
            }

            if (string.IsNullOrEmpty(url))
            {
                await Handle404ExceptionAsync(context, url).ConfigureAwait(false);
                return;
            }

            url = url.Replace(@"\", @"/");

            // update clicks, this must be done first, because the url tracker works with unmodified urls, like tabid, fileid etc
            var objUrls = new UrlController();
            objUrls.UpdateUrlTracking(portalSettings.PortalId, url, moduleId, -1);
            TabType urlType = Globals.GetURLType(url);
            if (urlType == TabType.Tab)
            {
                // verify whether the tab exists, otherwise throw out 404.
                if (TabController.Instance.GetTab(int.Parse(url, CultureInfo.InvariantCulture), portalSettings.PortalId, false) == null)
                {
                    await Handle404ExceptionAsync(context, rawUrl).ConfigureAwait(false);
                    return;
                }
            }

            if (urlType != TabType.File)
            {
                url = Globals.LinkClick(url, tabId, moduleId, false);
            }

            if (urlType == TabType.File && !url.StartsWith("fileid=", StringComparison.OrdinalIgnoreCase))
            {
                // to handle legacy scenarios before the introduction of the FileServerHandler
                var legacyFileName = Path.GetFileName(url);

                var folderPath = url.Substring(0, url.LastIndexOf(legacyFileName, StringComparison.InvariantCulture));
                var legacyFolder = FolderManager.Instance.GetFolder(portalSettings.PortalId, folderPath);

                var legacyFile = FileManager.Instance.GetFile(legacyFolder, legacyFileName);

                url = "FileID=" + legacyFile.FileId;
            }

            // get optional parameters
            bool blnForceDownload = false;
            if (request.Query.ContainsKey("forcedownload") || request.Query.ContainsKey("contenttype"))
            {
                if (!bool.TryParse(request.Query["forcedownload"].ToString(), out blnForceDownload))
                {
                    blnForceDownload = false;
                }
            }

            var contentDisposition = blnForceDownload ? ContentDisposition.Attachment : ContentDisposition.Inline;

            // The old handler called Response.Clear() here. In ASP.NET Core nothing has been
            // written to the response yet at this point, so there is nothing to clear.
            var fileManager = FileManager.Instance;
            try
            {
                switch (urlType)
                {
                    case TabType.File:
                        var download = false;
                        var file = fileManager.GetFile(int.Parse(UrlUtils.GetParameterValue(url), CultureInfo.InvariantCulture));
                        if (file != null)
                        {
                            if (!file.IsEnabled || !HasAPublishedVersion(file))
                            {
                                RedirectToAccessDenied(context);
                                return;
                            }

                            try
                            {
                                var folderMapping = FolderMappingController.Instance.GetFolderMapping(file.PortalId, file.FolderMappingID);
                                var directUrl = fileManager.GetUrl(file);

                                EventManager.Instance.OnFileDownloaded(new FileDownloadedEventArgs()
                                {
                                    FileInfo = file,
                                    UserId = UserController.Instance.GetCurrentUserInfo().UserID,
                                });

                                if (directUrl.Contains("LinkClick") || (blnForceDownload && folderMapping.FolderProviderType == "StandardFolderProvider"))
                                {
                                    await WriteFileToResponseAsync(context, file, contentDisposition).ConfigureAwait(false);
                                    download = true;
                                }
                                else
                                {
                                    context.Response.Redirect(directUrl);
                                    return;
                                }
                            }
                            catch (PermissionsNotMetException)
                            {
                                RedirectToAccessDenied(context);
                                return;
                            }
                            catch (Exception ex)
                            {
                                Logger.FileServerHandlerHandleFileLinkException(ex);
                            }
                        }

                        if (!download)
                        {
                            await Handle404ExceptionAsync(context, url).ConfigureAwait(false);
                        }

                        break;
                    case TabType.Url:
                        // prevent phishing by verifying that URL exists in URLs table for Portal
                        if (objUrls.GetUrl(portalSettings.PortalId, url) != null)
                        {
                            context.Response.Redirect(url);
                        }

                        break;
                    default:
                        // redirect to URL
                        context.Response.Redirect(url);
                        break;
                }
            }
            catch (Exception)
            {
                await Handle404ExceptionAsync(context, url).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Writes the file to the response, honouring an HTTP <c>Range</c> request so downloads
        /// stay resumable.
        /// </summary>
        /// <param name="context">The HTTP context of the request.</param>
        /// <param name="file">The file to serve.</param>
        /// <param name="contentDisposition">Whether the file is served inline or as an attachment.</param>
        /// <returns>A task which completes once the response body has been written.</returns>
        /// <exception cref="PermissionsNotMetException">Thrown when the current user may not view the file's folder.</exception>
        /// <remarks>
        /// This replaces <c>IFileManager.WriteFileToResponse</c>, which wrote synchronously to
        /// <c>HttpContext.Current.Response</c> and ended the request. The permission gate and the
        /// file auto-sync it performed are reproduced here, so behaviour (including the
        /// <see cref="PermissionsNotMetException"/> the caller turns into an Access Denied
        /// redirect) is unchanged; the headers are the ones the old handler appended.
        /// </remarks>
        private static async Task WriteFileToResponseAsync(HttpContext context, IFileInfo file, ContentDisposition contentDisposition)
        {
            var folder = FolderManager.Instance.GetFolder(file.FolderId);

            if (!FolderPermissionController.Instance.CanViewFolder(folder))
            {
                throw new PermissionsNotMetException(Localization.GetExceptionMessage("WriteFileToResponsePermissionsNotMet", "Permissions are not met. The file cannot be downloaded."));
            }

            // Reproduce the auto-sync FileManager.WriteFileToResponse performed before serving.
            if (FileManager.Instance is FileManager concreteFileManager && concreteFileManager.IsFileAutoSyncEnabled())
            {
                concreteFileManager.AutoSyncFile(file);
            }

            var response = context.Response;
            var dispositionType = contentDisposition switch
            {
                ContentDisposition.Attachment => "attachment",
                ContentDisposition.Inline => "inline",
                _ => throw new ArgumentOutOfRangeException(nameof(contentDisposition)),
            };

            response.Headers[HeaderNames.ContentDisposition] = $"{dispositionType}; filename=\"{file.FileName}\"";
            response.ContentType = FileContentTypeManager.Instance.GetContentType(file.Extension.Replace(".", string.Empty));

            // Resumable downloads: advertise range support, and serve a single requested range.
            response.Headers[HeaderNames.AcceptRanges] = "bytes";

            using var fileContent = FileManager.Instance.GetFileContent(file);

            // file.Size can be negative through integer overflow for files > 2GB, in which case
            // the old handler sent no Content-Length; without a length no range can be computed.
            long? totalLength = file.Size >= 0 ? file.Size : null;
            if (totalLength is null && fileContent.CanSeek)
            {
                totalLength = fileContent.Length;
            }

            var range = ParseSingleRange(context.Request.Headers[HeaderNames.Range].ToString(), totalLength);
            if (range is { Satisfiable: false })
            {
                response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                response.Headers[HeaderNames.ContentRange] = $"bytes */{totalLength}";
                return;
            }

            if (range is { Satisfiable: true } satisfiable && fileContent.CanSeek)
            {
                var length = (satisfiable.End - satisfiable.Start) + 1;
                response.StatusCode = StatusCodes.Status206PartialContent;
                response.Headers[HeaderNames.ContentRange] = $"bytes {satisfiable.Start}-{satisfiable.End}/{totalLength}";
                response.ContentLength = length;

                fileContent.Seek(satisfiable.Start, SeekOrigin.Begin);
                await CopyRangeAsync(fileContent, response.Body, length, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            // No (usable) range: send the whole file, as the old handler always did.
            if (totalLength is not null)
            {
                response.ContentLength = totalLength;
            }

            await fileContent.CopyToAsync(response.Body, StreamBufferSize, context.RequestAborted).ConfigureAwait(false);
        }

        private static async Task CopyRangeAsync(Stream source, Stream destination, long count, System.Threading.CancellationToken cancellationToken)
        {
            var buffer = new byte[StreamBufferSize];
            var remaining = count;
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(buffer.Length, remaining);
                var read = await source.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                remaining -= read;
            }
        }

        /// <summary>Parses a single-range <c>Range: bytes=…</c> header value.</summary>
        /// <param name="rangeHeader">The raw header value, which may be empty.</param>
        /// <param name="totalLength">The total length of the file, when known.</param>
        /// <returns>
        /// <see langword="null"/> when there is no range to honour (no header, an unsupported
        /// multi-range request, or an unknown file length — in which case the whole file is sent,
        /// exactly as before). Otherwise the resolved range, with
        /// <see cref="RequestedRange.Satisfiable"/> indicating whether it fits the file.
        /// </returns>
        private static RequestedRange? ParseSingleRange(string rangeHeader, long? totalLength)
        {
            if (string.IsNullOrWhiteSpace(rangeHeader) || totalLength is null || totalLength <= 0)
            {
                return null;
            }

            const string bytesUnit = "bytes=";
            if (!rangeHeader.StartsWith(bytesUnit, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var spec = rangeHeader.Substring(bytesUnit.Length).Trim();
            if (spec.Contains(','))
            {
                // Multiple ranges would need a multipart/byteranges body; serve the whole file.
                return null;
            }

            var separator = spec.IndexOf('-');
            if (separator < 0)
            {
                return null;
            }

            var total = totalLength.Value;
            var firstPart = spec.Substring(0, separator).Trim();
            var lastPart = spec.Substring(separator + 1).Trim();

            long start;
            long end;
            if (firstPart.Length == 0)
            {
                // "-N": the last N bytes.
                if (!long.TryParse(lastPart, NumberStyles.None, CultureInfo.InvariantCulture, out var suffixLength) || suffixLength == 0)
                {
                    return new RequestedRange(0, 0, false);
                }

                start = Math.Max(0, total - suffixLength);
                end = total - 1;
            }
            else
            {
                if (!long.TryParse(firstPart, NumberStyles.None, CultureInfo.InvariantCulture, out start))
                {
                    return new RequestedRange(0, 0, false);
                }

                if (lastPart.Length == 0)
                {
                    end = total - 1;
                }
                else if (!long.TryParse(lastPart, NumberStyles.None, CultureInfo.InvariantCulture, out end))
                {
                    return new RequestedRange(0, 0, false);
                }

                if (end > total - 1)
                {
                    end = total - 1;
                }
            }

            if (start > end || start >= total)
            {
                return new RequestedRange(0, 0, false);
            }

            return new RequestedRange(start, end, true);
        }

        private static void RedirectToAccessDenied(HttpContext context)
        {
            context.Response.Redirect(context.Request.IsAuthenticated()
                ? Globals.AccessDeniedURL(Localization.GetString("FileAccess.Error"))
                : Globals.AccessDeniedURL());
        }

        private static bool HasAPublishedVersion(IFileInfo file)
        {
            if (file.HasBeenPublished)
            {
                return true;
            }

            // We should allow creator to see the file that is pending to be approved
            var user = UserController.Instance.GetCurrentUserInfo();
            return user != null && user.UserID == file.CreatedByUserID;
        }

        private static async Task Handle404ExceptionAsync(HttpContext context, string url)
        {
            try
            {
                Exceptions.Exceptions.ProcessHttpException(url);
            }
            catch (Exception)
            {
                await UrlUtils.Handle404ExceptionAsync(context.Response, PortalSettings.Current).ConfigureAwait(false);
            }
        }

        /// <summary>A resolved byte range of the requested file.</summary>
        /// <param name="Start">The first byte to send.</param>
        /// <param name="End">The last byte to send (inclusive).</param>
        /// <param name="Satisfiable">Whether the requested range fits within the file.</param>
        private readonly record struct RequestedRange(long Start, long End, bool Satisfiable);
    }
}
