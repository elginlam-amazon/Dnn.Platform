// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Extensions.Components.BulkInstall.WebAPI
{
    using System;
    using System.Linq;
    using System.Net;

    using Dnn.PersonaBar.Extensions.Components.BulkInstall;
    using Dnn.PersonaBar.Extensions.Components.BulkInstall.DataAccess.Models;
    using Dnn.PersonaBar.Extensions.Components.BulkInstall.WebAPI.ActionFilters;
    using Dnn.PersonaBar.Library;
    using Dnn.PersonaBar.Library.Attributes;
    using DotNetNuke.Web.Api;
    using DotNetNuke.Web.Api.Auth.ApiTokens;
    using Microsoft.AspNetCore.Mvc;

    using ValidateAntiForgeryToken = DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute;

    /// <summary>A web API controller for <see cref="APIUser"/>.</summary>
    /// <param name="apiUserManager">The API user manager.</param>
    /// <param name="apiTokenSettingsRepository">The API token settings repository.</param>
    [RequireHost]
    [ValidateAntiForgeryToken]
    [InBulkInstallWhitelist]
    [MenuPermission(Scope = ServiceScope.Host)]
    public class APIUserController(APIUserManager apiUserManager, ApiTokenSettingsRepository apiTokenSettingsRepository) : PersonaBarApiController
    {
        private readonly APIUserManager apiUserManager = apiUserManager;
        private readonly ApiTokenSettingsRepository apiTokenSettingsRepository = apiTokenSettingsRepository;

        /// <summary>Gets all <see cref="APIUser"/> instances.</summary>
        /// <returns>A result with a list of <see cref="APIUser"/>.</returns>
        [HttpGet]
        public IActionResult GetAll()
        {
            var settings = ApiTokenSettings.GetSettings(this.apiTokenSettingsRepository, this.PortalSettings.PortalId);
            var apiUsers = this.apiUserManager.GetAll().ToList();

            // Loop and remove sensitive information.
            foreach (var apiUser in apiUsers)
            {
                apiUser.EncryptedEncryptionKey = null;
                apiUser.Salt = null;
            }

            return this.Ok(new { Users = apiUsers, Enabled = settings.ApiTokensEnabled, });
        }

        /// <summary>Creates a new <see cref="APIUser"/>.</summary>
        /// <param name="name">The label.</param>
        /// <param name="bypass">Whether the user can bypass the IP address allow list.</param>
        /// <param name="expiresOn">The date/time on which the API user's token expires.</param>
        /// <returns>A result with the new <see cref="APIUser"/>.</returns>
        [HttpPost]
        public IActionResult Create(string name, bool bypass = false, DateTime? expiresOn = null)
        {
            // Check we have a name.
            if (string.IsNullOrEmpty(name))
            {
                return DnnApiResults.Response(HttpStatusCode.BadRequest, new { Message = "name is required", });
            }

            var settings = ApiTokenSettings.GetSettings(this.apiTokenSettingsRepository, this.PortalSettings.PortalId);
            if (!settings.ApiTokensEnabled)
            {
                return DnnApiResults.Response(HttpStatusCode.BadRequest, new { Message = "API tokens are disabled", });
            }

            // Create user.
            var apiUser = this.apiUserManager.Create(name, bypass, expiresOn ?? DateTime.UtcNow.AddYears(1), this.UserInfo.UserID);

            apiUser.EncryptedEncryptionKey = null;
            apiUser.Salt = null;

            return DnnApiResults.Response(HttpStatusCode.Created, new { User = apiUser, });
        }

        /// <summary>Deletes an <see cref="APIUser"/>.</summary>
        /// <param name="id">The API user ID.</param>
        /// <returns>A result indicating success.</returns>
        [HttpDelete]
        public IActionResult Delete(int id)
        {
            var apiUser = this.apiUserManager.GetById(id);

            if (apiUser == null)
            {
                return DnnApiResults.Error(HttpStatusCode.NotFound, "API user not found.");
            }

            try
            {
                this.apiUserManager.Delete(apiUser);
            }
            catch (Exception)
            {
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, "Failed to delete API user.");
            }

            return DnnApiResults.Response(HttpStatusCode.NoContent);
        }
    }
}
