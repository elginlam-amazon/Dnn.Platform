// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.UrlManagement
{
    using System;
    using System.Linq;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Urls;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A control which displays the settings of a URL provider.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>UrlProviderSettings.ascx</c> + <c>UrlProviderSettings.ascx.cs</c> +
    /// <c>UrlProviderSettings.ascx.designer.cs</c>; the markup now lives in
    /// <c>UrlProviderSettings.razor</c>. The designer fields map as follows:
    /// <c>providerSettingsPlaceHolder</c> is <see cref="SettingsControlFragment"/>, and
    /// <c>cmdUpdate</c>/<c>cmdCancel</c> are <see cref="CmdUpdateClick"/>/<see cref="CmdCancelClick"/>
    /// plus <see cref="CancelVisible"/> (which was <c>cmdCancel.Visible</c>).
    /// </para>
    /// <para>
    /// The provider's own settings control is still located through
    /// <c>ExtensionUrlProviderController.GetModuleProviders(PortalId)</c> and its
    /// <c>SettingsControlSrc</c>, and it is still driven through
    /// <see cref="IExtensionUrlProviderSettingsControl"/>: <c>Provider</c> is assigned and
    /// <c>LoadSettings()</c> is called before the user sees it, and <c>SaveSettings()</c> is
    /// persisted key by key through <c>ExtensionUrlProviderController.SaveSetting</c> for the same
    /// provider id and portal id. The only change is <i>when</i> the two happen: Blazor creates the
    /// child component while this component renders, so the instance is only reachable once that
    /// render completed - the settings are therefore loaded right after the first render, which
    /// re-renders the child with its values, instead of between the child's Init and Load phases.
    /// </para>
    /// <para>
    /// The <c>ProviderId</c> and <c>Display</c> request values are read during initialization and
    /// kept in fields, because <c>HttpContext</c> is no longer available once the circuit handling
    /// the two button clicks is interactive.
    /// </para>
    /// </remarks>
    public partial class UrlProviderSettings : ModuleUserControlBase
    {
        private readonly INavigationManager navigationManager;

        private int providerId;
        private string displayMode = string.Empty;
        private Type settingsControlType;
        private ExtensionUrlProviderInfo provider;
        private IExtensionUrlProviderSettingsControl providerSettingsControl;
        private bool settingsLoaded;

        /// <summary>Initializes a new instance of the <see cref="UrlProviderSettings"/> class.</summary>
        public UrlProviderSettings()
        {
            this.navigationManager = Globals.GetCurrentServiceProvider().GetService<INavigationManager>();
        }

        /// <summary>Gets or sets the navigation manager used for the redirects <c>Response.Redirect</c> used to issue.</summary>
        [Inject]
        protected Microsoft.AspNetCore.Components.NavigationManager Navigation { get; set; }

        /// <summary>Gets a value indicating whether <c>cmdCancel</c> renders.</summary>
        protected bool CancelVisible => this.DisplayMode != "editor" && this.DisplayMode != "settings";

        /// <summary>Gets the caption of <c>cmdUpdate</c> (was its <c>resourcekey</c>).</summary>
        protected string UpdateText => Localization.GetString("cmdUpdate", Localization.SharedResourceFile);

        /// <summary>Gets the caption of <c>cmdCancel</c> (was its <c>resourcekey</c>).</summary>
        protected string CancelText => Localization.GetString("cmdCancel", Localization.SharedResourceFile);

        /// <summary>Gets the content of the <c>providerSettingsPlaceHolder</c>.</summary>
        /// <remarks>
        /// Replaces <c>providerSettingsPlaceHolder.Controls.Add(Page.LoadControl(src))</c>. The
        /// component reference capture is how the created instance is reached, which is what the
        /// <c>as IExtensionUrlProviderSettingsControl</c> cast of the loaded control did.
        /// </remarks>
        protected RenderFragment SettingsControlFragment => builder =>
        {
            if (this.settingsControlType is null)
            {
                return;
            }

            builder.OpenComponent(0, this.settingsControlType);
            builder.AddComponentReferenceCapture(1, instance =>
            {
                this.providerSettingsControl = instance as IExtensionUrlProviderSettingsControl;
            });

            builder.CloseComponent();
        };

        private string DisplayMode => this.displayMode;

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.displayMode = (this.Request?.Query["Display"].ToString() ?? string.Empty).ToLowerInvariant();

            // Was Convert.ToInt32(this.Request.Params["ProviderId"]), i.e. the query string or the
            // posted form; GetValue reads both, in the same order.
            this.providerId = Convert.ToInt32(this.Request.GetValue("ProviderId"));

            var provider = ExtensionUrlProviderController.GetModuleProviders(this.ModuleContext.PortalId)
                                .SingleOrDefault(p => p.ProviderConfig.ExtensionUrlProviderId == this.providerId);

            if (provider != null)
            {
                var settingsControlSrc = provider.ProviderConfig.SettingsControlSrc;

                this.settingsControlType = ResolveSettingsControlType(settingsControlSrc);
                this.provider = provider.ProviderConfig;
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Replaces the <c>OnLoad</c> of the Web Forms control, which assigned <c>Provider</c> and
        /// called <c>LoadSettings()</c>. Those happen here because the child component - and
        /// therefore the <see cref="IExtensionUrlProviderSettingsControl"/> - only exists once this
        /// component has rendered once.
        /// </remarks>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (this.settingsLoaded || this.providerSettingsControl is null)
            {
                return;
            }

            this.settingsLoaded = true;
            this.providerSettingsControl.Provider = this.provider;
            this.providerSettingsControl.LoadSettings();
            this.StateHasChanged();
        }

        /// <summary>Resolves the component that replaced a provider's settings markup file.</summary>
        /// <param name="settingsControlSrc">The <c>SettingsControlSrc</c> of the provider.</param>
        /// <returns>The component type, or <see langword="null"/> when it is unknown.</returns>
        private static Type ResolveSettingsControlType(string settingsControlSrc)
        {
            if (string.IsNullOrWhiteSpace(settingsControlSrc))
            {
                return null;
            }

            var resolver = Globals.GetCurrentServiceProvider().GetService<IDnnControlTypeResolver>()
                           ?? DnnControlTypeResolver.Instance;

            return resolver.Resolve(settingsControlSrc);
        }

        private void CmdCancelClick()
        {
            this.Navigation.NavigateTo(this.navigationManager.NavigateURL(this.ModuleContext.PortalSettings.ActiveTab.TabID));
        }

        private void CmdUpdateClick()
        {
            // The Web Forms handler started with `if (!this.Page.IsValid) return;`. This control
            // declares no validator of its own, and the provider's settings control - which is the
            // only thing that could have declared one - now validates inside itself, so there is no
            // page-wide validity flag left to consult.
            if (this.providerSettingsControl != null)
            {
                var settings = this.providerSettingsControl.SaveSettings();
                foreach (var setting in settings)
                {
                    ExtensionUrlProviderController.SaveSetting(this.providerId, this.ModuleContext.PortalId, setting.Key, setting.Value);
                }
            }

            if (this.DisplayMode != "editor" && this.DisplayMode != "settings")
            {
                this.Navigation.NavigateTo(this.navigationManager.NavigateURL(this.ModuleContext.PortalSettings.ActiveTab.TabID));
            }
        }
    }
}
