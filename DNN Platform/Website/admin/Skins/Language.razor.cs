// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Skins.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.UserControls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The Language SkinObject allows the visitor to select the language of the page.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>language.ascx</c> + <c>Language.ascx.cs</c> + <c>Language.ascx.designer.cs</c>; the
    /// markup now lives in <c>Language.razor</c>. The designer fields map as follows:
    /// <c>litCommonHeaderTemplate</c>/<c>litCommonFooterTemplate</c> -&gt;
    /// <see cref="CommonHeaderMarkup"/>/<see cref="CommonFooterMarkup"/> (+ their
    /// <c>*Visible</c> flags), <c>selectCulture</c> -&gt; <see cref="CultureItems"/> +
    /// <see cref="SelectedCulture"/> + <see cref="SelectCultureVisible"/>, and
    /// <c>rptLanguages</c> -&gt; <see cref="RepeaterMarkup"/>, which
    /// <see cref="BuildRepeaterMarkup"/> assembles with the very template selection rules
    /// <c>rptLanguages_ItemDataBound</c> applied (Header, Item/AlternatingItem with the
    /// SelectedItemTemplate override for the current culture, Separator between items, Footer).
    /// </para>
    /// <para>
    /// The <c>AutoPostBack</c> redirect of the drop down becomes
    /// <see cref="SelectCultureChangedAsync"/>. Because the component is interactive, the whole
    /// request-bound state (the filtered locales, their per-locale URLs and the templates) is built
    /// once, during the first - server rendered - pass, and reused: <c>HttpContext</c> (and with it
    /// <c>PortalSettings.Current</c>) is not available once the circuit is live.
    /// </para>
    /// <para>
    /// Text lookups deliberately keep <see cref="Localization.GetString(string, string, string)"/>
    /// against the control's <c>App_LocalResources</c> file: the language templates are the textbook
    /// per-portal Language Editor customisation, which an <c>IStringLocalizer</c> would not see.
    /// </para>
    /// </remarks>
    public partial class Language
    {
        private const string MyFileName = "Language.ascx";

        private readonly IPortalController portalController;

        private readonly List<ListItem> cultureItems = new List<ListItem>();

        /// <summary>The locale codes that passed the permission filter, in display order.</summary>
        private readonly List<Locale> visibleLocales = new List<Locale>();

        /// <summary>The URL to navigate to per locale code, resolved while the request is available.</summary>
        private readonly Dictionary<string, string> localeUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether the request-bound state was already built (the WebForms Load ran once).</summary>
        private bool loaded;

        private int portalId;

        private string localResourceFile;

        private LanguageTokenReplace localTokenReplace;

        /// <summary>Initializes a new instance of the <see cref="Language"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.2. Please use overload with IPortalController. Scheduled removal in v12.0.0.")]
        public Language()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Language"/> class.</summary>
        /// <param name="portalController">The portal controller.</param>
        public Language(IPortalController portalController)
        {
            this.portalController = portalController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>();
        }

        [Parameter]
        public string AlternateTemplate { get; set; }

        [Parameter]
        public string CommonFooterTemplate { get; set; }

        [Parameter]
        public string CommonHeaderTemplate { get; set; }

        [Parameter]
        public string FooterTemplate { get; set; }

        [Parameter]
        public string HeaderTemplate { get; set; }

        [Parameter]
        public string ItemTemplate { get; set; }

        [Parameter]
        public string SelectedItemTemplate { get; set; }

        [Parameter]
        public string SeparatorTemplate { get; set; }

        /// <summary>Gets or sets a value indicating whether the template driven link list is rendered.</summary>
        [Parameter]
        public bool ShowLinks { get; set; }

        /// <summary>Gets or sets a value indicating whether the culture drop down is rendered.</summary>
        [Parameter]
        public bool ShowMenu { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the templates are localized in the current culture.</summary>
        [Parameter]
        public bool UseCurrentCultureForTemplate { get; set; }

        /// <summary>Gets the items of the culture drop down (replaces <c>selectCulture.Items</c>).</summary>
        protected IReadOnlyList<ListItem> CultureItems => this.cultureItems;

        /// <summary>Gets the value of the selected drop down item (replaces <c>selectCulture.SelectedItem</c>).</summary>
        protected string SelectedCulture { get; private set; }

        /// <summary>Gets a value indicating whether the drop down renders (<c>selectCulture.Visible</c>).</summary>
        protected bool SelectCultureVisible { get; private set; }

        /// <summary>Gets a value indicating whether the link list renders (<c>rptLanguages.Visible</c>).</summary>
        protected bool RepeaterVisible { get; private set; }

        /// <summary>Gets the rendered link list (replaces the <c>rptLanguages</c> output).</summary>
        protected string RepeaterMarkup { get; private set; }

        /// <summary>Gets the rendered common header (replaces <c>litCommonHeaderTemplate.Text</c>).</summary>
        protected string CommonHeaderMarkup { get; private set; }

        /// <summary>Gets a value indicating whether the common header renders (<c>litCommonHeaderTemplate.Visible</c>).</summary>
        protected bool CommonHeaderVisible { get; private set; } = true;

        /// <summary>Gets the rendered common footer (replaces <c>litCommonFooterTemplate.Text</c>).</summary>
        protected string CommonFooterMarkup { get; private set; }

        /// <summary>Gets a value indicating whether the common footer renders (<c>litCommonFooterTemplate.Visible</c>).</summary>
        protected bool CommonFooterVisible { get; private set; } = true;

        protected string CurrentCulture => CultureInfo.CurrentCulture.ToString();

        protected string TemplateCulture => this.UseCurrentCultureForTemplate ? this.CurrentCulture : "en-US";

        protected string LocalResourceFile
        {
            get
            {
                if (string.IsNullOrEmpty(this.localResourceFile))
                {
                    this.localResourceFile = Localization.GetResourceFile(this, MyFileName);
                }

                return this.localResourceFile;
            }
        }

        protected LanguageTokenReplace LocalTokenReplace
        {
            get
            {
                if (this.localTokenReplace == null)
                {
                    this.localTokenReplace = new LanguageTokenReplace { resourceFile = this.LocalResourceFile };
                }

                return this.localTokenReplace;
            }
        }

        /// <summary>
        /// Gets or sets the Blazor navigation manager used by
        /// <see cref="SelectCultureChangedAsync"/>.
        /// </summary>
        /// <remarks>
        /// Fully qualified on purpose: <c>NavigationManager</c> alone is ambiguous between
        /// <c>DotNetNuke.Common.NavigationManager</c> and the Blazor one.
        /// </remarks>
        [Inject]
        private Microsoft.AspNetCore.Components.NavigationManager Navigation { get; set; }

        /// <summary>Gets the effective <see cref="AlternateTemplate"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveAlternateTemplate =>
            string.IsNullOrEmpty(this.AlternateTemplate)
                ? Localization.GetString("AlternateTemplate.Default", this.LocalResourceFile, this.TemplateCulture)
                : this.AlternateTemplate;

        /// <summary>Gets the effective <see cref="CommonFooterTemplate"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveCommonFooterTemplate =>
            string.IsNullOrEmpty(this.CommonFooterTemplate)
                ? Localization.GetString("CommonFooterTemplate.Default", this.LocalResourceFile, this.TemplateCulture)
                : this.CommonFooterTemplate;

        /// <summary>Gets the effective <see cref="CommonHeaderTemplate"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveCommonHeaderTemplate =>
            string.IsNullOrEmpty(this.CommonHeaderTemplate)
                ? Localization.GetString("CommonHeaderTemplate.Default", this.LocalResourceFile, this.TemplateCulture)
                : this.CommonHeaderTemplate;

        /// <summary>Gets the effective <see cref="FooterTemplate"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveFooterTemplate =>
            string.IsNullOrEmpty(this.FooterTemplate)
                ? Localization.GetString("FooterTemplate.Default", this.LocalResourceFile, this.TemplateCulture)
                : this.FooterTemplate;

        /// <summary>Gets the effective <see cref="HeaderTemplate"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveHeaderTemplate =>
            string.IsNullOrEmpty(this.HeaderTemplate)
                ? Localization.GetString("HeaderTemplate.Default", this.LocalResourceFile, this.TemplateCulture)
                : this.HeaderTemplate;

        /// <summary>Gets the effective <see cref="ItemTemplate"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveItemTemplate =>
            string.IsNullOrEmpty(this.ItemTemplate)
                ? Localization.GetString("ItemTemplate.Default", this.LocalResourceFile, this.TemplateCulture)
                : this.ItemTemplate;

        /// <summary>Gets the effective <see cref="SelectedItemTemplate"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveSelectedItemTemplate =>
            string.IsNullOrEmpty(this.SelectedItemTemplate)
                ? Localization.GetString("SelectedItemTemplate.Default", this.LocalResourceFile, this.TemplateCulture)
                : this.SelectedItemTemplate;

        /// <summary>Gets the effective <see cref="SeparatorTemplate"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveSeparatorTemplate =>
            string.IsNullOrEmpty(this.SeparatorTemplate)
                ? Localization.GetString("SeparatorTemplate.Default", this.LocalResourceFile, this.TemplateCulture)
                : this.SeparatorTemplate;

        /// <summary>Gets a value indicating whether the culture drop down is actually rendered.</summary>
        /// <remarks>
        /// The Web Forms getter forced itself to <see langword="true"/> when neither selector was
        /// enabled, "to make sure that at least one type of selector will be visible if multiple
        /// languages are enabled". A component parameter has to be an auto property, so that rule
        /// lives here and is what this control renders on; the behaviour is identical, including
        /// the case where a skin sets showMenu="false" without setting showLinks.
        /// </remarks>
        private bool EffectiveShowMenu => this.ShowMenu || !this.ShowLinks;

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (this.loaded)
            {
                return;
            }

            this.loaded = true;

            try
            {
                this.portalId = this.PortalSettings.PortalId;

                var locales = new Dictionary<string, Locale>();
                IEnumerable<ListItem> cultureListItems = Localization.LoadCultureInListItems(CultureDropDownTypes.NativeName, this.CurrentCulture, string.Empty, false);
                foreach (Locale loc in LocaleController.Instance.GetLocales(this.PortalSettings.PortalId).Values)
                {
                    string defaultRoles = PortalController.GetPortalSetting(this.portalController, $"DefaultTranslatorRoles-{loc.Code}", this.PortalSettings.PortalId, "Administrators");

                    // AUTHORIZATION: unchanged. Without content localization every enabled locale is
                    // offered; with it, a locale is only offered when the localized page exists and
                    // is viewable (LocaleIsAvailable -> TabPermissionController.CanViewPage) AND the
                    // visitor is a portal administrator, or the locale is published, or the visitor
                    // holds one of the locale's translator roles. This is the edit-mode branch.
                    if (!this.PortalSettings.ContentLocalizationEnabled ||
                        (this.LocaleIsAvailable(loc) &&
                            (PortalSecurity.IsInRoles(this.PortalSettings.AdministratorRoleName) || loc.IsPublished || PortalSecurity.IsInRoles(defaultRoles))))
                    {
                        locales.Add(loc.Code, loc);
                        this.visibleLocales.Add(loc);
                        foreach (var cultureItem in cultureListItems)
                        {
                            if (cultureItem.Value == loc.Code)
                            {
                                this.cultureItems.Add(cultureItem);
                            }
                        }
                    }
                }

                // The redirect target of every offered locale, resolved while the request (and with
                // it PortalSettings.Current) is available - see SelectCultureChangedAsync.
                foreach (var locale in this.visibleLocales)
                {
                    this.localeUrls[locale.Code] = this.ResolveLocaleUrl(locale.Code);
                }

                if (this.ShowLinks)
                {
                    if (locales.Count > 1)
                    {
                        this.RepeaterVisible = true;
                        this.RepeaterMarkup = this.BuildRepeaterMarkup();
                    }
                    else
                    {
                        this.RepeaterVisible = false;
                    }
                }

                if (this.EffectiveShowMenu)
                {
                    // select the default item
                    if (this.CurrentCulture != null)
                    {
                        ListItem item = this.cultureItems.FindByValue(this.CurrentCulture);
                        if (item != null)
                        {
                            foreach (var cultureItem in this.cultureItems)
                            {
                                cultureItem.Selected = false;
                            }

                            item.Selected = true;
                            this.SelectedCulture = item.Value;
                        }
                    }

                    // only show language selector if more than one language
                    this.SelectCultureVisible = this.cultureItems.Count > 1;
                }
                else
                {
                    this.SelectCultureVisible = false;
                }

                this.HandleCommonTemplates();
            }
            catch (Exception ex)
            {
                Exceptions.ProcessPageLoadException(ex, this.Request?.GetRawUrl());
            }
        }

        /// <summary>
        /// Builds the output <c>rptLanguages</c> produced, applying the same template per item type.
        /// </summary>
        /// <returns>The rendered list.</returns>
        /// <remarks>
        /// Replaces <c>rptLanguages.DataSource</c>/<c>DataBind()</c> plus
        /// <c>rptLanguages_ItemDataBound</c>. A WebForms Repeater rendered the header, then the
        /// items with the separator template between two items, then the footer; an empty template
        /// rendered nothing (the literal was hidden), and the header/footer/separator templates were
        /// parsed with the page culture because their item had no data item.
        /// </remarks>
        private string BuildRepeaterMarkup()
        {
            var markup = new StringBuilder();

            if (!string.IsNullOrEmpty(this.EffectiveHeaderTemplate))
            {
                markup.Append(this.ParseTemplate(this.EffectiveHeaderTemplate, this.CurrentCulture));
            }

            for (var index = 0; index < this.visibleLocales.Count; index++)
            {
                if (index > 0 && !string.IsNullOrEmpty(this.EffectiveSeparatorTemplate))
                {
                    markup.Append(this.ParseTemplate(this.EffectiveSeparatorTemplate, this.CurrentCulture));
                }

                var locale = this.visibleLocales[index];

                // even indexes were ListItemType.Item, odd ones ListItemType.AlternatingItem, which
                // fell back to the item template when no alternate template was defined.
                var template = index % 2 == 0
                    ? this.EffectiveItemTemplate
                    : (!string.IsNullOrEmpty(this.EffectiveAlternateTemplate) ? this.EffectiveAlternateTemplate : this.EffectiveItemTemplate);

                if (locale.Code == this.CurrentCulture && !string.IsNullOrEmpty(this.EffectiveSelectedItemTemplate))
                {
                    template = this.EffectiveSelectedItemTemplate;
                }

                if (string.IsNullOrEmpty(template))
                {
                    continue;
                }

                markup.Append(this.ParseTemplate(template, locale.Code));
            }

            if (!string.IsNullOrEmpty(this.EffectiveFooterTemplate))
            {
                markup.Append(this.ParseTemplate(this.EffectiveFooterTemplate, this.CurrentCulture));
            }

            return markup.ToString();
        }

        /// <summary>Resolves the URL of the page in the given locale.</summary>
        /// <param name="locale">The locale code.</param>
        /// <returns>The URL.</returns>
        private string ResolveLocaleUrl(string locale)
        {
            this.LocalTokenReplace.Language = locale;
            return this.LocalTokenReplace.ReplaceEnvironmentTokens("[URL]");
        }

        private string ParseTemplate(string template, string locale)
        {
            string strReturnValue = template;
            try
            {
                if (!string.IsNullOrEmpty(locale))
                {
                    // for non data items use locale
                    this.LocalTokenReplace.Language = locale;
                }
                else
                {
                    // for non data items use page culture
                    this.LocalTokenReplace.Language = this.CurrentCulture;
                }

                // perform token replacements
                strReturnValue = this.LocalTokenReplace.ReplaceEnvironmentTokens(strReturnValue);
            }
            catch (Exception ex)
            {
                Exceptions.ProcessPageLoadException(ex, this.Request?.GetRawUrl());
            }

            return strReturnValue;
        }

        private void HandleCommonTemplates()
        {
            if (string.IsNullOrEmpty(this.EffectiveCommonHeaderTemplate))
            {
                this.CommonHeaderVisible = false;
            }
            else
            {
                this.CommonHeaderMarkup = this.ParseTemplate(this.EffectiveCommonHeaderTemplate, this.CurrentCulture);
            }

            if (string.IsNullOrEmpty(this.EffectiveCommonFooterTemplate))
            {
                this.CommonFooterVisible = false;
            }
            else
            {
                this.CommonFooterMarkup = this.ParseTemplate(this.EffectiveCommonFooterTemplate, this.CurrentCulture);
            }
        }

        private bool LocaleIsAvailable(Locale locale)
        {
            var tab = this.PortalSettings.ActiveTab;
            if (tab.DefaultLanguageTab != null)
            {
                tab = tab.DefaultLanguageTab;
            }

            var localizedTab = TabController.Instance.GetTabByCulture(tab.TabID, tab.PortalID, locale);

            // AUTHORIZATION: a deleted or unviewable localized page is never offered.
            return localizedTab != null && !localizedTab.IsDeleted && TabPermissionController.CanViewPage(localizedTab);
        }

        /// <summary>Navigates to the same page in the newly selected culture.</summary>
        /// <param name="args">The change event arguments.</param>
        /// <returns>A task that completes once the navigation was started.</returns>
        /// <remarks>
        /// Replaces <c>selectCulture.SelectedIndexChanged</c> with <c>AutoPostBack="true"</c> and
        /// its <c>Response.Redirect</c>. DNN-6170: the portal settings cache has to be dropped so
        /// the skin value is culture specific in case of static localization.
        /// </remarks>
        private Task SelectCultureChangedAsync(ChangeEventArgs args)
        {
            var selected = args.Value?.ToString();
            if (string.IsNullOrEmpty(selected))
            {
                return Task.CompletedTask;
            }

            this.SelectedCulture = selected;
            foreach (var cultureItem in this.cultureItems)
            {
                cultureItem.Selected = string.Equals(cultureItem.Value, selected, StringComparison.Ordinal);
            }

            // DNN-6170 ensure skin value is culture specific in case of  static localization
            DataCache.RemoveCache(string.Format(CultureInfo.InvariantCulture, DataCache.PortalSettingsCacheKey, this.portalId, Null.NullString));

            if (!this.localeUrls.TryGetValue(selected, out var url) || string.IsNullOrEmpty(url))
            {
                url = this.ResolveLocaleUrl(selected);
            }

            // Was Response.Redirect: a full navigation is required, the culture has to be
            // re-negotiated for the whole page.
            this.Navigation.NavigateTo(url, forceLoad: true);
            return Task.CompletedTask;
        }
    }
}
