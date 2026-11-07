// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Modules
{
    using DotNetNuke.Entities.Modules;

    using Microsoft.AspNetCore.Components;

    public class ReflectedModuleControlFactory : BaseModuleControlFactory
    {
        /// <inheritdoc />
        public override int Priority => -1;

        /// <inheritdoc />
        public override bool SupportsControl(ModuleInfo moduleConfiguration, string controlSrc)
        {
            // This factory is used as fallback
            return true;
        }

        /// <inheritdoc />
        public override IComponent CreateControl(IComponent containerControl, string controlKey, string controlSrc)
        {
            // load from a typename in an assembly ( ie. server control)
            // TemplateControl.LoadControl(Type, object[]) is gone: the component is created from
            // the request's service provider so that it can take constructor dependencies.
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
