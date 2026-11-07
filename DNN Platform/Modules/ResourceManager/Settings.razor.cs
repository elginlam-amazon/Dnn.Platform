// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.Modules.ResourceManager
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Dnn.Modules.ResourceManager.Components.Common;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.UI;
    using DotNetNuke.Web.UI.WebControls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    using Constants = Dnn.Modules.ResourceManager.Components.Constants;
    using DnnExceptions = DotNetNuke.Services.Exceptions.Exceptions;

    /// <summary>Provides module settings control.</summary>
    /// <remarks>
    /// Was <c>Settings.ascx</c> + <c>Settings.ascx.cs</c> + <c>Settings.ascx.designer.cs</c>; the
    /// markup now lives in <c>Settings.razor</c>. The control is still a
    /// <see cref="ModuleSettingsBase"/> (an <c>ISettingsControl</c>), so the module settings host
    /// keeps calling <see cref="LoadSettings"/> and <see cref="UpdateSettings"/> exactly as before.
    /// The designer file's two <c>UserControl</c> labels became <c>LabelControl</c> components,
    /// <c>ddlMode</c> became a plain <c>select</c> bound to <see cref="SelectedMode"/> and
    /// <c>ddlFolder</c> became the migrated <see cref="DnnFolderDropDownList"/> component captured
    /// in <see cref="ddlFolder"/>.
    /// </remarks>
    public partial class Settings : ModuleSettingsBase
    {
        private readonly IModuleController moduleController;

        /// <summary>
        /// The mode list items, which replace <c>ddlMode.Items</c>.
        /// </summary>
        private readonly List<DnnListItem> modeItems = new List<DnnListItem>();

        /// <summary>The folder picker, which replaces the designer's <c>ddlFolder</c> field.</summary>
        private DnnFolderDropDownList ddlFolder;

        /// <summary>
        /// The home folder resolved by <see cref="LoadSettings"/>. The picker is a child
        /// component, so it only exists once this component has rendered; the selection is
        /// therefore applied in <see cref="OnPreRender"/> (the phase that was <c>PreRender</c>).
        /// </summary>
        private IFolderInfo homeFolder;

        /// <summary>
        /// Whether <see cref="LoadSettings"/> already ran. Replaces the <c>Page.IsPostBack</c>
        /// early return: the settings must be read from the database once per instance, not on
        /// every subsequent render of the circuit.
        /// </summary>
        private bool settingsLoaded;

        /// <summary>Initializes a new instance of the <see cref="Settings"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IModuleController. Scheduled removal in v12.0.0.")]
        public Settings()
            : this(HttpContextSource.Current.GetScope().ServiceProvider.GetRequiredService<IModuleController>())
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Settings"/> class.</summary>
        /// <param name="moduleController">The module controller.</param>
        public Settings(IModuleController moduleController)
        {
            this.moduleController = moduleController ?? HttpContextSource.Current.GetScope().ServiceProvider.GetRequiredService<IModuleController>();
        }

        /// <summary>Gets the mode list items rendered by the <c>ddlMode</c> select element.</summary>
        private IReadOnlyList<DnnListItem> ModeItems => this.modeItems;

        /// <summary>Gets or sets the selected mode, which replaces <c>ddlMode.SelectedValue</c>.</summary>
        private string SelectedMode { get; set; }

        /// <summary>Gets or sets the folder picker reference captured by <c>@ref</c>.</summary>
        /// <remarks>
        /// The selection is applied here rather than in <see cref="OnPreRender"/> on purpose.
        /// <c>@ref</c> is assigned while the render batch is still being produced, i.e. before any
        /// <c>OnAfterRender</c> runs, so this is the last moment at which the selection is still
        /// visible to the picker's own <c>OnPreRender</c> - and that is what computes
        /// <c>ExpandPath</c> so the client-side tree opens on and scrolls to the selected folder.
        /// Assigning from the parent's <see cref="OnPreRender"/> would run after the child's and
        /// the tree would stay collapsed, which is a behaviour change from WebForms, where
        /// <c>LoadSettings</c> ran in <c>Load</c> - well before the child's <c>PreRender</c>.
        /// </remarks>
        private DnnFolderDropDownList FolderPicker
        {
            get => this.ddlFolder;

            set
            {
                this.ddlFolder = value;

                if (value != null && this.homeFolder != null)
                {
                    value.SelectedFolder = this.homeFolder;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether <see cref="LoadSettings"/> has run, which is what gates
        /// rendering the folder picker so that <see cref="FolderPicker"/> can apply the selection
        /// as the reference is captured.
        /// </summary>
        private bool SettingsLoaded => this.settingsLoaded;

        /// <inheritdoc />
        public override void LoadSettings()
        {
            try
            {
                if (this.settingsLoaded)
                {
                    return;
                }

                this.settingsLoaded = true;

                var displayTypesValues = Enum.GetValues(typeof(Constants.ModuleModes)).Cast<Constants.ModuleModes>();
                var displayTypes = displayTypesValues
                    .Select(t => new DnnListItem(Utils.GetEnumDescription(t), ((int)t).ToString(CultureInfo.InvariantCulture)))
                    .ToArray();

                this.modeItems.AddRange(displayTypes);

                this.SelectedMode = this.Settings.Contains(Constants.ModeSettingName)
                    ? this.Settings[Constants.ModeSettingName].ToString()
                    : Constants.DefaultMode.ToString(CultureInfo.InvariantCulture);

                IFolderInfo homeFolder = null;
                if (this.Settings.Contains(Constants.HomeFolderSettingName))
                {
                    int.TryParse(this.Settings[Constants.HomeFolderSettingName].ToString(), out var homeFolderId);
                    homeFolder = FolderManager.Instance.GetFolder(homeFolderId);
                }

                if (homeFolder == null)
                {
                    homeFolder = FolderManager.Instance.GetFolder(this.PortalId, string.Empty);
                }

                this.homeFolder = homeFolder;

                if (this.ddlFolder != null)
                {
                    this.ddlFolder.SelectedFolder = homeFolder;
                }
            }
            catch (Exception exc)
            {
                DnnExceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        public override void UpdateSettings()
        {
            try
            {
                this.moduleController.UpdateModuleSetting(this.ModuleId, Constants.ModeSettingName, this.SelectedMode);
                this.moduleController.UpdateModuleSetting(this.ModuleId, Constants.HomeFolderSettingName, this.ddlFolder.SelectedFolder.FolderID.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception exc)
            {
                DnnExceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// A safety net only. The selection is normally applied by <see cref="FolderPicker"/> as
        /// the <c>@ref</c> is captured (see the remarks there); this covers the case where
        /// <c>LoadSettings</c> resolves the home folder only after the picker already rendered, in
        /// which case the value is still selected even though the tree will not auto-expand.
        /// </remarks>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (this.ddlFolder == null || this.homeFolder == null)
            {
                return;
            }

            if (this.ddlFolder.SelectedFolder?.FolderID == this.homeFolder.FolderID)
            {
                return;
            }

            this.ddlFolder.SelectedFolder = this.homeFolder;
            this.StateHasChanged();
        }

        /// <summary>Keeps the selected mode in step with the <c>ddlMode</c> select element.</summary>
        /// <param name="e">The change event arguments.</param>
        private void OnModeChanged(ChangeEventArgs e)
        {
            this.SelectedMode = e.Value?.ToString() ?? Constants.DefaultMode.ToString(CultureInfo.InvariantCulture);
        }
    }
}
