// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.Admin.Users
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Http;
    using Microsoft.JSInterop;

    /// <summary>A settings control for the profile module.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>Settings.ascx</c> + <c>Settings.ascx.cs</c> + <c>Settings.ascx.designer.cs</c>; the
    /// markup now lives in <c>Settings.razor</c>. The designer fields map as follows:
    /// <c>IncludeButton.Checked</c> is <see cref="IncludeButtonChecked"/>, <c>txtTemplate.Text</c>
    /// is <see cref="TemplateText"/> and <c>cmdLoadDefault</c> is
    /// <see cref="LoadDefaultClickAsync"/> plus the two strings
    /// <see cref="LoadDefaultToolTip"/>/<see cref="LoadDefaultConfirmText"/> that carried its
    /// <c>ToolTip</c> and its <c>ClientAPI.AddButtonConfirm</c> prompt.
    /// </para>
    /// <para>
    /// Both settings keep their original names, their original stores (the template is a
    /// <i>tab</i> module setting, the flag a module setting) and their original formats, so the
    /// <c>ViewProfile</c> module reads back exactly what it read before.
    /// </para>
    /// </remarks>
    public partial class Settings : ModuleSettingsBase
    {
        private bool settingsLoaded;

        /// <summary>Gets or sets a value indicating whether <c>IncludeButton</c> is checked.</summary>
        /// <remarks>Starts <see langword="true"/>, which is the markup's <c>Checked="True"</c>.</remarks>
        protected bool IncludeButtonChecked { get; set; } = true;

        /// <summary>Gets or sets the text of <c>txtTemplate</c>.</summary>
        protected string TemplateText { get; set; } = string.Empty;

        /// <summary>Gets the tooltip of <c>cmdLoadDefault</c> (was <c>cmdLoadDefault.ToolTip</c>).</summary>
        protected string LoadDefaultToolTip { get; private set; }

        /// <summary>Gets the confirmation prompt of <c>cmdLoadDefault</c> (was its <c>ClientAPI.AddButtonConfirm</c> text).</summary>
        protected string LoadDefaultConfirmText { get; private set; }

        /// <summary>Gets or sets the JavaScript runtime used to ask the confirmation question.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <inheritdoc />
        public override void LoadSettings()
        {
            try
            {
                // Was ClientAPI.AddButtonConfirm(cmdLoadDefault, ...) + cmdLoadDefault.ToolTip.
                this.LoadDefaultConfirmText = Localization.GetString("LoadDefault.Confirm", this.LocalResourceFile);
                this.LoadDefaultToolTip = Localization.GetString("LoadDefault.Help", this.LocalResourceFile);

                // Was `if (!this.Page.IsPostBack)`: the host calls LoadSettings() on every request
                // and UpdateSettings() later in the same one, so re-loading in between would
                // overwrite the edited values with the stored ones.
                var isPostBack = HttpMethods.IsPost(this.Request?.Method ?? HttpMethods.Get);
                if (!this.settingsLoaded && !isPostBack)
                {
                    this.settingsLoaded = true;

                    if (!string.IsNullOrEmpty((string)this.TabModuleSettings["ProfileTemplate"]))
                    {
                        this.TemplateText = (string)this.TabModuleSettings["ProfileTemplate"];
                    }

                    if (this.Settings.ContainsKey("IncludeButton"))
                    {
                        this.IncludeButtonChecked = Convert.ToBoolean(this.Settings["IncludeButton"]);
                    }
                }
            }
            catch (Exception exc)
            {
                // Module failed to load
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        public override void UpdateSettings()
        {
            try
            {
                ModuleController.Instance.UpdateTabModuleSetting(this.TabModuleId, "ProfileTemplate", this.TemplateText);
                ModuleController.Instance.UpdateTabModuleSetting(this.TabModuleId, "IncludeButton", this.IncludeButtonChecked.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception exc)
            {
                // Module failed to load
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Loads the default profile template into the editor, after the confirmation the button carried.</summary>
        /// <returns>A task.</returns>
        /// <remarks>Was <c>cmdLoadDefault_Click</c> plus the button's <c>ClientAPI.AddButtonConfirm</c> prompt.</remarks>
        protected async Task LoadDefaultClickAsync()
        {
            if (!string.IsNullOrEmpty(this.LoadDefaultConfirmText) && this.JsRuntime != null)
            {
                var confirmed = await this.JsRuntime.InvokeAsync<bool>("confirm", this.LoadDefaultConfirmText);
                if (!confirmed)
                {
                    return;
                }
            }

            this.TemplateText = Localization.GetString("DefaultTemplate", this.LocalResourceFile);
        }
    }
}
