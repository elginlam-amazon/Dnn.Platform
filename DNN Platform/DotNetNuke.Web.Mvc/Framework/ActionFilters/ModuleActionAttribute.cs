// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionFilters
{
    using System;

    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.Security;
    using DotNetNuke.Web.Mvc.Framework.Controllers;

    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>Defines a single <see cref="ModuleAction"/> for an MVC module controller or action.</summary>
    public class ModuleActionAttribute : ActionFilterAttribute
    {
        /// <summary>Initializes a new instance of the <see cref="ModuleActionAttribute"/> class.</summary>
        public ModuleActionAttribute()
        {
            this.SecurityAccessLevel = SecurityAccessLevel.Edit;
        }

        /// <summary>Gets or sets the key for the module control.</summary>
        public string ControlKey { get; set; }

        /// <summary>Gets or sets the URL for the icon to be used in the Module Actions menu.</summary>
        public string Icon { get; set; }

        /// <summary>Gets or sets the security level for the module action.</summary>
        public SecurityAccessLevel SecurityAccessLevel { get; set; }

        /// <summary>Gets or sets the title for the Module Action. Used only when <see cref="TitleKey"/> is blank.</summary>
        public string Title { get; set; }

        /// <summary>Gets or sets the localization key for the title of the Module Action.</summary>
        public string TitleKey { get; set; }

        /// <inheritdoc />
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.Controller is not IDnnController controller)
            {
                throw new InvalidOperationException("This attribute can only be applied to Controllers that implement IDnnController");
            }

            controller.ModuleActions ??= new ModuleActionCollection();

            controller.ModuleActions.Add(
                -1,
                (!string.IsNullOrEmpty(this.TitleKey)) ? controller.LocalizeString(this.TitleKey) : this.Title,
                ModuleActionType.AddContent,
                string.Empty,
                this.Icon,
                controller.ModuleContext.EditUrl(this.ControlKey),
                false,
                this.SecurityAccessLevel,
                true,
                false);
        }
    }
}
