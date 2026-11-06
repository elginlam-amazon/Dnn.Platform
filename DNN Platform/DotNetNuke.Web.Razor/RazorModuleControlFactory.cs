// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor
{
    using System;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Components;

    /// <summary>A control factory for razor modules.</summary>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public partial class RazorModuleControlFactory : BaseModuleControlFactory
    {
        /// <inheritdoc />
        public override int Priority => 100;

        /// <inheritdoc />
        public override bool SupportsControl(ModuleInfo moduleConfiguration, string controlSrc)
        {
            return controlSrc.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||
                   controlSrc.EndsWith(".vbhtml", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public override IComponent CreateControl(IComponent containerControl, string controlKey, string controlSrc)
        {
            return new RazorHostControl("~/" + controlSrc);
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
