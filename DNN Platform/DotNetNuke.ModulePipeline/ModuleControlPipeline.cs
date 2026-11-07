// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.ModulePipeline
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The Module Pipeline that determines which Module pattern
    /// to invoke based on the input module type.
    /// </summary>
    public partial class ModuleControlPipeline : IModuleControlPipeline
    {
        private static readonly ILogger TraceLogger = DnnLoggingController.GetLogger("DNN.Trace");

        private readonly IModuleControlFactory[] factories;

        /// <summary>Initializes a new instance of the <see cref="ModuleControlPipeline"/> class.</summary>
        /// <param name="factories">The control factories.</param>
        public ModuleControlPipeline(IEnumerable<IModuleControlFactory> factories)
        {
            this.factories = factories.OrderByDescending(f => f.Priority).ToArray();
        }

        /// <inheritdoc />
        public IComponent LoadModuleControl(IComponent containerControl, ModuleInfo moduleConfiguration, string controlKey, string controlSrc)
        {
            TraceLogger.ModuleControlPipelineLoadModuleControlStart(moduleConfiguration.TabID, moduleConfiguration.ModuleID, moduleConfiguration.ModuleControl.ControlSrc);

            IComponent control = null;
            IModuleControlFactory controlFactory = this.GetModuleControlFactory(moduleConfiguration, controlSrc);

            if (controlFactory != null)
            {
                control = controlFactory.CreateControl(containerControl, controlKey, controlSrc);
            }

            // set the control ID to the resource file name ( i.e. controlname.ascx = controlname )
            // this is necessary for the Localization in PageBase
            if (control != null)
            {
                SetControlId(control, Path.GetFileNameWithoutExtension(controlSrc));

                if (control is IModuleControl moduleControl)
                {
                    moduleControl.ModuleContext.Configuration = moduleConfiguration;
                }
            }

            TraceLogger.ModuleControlPipelineLoadModuleControlEnd(moduleConfiguration.TabID, moduleConfiguration.ModuleID, moduleConfiguration.ModuleControl.ControlSrc);
            return control;
        }

        /// <inheritdoc />
        public IComponent LoadModuleControl(IComponent containerControl, ModuleInfo moduleConfiguration)
        {
            TraceLogger.ModuleControlPipelineLoadModuleControlStart(moduleConfiguration.TabID, moduleConfiguration.ModuleID, moduleConfiguration.ModuleControl.ControlSrc);

            IComponent control = null;
            IModuleControlFactory controlFactory = this.GetModuleControlFactory(moduleConfiguration, moduleConfiguration.ModuleControl.ControlSrc);

            if (controlFactory != null)
            {
                control = controlFactory.CreateModuleControl(containerControl, moduleConfiguration);
            }

            // set the control ID to the resource file name ( i.e. controlname.ascx = controlname )
            // this is necessary for the Localization in PageBase
            if (control != null)
            {
                SetControlId(control, Path.GetFileNameWithoutExtension(moduleConfiguration.ModuleControl.ControlSrc));

                if (control is IModuleControl moduleControl)
                {
                    moduleControl.ModuleContext.Configuration = moduleConfiguration;
                }
            }

            TraceLogger.ModuleControlPipelineLoadModuleControlEnd(moduleConfiguration.TabID, moduleConfiguration.ModuleID, moduleConfiguration.ModuleControl.ControlSrc);
            return control;
        }

        /// <inheritdoc />
        public IComponent LoadSettingsControl(IComponent containerControl, ModuleInfo moduleConfiguration, string controlSrc)
        {
            TraceLogger.ModuleControlPipelineLoadSettingsControlStart(moduleConfiguration.TabID, moduleConfiguration.ModuleID, moduleConfiguration.ModuleControl.ControlSrc);

            IComponent control = null;
            IModuleControlFactory controlFactory = this.GetModuleControlFactory(moduleConfiguration, controlSrc);

            if (controlFactory != null)
            {
                control = controlFactory.CreateSettingsControl(containerControl, moduleConfiguration, controlSrc);
            }

            // set the control ID to the resource file name ( i.e. controlname.ascx = controlname )
            // this is necessary for the Localization in PageBase
            if (control != null)
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(controlSrc);
                if (fileNameWithoutExtension != null)
                {
                    SetControlId(control, fileNameWithoutExtension.Replace('.', '-'));
                }

                if (control is ISettingsControl settingsControl)
                {
                    settingsControl.ModuleContext.Configuration = moduleConfiguration;
                }
            }

            TraceLogger.ModuleControlPipelineLoadSettingsControlEnd(moduleConfiguration.TabID, moduleConfiguration.ModuleID, moduleConfiguration.ModuleControl.ControlSrc);
            return control;
        }

        /// <inheritdoc />
        public IComponent CreateCachedControl(string cachedContent, ModuleInfo moduleConfiguration)
        {
            var moduleControl = new CachedModuleControl(cachedContent);
            moduleControl.ModuleContext.Configuration = moduleConfiguration;
            return moduleControl;
        }

        /// <inheritdoc />
        public IComponent CreateModuleControl(ModuleInfo moduleConfiguration)
        {
            IModuleControlFactory factory = this.GetModuleControlFactory(moduleConfiguration, moduleConfiguration.ModuleControl.ControlSrc);
            return factory.CreateModuleControl(moduleConfiguration);
        }

        /// <summary>
        /// Assigns the server side identifier to <paramref name="control"/> when the component
        /// exposes DNN's control-tree surface.
        /// </summary>
        /// <remarks>
        /// <see cref="IComponent"/> itself has no <c>ID</c>; it is declared on
        /// <see cref="IDnnControl"/> (via <c>IClientApiControl.Id</c>) and implemented by
        /// <c>DnnComponentBase</c>. A component produced by a third party factory need not derive
        /// from that base, so the assignment is guarded by a type test rather than a cast.
        /// </remarks>
        /// <param name="control">The control to identify.</param>
        /// <param name="id">The identifier to assign.</param>
        private static void SetControlId(IComponent control, string id)
        {
            if (control is IDnnControl dnnControl)
            {
                dnnControl.Id = id;
            }
        }

        private IModuleControlFactory GetModuleControlFactory(ModuleInfo moduleConfiguration, string controlSrc)
        {
            var length = this.factories.Length;

            for (var i = 0; i < length; i++)
            {
                var factory = this.factories[i];

                if (factory.SupportsControl(moduleConfiguration, controlSrc))
                {
                    return factory;
                }
            }

            // The following exception should never be thrown, as the default factory should always be able to create a control
            throw new NotSupportedException($"No module control factory found for module {moduleConfiguration.ModuleID} with control source {controlSrc}");
        }
    }
}
