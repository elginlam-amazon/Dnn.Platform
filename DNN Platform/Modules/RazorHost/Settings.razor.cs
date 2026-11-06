// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.RazorHost
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using DotNetNuke.Common;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Exceptions;

    using Microsoft.AspNetCore.Components;

    /// <summary>Implements the Settings view logic.</summary>
    /// <remarks>
    /// Was <c>Settings.ascx</c> + <c>Settings.ascx.cs</c> + <c>Settings.ascx.designer.cs</c>. The
    /// markup is <c>Settings.razor</c>; the two designer-generated fields are the label the markup
    /// declares (<c>scriptListLabel</c>) and the script list, whose <c>Items</c>/<c>SelectedValue</c>
    /// are <see cref="Scripts"/> and <see cref="SelectedScript"/> below.
    /// </remarks>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public partial class Settings : ModuleSettingsBase
    {
        private readonly string razorScriptFolder = "~/DesktopModules/RazorModules/RazorHost/Scripts/";
        private bool settingsLoaded;

        /// <summary>Gets the scripts offered by the script list.</summary>
        /// <remarks>Replaces <c>scriptList.Items</c>.</remarks>
        protected List<string> Scripts { get; } = new List<string>();

        /// <summary>Gets the script currently selected in the script list.</summary>
        /// <remarks>
        /// Replaces <c>scriptList.SelectedValue</c>. As with the WebForms list, when no item matches
        /// the stored setting the first item is the selected one.
        /// </remarks>
        protected string SelectedScript { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// The WebForms control filled <c>scriptList.Items</c> on every load, relying on view state
        /// being rebuilt; a Blazor component is not recreated per post back, so the list is built
        /// once (<c>settingsLoaded</c>) and keeps the user's selection afterwards.
        /// </remarks>
        public override void LoadSettings()
        {
            try
            {
                if (this.settingsLoaded)
                {
                    return;
                }

                this.settingsLoaded = true;

                string basePath = HttpContextSource.MapPath(this.razorScriptFolder);
                var scriptFileSetting = this.Settings["ScriptFile"] as string;

                foreach (string script in Directory.GetFiles(basePath, "*.??html"))
                {
                    string scriptPath = script.Replace(basePath, string.Empty);
                    this.Scripts.Add(scriptPath);
                    if (!string.IsNullOrEmpty(scriptFileSetting) && scriptPath.ToLowerInvariant() == scriptFileSetting.ToLowerInvariant())
                    {
                        this.SelectedScript = scriptPath;
                    }
                }

                if (string.IsNullOrEmpty(this.SelectedScript) && this.Scripts.Count > 0)
                {
                    this.SelectedScript = this.Scripts[0];
                }

                base.LoadSettings();
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Deliberately NOT wrapped in a try/catch: the net48 original let a failed
        /// <c>UpdateModuleSetting</c> propagate so the settings-page save pipeline aborts the save
        /// and reports it. Swallowing it here would make a failed save look successful.
        /// </remarks>
        public override void UpdateSettings()
        {
            ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ScriptFile", this.SelectedScript ?? string.Empty);
        }

        /// <summary>Handles a script being picked in the script list.</summary>
        /// <param name="args">The arguments carrying the newly selected value.</param>
        private void OnScriptListChanged(ChangeEventArgs args)
        {
            this.SelectedScript = args?.Value?.ToString();
        }
    }
}
