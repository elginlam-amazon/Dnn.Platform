// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Controllers
{
    using System.Collections.Generic;

    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Helpers;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewEngines;

    /// <summary>The contract of a DNN MVC module controller.</summary>
    /// <remarks>
    /// The legacy interface derived from <c>System.Web.Mvc.IController</c>; ASP.NET Core has no controller
    /// interface (controllers are POCOs discovered by the application model), so the DNN specific members
    /// are all that remain.
    /// </remarks>
    public interface IDnnController
    {
        /// <summary>Gets the controller context of the executing action.</summary>
        ControllerContext ControllerContext { get; }

        /// <summary>Gets the result produced by the last executed action, without executing it.</summary>
        IActionResult ResultOfLastExecute { get; }

        /// <summary>Gets or sets the DNN page (host component) the module is rendered into.</summary>
        IComponent DnnPage { get; set; }

        /// <summary>Gets or sets the local resource file used for localization.</summary>
        string LocalResourceFile { get; set; }

        /// <summary>Gets or sets the module actions.</summary>
        ModuleActionCollection ModuleActions { get; set; }

        /// <summary>Gets or sets the module instance context.</summary>
        ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets or sets a value indicating whether the request should be validated.</summary>
        bool ValidateRequest { get; set; }

        /// <summary>Gets or sets the view engines of this module.</summary>
        IReadOnlyList<IViewEngine> ViewEngines { get; set; }

        /// <summary>Gets or sets the DNN URL helper.</summary>
        DnnUrlHelper Url { get; set; }

        /// <summary>Localizes the given resource key.</summary>
        /// <param name="key">The resource key.</param>
        /// <returns>The localized string.</returns>
        string LocalizeString(string key);
    }
}
