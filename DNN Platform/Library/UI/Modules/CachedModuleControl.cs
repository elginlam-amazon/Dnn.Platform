// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Modules
{
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;

    /// <summary>
    /// CachedModuleControl represents a cached "ModuleControl".  It renders the cached markup and
    /// implements the IModuleControl interface.
    /// </summary>
    /// <remarks>
    /// Was a <c>System.Web.UI.WebControls.Literal</c>; it is now a
    /// <see cref="DnnLiteralControl"/>, which renders its <see cref="DnnLiteralControl.Text"/> as
    /// markup exactly as the <c>Literal</c> did.
    /// </remarks>
    public class CachedModuleControl : DnnLiteralControl, IModuleControl
    {
        private string localResourceFile;
        private ModuleInstanceContext moduleContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedModuleControl"/> class.
        /// </summary>
        public CachedModuleControl()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedModuleControl"/> class.
        /// Constructs a new CachedModuleControl.
        /// </summary>
        /// <param name="cachedContent">The cached Content for this control.</param>
        public CachedModuleControl(string cachedContent)
        {
            this.Text = cachedContent;
        }

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
