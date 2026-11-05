// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Modules
{
    using System;
    using System.IO;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Framework;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Modules.Html5;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.Logging;

    /// <inheritdoc cref="IModuleControlPipeline" />
    [DnnDeprecated(9, 4, 0, "This implementation has moved to DotNetNuke.ModulePipeline.ModuleControlPipeline")]
    public partial class ModuleControlFactory
    {
        private static readonly ILogger TracelLogger = DnnLoggingController.GetLogger("DNN.Trace");

        /// <inheritdoc cref="IModuleControlPipeline.LoadModuleControl(IComponent,ModuleInfo,string,string)" />
        [DnnDeprecated(9, 4, 0, "This implementation has moved to DotNetNuke.ModulePipeline.ModuleControlPipeline")]
        public static partial IComponent LoadModuleControl(IComponent containerControl, ModuleInfo moduleConfiguration, string controlKey, string controlSrc)
        {
            TracelLogger.ModuleControlFactoryLoadModuleControlStart(moduleConfiguration.TabID, moduleConfiguration.ModuleID, moduleConfiguration.ModuleControl.ControlSrc);

            IComponent control = null;
            IModuleControlFactory controlFactory = GetModuleControlFactory(controlSrc);

            if (controlFactory != null)
            {
                control = controlFactory.CreateControl(containerControl, controlKey, controlSrc);
            }

            // set the control ID to the resource file name ( i.e. controlname.ascx = controlname )
            // this is necessary for the Localization in PageBase
            if (control != null)
            {
                // Control.ID is now IClientApiControl.Id on the DNN component tree; a module
                // control that is not part of that tree (PortalModuleBase) derives its resource
                // file from its component type instead, so there is nothing to assign.
                SetControlId(control, Path.GetFileNameWithoutExtension(controlSrc));
                if (control is IModuleControl moduleControl)
                {
                    moduleControl.ModuleContext.Configuration = moduleConfiguration;
                }
            }

            TracelLogger.ModuleControlFactoryLoadModuleControlEnd(moduleConfiguration.TabID, moduleConfiguration.ModuleID, moduleConfiguration.ModuleControl.ControlSrc);
            return control;
        }

        /// <inheritdoc cref="IModuleControlPipeline.LoadModuleControl(IComponent,ModuleInfo)" />
        [DnnDeprecated(9, 4, 0, "This implementation has moved to DotNetNuke.ModulePipeline.ModuleControlPipeline")]
        public static partial IComponent LoadModuleControl(IComponent containerControl, ModuleInfo moduleConfiguration)
        {
            TracelLogger.ModuleControlFactoryLoadModuleControlStart(moduleConfiguration.TabID, moduleConfiguration.ModuleID, moduleConfiguration.ModuleControl.ControlSrc);

            IComponent control = null;
            IModuleControlFactory controlFactory = GetModuleControlFactory(moduleConfiguration.ModuleControl.ControlSrc);

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

            TracelLogger.ModuleControlFactoryLoadModuleControlEnd(moduleConfiguration.TabID, moduleConfiguration.ModuleID, moduleConfiguration.ModuleControl.ControlSrc);
            return control;
        }

        /// <inheritdoc cref="IModuleControlPipeline.LoadSettingsControl" />
        [DnnDeprecated(9, 4, 0, "This implementation has moved to DotNetNuke.ModulePipeline.ModuleControlPipeline")]
        public static partial IComponent LoadSettingsControl(IComponent containerControl, ModuleInfo moduleConfiguration, string controlSrc)
        {
            TracelLogger.ModuleControlFactoryLoadSettingsControlStart(moduleConfiguration.TabID, moduleConfiguration.ModuleID, moduleConfiguration.ModuleControl.ControlSrc);

            IComponent control = null;
            IModuleControlFactory controlFactory = GetModuleControlFactory(controlSrc);

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

            TracelLogger.ModuleControlFactoryLoadSettingsControlEnd(moduleConfiguration.TabID, moduleConfiguration.ModuleID, moduleConfiguration.ModuleControl.ControlSrc);
            return control;
        }

        /// <inheritdoc cref="IModuleControlPipeline.CreateCachedControl" />
        [DnnDeprecated(9, 4, 0, "This implementation has moved to DotNetNuke.ModulePipeline.ModuleControlPipeline")]
        public static partial IComponent CreateCachedControl(string cachedContent, ModuleInfo moduleConfiguration)
        {
            var moduleControl = new CachedModuleControl(cachedContent);
            moduleControl.ModuleContext.Configuration = moduleConfiguration;
            return moduleControl;
        }

        /// <inheritdoc cref="IModuleControlPipeline.CreateModuleControl" />
        [DnnDeprecated(9, 4, 0, "This implementation has moved to DotNetNuke.ModulePipeline.ModuleControlPipeline")]
        public static partial IComponent CreateModuleControl(ModuleInfo moduleConfiguration)
        {
            string extension = Path.GetExtension(moduleConfiguration.ModuleControl.ControlSrc.ToLowerInvariant());
            var moduleControl = new ModuleControlBase();
            moduleControl.ModuleContext.Configuration = moduleConfiguration;

            switch (extension)
            {
                case ".mvc":
                    var segments = moduleConfiguration.ModuleControl.ControlSrc.Split('/');

                    moduleControl.LocalResourceFile = $"~/DesktopModules/MVC/{moduleConfiguration.DesktopModule.FolderName}/{Localization.LocalResourceDirectory}/{(segments.Length == 2 ? segments[0] : segments[1])}.resx";
                    break;
                default:
                    moduleControl.LocalResourceFile = moduleConfiguration.ModuleControl.ControlSrc.Replace(Path.GetFileName(moduleConfiguration.ModuleControl.ControlSrc), string.Empty) +
                                        Localization.LocalResourceDirectory + "/" +
                                        Path.GetFileName(moduleConfiguration.ModuleControl.ControlSrc);
                    break;
            }

            return moduleControl;
        }

        /// <summary>Assigns the server side identifier of a loaded control, when it has one.</summary>
        /// <param name="control">The loaded control.</param>
        /// <param name="id">The identifier to assign.</param>
        private static void SetControlId(IComponent control, string id)
        {
            if (control is DotNetNuke.UI.Utilities.IClientApiControl treeControl && !string.IsNullOrEmpty(id))
            {
                treeControl.Id = id;
            }
        }

        [DnnDeprecated(9, 4, 0, "This implementation has moved to DotNetNuke.ModulePipeline.ModuleControlPipeline")]
        private static partial IModuleControlFactory GetModuleControlFactory(string controlSrc)
        {
            string extension = Path.GetExtension(controlSrc.ToLowerInvariant());

            IModuleControlFactory controlFactory = null;
            Type factoryType;
            switch (extension)
            {
                case ".ascx":
                    controlFactory = new WebFormsModuleControlFactory();
                    break;
                case ".html":
                case ".htm":
                    controlFactory = new Html5ModuleControlFactory();
                    break;
                case ".cshtml":
                case ".vbhtml":
                    factoryType = Reflection.CreateType("DotNetNuke.Web.Razor.RazorModuleControlFactory");
                    if (factoryType != null)
                    {
                        controlFactory = Reflection.CreateObject(factoryType) as IModuleControlFactory;
                    }

                    break;
                case ".mvc":
                    factoryType = Reflection.CreateType("DotNetNuke.Web.Mvc.MvcModuleControlFactory");
                    if (factoryType != null)
                    {
                        controlFactory = Reflection.CreateObject(factoryType) as IModuleControlFactory;
                    }

                    break;
                default:
                    controlFactory = new ReflectedModuleControlFactory();
                    break;
            }

            return controlFactory;
        }
    }
}
