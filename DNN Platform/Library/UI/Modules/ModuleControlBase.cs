// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Modules
{


    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;

    /// <summary>
    /// ModuleControlBase is a base class for Module Controls that inherits from the
    /// Control base class.  As with all ModuleControl base classes it implements
    /// IModuleControl.
    /// </summary>
    /// <remarks>
    /// Was a <c>System.Web.UI.Control</c>; it is now a DNN skinning component
    /// (<see cref="DnnComponentBase"/>), which is the ASP.NET Core replacement for the control tree
    /// this class participated in.
    /// </remarks>
    public class ModuleControlBase : DnnComponentBase, IModuleControl
    {
        private string localResourceFile;
        private ModuleInstanceContext moduleContext;

        /// <summary>Gets the underlying base control for this ModuleControl.</summary>
        /// <returns>This component.</returns>
        public IComponent Control => this;

        /// <summary>Gets the Path for this control (used primarily for UserControls).</summary>
        /// <returns>A String.</returns>
        public string ControlPath => this.TemplateSourceDirectory + "/";

        /// <summary>Gets the Name for this control.</summary>
        /// <returns>A String.</returns>
        public string ControlName => this.GetType().Name.Replace("_", ".");

        /// <summary>Gets the Module Context for this control.</summary>
        /// <returns>A ModuleInstanceContext.</returns>
        public ModuleInstanceContext ModuleContext => this.moduleContext ??= new ModuleInstanceContext(this);

        /// <summary>Gets or sets the local resource file for this control.</summary>
        /// <returns>A String.</returns>
        public string LocalResourceFile
        {
            get
            {
                string fileRoot;
                if (string.IsNullOrEmpty(this.localResourceFile))
                {
                    fileRoot = this.ControlPath + "/" + Localization.LocalResourceDirectory + "/" + (this.ID ?? this.ControlName);
                }
                else
                {
                    fileRoot = this.localResourceFile;
                }

                return fileRoot;
            }

            set
            {
                this.localResourceFile = value;
            }
        }
    }
}
