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
    using DotNetNuke.Entities.Modules.Definitions;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.FileSystem.Internal;
    using DotNetNuke.Services.Installer;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.UI.Skins.Controls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>Implements the logic for the CreateModule view.</summary>
    /// <remarks>
    /// Was <c>CreateModule.ascx</c> + <c>CreateModule.ascx.cs</c> +
    /// <c>CreateModule.ascx.designer.cs</c>. The markup is <c>CreateModule.razor</c>; the
    /// designer-generated fields are the labels and action links the markup declares plus the bound
    /// values below.
    /// </remarks>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public partial class CreateModule : ModuleUserControlBase
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<CreateModule>();
        private readonly INavigationManager navigationManager;

        private readonly string razorScriptFileFormatString = "~/DesktopModules/RazorModules/RazorHost/Scripts/{0}";
        private readonly string razorScriptFolder = "~/DesktopModules/RazorModules/RazorHost/Scripts/";
        private bool scriptsLoaded;
        private bool validationAttempted;

        /// <summary>Initializes a new instance of the <see cref="CreateModule"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.0. Please use overload with INavigationManager. Scheduled removal in v12.0.0.")]
        public CreateModule()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="CreateModule"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        public CreateModule(INavigationManager navigationManager)
        {
            this.navigationManager = navigationManager ?? Globals.DependencyProvider.GetRequiredService<INavigationManager>();
        }

        /// <summary>Gets the file name of the module control registered for the generated module.</summary>
        /// <remarks>
        /// On ASP.NET this was <c>&lt;script name&gt;.ascx</c>: a stub user control whose only content
        /// was a <c>&lt;%@ Control Inherits="DotNetNuke.Web.Razor.RazorModuleBase" %&gt;</c> directive,
        /// written next to the copied script. ASP.NET Core cannot parse or load an <c>.ascx</c>, so
        /// that indirection is gone: the migrated host's
        /// <c>RazorModuleControlFactory.SupportsControl</c> accepts a <c>.cshtml</c>/<c>.vbhtml</c>
        /// <c>controlSrc</c> directly and renders it through <c>RazorHostControl</c>. The generated
        /// module therefore points its <c>controlSrc</c> straight at the copied razor script, and this
        /// property returns that script's file name so <c>lblModuleControl</c>, the generated manifest
        /// and the registered module control all agree.
        /// </remarks>
        protected string ModuleControl
        {
            get
            {
                return Path.GetFileName(this.SelectedScript ?? string.Empty);
            }
        }

        /// <summary>Gets the razor script path.</summary>
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

        /// <summary>Gets the localized description of the source script.</summary>
        /// <remarks>Replaces <c>lblSourceFile.Text</c>.</remarks>
        protected string SourceFileText { get; private set; } = string.Empty;

        /// <summary>Gets the localized description of the generated module control.</summary>
        /// <remarks>Replaces <c>lblModuleControl.Text</c>.</remarks>
        protected string ModuleControlText { get; private set; } = string.Empty;

        /// <summary>Gets the target folder typed for the new module.</summary>
        /// <remarks>Replaces <c>txtFolder.Text</c>.</remarks>
        protected string Folder { get; private set; } = string.Empty;

        /// <summary>Gets the name typed for the new module.</summary>
        /// <remarks>Replaces <c>txtName.Text</c>.</remarks>
        protected string Name { get; private set; } = string.Empty;

        /// <summary>Gets the description typed for the new module.</summary>
        /// <remarks>Replaces <c>txtDescription.Text</c>.</remarks>
        protected string Description { get; private set; } = string.Empty;

        /// <summary>Gets a value indicating whether a test page should be created.</summary>
        /// <remarks>Replaces <c>chkAddPage.Checked</c>.</remarks>
        protected bool AddPage { get; private set; }

        /// <summary>Gets the installer log table rendered where <c>phInstallLogs</c> was.</summary>
        protected MarkupString InstallLogs { get; private set; }

        /// <summary>Gets a value indicating whether the folder passes its required-field check.</summary>
        /// <remarks>
        /// Replaces <c>valFolder</c>. The original validator was <c>Display="Dynamic"</c>, so the
        /// message only appeared once validation had run; hence the <c>validationAttempted</c> gate.
        /// </remarks>
        protected bool FolderValid => !this.validationAttempted || !string.IsNullOrWhiteSpace(this.Folder);

        /// <summary>Gets a value indicating whether the name passes its required-field check.</summary>
        /// <remarks>Replaces <c>valName</c>, see <see cref="FolderValid"/>.</remarks>
        protected bool NameValid => !this.validationAttempted || !string.IsNullOrWhiteSpace(this.Name);

        /// <summary>Gets or sets the navigation manager the redirects go through.</summary>
        /// <remarks>Replaces <c>Response.Redirect(url, true)</c>.</remarks>
        [Inject]
        protected Microsoft.AspNetCore.Components.NavigationManager Navigation { get; set; }

        /// <inheritdoc />
        /// <remarks>
        /// This is <c>OnLoad</c> of the original, including its super-user guard. The
        /// <c>!Page.IsPostBack</c> guard becomes a first-pass flag, because a Blazor component is not
        /// recreated per post back and reloading the list would discard the user's input. The event
        /// wiring of the original <c>OnInit</c> is the markup's <c>@onclick</c>/<c>@onchange</c>
        /// attributes.
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!this.ModuleContext.PortalSettings.UserInfo.IsSuperUser)
            {
                this.Navigation.NavigateTo(this.navigationManager.NavigateURL("Access Denied"), forceLoad: true);
                return;
            }

            if (!this.scriptsLoaded)
            {
                this.scriptsLoaded = true;
                this.LoadScripts();
                this.DisplayFile();
            }
        }

        private void Create()
        {
            // Create new Folder
            string folderMapPath = HttpContextSource.MapPath($"~/DesktopModules/RazorModules/{this.Folder}");
            if (Directory.Exists(folderMapPath))
            {
                UI.Skins.Skin.AddModuleMessage(this, Localization.GetString("FolderExists", this.LocalResourceFile), ModuleMessage.ModuleMessageType.RedError);
                return;
            }
            else
            {
                // Create folder
                Directory.CreateDirectory(folderMapPath);
            }

            // Copy Script to new Folder
            string scriptSourceFile = HttpContextSource.MapPath(string.Format(CultureInfo.InvariantCulture, this.razorScriptFileFormatString, this.SelectedScript));
            string scriptTargetFile = folderMapPath + "/" + this.SelectedScript;
            try
            {
                File.Copy(scriptSourceFile, scriptTargetFile);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                UI.Skins.Skin.AddModuleMessage(this, Localization.GetString("ScriptCopyError", this.LocalResourceFile), ModuleMessage.ModuleMessageType.RedError);
                return;
            }

            // Create new Manifest in target folder
            string manifestMapPath = folderMapPath + "/" + Path.GetFileNameWithoutExtension(this.ModuleControl).TrimStart('_') + ".dnn";
            try
            {
                using (var manifestWriter = new StreamWriter(manifestMapPath))
                {
                    string manifestTemplate = Localization.GetString("ManifestText.Text", this.LocalResourceFile);
                    string manifest = string.Format(CultureInfo.InvariantCulture, manifestTemplate, this.Name, this.Description, this.Folder, this.ModuleControl, this.SelectedScript);
                    manifestWriter.Write(manifest);
                    manifestWriter.Flush();
                }
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                UI.Skins.Skin.AddModuleMessage(this, Localization.GetString("ManifestCreationError", this.LocalResourceFile), ModuleMessage.ModuleMessageType.RedError);
                return;
            }

            // Register Module
            ModuleDefinitionInfo moduleDefinition = this.ImportManifest(manifestMapPath);

            // remove the manifest file
            try
            {
                FileWrapper.Instance.Delete(manifestMapPath);
            }
            catch (Exception ex)
            {
                Logger.CreateModuleDeleteFileException(ex);
            }

            // Optionally goto new Page
            if (this.AddPage)
            {
                string tabName = $"Test {this.Name} Page";
                string tabPath = Globals.GenerateTabPath(Null.NullInteger, tabName);
                int tabID = TabController.GetTabByTabPath(this.ModuleContext.PortalId, tabPath, this.ModuleContext.PortalSettings.CultureCode);

                if (tabID == Null.NullInteger)
                {
                    // Create a new page
                    var newTab = new TabInfo();
                    newTab.TabName = "Test " + this.Name + " Page";
                    newTab.ParentId = Null.NullInteger;
                    newTab.PortalID = this.ModuleContext.PortalId;
                    newTab.IsVisible = true;
                    newTab.TabID = TabController.Instance.AddTabBefore(newTab, this.ModuleContext.PortalSettings.AdminTabId);

                    var objModule = new ModuleInfo();
                    objModule.Initialize(this.ModuleContext.PortalId);

                    objModule.PortalID = this.ModuleContext.PortalId;
                    objModule.TabID = newTab.TabID;
                    objModule.ModuleOrder = Null.NullInteger;
                    objModule.ModuleTitle = moduleDefinition.FriendlyName;
                    objModule.PaneName = Globals.glbDefaultPane;
                    objModule.ModuleDefID = moduleDefinition.ModuleDefID;
                    objModule.InheritViewPermissions = true;
                    objModule.AllTabs = false;
                    ModuleController.Instance.AddModule(objModule);

                    this.Navigation.NavigateTo(this.navigationManager.NavigateURL(newTab.TabID), forceLoad: true);
                }
                else
                {
                    UI.Skins.Skin.AddModuleMessage(this, Localization.GetString("TabExists", this.LocalResourceFile), ModuleMessage.ModuleMessageType.RedError);
                }
            }
            else
            {
                // Redirect to main extensions page
                this.Navigation.NavigateTo(this.navigationManager.NavigateURL(), forceLoad: true);
            }
        }

        private ModuleDefinitionInfo ImportManifest(string manifest)
        {
            ModuleDefinitionInfo moduleDefinition = null;
            try
            {
                var installer = new Installer(manifest, HttpContextSource.MapPath("."), true);

                if (installer.IsValid)
                {
                    // Reset Log
                    installer.InstallerInfo.Log.Logs.Clear();

                    // Install
                    installer.Install();

                    if (installer.IsValid)
                    {
                        DesktopModuleInfo desktopModule = DesktopModuleController.GetDesktopModuleByPackageID(installer.InstallerInfo.PackageID);
                        if (desktopModule != null && desktopModule.ModuleDefinitions.Count > 0)
                        {
                            foreach (KeyValuePair<string, ModuleDefinitionInfo> kvp in desktopModule.ModuleDefinitions)
                            {
                                moduleDefinition = kvp.Value;
                                break;
                            }
                        }
                    }
                    else
                    {
                        UI.Skins.Skin.AddModuleMessage(this, Localization.GetString("InstallError.Text", this.LocalResourceFile), ModuleMessage.ModuleMessageType.RedError);
                        this.InstallLogs = installer.InstallerInfo.Log.GetLogsTable();
                    }
                }
                else
                {
                    UI.Skins.Skin.AddModuleMessage(this, Localization.GetString("InstallError.Text", this.LocalResourceFile), ModuleMessage.ModuleMessageType.RedError);
                    this.InstallLogs = installer.InstallerInfo.Log.GetLogsTable();
                }
            }
            catch (Exception exc)
            {
                Exceptions.LogException(exc);
                UI.Skins.Skin.AddModuleMessage(this, Localization.GetString("ImportControl.ErrorMessage", this.LocalResourceFile), ModuleMessage.ModuleMessageType.RedError);
            }

            return moduleDefinition;
        }

        private void LoadScripts()
        {
            string basePath = HttpContextSource.MapPath(this.razorScriptFolder);
            var scriptFileSetting = this.ModuleContext.Settings["ScriptFile"] as string;

            foreach (string script in Directory.GetFiles(basePath, "*.??html"))
            {
                string scriptPath = script.Replace(basePath, string.Empty);
                this.Scripts.Add(scriptPath);
                if (!string.IsNullOrEmpty(scriptFileSetting) && string.Equals(scriptPath, scriptFileSetting, StringComparison.OrdinalIgnoreCase))
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
            string scriptFile = string.Format(CultureInfo.InvariantCulture, this.razorScriptFileFormatString, this.SelectedScript);

            this.SourceFileText = string.Format(CultureInfo.CurrentCulture, Localization.GetString("SourceFile", this.LocalResourceFile), scriptFile);
            this.ModuleControlText = string.Format(CultureInfo.CurrentCulture, Localization.GetString("SourceControl", this.LocalResourceFile), this.ModuleControl);
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

        private void CmdCreate_Click()
        {
            try
            {
                if (!this.ModuleContext.PortalSettings.UserInfo.IsSuperUser)
                {
                    this.Navigation.NavigateTo(this.navigationManager.NavigateURL("Access Denied"), forceLoad: true);
                    return;
                }

                this.validationAttempted = true;
                if (this.FolderValid && this.NameValid)
                {
                    this.Create();
                }
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
            this.SelectedScript = args?.Value?.ToString();
            this.DisplayFile();
        }

        /// <summary>Handles the folder being typed into.</summary>
        /// <param name="args">The arguments carrying the new value.</param>
        private void OnFolderChanged(ChangeEventArgs args)
        {
            this.Folder = args?.Value?.ToString() ?? string.Empty;
        }

        /// <summary>Handles the name being typed into.</summary>
        /// <param name="args">The arguments carrying the new value.</param>
        private void OnNameChanged(ChangeEventArgs args)
        {
            this.Name = args?.Value?.ToString() ?? string.Empty;
        }

        /// <summary>Handles the description being typed into.</summary>
        /// <param name="args">The arguments carrying the new value.</param>
        private void OnDescriptionChanged(ChangeEventArgs args)
        {
            this.Description = args?.Value?.ToString() ?? string.Empty;
        }

        /// <summary>Handles the "add page" check box being toggled.</summary>
        /// <param name="args">The arguments carrying the new value.</param>
        private void OnAddPageChanged(ChangeEventArgs args)
        {
            this.AddPage = Convert.ToBoolean(args?.Value ?? false, CultureInfo.InvariantCulture);
        }
    }
}
