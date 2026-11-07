// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.AzureConnector.Services
{
    using System;
    using System.Collections.Generic;
    using System.Net;

    using Amazon.S3;

    using DotNetNuke.Providers.FolderProviders.AzureFolderProvider;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.DependencyInjection;

    /// <inheritdoc />
    /// <remarks>
    /// Was a Web API 2 controller. The <c>Container</c> values this controller lists are Amazon S3 bucket
    /// names (the project's Azure Blob Storage usage was ported to AWSSDK.S3), and the credentials held by
    /// the folder mapping are the AWS access key id and secret access key. The route registered by
    /// <see cref="ServiceRouteMapper"/>, the action names, the <see cref="DnnAuthorizeAttribute"/> rule and
    /// the response payload shapes are unchanged, because <c>Scripts/connector.js</c> consumes them.
    /// </remarks>
    [DnnAuthorize]
    public class ServicesController : DnnApiController
    {
        private readonly IFolderMappingController folderMappingController;

        /// <summary>Initializes a new instance of the <see cref="ServicesController"/> class.</summary>
        /// <param name="folderMappingController">The folder mapping controller.</param>
        public ServicesController(IFolderMappingController folderMappingController)
        {
            this.folderMappingController = folderMappingController;
        }

        /// <summary>Gets all containers.</summary>
        /// <param name="id">The folder mapping ID.</param>
        /// <returns>An <see cref="IActionResult"/> wrapping a <see cref="List{T}"/> of <see cref="string"/>.</returns>
        [HttpGet]
        public IActionResult GetAllContainers(int id)
        {
            try
            {
                var containers = new List<string>();
                var folderProvider = ActivatorUtilities.GetServiceOrCreateInstance<AzureFolderProvider>(this.ServiceProvider);
                var folderMapping = Components.AzureConnector.FindAzureFolderMappingStatic(this.folderMappingController, this.PortalSettings.PortalId, id, false);
                if (folderMapping != null)
                {
                    containers = folderProvider.GetAllContainers(folderMapping);
                }

                return this.Ok(containers);
            }
            catch (AmazonS3Exception ex)
            {
                Exceptions.LogException(ex);

                // S3 has no equivalent of the Azure RequestInformation.HttpStatusMessage, so the error
                // code is appended to the message where one is available.
                var message = string.IsNullOrEmpty(ex.ErrorCode)
                    ? ex.Message
                    : $"{ex.Message} ({ex.ErrorCode})";
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, message);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                const string message = "An error has occurred connecting to the Azure account.";
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, message);
            }
        }

        /// <summary>Gets the folder mapping ID.</summary>
        /// <returns>An <see cref="IActionResult"/> wrapping the folder mapping ID.</returns>
        [HttpGet]
        public IActionResult GetFolderMappingId()
        {
            return this.Ok(
                Components.AzureConnector.FindAzureFolderMappingStatic(this.folderMappingController, this.PortalSettings.PortalId).FolderMappingID);
        }
    }
}
