// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Extensions.Services
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    using Dnn.PersonaBar.Extensions.Services.Dto;
    using Dnn.PersonaBar.Library;
    using Dnn.PersonaBar.Library.Attributes;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Installer;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Api;
    using DotNetNuke.Web.Api.Internal;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    [MenuPermission(Scope = ServiceScope.Host)]
    public class UpgradesController : PersonaBarApiController
    {
        private const string ResourceFile = "~/DesktopModules/Admin/Dnn.PersonaBar/Modules/Dnn.Servers/App_LocalResources/Servers.resx";
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<UpgradesController>();
        private readonly IApplicationStatusInfo applicationStatusInfo;
        private readonly ILocalUpgradeService localUpgradeService;

        /// <summary>Initializes a new instance of the <see cref="UpgradesController"/> class.</summary>
        /// <param name="applicationStatusInfo">The application status.</param>
        /// <param name="localUpgradeService">The local upgrade service.</param>
        public UpgradesController(IApplicationStatusInfo applicationStatusInfo, ILocalUpgradeService localUpgradeService)
        {
            this.applicationStatusInfo = applicationStatusInfo;
            this.localUpgradeService = localUpgradeService;
        }

        /// <summary>Retrieves upgrade settings.</summary>
        /// <returns>An <see cref="IActionResult"/> containing the upgrade settings with a status code
        /// of <see cref="HttpStatusCode.OK"/> if successful, or an error message with a status code of
        /// <see cref="HttpStatusCode.InternalServerError"/> if an exception occurs.</returns>
        [HttpGet]
        public IActionResult GetSettings()
        {
            try
            {
                if (!bool.TryParse(DotNetNuke.Common.Utilities.Config.GetSetting("AllowDnnUpgradeUpload"), out var allowDnnUpgradeUpload))
                {
                    allowDnnUpgradeUpload = false;
                }

                return this.Ok(new
                {
                    AllowDnnUpgradeUpload = allowDnnUpgradeUpload,
                });
            }
            catch (Exception ex)
            {
                return DnnApiResults.Response(HttpStatusCode.InternalServerError, new { ex.Message, });
            }
        }

        /// <summary>Retrieves a list of local upgrades.</summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An <see cref="IActionResult"/> containing the list of local upgrades with a status code
        /// of <see cref="HttpStatusCode.OK"/> if successful, or an error message with a status code of
        /// <see cref="HttpStatusCode.InternalServerError"/> if an exception occurs.</returns>
        [HttpGet]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            try
            {
                var upgrades = await this.localUpgradeService.GetLocalUpgrades(cancellationToken);

                return this.Ok(upgrades);
            }
            catch (Exception ex)
            {
                return DnnApiResults.Response(HttpStatusCode.InternalServerError, new { Message = ex.Message });
            }
        }

        /// <summary>Starts a local upgrade.</summary>
        /// <param name="data">The DTO containing the packageName.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An empty success response.</returns>
        [HttpPost]
        public async Task<IActionResult> StartUpgrade(UpgradePackageRequestDto data, CancellationToken cancellationToken)
        {
            var upgrades = await this.localUpgradeService.GetLocalUpgrades(cancellationToken);
            if (!upgrades.Any(u => u.IsValid && !u.IsOutdated))
            {
                return DnnApiResults.Response(HttpStatusCode.BadRequest, new { message = LocalizeString("Upgrade_NoUpgrade"), });
            }

            var upgrade = upgrades.FirstOrDefault(u => u.PackageName.Equals(data.PackageName, StringComparison.OrdinalIgnoreCase));
            if (upgrade is not { CanInstall: true, })
            {
                return DnnApiResults.Response(HttpStatusCode.BadRequest, new { message = LocalizeString($"Upgrade_NoValidUpgrade", data.PackageName), });
            }

            await this.localUpgradeService.StartLocalUpgrade(upgrade, cancellationToken);

            await this.localUpgradeService.DeleteLocalUpgrade(data.PackageName, cancellationToken);

            return DnnApiResults.Response(HttpStatusCode.NoContent);
        }

        /// <summary>Starts a local upgrade.</summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An empty success response.</returns>
        [HttpPost]
        public async Task<IActionResult> StartFirstUpgrade(CancellationToken cancellationToken)
        {
            var upgrades = await this.localUpgradeService.GetLocalUpgrades(cancellationToken);
            if (!upgrades.Any(u => u.IsValid && !u.IsOutdated))
            {
                return DnnApiResults.Response(HttpStatusCode.BadRequest, new { message = LocalizeString("Upgrade_NoUpgrade"), });
            }

            await this.localUpgradeService.StartLocalUpgrade(upgrades, cancellationToken);

            return DnnApiResults.Response(HttpStatusCode.NoContent);
        }

        /// <summary>Deletes a local upgrade package.</summary>
        /// <param name="data">The DTO containing the packageName.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An empty success response.</returns>
        [HttpPost]
        public async Task<IActionResult> Delete(UpgradePackageRequestDto data, CancellationToken cancellationToken)
        {
            try
            {
                await this.localUpgradeService.DeleteLocalUpgrade(data.PackageName, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.UpgradesControllerDeleteException(ex);
                return DnnApiResults.Response(HttpStatusCode.InternalServerError, new { message = ex.Message, });
            }

            return DnnApiResults.Response(HttpStatusCode.NoContent);
        }

        /// <summary>Uploads a DNN package for local upgrade.</summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Either a package info or an error message.</returns>
        [HttpPost]
        [IFrameSupportedValidateAntiForgeryToken]
        public Task<IActionResult> Upload(CancellationToken cancellationToken)
        {
            try
            {
                if (!bool.TryParse(DotNetNuke.Common.Utilities.Config.GetSetting("AllowDnnUpgradeUpload"), out var allowDnnUpgradeUpload))
                {
                    allowDnnUpgradeUpload = false;
                }

                if (!allowDnnUpgradeUpload)
                {
                    return Task.FromResult(DnnApiResults.Response(HttpStatusCode.Forbidden, new { message = LocalizeString("Upgrade_UploadNotAllowed"), }));
                }

                return this.UploadFileAction(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.UpgradesControllerUploadException(ex);
                return Task.FromResult(DnnApiResults.Response(HttpStatusCode.InternalServerError, new { message = ex.Message, }));
            }
        }

        /// <summary>
        /// Completes the uploads of a DNN package for local upgrade.
        /// </summary>
        /// <param name="data">The upload complete data.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Either a package info or an error message.</returns>
        [HttpPost]
        [IFrameSupportedValidateAntiForgeryToken]
        public async Task<IActionResult> UploadComplete(UpgradeUploadCompleteDto data, CancellationToken cancellationToken)
        {
            var fileName = Path.GetFileName(data.FileName);
            var fileId = Path.GetFileName(data.FileId);

            if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(fileId))
            {
                var writtenFile = Path.Combine(this.applicationStatusInfo.ApplicationMapPath, "App_Data", "Upgrade", fileId + ".resources");
                if (!System.IO.File.Exists(writtenFile))
                {
                    return DnnApiResults.Response(HttpStatusCode.BadRequest, new { message = LocalizeString($"Upgrade_InvalidPackage", fileName), });
                }

                var info = await this.localUpgradeService.GetLocalUpgradeInfo(writtenFile, cancellationToken);
                if (!info.IsValid)
                {
                    return DnnApiResults.Response(HttpStatusCode.BadRequest, new { message = LocalizeString($"Upgrade_InvalidPackage", fileName), });
                }
                else if (info.IsOutdated)
                {
                    return DnnApiResults.Response(HttpStatusCode.BadRequest, new { message = LocalizeString($"Upgrade_OutdatedPackage", fileName), });
                }
                else
                {
                    var upgradePath = Path.Combine(this.applicationStatusInfo.ApplicationMapPath, "App_Data", "Upgrade", Path.GetFileNameWithoutExtension(fileName) + ".zip");
                    System.IO.File.Move(writtenFile, upgradePath);
                    return this.Ok(fileName);
                }
            }

            return DnnApiResults.Response(HttpStatusCode.BadRequest, new { message = LocalizeString($"Upgrade_InvalidPackage", fileName), });
        }

        private static string LocalizeString(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, Localization.GetString(key, ResourceFile), args);
        }

        private async Task<IActionResult> UploadFileAction(CancellationToken cancellationToken)
        {
            var request = this.Request;
            if (!request.HasFormContentType ||
                !Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType) ||
                !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                return DnnApiResults.Response(HttpStatusCode.UnsupportedMediaType);
            }

            // Reading the form materializes the whole multipart body, which is what the legacy
            // Content.LoadIntoBufferAsync()/ReadAsMultipartAsync() pair did.
            var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);

            long startPosition = 0;
            long totalSize = 0;
            string fileId = string.Empty;
            Stream stream = null;

            foreach (var file in form.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (file.Name.ToUpperInvariant() == "CHUNK")
                {
                    stream = file.OpenReadStream();
                }
            }

            foreach (var field in form)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (field.Key.ToUpperInvariant())
                {
                    case "START":
                        if (!long.TryParse(field.Value.ToString(), out startPosition))
                        {
                            startPosition = 0;
                        }

                        break;
                    case "TOTALSIZE":
                        if (!long.TryParse(field.Value.ToString(), out totalSize))
                        {
                            totalSize = 0;
                        }

                        break;
                    case "FILEID":
                        fileId = field.Value.ToString();
                        break;
                }
            }

            if (stream != null && totalSize != 0 && !string.IsNullOrEmpty(fileId))
            {
                var fileToWriteTo = Path.Combine(this.applicationStatusInfo.ApplicationMapPath, "App_Data", "Upgrade", fileId + ".resources");

                // use the file stream and start position to write the chunk to the file
                using var fileStream = new FileStream(fileToWriteTo, FileMode.OpenOrCreate, FileAccess.Write);
                fileStream.Position = startPosition;
                await stream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            }

            return DnnApiResults.Response(HttpStatusCode.OK);
        }
    }
}
