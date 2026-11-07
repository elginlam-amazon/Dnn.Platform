// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Html
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Modules.Html.Components;
    using DotNetNuke.Services.Exceptions;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The Settings ModuleSettingsBase is used to manage the settings for the HTML Module.</summary>
    /// <remarks>
    /// Was <c>Settings.ascx</c> + <c>Settings.ascx.cs</c> + <c>Settings.ascx.designer.cs</c>; the
    /// markup is <c>Settings.razor</c> and the designer-generated fields are the three bound values
    /// below (<c>chkReplaceTokens</c> → <see cref="ReplaceTokens"/>, <c>cbDecorate</c> →
    /// <see cref="UseDecorate"/>, <c>txtSearchDescLength</c> →
    /// <see cref="SearchDescLengthText"/>) plus the labels and the validator, which the markup
    /// declares.
    /// </remarks>
    public partial class Settings : ModuleSettingsBase
    {
        /// <summary>The ValidationExpression of the original RegularExpressionValidator.</summary>
        private static readonly Regex DigitsOnly = new Regex(@"^\d+$", RegexOptions.CultureInvariant);

        private readonly HtmlModuleSettingsRepository settingsRepository;
        private HtmlModuleSettings moduleSettings;
        private bool settingsLoaded;

        /// <summary>Initializes a new instance of the <see cref="Settings"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with HtmlModuleSettingsRepository. Scheduled removal in v12.0.0.")]
        public Settings()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Settings"/> class.</summary>
        /// <param name="settingsRepository">The settings repository.</param>
        public Settings(HtmlModuleSettingsRepository settingsRepository)
        {
            this.settingsRepository = settingsRepository ?? this.DependencyProvider.GetRequiredService<HtmlModuleSettingsRepository>();
        }

        /// <summary>Gets a value indicating whether the token replacement check box is checked.</summary>
        /// <remarks>Replaces <c>chkReplaceTokens.Checked</c>.</remarks>
        protected bool ReplaceTokens { get; private set; }

        /// <summary>Gets a value indicating whether the form decoration check box is checked.</summary>
        /// <remarks>Replaces <c>cbDecorate.Checked</c>.</remarks>
        protected bool UseDecorate { get; private set; }

        /// <summary>Gets the text of the search description length box.</summary>
        /// <remarks>Replaces <c>txtSearchDescLength.Text</c>.</remarks>
        protected string SearchDescLengthText { get; private set; } = string.Empty;

        /// <summary>Gets a value indicating whether the search description length is a number.</summary>
        /// <remarks>
        /// Replaces <c>RegularExpressionValidator1.IsValid</c>; the expression is the original
        /// <c>^\d+$</c>. An empty box is valid, as it was for the WebForms validator (which did not
        /// validate an empty value - that was the job of a RequiredFieldValidator, and the original
        /// had none).
        /// </remarks>
        protected bool IsSearchDescLengthValid =>
            string.IsNullOrEmpty(this.SearchDescLengthText) || DigitsOnly.IsMatch(this.SearchDescLengthText);

        /// <summary>Gets or sets the client resource controller the stylesheet is registered on.</summary>
        [Inject]
        private IClientResourceController ClientResourceController { get; set; }

        private new HtmlModuleSettings ModuleSettings => this.moduleSettings ??= this.settingsRepository.GetSettings(this.ModuleConfiguration);

        /// <summary>LoadSettings loads the settings from the Database and displays them.</summary>
        /// <remarks>
        /// The <c>!Page.IsPostBack</c> guard of the original becomes a first-pass guard: a Blazor
        /// component is not recreated per post back, so the inputs keep whatever the user typed.
        /// </remarks>
        public override void LoadSettings()
        {
            try
            {
                if (!this.settingsLoaded)
                {
                    this.settingsLoaded = true;
                    this.ReplaceTokens = this.ModuleSettings.ReplaceTokens;
                    this.UseDecorate = this.ModuleSettings.UseDecorate;
                    this.SearchDescLengthText = this.ModuleSettings.SearchDescLength.ToString(CultureInfo.CurrentCulture);
                }

                // Module failed to load
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>UpdateSettings saves the modified settings to the Database.</summary>
        /// <remarks>
        /// The validator of the original blocked the post back that reached this method; here the
        /// same check blocks the save and leaves the localized message on screen, so
        /// <see cref="int.Parse(string)"/> is never handed a value the expression rejects.
        /// </remarks>
        public override void UpdateSettings()
        {
            try
            {
                if (!this.IsSearchDescLengthValid)
                {
                    // The page is not valid: the message is rendered and nothing is saved, which is
                    // what the client and server side validation of the original did.
                    this.StateHasChanged();
                    return;
                }

                // update replace token setting
                this.ModuleSettings.ReplaceTokens = this.ReplaceTokens;
                this.ModuleSettings.UseDecorate = this.UseDecorate;
                this.ModuleSettings.SearchDescLength = int.Parse(this.SearchDescLengthText);
                this.settingsRepository.SaveSettings(this.ModuleConfiguration, this.ModuleSettings);

                // disable module caching if token replace is enabled
                if (this.ReplaceTokens)
                {
                    var module = ModuleController.Instance.GetModule(this.ModuleId, this.TabId, false);
                    if (module.CacheTime > 0)
                    {
                        module.CacheTime = 0;
                        ModuleController.Instance.UpdateModule(module);
                    }
                }

                // Module failed to load
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Registers the stylesheet the <c>dnncl:DnnCssInclude</c> of the markup registered. This
        /// is the component's pre-render phase, which is where the tag helper ran.
        /// </remarks>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            this.ClientResourceController?.CreateStylesheet(Globals.ResolveUrl("~/DesktopModules/HTML/edit.css")).Register();
        }

        /// <summary>Handles the token replacement check box being toggled.</summary>
        /// <param name="args">The arguments carrying the new value.</param>
        private void OnReplaceTokensChanged(ChangeEventArgs args)
        {
            this.ReplaceTokens = Convert.ToBoolean(args?.Value ?? false, CultureInfo.InvariantCulture);
        }

        /// <summary>Handles the form decoration check box being toggled.</summary>
        /// <param name="args">The arguments carrying the new value.</param>
        private void OnUseDecorateChanged(ChangeEventArgs args)
        {
            this.UseDecorate = Convert.ToBoolean(args?.Value ?? false, CultureInfo.InvariantCulture);
        }

        /// <summary>Handles the search description length being typed into.</summary>
        /// <param name="args">The arguments carrying the new value.</param>
        private void OnSearchDescLengthChanged(ChangeEventArgs args)
        {
            this.SearchDescLengthText = args?.Value?.ToString() ?? string.Empty;
        }
    }
}
