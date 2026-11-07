// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.CoreMessaging.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;

    using DotNetNuke.Common;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Web.Api;
    using DotNetNuke.Web.Api.Internal;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json;

    /// <summary>Provides a file upload web service.</summary>
    public class FileUploadController : DnnApiController
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<FileUploadController>();
        private readonly IFileManager fileManager = FileManager.Instance;
        private readonly IFolderManager folderManager = FolderManager.Instance;
        private readonly IFileContentTypeManager fileContentTypeManager = FileContentTypeManager.Instance;

        /// <summary>Attempts to upload a file.</summary>
        /// <returns>A collection of <see cref="FilesStatus"/>.</returns>
        [DnnAuthorize]
        [HttpPost]
        [IFrameSupportedValidateAntiForgeryToken]
        public IActionResult UploadFile()
        {
            var statuses = new List<FilesStatus>();
            try
            {
                this.UploadWholeFile(this.HttpContext, statuses);
            }
            catch (Exception exc)
            {
                Logger.FileUploadControllerUploadFileException(exc);
            }

            return IframeSafeJson(statuses);
        }

        /// <summary>
        /// Writes the statuses as JSON but labels the response as plain text, which is what the legacy
        /// <c>StringContent</c> (default <c>text/plain</c>) media type did. The iframe upload transport
        /// used by the client depends on that content type, so it must not become <c>application/json</c>.
        /// </summary>
        /// <param name="statuses">The file statuses to serialize.</param>
        /// <returns>A <see cref="ContentResult"/>.</returns>
        private static ContentResult IframeSafeJson(List<FilesStatus> statuses)
        {
            // return json but label it as plain text
            return new ContentResult
            {
                Content = JsonConvert.SerializeObject(statuses),
                ContentType = "text/plain; charset=utf-8",
                StatusCode = (int)HttpStatusCode.OK,
            };
        }

        // Upload entire file
        private void UploadWholeFile(HttpContext context, ICollection<FilesStatus> statuses)
        {
            var files = context.Request.Form.Files;
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (file == null)
                {
                    continue;
                }

                var fileName = Path.GetFileName(file.FileName);

                try
                {
                    var userFolder = this.folderManager.GetUserFolder(this.UserInfo);

                    // todo: deal with the case where the exact file name already exists.
                    using var stream = file.OpenReadStream();
                    var fileInfo = this.fileManager.AddFile(userFolder, fileName, stream, true, true, this.fileContentTypeManager.GetContentType(Path.GetExtension(fileName)));
                    var fileIcon = Entities.Icons.IconController.IconURL("Ext" + fileInfo.Extension, "32x32");
                    // Fully qualified: the inherited MVC ControllerBase.File(...) helper otherwise
                    // shadows the System.IO.File type this check has always used.
                    if (!System.IO.File.Exists(HttpContextSource.MapPath(fileIcon)))
                    {
                        fileIcon = Entities.Icons.IconController.IconURL("File", "32x32");
                    }

                    statuses.Add(new FilesStatus
                    {
                        success = true,
                        name = fileName,
                        extension = fileInfo.Extension,
                        type = fileInfo.ContentType,
                        size = (int)file.Length,
                        progress = "1.0",
                        url = this.fileManager.GetUrl(fileInfo),
                        thumbnail_url = fileIcon,
                        message = "success",
                        id = fileInfo.FileId,
                    });
                }
                catch (InvalidFileExtensionException)
                {
                    statuses.Add(new FilesStatus
                    {
                        success = false,
                        name = fileName,
                        message = "File type not allowed.",
                    });
                }
            }
        }
    }
}
