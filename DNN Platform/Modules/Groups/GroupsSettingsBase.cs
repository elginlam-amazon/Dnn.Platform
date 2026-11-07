// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Groups
{
    using DotNetNuke.Entities.Modules;

    /// <summary>The base component of the Social Groups settings control.</summary>
    /// <remarks>
    /// This was a WebForms control base. <see cref="ModuleSettingsBase"/> is now a Blazor component
    /// base (it derives from the migrated <see cref="PortalModuleBase"/>) and still exposes the
    /// overridable <see cref="LoadSettings"/>/<see cref="UpdateSettings"/> pair plus
    /// <c>ModuleSettings</c>/<c>TabModuleSettings</c>, which is the whole contract the settings
    /// control (ListSettings) relies on, so the class keeps deriving from it unchanged.
    /// </remarks>
    public class GroupsSettingsBase : ModuleSettingsBase
    {
        /// <inheritdoc />
        /// <remarks>Overridden by the settings component to populate its controls.</remarks>
        public override void LoadSettings()
        {
            base.LoadSettings();
        }

        /// <inheritdoc />
        /// <remarks>Overridden by the settings component to persist its controls.</remarks>
        public override void UpdateSettings()
        {
            base.UpdateSettings();
        }
    }
}
