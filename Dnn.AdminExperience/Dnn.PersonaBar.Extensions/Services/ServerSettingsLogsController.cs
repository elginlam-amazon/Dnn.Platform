// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Servers.Services
{
    using System;
    using System.IO;
    using System.Net;

    using Dnn.PersonaBar.Library;
    using Dnn.PersonaBar.Library.Attributes;
    using Dnn.PersonaBar.Servers.Components.Log;
    using DotNetNuke.Common;
    using DotNetNuke.Data;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    [MenuPermission(Scope = ServiceScope.Host)]
    public class ServerSettingsLogsController : PersonaBarApiController
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<ServerSettingsLogsController>();
        private readonly LogController logController = new LogController();

        [HttpGet]
        public IActionResult GetLogs()
        {
            try
            {
                var response = new
                {
                    Success = true,
                    Results = new
                    {
                        LogList = this.logController.GetLogFilesList(),
                        UpgradeLogList = this.logController.GetUpgradeLogList(),
                    },
                    TotalResults = 1,
                };
                return this.Ok(response);
            }
            catch (Exception exc)
            {
                Logger.ServerSettingsLogsControllerGetLogsException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        [HttpGet]
        public IActionResult GetLogFile(string fileName)
        {
            try
            {
                var cleanedFileName = Path.GetFileName(fileName);
                var logFilePath = Path.Combine(Globals.ApplicationMapPath, @"portals\_default\logs", cleanedFileName);
                return this.CreateLogFileResponse(logFilePath);
            }
            catch (ArgumentException exc)
            {
                return DnnApiResults.Error(HttpStatusCode.BadRequest, exc.Message);
            }
            catch (Exception exc)
            {
                Logger.ServerSettingsLogsControllerGetLogFileException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        [HttpGet]
        public IActionResult GetUpgradeLogFile(string logName)
        {
            try
            {
                var providerPath = DataProvider.Instance().GetProviderPath();
                var cleanedLogName = Path.GetFileName(logName);
                var logFilePath = Path.Combine(providerPath, cleanedLogName);
                return this.CreateLogFileResponse(logFilePath);
            }
            catch (ArgumentException exc)
            {
                return DnnApiResults.Error(HttpStatusCode.BadRequest, exc.Message);
            }
            catch (Exception exc)
            {
                Logger.ServerSettingsLogsControllerGetUpgradeLogFileException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        [NonAction]
        private static void ValidateFilePath(string physicalPath)
        {
            var fileInfo = new FileInfo(physicalPath);
            if (!fileInfo.DirectoryName.StartsWith(Globals.ApplicationMapPath, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ArgumentException("Invalid File Path");
            }
        }

        [NonAction]
        private IActionResult CreateLogFileResponse(string logFilePath)
        {
            ValidateFilePath(logFilePath);
            if (!System.IO.File.Exists(logFilePath))
            {
                return DnnApiResults.Response(HttpStatusCode.NotFound);
            }

            using var reader = System.IO.File.OpenText(logFilePath);
            var logText = reader.ReadToEnd();
            return this.Ok(logText);
        }
    }
}
