// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Modules.ResourceManager
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.FileSystem.Internal;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.UI.Skins.Controls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Provides editing capabilities for a folder mapping.</summary>
    /// <remarks>
    /// Was <c>EditFolderMapping.ascx</c> + <c>EditFolderMapping.ascx.cs</c> +
    /// <c>EditFolderMapping.ascx.designer.cs</c>; the markup now lives in
    /// <c>EditFolderMapping.razor</c>. Every designer control has an equivalent here: the text box
    /// and the drop-down became <see cref="MappingName"/> / <see cref="SelectedFolderProviderType"/>
    /// with their <c>@onchange</c> handlers, the two required-field validators became
    /// <see cref="nameError"/> / <see cref="folderProviderError"/> filled by <see cref="Validate"/>,
    /// and <c>ProviderSettingsPlaceHolder</c> became the <c>DynamicComponent</c> captured in
    /// <see cref="settingsControlComponent"/>.
    /// <para>
    /// <c>HttpContext</c> is <see langword="null"/> once the circuit is interactive, so every
    /// request-derived value (the <c>ItemID</c> query string, <c>IsHostMenu</c>, the portal id and
    /// the user's roles) is read in <see cref="OnInit"/> and kept in a field.
    /// </para>
    /// </remarks>
    public partial class EditFolderMapping : PortalModuleBase
    {
        private readonly INavigationManager navigationManager;
        private readonly IFolderMappingController folderMappingController = FolderMappingController.Instance;
        private readonly IModuleControlPipeline moduleControlPipeline;

        /// <summary>The folder provider list items, which replace <c>FolderProvidersComboBox.Items</c>.</summary>
        private readonly List<DnnListItem> folderProviderItems = new List<DnnListItem>();

        private int folderMappingID = Null.NullInteger;

        /// <summary>The <c>ItemID</c> query string value, captured while the request is in flight.</summary>
        private string capturedItemId;

        /// <summary>
        /// The portal id the folder mapping belongs to, captured in <see cref="OnInit"/> because
        /// both <c>IsHostMenu</c> and <c>PortalId</c> are resolved from the request's portal
        /// settings, which do not exist on an interactive render.
        /// </summary>
        private int capturedFolderPortalID = Null.NullInteger;

        /// <summary>The settings component rendered in place of <c>ProviderSettingsPlaceHolder</c>.</summary>
        private DynamicComponent settingsControlComponent;
        private Type settingsControlType;
        private IDictionary<string, object> settingsControlParameters;

        private string nameError;
        private string folderProviderError;

        /// <summary>Replaces the <c>!IsPostBack</c> guard of the WebForms <c>OnLoad</c>.</summary>
        private bool initialBindDone;

        /// <summary>Whether the existing mapping's settings were already pushed into the settings component.</summary>
        private bool providerSettingsLoaded;

        /// <summary>Replaces <c>Response.IsRequestBeingRedirected</c>.</summary>
        private bool redirected;

        /// <summary>Initializes a new instance of the <see cref="EditFolderMapping"/> class.</summary>
        public EditFolderMapping()
        {
            this.navigationManager = this.DependencyProvider.GetRequiredService<INavigationManager>();
            this.moduleControlPipeline = this.DependencyProvider.GetRequiredService<IModuleControlPipeline>();
        }

        /// <summary>Gets the portalId for the folder.</summary>
        public int FolderPortalID
        {
            get
            {
                return this.capturedFolderPortalID;
            }
        }

        /// <summary>Gets the id of the folder mapping.</summary>
        public int FolderMappingID
        {
            get
            {
                if (this.folderMappingID == Null.NullInteger)
                {
                    if (!string.IsNullOrEmpty(this.capturedItemId))
                    {
                        int.TryParse(this.capturedItemId, out this.folderMappingID);
                    }
                }

                return this.folderMappingID;
            }
        }

        /// <summary>Gets or sets the navigation manager used for the redirects <c>Response.Redirect</c> issued.</summary>
        [Inject]
        protected NavigationManager Navigation { get; set; }

        /// <summary>Gets or sets the mapping name, which replaces <c>NameTextbox.Text</c>.</summary>
        private string MappingName { get; set; } = string.Empty;

        /// <summary>Gets or sets the selected provider, which replaces <c>FolderProvidersComboBox.SelectedValue</c>.</summary>
        private string SelectedFolderProviderType { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the provider select is enabled (was <c>FolderProvidersComboBox.Enabled</c>).</summary>
        private bool ProviderSelectEnabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the sync warning shows (was <c>SyncWarningPlaceHolder.Visible</c>).</summary>
        private bool SyncWarningVisible { get; set; }

        /// <summary>Gets or sets the update action's text (was <c>UpdateButton.Text</c>).</summary>
        private string UpdateButtonText { get; set; }

        /// <summary>Gets or sets the cancel action's URL (was <c>CancelHyperLink.NavigateUrl</c>).</summary>
        private string CancelUrl { get; set; }

        /// <summary>Gets the folder provider list items rendered by the provider select.</summary>
        private IReadOnlyList<DnnListItem> FolderProviderItems => this.folderProviderItems;

        /// <summary>Gets the info message text (was <c>DescriptionLabel</c>'s <c>resourcekey="Description"</c>).</summary>
        private string DescriptionText => Localization.GetString("Description.Text", this.LocalResourceFile);

        /// <summary>Gets the sync warning text (was <c>SyncLabel</c>'s <c>ResourceKey="SyncWarning"</c>).</summary>
        private string SyncWarningText => Localization.GetString("SyncWarning.Text", this.LocalResourceFile);

        /// <summary>
        /// Gets the cancel action's text (was <c>CancelHyperLink</c>'s <c>resourcekey="cmdCancel"</c>).
        /// </summary>
        /// <remarks>
        /// <c>cmdCancel</c> is not in this module's resource file; it lives in the shared resources
        /// and the WebForms localizer resolved it through the shared-resource fallback, which is
        /// what this lookup keeps doing.
        /// </remarks>
        private string CancelText => Localization.GetString("cmdCancel.Text", Localization.SharedResourceFile);

        /// <summary>Gets the rendered provider settings component, which replaces <c>ProviderSettingsPlaceHolder.Controls[0]</c>.</summary>
        private FolderMappingSettingsControlBase SettingsControl =>
            this.settingsControlComponent?.Instance as FolderMappingSettingsControlBase;

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            // Read every request-derived value here; HttpContext is null for the rest of the circuit.
            this.capturedItemId = this.GetQueryValue("ItemID");

            // IsHostMenu read PortalSettings.ActiveTab.TabID - this control's own tab - and PortalId
            // read PortalSettings.PortalId. Both of those go through PortalSettings.Current, which is
            // null on an interactive circuit. TabId falls back to the module configuration and
            // ModuleConfiguration is a Blazor parameter, so these two reads mean the same thing while
            // also surviving onto the interactive instance.
            this.capturedFolderPortalID = Globals.IsHostTab(this.TabId)
                ? Null.NullInteger
                : this.ModuleConfiguration.PortalID;

            // The access gate needs the request's portal settings (the user and the administrator
            // role name), which exist only while a request is in flight - i.e. on the initial static
            // render, which runs first and navigates away before an interactive instance is ever
            // created. On the interactive pass PortalSettings.Current is null, so re-evaluating the
            // gate would NullReference; skipping it there is safe precisely because the static pass
            // already enforced it.
            if (this.PortalSettings != null
                && !this.UserInfo.IsSuperUser
                && !this.UserInfo.IsInRole(this.PortalSettings.AdministratorRoleName))
            {
                // Replaces Response.Redirect(Globals.AccessDeniedURL(), true).
                this.redirected = true;
                this.Navigation.NavigateTo(Globals.AccessDeniedURL(), forceLoad: true);
                return;
            }

            this.UpdateButtonText = (this.FolderMappingID == Null.NullInteger) ? Localization.GetString("Add") : Localization.GetString("Update", this.LocalResourceFile);
            this.CancelUrl = this.navigationManager.NavigateURL();

            var controlTitle = Localization.GetString("ControlTitle", this.LocalResourceFile);
            var controlTitlePrefix = (this.FolderMappingID == Null.NullInteger) ? Localization.GetString("New") : Localization.GetString("Edit");

            this.SyncWarningVisible = this.FolderMappingID != Null.NullInteger;

            this.ModuleConfiguration.ModuleControl.ControlTitle = string.Format(controlTitle, controlTitlePrefix);
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // UpdateButton.Click += this.CmdUpdate_Click is now the @onclick binding in the markup.
            try
            {
                this.BindFolderMappingSettings();

                if (!this.initialBindDone)
                {
                    this.initialBindDone = true;

                    this.BindFolderProviders();

                    if (this.FolderMappingID != Null.NullInteger)
                    {
                        this.BindFolderMapping();
                    }
                }
            }
            catch (Exception exc)
            {
                DotNetNuke.Services.Exceptions.Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// The provider settings component is a child component, so it only exists once this
        /// component has rendered. This is where the WebForms
        /// <c>ProviderSettingsPlaceHolder.Controls[0].LoadSettings(folderMapping.FolderMappingSettings)</c>
        /// call of the <c>!IsPostBack</c> branch now happens.
        /// </remarks>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            try
            {
                if (this.providerSettingsLoaded || this.FolderMappingID == Null.NullInteger)
                {
                    return;
                }

                var settingsControl = this.SettingsControl;
                if (settingsControl == null)
                {
                    return;
                }

                this.providerSettingsLoaded = true;

                var folderMapping = this.folderMappingController.GetFolderMapping(this.FolderMappingID);
                settingsControl.LoadSettings(folderMapping.FolderMappingSettings);

                this.StateHasChanged();
            }
            catch (Exception exc)
            {
                DotNetNuke.Services.Exceptions.Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Handles the folder providers selection change.</summary>
        /// <param name="e">The change event arguments.</param>
        /// <remarks>
        /// Replaces <c>CboFolderProviders_SelectedIndexChanged</c>; the drop-down's
        /// <c>AutoPostBack</c> round trip is now the circuit's change event.
        /// </remarks>
        protected void CboFolderProviders_SelectedIndexChanged(ChangeEventArgs e)
        {
            this.SelectedFolderProviderType = e.Value?.ToString() ?? string.Empty;
            this.BindFolderMappingSettings();
        }

        private static bool IsSettingsControlType(Type type)
        {
            return type != null && typeof(FolderMappingSettingsControlBase).IsAssignableFrom(type);
        }

        /// <summary>Reads a query string value, from the captured request when there is one and from
        /// the current URL otherwise (an interactive circuit has no <c>HttpContext</c>).</summary>
        /// <param name="name">The query string key.</param>
        /// <returns>The value, or <see langword="null"/> when the key is absent - the same
        /// <c>Request.QueryString[key]</c> result the WebForms control relied on.</returns>
        /// <remarks>
        /// Without the URL fallback the <c>ItemID</c> would be lost on the interactive instance,
        /// <see cref="FolderMappingID"/> would read <see cref="Null.NullInteger"/> and
        /// <see cref="CmdUpdate_Click"/> would add a duplicate mapping instead of updating the one
        /// being edited. Mirrors <c>ModuleSettings.razor.cs</c>.
        /// </remarks>
        private string GetQueryValue(string name)
        {
            var request = this.Request;
            if (request != null && request.Query.ContainsKey(name))
            {
                return request.Query[name].ToString();
            }

            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(this.Navigation.Uri).Query);
            return query.TryGetValue(name, out var values) ? values.ToString() : null;
        }

        private void OnMappingNameChanged(ChangeEventArgs e)
        {
            this.MappingName = e.Value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Validates the two required fields, replacing
        /// <c>Page.Validate("vgEditFolderMapping")</c> plus <c>Page.IsValid</c>.
        /// </summary>
        /// <returns><see langword="true"/> when both fields are valid.</returns>
        private bool Validate()
        {
            this.nameError = null;
            this.folderProviderError = null;

            if (string.IsNullOrEmpty(this.MappingName))
            {
                this.nameError = Localization.GetString("NameValidator.ErrorMessage", this.LocalResourceFile);
            }

            // The validator's InitialValue was the empty first list item.
            if (string.IsNullOrEmpty(this.SelectedFolderProviderType))
            {
                this.folderProviderError = Localization.GetString("FolderProviderValidator.ErrorMessage", this.LocalResourceFile);
            }

            return string.IsNullOrEmpty(this.nameError) && string.IsNullOrEmpty(this.folderProviderError);
        }

        private void CmdUpdate_Click()
        {
            if (!this.Validate())
            {
                return;
            }

            try
            {
                var folderMapping = new FolderMappingInfo();

                if (this.FolderMappingID != Null.NullInteger)
                {
                    folderMapping = this.folderMappingController.GetFolderMapping(this.FolderMappingID) ?? new FolderMappingInfo();
                }

                folderMapping.FolderMappingID = this.FolderMappingID;
                folderMapping.MappingName = this.MappingName;
                folderMapping.FolderProviderType = this.SelectedFolderProviderType;
                folderMapping.PortalID = this.FolderPortalID;

                var originalSettings = folderMapping.FolderMappingSettings;

                try
                {
                    var folderMappingID = this.FolderMappingID;

                    if (folderMappingID == Null.NullInteger)
                    {
                        folderMappingID = this.folderMappingController.AddFolderMapping(folderMapping);
                    }
                    else
                    {
                        this.folderMappingController.UpdateFolderMapping(folderMapping);
                    }

                    var settingsControl = this.SettingsControl;
                    if (settingsControl != null)
                    {
                        try
                        {
                            settingsControl.UpdateSettings(folderMappingID);
                        }
                        catch
                        {
                            if (this.FolderMappingID == Null.NullInteger)
                            {
                                this.folderMappingController.DeleteFolderMapping(this.FolderPortalID, folderMappingID);
                            }

                            return;
                        }
                    }

                    if (this.FolderMappingID != Null.NullInteger)
                    {
                        // Check if some setting has changed
                        var updatedSettings = this.folderMappingController.GetFolderMappingSettings(this.FolderMappingID);

                        if (originalSettings.Keys.Cast<object>().Any(key => updatedSettings.ContainsKey(key) && !originalSettings[key].ToString().Equals(updatedSettings[key].ToString())))
                        {
                            // Re-synchronize folders using the existing mapping. It's important to synchronize them in descending order
                            var folders = FolderManager.Instance.GetFolders(this.FolderPortalID).Where(f => f.FolderMappingID == this.FolderMappingID).OrderByDescending(f => f.FolderPath);

                            foreach (var folder in folders)
                            {
                                FolderManager.Instance.Synchronize(this.FolderPortalID, folder.FolderPath, false, true);
                            }
                        }
                    }
                }
                catch
                {
                    DotNetNuke.UI.Skins.Skin.AddModuleMessage(this, Localization.GetString("DuplicateMappingName", this.LocalResourceFile), ModuleMessage.ModuleMessageType.RedError);
                    return;
                }

                if (!this.redirected)
                {
                    this.redirected = true;
                    this.Navigation.NavigateTo(this.navigationManager.NavigateURL(), forceLoad: true);
                }
            }
            catch (Exception exc)
            {
                DotNetNuke.Services.Exceptions.Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        private void BindFolderProviders()
        {
            var defaultProviders = DefaultFolderProviders.GetDefaultProviders();

            foreach (var provider in FolderProvider.GetProviderList().Keys.Where(provider => !defaultProviders.Contains(provider)).OrderBy(provider => provider))
            {
                this.folderProviderItems.Add(new DnnListItem(provider, provider));
            }

            this.folderProviderItems.Insert(0, new DnnListItem(string.Empty, string.Empty));
        }

        private void BindFolderMapping()
        {
            var folderMapping = this.folderMappingController.GetFolderMapping(this.FolderMappingID);

            this.MappingName = folderMapping.MappingName;

            this.SelectedFolderProviderType = folderMapping.FolderProviderType;
            this.ProviderSelectEnabled = false;
        }

        /// <summary>
        /// Resolves and renders the folder provider's own settings UI, replacing
        /// <c>LoadControl(virtualPath)</c> plus <c>ProviderSettingsPlaceHolder.Controls.Add</c>.
        /// </summary>
        /// <remarks>
        /// The settings UI is resolved by TYPE first, through
        /// <see cref="FolderProvider.GetSettingsControlType"/>, because a migrated provider ships
        /// its settings UI as a Blazor component compiled into its own assembly (there is no
        /// deployed <c>Settings.ascx</c> file to find any more). The file-based
        /// <see cref="FolderProvider.GetSettingsControlVirtualPath"/> lookup is kept as the
        /// fallback so third-party providers that still deploy a <c>Settings.ascx</c> keep working.
        /// Both paths end at a type plus its parameters, which the renderer instantiates through
        /// <c>DynamicComponent</c>; the instance is reachable as <see cref="SettingsControl"/>, so
        /// <c>LoadSettings</c>/<c>UpdateSettings</c> are still invoked on the real component.
        /// </remarks>
        private void BindFolderMappingSettings()
        {
            string folderProviderType;

            if (this.FolderMappingID != Null.NullInteger)
            {
                var folderMapping = this.folderMappingController.GetFolderMapping(this.FolderMappingID);
                folderProviderType = folderMapping.FolderProviderType;
            }
            else
            {
                folderProviderType = this.SelectedFolderProviderType;
            }

            if (string.IsNullOrEmpty(folderProviderType))
            {
                this.SetSettingsControl(null, null);
                return;
            }

            var provider = FolderProvider.Instance(folderProviderType);

            // Type-based hook (the migrated providers).
            var controlType = provider.GetSettingsControlType();
            if (IsSettingsControlType(controlType))
            {
                // This is important to allow the settings component to be localizable: it was
                // settingsControl.ID = baseType.Name, which made the WebForms localizer read
                // App_LocalResources/<TypeName>.resx. PortalModuleBase.LocalResourceFile now
                // derives exactly that name from the component's own type, and ModuleConfiguration
                // is passed so its ControlPath (and therefore its resource directory) resolves.
                this.SetSettingsControl(
                    controlType,
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        [nameof(this.ModuleConfiguration)] = this.ModuleConfiguration,
                    });
                return;
            }

            // Legacy file-based fallback.
            var settingsControlVirtualPath = provider.GetSettingsControlVirtualPath();
            if (string.IsNullOrEmpty(settingsControlVirtualPath))
            {
                this.SetSettingsControl(null, null);
                return;
            }

            var settingsControl = this.moduleControlPipeline.LoadSettingsControl(this, this.ModuleConfiguration, settingsControlVirtualPath);
            if (settingsControl is not FolderMappingSettingsControlBase)
            {
                this.SetSettingsControl(null, null);
                return;
            }

            // The pipeline already set the control's id to the resource file name, which is the
            // equivalent of the original settingsControl.ID = baseType.Name assignment.
            this.SetSettingsControl(settingsControl.GetType(), DnnComponentParameters.Capture(settingsControl));
        }

        /// <summary>Swaps the rendered settings component, replacing the placeholder's <c>Controls.Clear</c>/<c>Controls.Add</c> pair.</summary>
        /// <param name="type">The settings component type, or <see langword="null"/> when the provider has no settings UI.</param>
        /// <param name="parameters">The parameters to render it with.</param>
        private void SetSettingsControl(Type type, IDictionary<string, object> parameters)
        {
            if (this.settingsControlType == type)
            {
                return;
            }

            this.settingsControlType = type;
            this.settingsControlParameters = parameters;

            // A different component instance will be created, so its settings must be loaded again.
            this.providerSettingsLoaded = false;
        }
    }
}
