// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Entities.Portals
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Collections;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Security;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Skins;
    using DotNetNuke.Web.Client;

    using Microsoft.Extensions.DependencyInjection;

    public class PortalSettingsController : IPortalSettingsController
    {
        private readonly IHostSettings hostSettings;
        private readonly IHostSettingsService hostSettingsService;

        /// <summary>Initializes a new instance of the <see cref="PortalSettingsController"/> class.</summary>
        public PortalSettingsController()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="PortalSettingsController"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        public PortalSettingsController(IHostSettings hostSettings, IHostSettingsService hostSettingsService)
        {
            this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            this.hostSettingsService = hostSettingsService ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettingsService>();
        }

        public static IPortalSettingsController Instance() =>
            Globals.GetCurrentServiceProvider().GetRequiredService<IPortalSettingsController>();

        /// <inheritdoc />
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", Justification = "Breaking change")]
        public virtual void ConfigureActiveTab(PortalSettings portalSettings)
        {
            var activeTab = portalSettings.ActiveTab;

            if (activeTab == null || activeTab.TabID == Null.NullInteger)
            {
                return;
            }

            this.UpdateSkinSettings(activeTab, portalSettings);

            activeTab.BreadCrumbs = new ArrayList(this.GetBreadcrumbs(activeTab.TabID, portalSettings.PortalId));
        }

        /// <inheritdoc />
        public virtual TabInfo GetActiveTab(int tabId, PortalSettings portalSettings)
        {
            var portalId = portalSettings.PortalId;
            var portalTabs = TabController.Instance.GetTabsByPortal(portalId);
            var hostTabs = TabController.Instance.GetTabsByPortal(Null.NullInteger);

            // Check portal
            var activeTab = GetTab(tabId, portalTabs)
                ?? GetTab(tabId, hostTabs) // check host
                ?? GetSpecialTab(portalId, portalSettings.SplashTabId) // check splash tab
                ?? GetSpecialTab(portalId, portalSettings.HomeTabId); // check home tab

            if (activeTab == null)
            {
                var tab = (from TabInfo t in portalTabs.AsList() where !t.IsDeleted && t.IsVisible && t.HasAVisibleVersion select t).FirstOrDefault();

                if (tab != null)
                {
                    activeTab = tab.Clone();
                }
            }

            if (activeTab != null)
            {
                if (Null.IsNull(activeTab.StartDate))
                {
                    activeTab.StartDate = DateTime.MinValue;
                }

                if (Null.IsNull(activeTab.EndDate))
                {
                    activeTab.EndDate = DateTime.MaxValue;
                }
            }

            return activeTab;
        }

        /// <inheritdoc />
        public virtual PortalSettings.PortalAliasMapping GetPortalAliasMappingMode(int portalId)
        {
            var aliasMapping = PortalSettings.PortalAliasMapping.None;
            if (PortalController.Instance.GetPortalSettings(portalId).TryGetValue("PortalAliasMapping", out var setting))
            {
                switch (setting.ToUpperInvariant())
                {
                    case "CANONICALURL":
                        aliasMapping = PortalSettings.PortalAliasMapping.CanonicalUrl;
                        break;
                    case "REDIRECT":
                        aliasMapping = PortalSettings.PortalAliasMapping.Redirect;
                        break;
                    default:
                        aliasMapping = PortalSettings.PortalAliasMapping.None;
                        break;
                }
            }

            return aliasMapping;
        }

        /// <inheritdoc />
        public virtual IList<ModuleInfo> GetTabModules(PortalSettings portalSettings)
        {
            return portalSettings.ActiveTab.Modules.Cast<ModuleInfo>().Select(m => m).ToList();
        }

        /// <inheritdoc />
        public virtual void LoadPortal(PortalInfo portal, PortalSettings portalSettings)
        {
            portalSettings.PortalName = portal.PortalName;
            portalSettings.LogoFile = portal.LogoFile;
            portalSettings.FooterText = portal.FooterText;
            portalSettings.ExpiryDate = portal.ExpiryDate;
            portalSettings.UserRegistration = portal.UserRegistration;
            portalSettings.BannerAdvertising = portal.BannerAdvertising;
            portalSettings.Currency = portal.Currency;
            portalSettings.AdministratorId = portal.AdministratorId;
            portalSettings.Email = portal.Email;
            portalSettings.HostFee = portal.HostFee;
            portalSettings.HostSpace = Null.IsNull(portal.HostSpace) ? 0 : portal.HostSpace;
            portalSettings.PageQuota = portal.PageQuota;
            portalSettings.UserQuota = portal.UserQuota;
            portalSettings.AdministratorRoleId = portal.AdministratorRoleId;
            portalSettings.AdministratorRoleName = portal.AdministratorRoleName;
            portalSettings.RegisteredRoleId = portal.RegisteredRoleId;
            portalSettings.RegisteredRoleName = portal.RegisteredRoleName;
            portalSettings.Description = portal.Description;
            portalSettings.KeyWords = portal.KeyWords;
            portalSettings.BackgroundFile = portal.BackgroundFile;
            portalSettings.GUID = portal.GUID;
            portalSettings.AdminTabId = portal.AdminTabId;
            portalSettings.SuperTabId = portal.SuperTabId;
            portalSettings.SplashTabId = portal.SplashTabId;
            portalSettings.HomeTabId = portal.HomeTabId;
            portalSettings.LoginTabId = portal.LoginTabId;
            portalSettings.RegisterTabId = portal.RegisterTabId;
            portalSettings.UserTabId = portal.UserTabId;
            portalSettings.SearchTabId = portal.SearchTabId;
            portalSettings.ErrorPage404 = portal.Custom404TabId;
            portalSettings.ErrorPage500 = portal.Custom500TabId;
            portalSettings.TermsTabId = portal.TermsTabId;
            portalSettings.PrivacyTabId = portal.PrivacyTabId;
            portalSettings.DefaultLanguage = Null.IsNull(portal.DefaultLanguage) ? Localization.SystemLocale : portal.DefaultLanguage;
            portalSettings.HomeDirectory = Globals.ApplicationPath + "/" + portal.HomeDirectory + "/";
            portalSettings.HomeDirectoryMapPath = portal.HomeDirectoryMapPath;
            portalSettings.HomeSystemDirectory = Globals.ApplicationPath + "/" + portal.HomeSystemDirectory + "/";
            portalSettings.HomeSystemDirectoryMapPath = portal.HomeSystemDirectoryMapPath;
            portalSettings.Pages = portal.Pages;
            portalSettings.Users = portal.Users;
            portalSettings.CultureCode = portal.CultureCode;
        }

        /// <inheritdoc />
        public virtual void LoadPortalSettings(PortalSettings portalSettings)
        {
            var settings = PortalController.Instance.GetPortalSettings(portalSettings.PortalId);
            portalSettings.Registration = new RegistrationSettings(settings);

            var clientResourcesSettings = new ClientResourceSettings();
            bool overridingDefaultSettings = clientResourcesSettings.IsOverridingDefaultSettingsEnabled(portalSettings.PortalId);

            int crmVersion;
            if (overridingDefaultSettings)
            {
                int? globalVersion = new ClientResourceSettings().GetVersion(portalSettings.PortalId);
                crmVersion = globalVersion ?? default(int);
            }
            else
            {
                crmVersion = GetSetting(settings, "CrmVersion", this.hostSettingsService.GetInteger("CrmVersion"));
            }

            portalSettings.AllowUserUICulture = GetSetting(settings, "AllowUserUICulture", false);
            portalSettings.CdfVersion = crmVersion;
            portalSettings.ContentLocalizationEnabled = GetSetting(settings, "ContentLocalizationEnabled", false);
            portalSettings.DefaultAdminContainer = GetSetting(settings, "DefaultAdminContainer", this.hostSettings.DefaultAdminContainer);
            portalSettings.DefaultAdminSkin = GetSetting(settings, "DefaultAdminSkin", this.hostSettings.DefaultAdminSkin);
            portalSettings.DefaultIconLocation = GetSetting(settings, "DefaultIconLocation", "icons/sigma");
            portalSettings.DefaultModuleId = GetSetting(settings, "defaultmoduleid", Null.NullInteger);
            portalSettings.DefaultModuleActionMenu = GetSetting(settings, "DefaultModuleActionMenu", "~/admin/Menus/ModuleActions/ModuleActions.ascx");
            portalSettings.DefaultPortalContainer = GetSetting(settings, "DefaultPortalContainer", this.hostSettings.DefaultPortalContainer);
            portalSettings.DefaultPortalSkin = GetSetting(settings, "DefaultPortalSkin", this.hostSettings.DefaultPortalSkin);
            portalSettings.DefaultTabId = GetSetting(settings, "defaulttabid", Null.NullInteger);
            portalSettings.EnableBrowserLanguage = GetSetting(settings, "EnableBrowserLanguage", this.hostSettings.EnableBrowserLanguage);
            portalSettings.EnableCompositeFiles = GetSetting(settings, "EnableCompositeFiles", false);
            portalSettings.EnablePopUps = GetSetting(settings, "EnablePopUps", true);
            portalSettings.HideLoginControl = GetSetting(settings, "HideLoginControl", false);
            portalSettings.EnableSkinWidgets = GetSetting(settings, "EnableSkinWidgets", true);
            portalSettings.ShowCookieConsent = GetSetting(settings, "ShowCookieConsent", false);
            portalSettings.CookieMoreLink = GetSetting(settings, "CookieMoreLink", Null.NullString);
            portalSettings.EnableUrlLanguage = GetSetting(settings, "EnableUrlLanguage", this.hostSettings.EnableUrlLanguage);
            portalSettings.HideFoldersEnabled = GetSetting(settings, "HideFoldersEnabled", true);
            portalSettings.InlineEditorEnabled = GetSetting(settings, "InlineEditorEnabled", true);
            portalSettings.AllowJsInHtmlModule = GetSetting(settings, "AllowJsInHtmlModule", true);
            portalSettings.AllowJsInModuleHeaders = GetSetting(settings, "AllowJsInModuleHeaders", true);
            portalSettings.AllowJsInModuleFooters = GetSetting(settings, "AllowJsInModuleFooters", true);
            portalSettings.SearchIncludeCommon = GetSetting(settings, "SearchIncludeCommon", this.hostSettings.SearchIncludeCommon);
            portalSettings.SearchIncludeNumeric = GetSetting(settings, "SearchIncludeNumeric", this.hostSettings.SearchIncludeNumeric);
            portalSettings.SearchIncludedTagInfoFilter = GetSetting(settings, "SearchIncludedTagInfoFilter", this.hostSettings.SearchIncludedTagInfoFilter);
            portalSettings.SearchMaxWordlLength = GetSetting(settings, "MaxSearchWordLength", this.hostSettings.SearchMaxWordLength);
            portalSettings.SearchMinWordlLength = GetSetting(settings, "MinSearchWordLength", this.hostSettings.SearchMinWordLength);
            portalSettings.SSLSetup = (Abstractions.Security.SiteSslSetup)GetSetting(settings, "SSLSetup", (int)Abstractions.Security.SiteSslSetup.Off);
            portalSettings.SSLEnforced = GetSetting(settings, "SSLEnforced", false);
            portalSettings.SSLURL = GetSetting(settings, "SSLURL", Null.NullString);
            portalSettings.STDURL = GetSetting(settings, "STDURL", Null.NullString);
            portalSettings.EnableRegisterNotification = GetSetting(settings, "EnableRegisterNotification", true);
            portalSettings.EnableUnapprovedPasswordReminderNotification = GetSetting(settings, "EnableUnapprovedPasswordReminderNotification", true);
            portalSettings.DefaultAuthProvider = GetSetting(settings, "DefaultAuthProvider", "DNN");
            portalSettings.SMTPConnectionLimit = GetSetting(settings, "SMTPConnectionLimit", 2);
            portalSettings.SMTPMaxIdleTime = GetSetting(settings, "SMTPMaxIdleTime", 100000);

            portalSettings.ControlPanelSecurity = PortalSettings.ControlPanelPermission.ModuleEditor;
            var setting = GetSetting(settings, "ControlPanelSecurity", string.Empty);
            if (setting.Equals("TAB", StringComparison.OrdinalIgnoreCase))
            {
                portalSettings.ControlPanelSecurity = PortalSettings.ControlPanelPermission.TabEditor;
            }

            portalSettings.DefaultControlPanelMode = PortalSettings.Mode.View;
            setting = GetSetting(settings, "ControlPanelMode", string.Empty);
            if (setting.Equals("EDIT", StringComparison.OrdinalIgnoreCase))
            {
                portalSettings.DefaultControlPanelMode = PortalSettings.Mode.Edit;
            }

            setting = GetSetting(settings, "ControlPanelVisibility", string.Empty);
            portalSettings.DefaultControlPanelVisibility = !setting.Equals("MIN", StringComparison.OrdinalIgnoreCase);

            setting = GetSetting(settings, "TimeZone", string.Empty);
            if (!string.IsNullOrEmpty(setting))
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(setting);
                if (timeZone != null)
                {
                    portalSettings.TimeZone = timeZone;
                }
            }

            setting = GetSetting(settings, "DataConsentActive", "False");
            portalSettings.DataConsentActive = bool.Parse(setting);
            setting = GetSetting(settings, "DataConsentTermsLastChange", string.Empty);
            if (!string.IsNullOrEmpty(setting))
            {
                portalSettings.DataConsentTermsLastChange = DateTime.Parse(setting, CultureInfo.InvariantCulture);
            }

            setting = GetSetting(settings, "DataConsentConsentRedirect", "-1");
            portalSettings.DataConsentConsentRedirect = int.Parse(setting, CultureInfo.InvariantCulture);
            setting = GetSetting(settings, "DataConsentUserDeleteAction", "0");
            portalSettings.DataConsentUserDeleteAction = (PortalSettings.UserDeleteAction)int.Parse(setting, CultureInfo.InvariantCulture);
            setting = GetSetting(settings, "DataConsentDelay", "1");
            portalSettings.DataConsentDelay = int.Parse(setting, CultureInfo.InvariantCulture);
            setting = GetSetting(settings, "DataConsentDelayMeasurement", "d");
            portalSettings.DataConsentDelayMeasurement = setting;
            setting = GetSetting(settings, "AllowedExtensionsWhitelist", this.hostSettingsService.GetString("DefaultEndUserExtensionWhitelist"));
            portalSettings.AllowedExtensionsWhitelist = new FileExtensionWhitelist(setting);

            setting = GetSetting(settings, "CspHeaderMode", "OFF");
            switch (setting.ToUpperInvariant())
            {
                case "ON":
                    portalSettings.CspHeaderMode = PortalSettings.CspMode.On;
                    break;
                case "REPORTONLY":
                    portalSettings.CspHeaderMode = PortalSettings.CspMode.ReportOnly;
                    break;
                default:
                    portalSettings.CspHeaderMode = PortalSettings.CspMode.Off;
                    break;
            }

            portalSettings.CspHeaderFixed = GetSetting(settings, "CspHeaderFixed", false);
            portalSettings.CspHeader = GetSetting(settings, "CspHeader", "style-src 'unsafe-inline'; object-src 'none'; frame-ancestors 'none';");
            portalSettings.CspReportingHeader = GetSetting(settings, "CspReportingHeader", string.Empty);
        }

        /// <summary>
        /// Reads a portal setting, converting it to <typeparamref name="T"/> and falling back to
        /// <paramref name="defaultValue"/> when it is absent or empty.
        /// </summary>
        /// <remarks>
        /// This calls DNN's own <see cref="DotNetNuke.Collections.CollectionExtensions.GetValueOrDefault{T}(System.Collections.IDictionary, string, T)"/>
        /// explicitly. On modern .NET the BCL added
        /// <c>System.Collections.Generic.CollectionExtensions.GetValueOrDefault</c>, which makes the
        /// extension-method call ambiguous (and silently returns <see cref="string"/> rather than
        /// converting) for a <c>Dictionary&lt;string, string&gt;</c>.
        /// </remarks>
        /// <typeparam name="T">The setting's type.</typeparam>
        /// <param name="settings">The portal settings dictionary.</param>
        /// <param name="key">The setting name.</param>
        /// <param name="defaultValue">The value to use when the setting is not present.</param>
        /// <returns>The converted setting value.</returns>
        private static T GetSetting<T>(System.Collections.IDictionary settings, string key, T defaultValue)
        {
            return DotNetNuke.Collections.CollectionExtensions.GetValueOrDefault(settings, key, defaultValue);
        }

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected List<TabInfo> GetBreadcrumbs(int tabId, int portalId)
        {
            var breadCrumbs = new List<TabInfo>();
            GetBreadCrumbs(breadCrumbs, tabId, portalId);
            return breadCrumbs;
        }

        protected virtual void UpdateSkinSettings(TabInfo activeTab, PortalSettings portalSettings)
        {
            if (Globals.IsAdminSkin())
            {
                // DNN-6170 ensure skin value is culture specific
                activeTab.SkinSrc = string.IsNullOrEmpty(
                    PortalController.GetPortalSetting(
                        "DefaultAdminSkin",
                        portalSettings.PortalId,
                        this.hostSettings.DefaultAdminSkin,
                        portalSettings.CultureCode))
                    ? portalSettings.DefaultAdminSkin
                    : PortalController.GetPortalSetting(
                        "DefaultAdminSkin",
                        portalSettings.PortalId,
                        this.hostSettings.DefaultAdminSkin,
                        portalSettings.CultureCode);
            }
            else if (string.IsNullOrEmpty(activeTab.SkinSrc))
            {
                // DNN-6170 ensure skin value is culture specific
                activeTab.SkinSrc = string.IsNullOrEmpty(
                    PortalController.GetPortalSetting(
                        "DefaultPortalSkin",
                        portalSettings.PortalId,
                        this.hostSettings.DefaultPortalSkin,
                        portalSettings.CultureCode))
                    ? portalSettings.DefaultPortalSkin
                    : PortalController.GetPortalSetting(
                        "DefaultPortalSkin",
                        portalSettings.PortalId,
                        this.hostSettings.DefaultPortalSkin,
                        portalSettings.CultureCode);
            }

            activeTab.SkinSrc = SkinController.FormatSkinSrc(activeTab.SkinSrc, portalSettings);
            activeTab.SkinPath = SkinController.FormatSkinPath(activeTab.SkinSrc);

            if (Globals.IsAdminSkin())
            {
                activeTab.ContainerSrc = string.IsNullOrEmpty(
                    PortalController.GetPortalSetting(
                        "DefaultAdminContainer",
                        portalSettings.PortalId,
                        this.hostSettings.DefaultAdminContainer,
                        portalSettings.CultureCode))
                    ? portalSettings.DefaultAdminContainer
                    : PortalController.GetPortalSetting(
                        "DefaultAdminContainer",
                        portalSettings.PortalId,
                        this.hostSettings.DefaultAdminContainer,
                        portalSettings.CultureCode);
            }
            else if (string.IsNullOrEmpty(activeTab.ContainerSrc))
            {
                activeTab.ContainerSrc = string.IsNullOrEmpty(
                    PortalController.GetPortalSetting(
                        "DefaultPortalContainer",
                        portalSettings.PortalId,
                        this.hostSettings.DefaultPortalContainer,
                        portalSettings.CultureCode))
                    ? portalSettings.DefaultPortalContainer
                    : PortalController.GetPortalSetting(
                        "DefaultPortalContainer",
                        portalSettings.PortalId,
                        this.hostSettings.DefaultPortalContainer,
                        portalSettings.CultureCode);
            }

            activeTab.ContainerSrc = SkinController.FormatSkinSrc(activeTab.ContainerSrc, portalSettings);
            activeTab.ContainerPath = SkinController.FormatSkinPath(activeTab.ContainerSrc);
        }

        private static void GetBreadCrumbs(List<TabInfo> breadCrumbs, int tabId, int portalId)
        {
            var portalTabs = TabController.Instance.GetTabsByPortal(portalId);
            var hostTabs = TabController.Instance.GetTabsByPortal(Null.NullInteger);
            while (true)
            {
                TabInfo tab;
                if (portalTabs.TryGetValue(tabId, out tab) || hostTabs.TryGetValue(tabId, out tab))
                {
                    // add tab to breadcrumb collection
                    breadCrumbs.Insert(0, tab.Clone());

                    // get the tab parent
                    if (!Null.IsNull(tab.ParentId) && tabId != tab.ParentId)
                    {
                        tabId = tab.ParentId;
                        continue;
                    }
                }

                break;
            }
        }

        private static TabInfo GetSpecialTab(int portalId, int tabId)
        {
            TabInfo activeTab = null;

            if (tabId > 0)
            {
                var tab = TabController.Instance.GetTab(tabId, portalId, false);
                if (tab != null)
                {
                    activeTab = tab.Clone();
                }
            }

            return activeTab;
        }

        // This method is called few times with each request; it would be
        // better to have it cache the "activeTab" for a short period.
        private static TabInfo GetTab(int tabId, TabCollection tabs)
        {
            return tabId != Null.NullInteger && tabs.TryGetValue(tabId, out var tab) && !tab.IsDeleted
                ? tab.Clone()
                : null;
        }
    }
}
