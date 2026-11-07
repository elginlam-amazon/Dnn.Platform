// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Extensions.Components.BulkInstall.WebAPI
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;

    using Dnn.PersonaBar.Extensions.Components.BulkInstall;
    using Dnn.PersonaBar.Extensions.Components.BulkInstall.DataAccess.Models;
    using Dnn.PersonaBar.Extensions.Components.BulkInstall.Logging;
    using Dnn.PersonaBar.Extensions.Components.BulkInstall.WebAPI.ActionFilters;
    using Dnn.PersonaBar.Library;
    using Dnn.PersonaBar.Library.Attributes;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;

    using ValidateAntiForgeryToken = DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute;

    /// <summary>A web API controller for <see cref="Session"/>.</summary>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="eventLogManager">The event log manager.</param>
    /// <param name="appStatus">The application status.</param>
    /// <param name="serviceProvider">The DI container.</param>
    [RequireHost]
    [ValidateAntiForgeryToken]
    [InBulkInstallWhitelist]
    [MenuPermission(Scope = ServiceScope.Host)]
    public class BulkInstallSessionController(SessionManager sessionManager, EventLogManager eventLogManager, IApplicationStatusInfo appStatus, IServiceProvider serviceProvider) : PersonaBarApiController
    {
        private readonly SessionManager sessionManager = sessionManager;
        private readonly EventLogManager eventLogManager = eventLogManager;
        private readonly IApplicationStatusInfo appStatus = appStatus;
        private readonly IServiceProvider serviceProvider = serviceProvider;

        /// <summary>Create a new session.</summary>
        /// <returns>A result with the <see cref="Session"/>.</returns>
        [HttpPost]
        public IActionResult Create()
        {
            var session = this.sessionManager.CreateSession();

            var responseBody = new
            {
                Session = new SessionDto(session),
                MaxUploadFileSize = Config.GetMaxUploadSize(this.appStatus),
            };
            return this.Ok(responseBody);
        }

        /// <summary>Gets a session by its <paramref name="sessionGuid"/>.</summary>
        /// <param name="sessionGuid">The public identifier for the <see cref="Session"/>.</param>
        /// <returns>A result with either the <see cref="Session"/> or <see langword="null"/>.</returns>
        [HttpGet]
        public IActionResult Get(string sessionGuid)
        {
            var session = this.sessionManager.GetSession(sessionGuid);

            return this.Ok(new { Session = new SessionDto(session), });
        }

        /// <summary>Adds package files to the session.</summary>
        /// <param name="sessionGuid">The public identifier for the <see cref="Session"/>.</param>
        /// <returns>A result indicating success, or <c>415 Unsupported Media Type</c> when the request is not a multipart MIME type.</returns>
        [HttpPost]
        public async Task<IActionResult> AddPackages(string sessionGuid)
        {
            if (!this.sessionManager.SessionExists(sessionGuid))
            {
                // Session doesn't exist.
                return DnnApiResults.Error(HttpStatusCode.NotFound, "Invalid session.");
            }

            try
            {
                // Does the request contain multipart/form-data?
                if (!this.Request.HasFormContentType ||
                    !Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(this.Request.ContentType, out var mediaType) ||
                    !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                {
                    this.eventLogManager.Log("SESSION_EXCEPTION", EventLogSeverity.Warning, "Request was not multipart/form-data.");
                    return DnnApiResults.Response(HttpStatusCode.UnsupportedMediaType);
                }

                // Receive files.
                var form = await this.Request.ReadFormAsync();

                foreach (var file in form.Files)
                {
                    string filename = file.FileName.Replace("\"", string.Empty);
                    if (!string.Equals(Path.GetExtension(filename), ".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        this.eventLogManager.Log("INVALID_PACKAGE", EventLogSeverity.Warning, $"Attempted to upload {filename}, only zip archives are allowed");
                        return DnnApiResults.Error(HttpStatusCode.BadRequest, "Package files must be zip archives");
                    }

                    using MemoryStream ms = new MemoryStream();
                    using (var uploadStream = file.OpenReadStream())
                    {
                        await uploadStream.CopyToAsync(ms);
                    }

                    ms.Position = 0;
                    this.sessionManager.AddPackage(sessionGuid, ms, filename);
                }
            }
            catch (Exception ex)
            {
                this.eventLogManager.Log("SESSION_EXCEPTION", EventLogSeverity.Warning, ex);

                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }

            return DnnApiResults.Response(HttpStatusCode.Created);
        }

        /// <summary>Gets the summary of a session.</summary>
        /// <param name="sessionGuid">The public identifier for the <see cref="Session"/>.</param>
        /// <returns>A result with a sorted list of <see cref="InstallJob"/>.</returns>
        [HttpGet]
        public IActionResult Summary(string sessionGuid)
        {
            if (!this.sessionManager.SessionExists(sessionGuid))
            {
                // Session doesn't exist.
                return DnnApiResults.Error(HttpStatusCode.NotFound, "Invalid session.");
            }

            try
            {
                // Get the users ip address.
                string ipAddress = GetUserHostAddress();

                // Get the session.
                Session sessionObj = this.sessionManager.GetSession(sessionGuid);

                // Create a deploy operation.
                Deployment deployOperation = new Deployment(this.sessionManager, this.eventLogManager, this.appStatus, this.serviceProvider, sessionObj, ipAddress);

                var summary = deployOperation.Summary();

                return this.Ok(new { InstallJobs = summary.Values, });
            }
            catch (Exception ex)
            {
                this.eventLogManager.Log("SESSION_EXCEPTION", EventLogSeverity.Warning, ex);

                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex);
            }
        }

        /// <summary>Starts the installation for the session.</summary>
        /// <param name="sessionGuid">The public identifier for the <see cref="Session"/>.</param>
        /// <returns>A result indicating success.</returns>
        [HttpPost]
        public IActionResult Install(string sessionGuid)
        {
            if (!this.sessionManager.SessionExists(sessionGuid))
            {
                // Session doesn't exist.
                return DnnApiResults.Error(HttpStatusCode.NotFound, "Invalid session.");
            }

            try
            {
                // Get the users ip address.
                string ipAddress = GetUserHostAddress();

                // Get the session.
                Session sessionObj = this.sessionManager.GetSession(sessionGuid);

                // Create a deploy operation.
                Deployment deployOperation = new Deployment(this.sessionManager, this.eventLogManager, this.appStatus, this.serviceProvider, sessionObj, ipAddress);

                deployOperation.Deploy();

                return DnnApiResults.Response(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                this.eventLogManager.Log("SESSION_EXCEPTION", EventLogSeverity.Warning, ex);

                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex);
            }
        }

        /// <summary>
        /// Gets the IP address the current request physically came from, the ASP.NET Core equivalent
        /// of the legacy <c>HttpContext.Current.Request.UserHostAddress</c> (<c>REMOTE_ADDR</c>).
        /// </summary>
        /// <returns>The remote IP address, or <see cref="string.Empty"/> when there is no ambient request.</returns>
        private static string GetUserHostAddress()
        {
            var remoteIpAddress = HttpContextSource.Current?.Connection?.RemoteIpAddress;
            if (remoteIpAddress == null)
            {
                return string.Empty;
            }

            if (remoteIpAddress.IsIPv4MappedToIPv6)
            {
                remoteIpAddress = remoteIpAddress.MapToIPv4();
            }

            return remoteIpAddress.ToString();
        }

        private sealed class SessionDto(Session session)
        {
            public DateTime LastUsed => session.LastUsed;

            public string SessionGuid => session.SessionGuid;

            public SessionStatus Status => session.Status;

            public IList<InstallJob> Response =>
                string.IsNullOrWhiteSpace(session.Response)
                    ? Array.Empty<InstallJob>()
                    : JsonConvert.DeserializeObject<SortedList<string, InstallJob>>(session.Response).Values;
        }
    }
}
