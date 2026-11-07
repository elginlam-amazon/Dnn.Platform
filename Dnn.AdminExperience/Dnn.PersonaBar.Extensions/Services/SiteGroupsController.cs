// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.SiteGroups.Services
{
    using System;
    using System.Net;

    using Dnn.PersonaBar.Library;
    using Dnn.PersonaBar.Library.Attributes;
    using Dnn.PersonaBar.SiteGroups.Models;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    using ValidateAntiForgeryToken = DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute;

    [MenuPermission(Scope = ServiceScope.Host)]
    public class SiteGroupsController : PersonaBarApiController
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<SiteGroupsController>();

        private static IManagePortalGroups GroupManager => SiteGroups.Instance;

        [HttpGet]
        public IActionResult GetSiteGroups()
        {
            var groups = GroupManager.SiteGroups();
            return this.Ok(groups);
        }

        [HttpGet]
        public IActionResult GetAvailablePortals()
        {
            var portals = GroupManager.AvailablePortals();
            return this.Ok(portals);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(PortalGroupInfo portalGroup)
        {
            try
            {
                var id = GroupManager.Save(portalGroup);
                return this.Ok(id);
            }
            catch (Exception ex)
            {
                Logger.SiteGroupsControllerSaveException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int groupId)
        {
            try
            {
                GroupManager.Delete(groupId);
                return this.Ok(groupId);
            }
            catch (Exception ex)
            {
                Logger.SiteGroupsControllerDeleteException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
    }
}
