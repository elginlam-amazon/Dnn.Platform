// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Authentication
{
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;

    /// <summary>
    /// The AuthenticationSettingsBase class provides a base class for Authentication
    /// Settings controls.
    /// </summary>
    public abstract class AuthenticationSettingsBase : PortalModuleBase
    {
        private string authenticationType = Null.NullString;

        /// <summary>Gets or sets the Type of Authentication associated with this control.</summary>
        /// <remarks>
        /// A Blazor <see cref="Microsoft.AspNetCore.Components.ParameterAttribute"/>: the settings
        /// host resolves the provider's component type and renders it by type, so a value assigned
        /// on an instance the host created would not reach the instance the renderer creates.
        /// </remarks>
        [Microsoft.AspNetCore.Components.Parameter]
        public string AuthenticationType
        {
            get
            {
                return this.authenticationType;
            }

            set
            {
                this.authenticationType = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the host has requested that this control persist
        /// its settings on the current request.
        /// </summary>
        /// <remarks>
        /// Replaces the host's <c>settingControl.UpdateSettings()</c> call over a list of control
        /// instances it had added to its own control tree. A dynamically resolved provider
        /// component is rendered by type, so the host cannot hold on to the instance the renderer
        /// creates; it asks for the update through this parameter instead, and the control calls
        /// <see cref="UpdateSettings"/> on itself. The semantics are unchanged: every enabled
        /// provider's settings control saves its settings when the host's Update button is clicked.
        /// </remarks>
        [Microsoft.AspNetCore.Components.Parameter]
        public bool UpdateRequested { get; set; }

        /// <summary>UpdateSettings updates the settings in the Data Store.</summary>
        /// <remarks>This method must be overriden in the inherited class.</remarks>
        public abstract void UpdateSettings();
    }
}
