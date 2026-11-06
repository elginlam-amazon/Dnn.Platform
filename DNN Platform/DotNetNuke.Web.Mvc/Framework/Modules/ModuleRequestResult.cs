// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.UI.Modules;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>The captured result of a module execution.</summary>
    public class ModuleRequestResult
    {
        /// <summary>Gets or sets the action result produced by the module's controller (not yet executed).</summary>
        public IActionResult ActionResult { get; set; }

        /// <summary>Gets or sets the action context the result must be executed against.</summary>
        /// <remarks>Was a <c>System.Web.Mvc.ControllerContext</c>.</remarks>
        public ActionContext ActionContext { get; set; }

        /// <summary>Gets or sets the module instance context.</summary>
        public ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets or sets the module actions populated while executing the module.</summary>
        public ModuleActionCollection ModuleActions { get; set; }

        /// <summary>Gets or sets the module application which executed the request.</summary>
        public ModuleApplication ModuleApplication { get; set; }
    }
}
