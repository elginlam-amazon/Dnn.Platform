// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Modules
{
    using DotNetNuke.Entities.Modules;

    using Microsoft.AspNetCore.Components;

    public class WebFormsModuleControlFactory : BaseModuleControlFactory
    {
        /// <inheritdoc />
        public override int Priority => 100;

        /// <inheritdoc />
        public override bool SupportsControl(ModuleInfo moduleConfiguration, string controlSrc)
        {
            return controlSrc.EndsWith(".ascx", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public override IComponent CreateControl(IComponent containerControl, string controlKey, string controlSrc)
        {
            return ControlUtilities.LoadControl<IComponent>(containerControl, controlSrc);
        }

        /// <inheritdoc />
        public override IComponent CreateModuleControl(IComponent containerControl, ModuleInfo moduleConfiguration)
        {
            return this.CreateControl(containerControl, string.Empty, moduleConfiguration.ModuleControl.ControlSrc);
        }

        /// <inheritdoc />
        public override IComponent CreateSettingsControl(IComponent containerControl, ModuleInfo moduleConfiguration, string controlSrc)
        {
            return this.CreateControl(containerControl, string.Empty, controlSrc);
        }
    }
}
