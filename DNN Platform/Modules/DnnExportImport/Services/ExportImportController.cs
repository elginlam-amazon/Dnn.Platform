// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.ExportImport.Services
{
    using System.Linq;
    using System.Net;

    using Dnn.ExportImport.Components.Common;
    using Dnn.ExportImport.Components.Controllers;
    using Dnn.ExportImport.Components.Dto;

    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Localization;

    // DNN's own anti-forgery attribute (the one the services framework validates) is kept, aliased
    // because the Microsoft.AspNetCore.Mvc namespace also defines ValidateAntiForgeryTokenAttribute.
    using ValidateAntiForgeryToken = DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute;

    /// <summary>REST APIs to manage import/export jobs.</summary>
    [RequireHost]
    public class ExportImportController : DnnApiController
    {
        private readonly ExportController exportController;
        private readonly ImportController importController;
        private readonly BaseController baseController;
        private readonly IStringLocalizer<SharedResources> localizer;

        /// <summary>Initializes a new instance of the <see cref="ExportImportController"/> class.</summary>
        /// <param name="exportController">The export controller.</param>
        /// <param name="importController">The import controller.</param>
        /// <param name="baseController">The base controller.</param>
        /// <param name="localizer">The localizer for this module's shared resources.</param>
        public ExportImportController(
            ExportController exportController,
            ImportController importController,
            BaseController baseController,
            IStringLocalizer<SharedResources> localizer)
        {
            this.exportController = exportController;
            this.importController = importController;
            this.baseController = baseController;
            this.localizer = localizer;
        }

        /// <summary>Exports the specified site.</summary>
        /// <param name="exportDto">The details needed to export a site.</param>
        /// <returns>OK.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Export(ExportDto exportDto)
        {
            var jobId = this.exportController.QueueOperation(this.PortalSettings.UserId, exportDto);

            return this.Ok(new { jobId });
        }

        /// <summary>Imports a site.</summary>
        /// <param name="importDto">The details required to import a site.</param>
        /// <returns>OK or BadRequest with a message.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Import(ImportDto importDto)
        {
            if (this.importController.VerifyImportPackage(importDto.PackageId, null, out var message))
            {
                var jobId = this.importController.QueueOperation(this.PortalSettings.UserId, importDto);
                return this.Ok(new { jobId });
            }

            return DnnApiResults.Error(HttpStatusCode.BadRequest, message);
        }

        /// <summary>Verifies an import package validity.</summary>
        /// <param name="packageId">The ID of the package to validate.</param>
        /// <returns>OK with a summary or BadRequest with an error message.</returns>
        [HttpGet]
        public IActionResult VerifyImportPackage(string packageId)
        {
            var summary = new ImportExportSummary();
            var isValid = this.importController.VerifyImportPackage(packageId, summary, out var message);
            summary.ConvertToLocal(this.UserInfo);
            return isValid
                ? this.Ok(summary)
                : DnnApiResults.Error(HttpStatusCode.BadRequest, message);
        }

        /// <summary>Get list of packages to import.</summary>
        /// <param name="keyword">Keyword to search the import package. This will look into the package name and description.</param>
        /// <param name="order">Order by which the packages list should be sorted. Allowed values: newest, oldest, name.</param>
        /// <param name="pageIndex">Page index to get.</param>
        /// <param name="pageSize">Page size. Should not be more than 100.</param>
        /// <returns>An <see cref="IActionResult"/>.</returns>
        [HttpGet]
        public IActionResult GetImportPackages(string keyword = "", string order = "newest", int pageIndex = 0, int pageSize = 10)
        {
            var packages = this.importController.GetImportPackages(out var total, keyword, order, pageIndex, pageSize).ToList();
            packages.ForEach(package => package.ConvertToLocal(this.UserInfo));
            return this.Ok(new { packages, total });
        }

        /// <summary>
        /// Cancels a job.
        /// </summary>
        /// <param name="jobId">The ID of the job to cancel.</param>
        /// <remarks>This is a POST so users can't cancel using a simple browser link.</remarks>
        /// <returns>OK or a BadRequest with a cancel status value.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelProcess([FromQuery] int jobId)
        {
            var cancelStatus = this.baseController.CancelJob(this.UserInfo.IsSuperUser ? -1 : this.PortalSettings.PortalId, jobId);
            return DnnApiResults.Response(
                cancelStatus ? HttpStatusCode.OK : HttpStatusCode.BadRequest, new { success = cancelStatus });
        }

        /// <summary>
        /// Removes a job.
        /// </summary>
        /// <param name="jobId">The ID of the job to remove.</param>
        /// <remarks>This is a POST so users can't remove a job using a browser link.</remarks>
        /// <returns>OK or a BadRequest with a status value.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveJob([FromQuery] int jobId)
        {
            var cancelStatus = this.baseController.RemoveJob(this.UserInfo.IsSuperUser ? -1 : this.PortalSettings.PortalId, jobId);
            return DnnApiResults.Response(
                cancelStatus ? HttpStatusCode.OK : HttpStatusCode.BadRequest, new { success = cancelStatus });
        }

        /// <summary>
        /// Gets the last job time.
        /// </summary>
        /// <param name="portal">The ID of the portal to check.</param>
        /// <param name="jobType">Type of the job.</param>
        /// <returns>OK with the last time or a BadRequest with a message.</returns>
        [HttpGet]
        public IActionResult LastJobTime(int portal, JobType jobType)
        {
            if (!this.UserInfo.IsSuperUser && portal != this.PortalSettings.PortalId)
            {
                var error = this.GetLocalizedString("NotPortalAdmin.Text");
                return DnnApiResults.Error(HttpStatusCode.BadRequest, error);
            }

            if (portal < 0)
            {
                return DnnApiResults.Error(
                    HttpStatusCode.BadRequest,
                    this.GetLocalizedString("InvalidPortal.Text"));
            }

            var lastTime = this.baseController.GetLastJobTime(portal, jobType);
            return this.Ok(new { lastTime = Util.GetDateTimeString(lastTime) });
        }

        /// <summary>
        /// Gets all the jobs.
        /// </summary>
        /// <param name="portal">The ID of the portal to check.</param>
        /// <param name="pageSize">Size of the page.</param>
        /// <param name="pageIndex">Index of the page.</param>
        /// <param name="jobType">Type of the job.</param>
        /// <param name="keywords">The keywords to search for.</param>
        /// <returns>OK with a list of jobs or a BadRequest with an error message.</returns>
        [HttpGet]
        public IActionResult AllJobs(int portal, int? pageSize = 10, int? pageIndex = 0, int? jobType = null, string keywords = null)
        {
            if (!this.UserInfo.IsSuperUser && portal != this.PortalSettings.PortalId)
            {
                var error = this.GetLocalizedString("NotPortalAdmin.Text");
                return DnnApiResults.Error(HttpStatusCode.BadRequest, error);
            }

            var jobs = this.baseController.GetAllJobs(portal, this.PortalSettings.PortalId, pageSize, pageIndex, jobType, keywords);
            jobs?.ConvertToLocal(this.UserInfo);
            return DnnApiResults.Response(HttpStatusCode.OK, jobs);
        }

        /// <summary>
        /// Jobs the details about a single job.
        /// </summary>
        /// <param name="jobId">The ID of the job to get.</param>
        /// <returns>OK with the job details or BadRequest with an error message.</returns>
        [HttpGet]
        public IActionResult JobDetails(int jobId)
        {
            var job = this.baseController.GetJobDetails(this.UserInfo.IsSuperUser ? -1 : this.PortalSettings.PortalId, jobId);
            job?.ConvertToLocal(this.UserInfo);
            return job != null
                ? this.Ok(job)
                : DnnApiResults.Response(
                    HttpStatusCode.BadRequest,
                    new { message = this.GetLocalizedString("JobNotExist.Text") });
        }

        /// <summary>Reads a localized string, reproducing the legacy
        /// <c>Localization.GetString</c> behavior of returning an empty string (rather than the key)
        /// when the resource key does not exist.</summary>
        /// <param name="key">The resource key, including the DNN <c>.Text</c> suffix.</param>
        /// <returns>The localized value, or <see cref="string.Empty"/> when the key is not found.</returns>
        private string GetLocalizedString(string key)
        {
            var value = this.localizer[key];
            return value.ResourceNotFound ? string.Empty : value.Value;
        }
    }
}
