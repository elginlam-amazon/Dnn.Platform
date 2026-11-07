// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Prompt.Common
{
    using System.Net;

    using Dnn.PersonaBar.Library;
    using Dnn.PersonaBar.Library.Prompt.Models;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc;

    public abstract class ControllerBase : PersonaBarApiController
    {
        protected IActionResult OkResponse(string msg, object data = null)
        {
            return DnnApiResults.Response(HttpStatusCode.OK, new ResponseModel(false, msg, data?.ToString() ?? string.Empty));
        }

        protected IActionResult UnauthorizedResponse(string msg = "", object data = null)
        {
            if (string.IsNullOrEmpty(msg))
            {
                msg = Localization.GetString("Prompt_NotAuthorized", Components.Constants.LocalResourcesFile, true);
            }
            else
            {
                msg += " " + Localization.GetString("Prompt_SessionTimedOut", Components.Constants.LocalResourcesFile, true);
            }

            return DnnApiResults.Response(HttpStatusCode.Unauthorized, new ResponseModel(true, msg, data?.ToString() ?? string.Empty));
        }

        protected IActionResult BadRequestResponse(string msg = "", object data = null)
        {
            if (string.IsNullOrEmpty(msg))
            {
                msg = Localization.GetString("Prompt_InvalidData", Components.Constants.LocalResourcesFile, true);
            }

            return DnnApiResults.Response(HttpStatusCode.BadRequest, new ResponseModel(true, msg, data?.ToString() ?? string.Empty));
        }

        protected IActionResult ServerErrorResponse(string msg = "", object data = null)
        {
            if (string.IsNullOrEmpty(msg))
            {
                msg = Localization.GetString("Prompt_ServerError", Components.Constants.LocalResourcesFile, true);
            }

            return DnnApiResults.Response(HttpStatusCode.InternalServerError, new ResponseModel(true, msg, data?.ToString() ?? string.Empty));
        }

        protected IActionResult NotImplementedResponse(string msg = "", object data = null)
        {
            if (string.IsNullOrEmpty(msg))
            {
                msg = Localization.GetString("Prompt_NotImplemented", Components.Constants.LocalResourcesFile, true);
            }

            return DnnApiResults.Response(HttpStatusCode.NotImplemented, new ResponseModel(true, msg, data?.ToString() ?? string.Empty));
        }
    }
}
