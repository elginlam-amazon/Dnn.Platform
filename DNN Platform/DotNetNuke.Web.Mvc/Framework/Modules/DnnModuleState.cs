// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    using System.Web.UI;

    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>State handed from <see cref="ModuleApplication"/> to a <see cref="Controllers.DnnController"/> and back, flowed through <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/>.</summary>
    public class DnnModuleState
    {
        /// <summary>Gets or sets the hosting Web Forms page.</summary>
        public Page DnnPage { get; set; }

        /// <summary>Gets or sets the module instance context.</summary>
        public ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets or sets the local resource file.</summary>
        public string LocalResourceFile { get; set; }

        /// <summary>Gets or sets the module application.</summary>
        public ModuleApplication ModuleApplication { get; set; }

        /// <summary>Gets or sets the module actions produced by the module action.</summary>
        public ModuleActionCollection ModuleActions { get; set; }

        /// <summary>Gets or sets the action result produced by the module action.</summary>
        public IActionResult ResultOfLastExecute { get; set; }
    }
}
