// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.ContactList.Mvc.Controllers
{
    using Dnn.ContactList.Mvc.Models;
    using DotNetNuke.Collections;
    using DotNetNuke.Security;
    using DotNetNuke.Web.Mvc.Framework.ActionFilters;
    using DotNetNuke.Web.Mvc.Framework.Controllers;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// The Settings Controller manages the modules settings.
    /// </summary>
    [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Edit)]
    [DnnHandleError]
    public class SettingsController : DnnController
    {
        /// <summary>
        /// The Index action renders the default Settings View.
        /// </summary>
        /// <returns>The Settings view.</returns>
        [HttpGet]
        public ActionResult Index()
        {
            var settings = new Settings();
            settings.PageSize = this.ModuleContext.Configuration.ModuleSettings.GetValueOrDefault("PageSize", 9);
            settings.AllowContactCreation = this.ModuleContext.Configuration.ModuleSettings.GetValueOrDefault("AllowContactCreation", false);
            return this.View(settings);
        }

        /// <summary>
        /// Saves the posted settings.
        /// </summary>
        /// <param name="settings">The settings to save.</param>
        /// <returns>A redirect to the default route.</returns>
        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        public ActionResult Index(Settings settings)
        {
            this.ModuleContext.Configuration.ModuleSettings["PageSize"] = settings.PageSize.ToString();
            this.ModuleContext.Configuration.ModuleSettings["AllowContactCreation"] = settings.AllowContactCreation.ToString();
            return this.RedirectToDefaultRoute();
        }
    }
}
