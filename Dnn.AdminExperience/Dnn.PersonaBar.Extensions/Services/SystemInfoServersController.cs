// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Servers.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    using Dnn.PersonaBar.Extensions.Services.Dto;
    using Dnn.PersonaBar.Library;
    using Dnn.PersonaBar.Library.Attributes;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    [MenuPermission(Scope = ServiceScope.Host)]
    public class SystemInfoServersController : PersonaBarApiController
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<SystemInfoServersController>();

        [HttpGet]
        public IActionResult GetServers()
        {
            try
            {
                return this.Ok(GetServerList());
            }
            catch (Exception exc)
            {
                Logger.SystemInfoServersControllerGetServersException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        [HttpPost]
        public IActionResult DeleteServer(DeleteServerDTO data)
        {
            try
            {
                DotNetNuke.Entities.Host.ServerController.DeleteServer(data.ServerId);

                return this.Ok(true);
            }
            catch (Exception exc)
            {
                Logger.SystemInfoServersControllerDeleteServerException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        [HttpPost]
        public IActionResult EditServerUrl(EditServerUrlDTO data)
        {
            try
            {
                var server = DotNetNuke.Entities.Host.ServerController.GetServers().FirstOrDefault(s => s.ServerID == data.ServerId);
                server.Url = data.NewUrl;
                DotNetNuke.Entities.Host.ServerController.UpdateServer(server);

                return this.Ok(true);
            }
            catch (Exception exc)
            {
                Logger.SystemInfoServersControllerEditServerUrlException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        [HttpPost]
        public IActionResult DeleteNonActiveServers()
        {
            try
            {
                foreach (var s in DotNetNuke.Entities.Host.ServerController.GetInActiveServers(1440))
                {
                    DotNetNuke.Entities.Host.ServerController.DeleteServer(s.ServerID);
                }

                return this.Ok(true);
            }
            catch (Exception exc)
            {
                Logger.SystemInfoServersControllerDeleteNonActiveServersException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        private static IEnumerable<WebServer> GetServerList()
        {
            return DotNetNuke.Entities.Host.ServerController.GetServers().Select(s => new WebServer()
            {
                ServerId = s.ServerID,
                ServerGroup = s.ServerGroup,
                ServerName = s.ServerName,
                Url = s.Url,
                LastActivityDate = s.LastActivityDate,
                IsActive = s.LastActivityDate.AddDays(1) >= DateTime.Now,
                IsCurrent = s.ServerName == Environment.MachineName,
            });
        }
    }
}
