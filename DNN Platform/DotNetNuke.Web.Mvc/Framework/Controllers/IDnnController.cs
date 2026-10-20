// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Controllers
{
    using System.Web.UI;

    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Helpers;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>Contract for a DNN MVC module controller.</summary>
    public interface IDnnController
    {
        /// <summary>Gets the <see cref="ControllerContext"/> of the current controller.</summary>
        ControllerContext ControllerContext { get; }

        /// <summary>Gets the <see cref="IActionResult"/> produced by the executed action (without executing it).</summary>
        IActionResult ResultOfLastExecute { get; }

        /// <summary>Gets or sets the hosting Web Forms page.</summary>
        Page DnnPage { get; set; }

        /// <summary>Gets or sets the local resource file used for localization.</summary>
        string LocalResourceFile { get; set; }

        /// <summary>Gets or sets the module actions produced by the module.</summary>
        ModuleActionCollection ModuleActions { get; set; }

        /// <summary>Gets or sets the module instance context.</summary>
        ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets or sets a value indicating whether to validate the request.</summary>
        bool ValidateRequest { get; set; }

        /// <summary>Gets or sets the DNN URL helper.</summary>
        DnnUrlHelper Url { get; set; }

        /// <summary>Localizes the given resource key against the <see cref="LocalResourceFile"/>.</summary>
        /// <param name="key">The resource key.</param>
        /// <returns>The localized string.</returns>
        string LocalizeString(string key);
    }
}
