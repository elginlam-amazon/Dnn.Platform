// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>The result of executing an MVC module action.</summary>
    public class ModuleRequestResult
    {
        /// <summary>Gets or sets the action result produced by the module action.</summary>
        public IActionResult ActionResult { get; set; }

        /// <summary>Gets or sets the action context in which the module action executed.</summary>
        public ActionContext ActionContext { get; set; }

        /// <summary>Gets or sets the HTML rendered by the module action.</summary>
        public string RenderedOutput { get; set; }

        /// <summary>Gets or sets the module instance context.</summary>
        public ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets or sets the module actions produced by the module.</summary>
        public ModuleActionCollection ModuleActions { get; set; }

        /// <summary>Gets or sets the module application that produced this result.</summary>
        public ModuleApplication ModuleApplication { get; set; }
    }
}
