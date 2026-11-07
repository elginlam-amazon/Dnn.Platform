// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.RazorHost
{
    using System;

    using DotNetNuke.Common;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Security;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Extension methods for the razor host settings.</summary>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public static partial class RazorHostSettingsExtensions
    {
        /// <summary>Loads the razor host settings control.</summary>
        /// <param name="parent">The parent component hosting the settings control.</param>
        /// <param name="configuration">The module configuration, <see cref="ModuleInfo"/>.</param>
        /// <param name="localResourceFile">The local resource file for localization strings.</param>
        /// <returns>Razor Host Settings, <see cref="Settings"/>.</returns>
        /// <remarks>
        /// On ASP.NET this took a <c>System.Web.UI.UserControl</c> and called
        /// <c>parent.LoadControl("~/DesktopModules/RazorModules/RazorHost/Settings.ascx")</c>. Both are
        /// gone: <see cref="Settings"/> is a Blazor component compiled into this assembly, so it is
        /// created the way the migrated <c>IModuleControlFactory</c>/<c>BaseModuleControlFactory</c>
        /// surface creates settings components — through the request's service provider, so that any
        /// injected constructor dependency is satisfied — and then assigned its
        /// <c>ModuleConfiguration</c> and <c>LocalResourceFile</c> exactly as before. The parent is
        /// kept in the signature (as the component contract <see cref="IComponent"/>) because it is
        /// the hosting component the returned instance is rendered inside of; it is no longer the
        /// loader.
        /// </remarks>
        public static Settings LoadRazorSettingsControl(this IComponent parent, ModuleInfo configuration, string localResourceFile)
        {
            ArgumentNullException.ThrowIfNull(parent);
            ArgumentNullException.ThrowIfNull(configuration);

            var serviceProvider = Globals.GetCurrentServiceProvider();
            var control = serviceProvider is null
                ? new Settings()
                : ActivatorUtilities.CreateInstance<Settings>(serviceProvider);

            control.ModuleConfiguration = configuration;
            control.LocalResourceFile = localResourceFile;
            EnsureEditScriptControlIsRegistered(configuration.ModuleDefID);
            return control;
        }

        private static void EnsureEditScriptControlIsRegistered(int moduleDefId)
        {
            if (ModuleControlController.GetModuleControlByControlKey("EditRazorScript", moduleDefId) != null)
            {
                return;
            }

            var m = new ModuleControlInfo
            {
                ControlKey = "EditRazorScript",
                ControlSrc = "DesktopModules/RazorModules/RazorHost/EditScript.razor",
                ControlTitle = "Edit Script",
                ControlType = SecurityAccessLevel.Host,
                ModuleDefID = moduleDefId,
            };
            ModuleControlController.UpdateModuleControl(m);
        }
    }
}
