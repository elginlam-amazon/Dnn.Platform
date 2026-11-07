// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.RazorHost
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Implements the EditScript view logic.</summary>
    /// <remarks>
    /// Was <c>EditScript.ascx</c> + <c>EditScript.ascx.cs</c> + <c>EditScript.ascx.designer.cs</c>.
    /// The markup is <c>EditScript.razor</c>; the designer-generated fields are the labels and the
    /// action links the markup declares plus the four bound values below (<c>scriptList</c> →
    /// <see cref="Scripts"/>/<see cref="SelectedScript"/>, <c>lblSourceFile</c> →
    /// <see cref="SourceFileText"/>, <c>txtSource</c> → <see cref="Source"/>,
    /// <c>isCurrentScript</c> → <see cref="IsCurrentScript"/>).
    /// </remarks>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public partial class EditScript : ModuleUserControlBase
    {
        private readonly INavigationManager navigationManager;
        private readonly string razorScriptFileFormatString = "~/DesktopModules/RazorModules/RazorHost/Scripts/{0}";
        private readonly string razorScriptFolder = "~/DesktopModules/RazorModules/RazorHost/Scripts/";
        private bool scriptsLoaded;

        /// <summary>Initializes a new instance of the <see cref="EditScript"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.0. Please use overload with INavigationManager. Scheduled removal in v12.0.0.")]
        public EditScript()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="EditScript"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        public EditScript(INavigationManager navigationManager)
        {
            this.navigationManager = navigationManager ?? Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
        }

        /// <summary>Gets the razor script file.</summary>
        protected string RazorScriptFile
        {
            get
            {
                string razorScriptFile = Null.NullString;
                var scriptFileSetting = this.ModuleContext.Settings["ScriptFile"] as string;
                if (!string.IsNullOrEmpty(scriptFileSetting))
                {
                    razorScriptFile = string.Format(CultureInfo.InvariantCulture, this.razorScriptFileFormatString, scriptFileSetting);
                }

                return razorScriptFile;
            }
        }

        /// <summary>Gets the scripts offered by the script list.</summary>
        /// <remarks>Replaces <c>scriptList.Items</c>.</remarks>
        protected List<string> Scripts { get; } = new List<string>();

        /// <summary>Gets the script currently selected in the script list.</summary>
        /// <remarks>
        /// Replaces <c>scriptList.SelectedValue</c>; as with the WebForms list, when no item matches
        /// the stored setting the first item is the selected one.
        /// </remarks>
        protected string SelectedScript { get; private set; }

        /// <summary>Gets the localized description of the file being edited.</summary>
        /// <remarks>Replaces <c>lblSourceFile.Text</c>.</remarks>
        protected string SourceFileText { get; private set; } = string.Empty;

        /// <summary>Gets the source of the script being edited.</summary>
        /// <remarks>Replaces <c>txtSource.Text</c>.</remarks>
        protected string Source { get; private set; } = string.Empty;

        /// <summary>Gets a value indicating whether the edited script is the module's current script.</summary>
        /// <remarks>Replaces <c>isCurrentScript.Checked</c>.</remarks>
        protected bool IsCurrentScript { get; private set; }

        /// <summary>Gets or sets the navigation manager the redirects go through.</summary>
        /// <remarks>Replaces <c>Response.Redirect(url, true)</c>.</remarks>
        [Inject]
        protected Microsoft.AspNetCore.Components.NavigationManager Navigation { get; set; }

        /// <inheritdoc />
        /// <remarks>
        /// This is <c>OnLoad</c> of the original, whose <c>!Page.IsPostBack</c> guard becomes a
        /// first-pass flag: a Blazor component is not recreated per post back, so reloading the
        /// script list and re-reading the file on every pass would discard the user's edits.
        /// The event wiring of the original <c>OnInit</c> is the markup's <c>@onclick</c>/
        /// <c>@onchange</c> attributes.
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                if (!this.scriptsLoaded)
                {
                    this.scriptsLoaded = true;
                    this.LoadScripts();
                    this.DisplayFile();
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        private void LoadScripts()
        {
            string basePath = HttpContextSource.MapPath(this.razorScriptFolder);
            var scriptFileSetting = this.ModuleContext.Settings["ScriptFile"] as string;

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
        }

        private void DisplayFile()
        {
            if (string.IsNullOrEmpty(this.SelectedScript))
            {
                return;
            }

            var scriptFileSetting = this.ModuleContext.Settings["ScriptFile"] as string;
            string scriptFile = string.Format(CultureInfo.InvariantCulture, this.razorScriptFileFormatString, this.SelectedScript);
            string srcFile = HttpContextSource.MapPath(scriptFile);

            this.SourceFileText = string.Format(CultureInfo.CurrentCulture, Localization.GetString("SourceFile", this.LocalResourceFile), scriptFile);

            using (var objStreamReader = File.OpenText(srcFile))
            {
                this.Source = objStreamReader.ReadToEnd();
            }

            if (!string.IsNullOrEmpty(scriptFileSetting))
            {
                this.IsCurrentScript = this.SelectedScript.ToLowerInvariant() == scriptFileSetting.ToLowerInvariant();
            }
        }

        private void SaveScript()
        {
            string srcFile = HttpContextSource.MapPath(string.Format(CultureInfo.InvariantCulture, this.razorScriptFileFormatString, this.SelectedScript));

            // write file
            using (var objStream = File.CreateText(srcFile))
            {
                objStream.WriteLine(this.Source);
            }

            if (this.IsCurrentScript)
            {
                // Update setting
                ModuleController.Instance.UpdateModuleSetting(this.ModuleContext.ModuleId, "ScriptFile", this.SelectedScript);
            }
        }

        private void CmdCancel_Click()
        {
            try
            {
                this.Navigation.NavigateTo(this.navigationManager.NavigateURL(), forceLoad: true);
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        private void CmdSave_Click()
        {
            try
            {
                this.SaveScript();
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        private void CmdSaveClose_Click()
        {
            try
            {
                this.SaveScript();
                this.Navigation.NavigateTo(this.navigationManager.NavigateURL(), forceLoad: true);
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        private void CmdAdd_Click()
        {
            try
            {
                this.Navigation.NavigateTo(this.ModuleContext.EditUrl("Add"), forceLoad: true);
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Handles a script being picked in the script list, which was an AutoPostBack.</summary>
        /// <param name="args">The arguments carrying the newly selected value.</param>
        private void ScriptList_SelectedIndexChanged(ChangeEventArgs args)
        {
            try
            {
                this.SelectedScript = args?.Value?.ToString();
                this.DisplayFile();
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Handles the source being typed into.</summary>
        /// <param name="args">The arguments carrying the new source.</param>
        private void OnSourceChanged(ChangeEventArgs args)
        {
            this.Source = args?.Value?.ToString() ?? string.Empty;
        }

        /// <summary>Handles the "current script" check box being toggled.</summary>
        /// <param name="args">The arguments carrying the new value.</param>
        private void OnIsCurrentScriptChanged(ChangeEventArgs args)
        {
            this.IsCurrentScript = Convert.ToBoolean(args?.Value ?? false, CultureInfo.InvariantCulture);
        }
    }
}
