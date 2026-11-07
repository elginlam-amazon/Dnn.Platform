// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Servers.Services
{
    using System;
    using System.Net;

    using Dnn.PersonaBar.Library;
    using Dnn.PersonaBar.Library.Attributes;
    using Dnn.PersonaBar.Servers.Components.WebServer;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    [MenuPermission(Scope = ServiceScope.Host)]
    public class SystemInfoWebController : PersonaBarApiController
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<SystemInfoWebController>();

        [HttpGet]
        public IActionResult GetWebServerInfo()
        {
            try
            {
                var serverInfo = new ServerInfo();
                return this.Ok(new
                {
                    osVersion = serverInfo.OSVersion,
                    iisVersion = serverInfo.IISVersion,
                    framework = serverInfo.Framework,
                    netFrameworkVersion = serverInfo.NetFrameworkVersion,
                    identity = serverInfo.Identity,
                    hostName = serverInfo.HostName,
                    physicalPath = serverInfo.PhysicalPath,
                    url = serverInfo.Url,
                    relativePath = serverInfo.RelativePath,
                    serverTime = serverInfo.ServerTime,
                });
            }
            catch (Exception exc)
            {
                Logger.SystemInfoWebControllerGetWebServerInfoException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }
    }
}
