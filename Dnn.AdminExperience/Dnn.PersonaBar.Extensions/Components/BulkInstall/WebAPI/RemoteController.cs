// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Extensions.Components.BulkInstall.WebAPI
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;

    using Dnn.PersonaBar.Extensions.Components.BulkInstall;
    using Dnn.PersonaBar.Extensions.Components.BulkInstall.DataAccess.Models;
    using Dnn.PersonaBar.Extensions.Components.BulkInstall.Logging;
    using Dnn.PersonaBar.Extensions.Components.BulkInstall.WebAPI.ActionFilters;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.BulkInstall.Encryption;
    using DotNetNuke.Common;
    using DotNetNuke.Web.Api;
    using DotNetNuke.Web.Api.Auth.ApiTokens;
    using DotNetNuke.Web.Api.Auth.ApiTokens.Models;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>A web API controller for use by the Deploy Client.</summary>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="apiUserManager">The API user manager.</param>
    /// <param name="eventLogManager">The event log manager.</param>
    /// <param name="eventLogger">The event logger.</param>
    /// <param name="appStatus">The application status.</param>
    /// <param name="serviceProvider">The DI container.</param>
    /// <param name="apiTokenController">The API token controller.</param>
    [InBulkInstallWhitelist]
    [ApiTokenAuthorize(APIUserManager.BulkInstallApiTokenScopeKey, "~/DesktopModules/Admin/Dnn.PersonaBar/Modules/Dnn.BulkInstall/App_LocalResources/BulkInstall.resx", ApiTokenScope.Host)]
    public class RemoteController(SessionManager sessionManager, APIUserManager apiUserManager, EventLogManager eventLogManager, IEventLogger eventLogger, IApplicationStatusInfo appStatus, IServiceProvider serviceProvider, IApiTokenController apiTokenController) : DnnApiController
    {
        private readonly SessionManager sessionManager = sessionManager;
        private readonly APIUserManager apiUserManager = apiUserManager;
        private readonly EventLogManager eventLogManager = eventLogManager;
        private readonly IEventLogger eventLogger = eventLogger;
        private readonly IApplicationStatusInfo appStatus = appStatus;
        private readonly IServiceProvider serviceProvider = serviceProvider;
        private readonly IApiTokenController apiTokenController = apiTokenController;

        /// <summary>Creates a new session.</summary>
        /// <returns>A result with the <see cref="Session"/>.</returns>
        [HttpGet]
        public IActionResult CreateSession()
        {
            Session session = this.sessionManager.CreateSession();

            return this.Ok(session);
        }

        /// <summary>Gets a session by its <paramref name="sessionGuid"/>.</summary>
        /// <param name="sessionGuid">The public identifier for the <see cref="Session"/>.</param>
        /// <returns>A result with either the <see cref="Session"/> or <see langword="null"/>.</returns>
        [HttpGet]
        public IActionResult GetSession(string sessionGuid)
        {
            Session session = this.sessionManager.GetSession(sessionGuid);

            return this.Ok(session);
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
                    this.eventLogManager.Log("REMOTE_EXCEPTION", EventLogSeverity.Warning, "Request was not multipart/form-data.");
                    return DnnApiResults.Response(HttpStatusCode.UnsupportedMediaType);
                }

                // Get the api key from the header.
                var apiKey = this.apiTokenController.GetCurrentThreadApiToken();

                // Get the api user.
                APIUser apiUser = this.apiUserManager.FindAndPrepare(apiKey.ApiTokenId, GetAuthorizationParameter(this.Request));

                // Receive files.
                var form = await this.Request.ReadFormAsync();

                foreach (var file in form.Files)
                {
                    string filename = file.FileName.Replace("\"", string.Empty);

                    using MemoryStream ms = new MemoryStream();
                    using (var uploadStream = file.OpenReadStream())
                    {
                        await uploadStream.CopyToAsync(ms);
                    }

                    ms.Position = 0;
                    using Stream ds = Crypto.Decrypt(ms, apiUser.EncryptionKey);
                    this.sessionManager.AddPackage(sessionGuid, ds, filename);
                }
            }
            catch (Exception ex)
            {
                this.eventLogManager.Log("REMOTE_EXCEPTION", EventLogSeverity.Warning, ex);

                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }

            return DnnApiResults.Response(HttpStatusCode.Created);
        }

        /// <summary>Starts the installation for the session.</summary>
        /// <param name="sessionGuid">The public identifier for the <see cref="Session"/>.</param>
        /// <returns>A result indicating success.</returns>
        [HttpGet]
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

                // Get the api key from the header.
                var apiKey = this.apiTokenController.GetCurrentThreadApiToken();

                // Get the session.
                Session sessionObj = this.sessionManager.GetSession(sessionGuid);

                // Create a deployment operation.
                RemoteDeployment deployOperation = new RemoteDeployment(this.apiUserManager, this.sessionManager, this.eventLogManager, this.eventLogger, this.appStatus, this.serviceProvider, sessionObj, ipAddress, apiKey.ApiTokenId);

                // Deploy.
                deployOperation.Deploy();
            }
            catch (Exception ex)
            {
                this.eventLogManager.Log("REMOTE_EXCEPTION", EventLogSeverity.Warning, ex);

                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }

            return this.Ok("Operation started.");
        }

        /// <summary>
        /// Gets the credentials portion of the request's <c>Authorization</c> header, the ASP.NET Core
        /// equivalent of the legacy <c>Request.Headers.Authorization.Parameter</c>.
        /// </summary>
        /// <param name="request">The current request.</param>
        /// <returns>The authorization parameter, or <see langword="null"/> when the header is absent or has no parameter.</returns>
        private static string GetAuthorizationParameter(Microsoft.AspNetCore.Http.HttpRequest request)
        {
            var header = request.Headers["Authorization"];
            if (header.Count == 0)
            {
                return null;
            }

            var value = header.ToString();
            var separatorIndex = value.IndexOf(' ');
            return separatorIndex < 0 ? null : value.Substring(separatorIndex + 1).Trim();
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
    }
}
