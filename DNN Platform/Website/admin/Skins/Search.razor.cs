// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Skins.Controls
{
    using System;
    using System.Collections;
    using System.Net;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Icons;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.Client.ResourceManager;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>A skin/theme object which provides a search input.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>search.ascx</c> + <c>Search.ascx.cs</c> + <c>Search.ascx.designer.cs</c>; the markup
    /// now lives in <c>Search.razor</c>, which documents how each designer control maps onto the
    /// state below.
    /// </para>
    /// <para>
    /// Forced platform changes: <c>Server.UrlEncode</c> -&gt;
    /// <see cref="WebUtility.UrlEncode"/>; <c>Request.Url.Host</c> -&gt; the host of the captured
    /// request (falling back to the portal alias, so the <c>[DOMAIN]</c> token is never empty);
    /// <c>Response.Redirect</c> -&gt; <c>NavigationManager.NavigateTo(..., forceLoad: true)</c>;
    /// <c>UrlUtils.OpenNewWindow(Page, Type, url)</c> -&gt;
    /// <see cref="UrlUtils.OpenNewWindow(IJSRuntime, string)"/>; <c>Page.IsPostBack</c> -&gt; the
    /// <see cref="loaded"/> guard (the request bound work runs once, as the WebForms Load did).
    /// </para>
    /// </remarks>
    public partial class Search
    {
        private const string MyFileName = "Search.ascx";

        private readonly INavigationManager navigationManager;

        private readonly IClientResourceController clientResourceController;

        private readonly IServicesFramework servicesFramework;

        /// <summary>Whether the request bound initialization already ran.</summary>
        private bool loaded;

        /// <summary>The search results page resolved while the request was available.</summary>
        private int searchTabId = Null.NullInteger;

        /// <summary>The host of the current request, used for the <c>[DOMAIN]</c> token.</summary>
        private string requestHost;

        /// <summary>Initializes a new instance of the <see cref="Search"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public Search()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Search"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="clientResourceController">The client resources controller.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public Search(INavigationManager navigationManager, IClientResourceController clientResourceController)
            : this(navigationManager, clientResourceController, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Search"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="clientResourceController">The client resources controller.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        public Search(INavigationManager navigationManager, IClientResourceController clientResourceController, IServicesFramework servicesFramework)
        {
            this.navigationManager = navigationManager ?? Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
            this.servicesFramework = servicesFramework ?? Globals.GetCurrentServiceProvider().GetRequiredService<IServicesFramework>();
        }

        public string SeeMoreText
        {
            get
            {
                return Localization.GetSafeJSString("SeeMoreResults", Localization.GetResourceFile(this, MyFileName));
            }
        }

        public string ClearQueryText
        {
            get
            {
                return Localization.GetSafeJSString("SearchClearQuery", Localization.GetResourceFile(this, MyFileName));
            }
        }

        public string NoResultText
        {
            get
            {
                return Localization.GetSafeJSString("NoResult", Localization.GetResourceFile(this, MyFileName));
            }
        }

        public string PlaceHolderText
        {
            get
            {
                return Localization.GetSafeJSString("Placeholder", Localization.GetResourceFile(this, MyFileName));
            }
        }

        /// <summary>Gets or sets a value indicating whether the visibility setting for the radio button corresponding to site based searches.</summary>
        /// <remarks>Set this value to <see langword="false"/> to hide the "Site" radio button.  This setting has no effect
        /// if <see cref="UseDropDownList"/> is <see langword="true"/>.</remarks>
        [Parameter]
        public bool ShowSite { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the visibility setting for the radio button corresponding to web based searches.</summary>
        /// <remarks>Set this value to <see langword="false"/> to hide the "Web" radio button.  This setting has no effect
        /// if <see cref="UseDropDownList"/> is <see langword="true"/>.</remarks>
        [Parameter]
        public bool ShowWeb { get; set; } = true;

        /// <summary>Gets or sets the site icon URL.</summary>
        /// <value>The site icon URL.</value>
        [Parameter]
        public string SiteIconURL { get; set; }

        /// <summary>Gets or sets the text for the "site" radio button or option list item.</summary>
        [Parameter]
        public string SiteText { get; set; }

        /// <summary>Gets or sets the tooltip text for the "site" radio button.</summary>
        [Parameter]
        public string SiteToolTip { get; set; }

        /// <summary>Gets or sets the URL for doing web based site searches.</summary>
        [Parameter]
        public string SiteURL { get; set; }

        /// <summary>Gets or sets the html for the submit button.</summary>
        [Parameter]
        public string Submit { get; set; }

        /// <summary>Gets or sets a value indicating whether to use the web search engine for site searches.</summary>
        [Parameter]
        public bool UseWebForSite { get; set; }

        /// <summary>Gets or sets a value indicating whether to display the site/web options using a drop down list.</summary>
        [Parameter]
        public bool UseDropDownList { get; set; }

        /// <summary>Gets or sets the web icon URL.</summary>
        [Parameter]
        public string WebIconURL { get; set; }

        /// <summary>Gets or sets the text for the "web" radio button or option list item.</summary>
        [Parameter]
        public string WebText { get; set; }

        /// <summary>Gets or sets the tooltip text for the "web" radio button.</summary>
        [Parameter]
        public string WebToolTip { get; set; }

        /// <summary>Gets or sets the URL for doing web based searches.</summary>
        [Parameter]
        public string WebURL { get; set; }

        /// <summary>Gets or sets minium chars required to trigger auto search.</summary>
        [Parameter]
        public int MinCharRequired { get; set; }

        /// <summary>Gets or sets the millisecond to delay trigger auto search.</summary>
        [Parameter]
        public int AutoSearchDelayInMilliSecond { get; set; }

        /// <summary>Gets or sets a value indicating whether disable the wild search.</summary>
        [Parameter]
        public bool EnableWildSearch { get; set; } = true;

        protected int PortalId { get; set; }

        protected string SearchType { get; set; }

        protected string CultureCode { get; set; }

        /// <summary>Gets or sets the query typed into the classic text box (<c>txtSearch.Text</c>).</summary>
        protected string SearchText { get; set; } = string.Empty;

        /// <summary>Gets or sets the query typed into the drop-down text box (<c>txtSearchNew.Text</c>).</summary>
        protected string SearchTextNew { get; set; } = string.Empty;

        /// <summary>Gets a value indicating whether the classic rendering is shown (<c>ClassicSearch.Visible</c>).</summary>
        protected bool ClassicSearchVisible => !this.UseDropDownList;

        /// <summary>Gets a value indicating whether the drop-down rendering is shown (<c>DropDownSearch.Visible</c>).</summary>
        protected bool DropDownSearchVisible => this.UseDropDownList;

        /// <summary>Gets a value indicating whether the "web" radio button renders (<c>WebRadioButton.Visible</c>).</summary>
        protected bool WebRadioButtonVisible => this.ShowWeb;

        /// <summary>Gets a value indicating whether the "site" radio button renders (<c>SiteRadioButton.Visible</c>).</summary>
        protected bool SiteRadioButtonVisible => this.ShowSite;

        /// <summary>Gets a value indicating whether the "web" radio button is checked (<c>WebRadioButton.Checked</c>).</summary>
        protected bool WebRadioButtonChecked => this.ShowWeb;

        /// <summary>Gets a value indicating whether the "site" radio button is checked (<c>SiteRadioButton.Checked</c>).</summary>
        protected bool SiteRadioButtonChecked => this.ShowSite;

        /// <summary>Gets the CSS class of the options (was <c>RadioButton.CssClass</c>, "SkinObject" in the markup).</summary>
        protected string OptionCssClass => string.IsNullOrEmpty(this.CssClass) ? "SkinObject" : this.CssClass;

        /// <summary>Gets the CSS class of the classic button (was <c>cmdSearch.CssClass</c>).</summary>
        protected string ButtonCssClass => string.IsNullOrEmpty(this.CssClass) ? "SkinObject" : this.CssClass;

        /// <summary>Gets the CSS class of the drop-down button (was <c>cmdSearchNew.CssClass</c>).</summary>
        protected string SearchButtonCssClass => string.IsNullOrEmpty(this.CssClass) ? "SkinObject SearchButton" : this.CssClass;

        /// <summary>Gets the icon URL of the drop-down glyph (was <c>dnn:DnnImage IconKey="Action"</c>).</summary>
        protected string DownArrowIconUrl => IconController.IconURL("Action");

        /// <summary>Gets the alternate text/tooltip of the drop-down glyph (<c>downArrow.AlternateText</c>).</summary>
        protected string DownArrowAlternateText =>
            Localization.GetString("DropDownGlyph.AltText", Localization.GetResourceFile(this, MyFileName));

        /// <summary>
        /// Gets or sets the Blazor navigation manager used for the site search redirect.
        /// </summary>
        /// <remarks>
        /// Fully qualified on purpose: <c>NavigationManager</c> alone is ambiguous between
        /// <c>DotNetNuke.Common.NavigationManager</c> and the Blazor one.
        /// </remarks>
        [Inject]
        private Microsoft.AspNetCore.Components.NavigationManager Navigation { get; set; }

        /// <summary>Gets or sets the JavaScript runtime used to open web searches in a new window.</summary>
        [Inject]
        private IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets the effective <see cref="SiteIconURL"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveSiteIconURL =>
            string.IsNullOrEmpty(this.SiteIconURL)
                ? IconController.IconURL("DnnSearch")
                : this.SiteIconURL;

        /// <summary>Gets the effective <see cref="SiteText"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveSiteText =>
            string.IsNullOrEmpty(this.SiteText)
                ? Localization.GetString("Site", Localization.GetResourceFile(this, MyFileName))
                : this.SiteText;

        /// <summary>Gets the effective <see cref="SiteToolTip"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveSiteToolTip =>
            string.IsNullOrEmpty(this.SiteToolTip)
                ? Localization.GetString("Site.ToolTip", Localization.GetResourceFile(this, MyFileName))
                : this.SiteToolTip;

        /// <summary>Gets the effective <see cref="SiteURL"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveSiteURL =>
            string.IsNullOrEmpty(this.SiteURL)
                ? Localization.GetString("URL", Localization.GetResourceFile(this, MyFileName))
                : this.SiteURL;

        /// <summary>Gets the effective <see cref="WebIconURL"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveWebIconURL =>
            string.IsNullOrEmpty(this.WebIconURL)
                ? IconController.IconURL("GoogleSearch")
                : this.WebIconURL;

        /// <summary>Gets the effective <see cref="WebText"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveWebText =>
            string.IsNullOrEmpty(this.WebText)
                ? Localization.GetString("Web", Localization.GetResourceFile(this, MyFileName))
                : this.WebText;

        /// <summary>Gets the effective <see cref="WebToolTip"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveWebToolTip =>
            string.IsNullOrEmpty(this.WebToolTip)
                ? Localization.GetString("Web.ToolTip", Localization.GetResourceFile(this, MyFileName))
                : this.WebToolTip;

        /// <summary>Gets the effective <see cref="WebURL"/>, falling back to the resource default.</summary>
        /// <remarks>
        /// The Web Forms getter returned the resource default when the property had not been set
        /// (and cached it into its backing field). A component parameter has to be an auto
        /// property - Blazor assigns parameters directly - so the fallback lives here and every
        /// reader inside this control uses this member. Same resource key, same resource file,
        /// same culture, so the value rendered is unchanged.
        /// </remarks>
        private string EffectiveWebURL =>
            string.IsNullOrEmpty(this.WebURL)
                ? Localization.GetString("URL", Localization.GetResourceFile(this, MyFileName))
                : this.WebURL;

        /// <summary>  Executes the search.</summary>
        /// <param name="searchText">The text which will be used to perform the search.</param>
        /// <param name="searchType">The type of the search. Use "S" for a site search, and "W" for a web search.</param>
        /// <returns>A task that completes once the search was routed.</returns>
        /// <remarks>
        ///   All web based searches will open in a new window, while site searches will open in the current window.  A site search uses the built
        ///   in search engine to perform the search, while both web based search variants will use an external search engine to perform a search.
        /// </remarks>
        protected async Task ExecuteSearch(string searchText, string searchType)
        {
            int searchTabId = this.searchTabId != Null.NullInteger ? this.searchTabId : this.GetSearchTabId();

            // SENTINEL: Null.NullInteger (-1) means "no search results page"; kept as an equality
            // check so a real TabId of 0 is still searched.
            if (searchTabId == Null.NullInteger)
            {
                return;
            }

            string strURL;
            if (!string.IsNullOrEmpty(searchText))
            {
                switch (searchType)
                {
                    case "S":
                        // site
                        if (this.UseWebForSite)
                        {
                            strURL = this.EffectiveSiteURL;
                            if (!string.IsNullOrEmpty(strURL))
                            {
                                strURL = strURL.Replace("[TEXT]", WebUtility.UrlEncode(searchText));
                                strURL = strURL.Replace("[DOMAIN]", this.requestHost ?? string.Empty);
                                await UrlUtils.OpenNewWindow(this.JsRuntime, strURL);
                            }
                        }
                        else
                        {
                            this.Navigation.NavigateTo(
                                this.navigationManager.NavigateURL(searchTabId, string.Empty, "search=" + WebUtility.UrlEncode(searchText)),
                                forceLoad: true);
                        }

                        break;
                    case "W":
                        // web
                        strURL = this.EffectiveWebURL;
                        if (!string.IsNullOrEmpty(strURL))
                        {
                            strURL = strURL.Replace("[TEXT]", WebUtility.UrlEncode(searchText));
                            strURL = strURL.Replace("[DOMAIN]", string.Empty);
                            await UrlUtils.OpenNewWindow(this.JsRuntime, strURL);
                        }

                        break;
                }
            }
            else
            {
                this.Navigation.NavigateTo(this.navigationManager.NavigateURL(searchTabId), forceLoad: true);
            }
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (this.loaded)
            {
                return;
            }

            this.loaded = true;

            this.servicesFramework.RequestAjaxAntiForgerySupport();
            this.clientResourceController.RegisterStylesheet("~/resources/Search/SearchSkinObjectPreview.css", FileOrder.Css.ModuleCss);
            this.clientResourceController.RegisterScript("~/resources/Search/SearchSkinObjectPreview.js");

            if (this.MinCharRequired == 0)
            {
                this.MinCharRequired = 2;
            }

            if (this.AutoSearchDelayInMilliSecond == 0)
            {
                this.AutoSearchDelayInMilliSecond = 400;
            }

            this.PortalId = this.PortalSettings.ActiveTab.IsSuperTab ? this.PortalSettings.PortalId : -1;

            if (!string.IsNullOrEmpty(this.Submit))
            {
                if (this.Submit.IndexOf("src=", StringComparison.Ordinal) != -1)
                {
                    this.Submit = this.Submit.Replace("src=\"", "src=\"" + this.PortalSettings.ActiveTab.SkinPath);
                    this.Submit = this.Submit.Replace("src='", "src='" + this.PortalSettings.ActiveTab.SkinPath);
                }
            }
            else
            {
                this.Submit = Localization.GetString("Search", Localization.GetResourceFile(this, MyFileName));
            }

            // Everything that needs the request has to be resolved now: HttpContext is gone once the
            // circuit is interactive, while the button handlers run on the circuit.
            this.searchTabId = this.GetSearchTabId();
            this.requestHost = this.Request?.Host.Host ?? (this.PortalSettings.PortalAlias as DotNetNuke.Abstractions.Portals.IPortalAliasInfo)?.HttpAlias;
            this.CultureCode = System.Threading.Thread.CurrentThread.CurrentCulture.ToString();
            this.SearchType = "S";

            if (this.UseDropDownList)
            {
                var page = this.Page;
                if (page != null)
                {
                    ClientAPI.RegisterClientVariable(page, "SearchIconWebUrl", string.Format("url({0})", Globals.ResolveUrl(this.EffectiveWebIconURL)), true);
                    ClientAPI.RegisterClientVariable(page, "SearchIconSiteUrl", string.Format("url({0})", Globals.ResolveUrl(this.EffectiveSiteIconURL)), true);

                    // We are going to use a dnn client variable to store which search option (web/site) is selected.
                    ClientAPI.RegisterClientVariable(page, "SearchIconSelected", "S", true);
                }

                var context = DotNetNuke.Common.HttpContextSource.Current;
                if (context != null)
                {
                    JavaScript.RegisterClientReference(context, ClientAPI.ClientNamespaceReferences.dnn);
                }

                this.clientResourceController.CreateScript("~/resources/Search/Search.js").SetPriority(FileOrder.Js.DefaultPriority).SetProvider(ClientResourceProviders.DnnFormBottomProvider).Register();
            }

            // The initialization glue the .ascx declared inline; invoked in OnAfterRenderAsync.
            this.clientResourceController.RegisterScript("~/resources/Search/SearchSkinObjectInit.js", FileOrder.Js.DefaultPriority);
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (!firstRender)
            {
                return;
            }

            // Replaces the inline <script> block of search.ascx; the settings object is identical.
            var settings = new
            {
                delayTriggerAutoSearch = this.AutoSearchDelayInMilliSecond,
                minCharRequiredTriggerAutoSearch = this.MinCharRequired,
                searchType = this.SearchType,
                enableWildSearch = this.EnableWildSearch,
                cultureCode = this.CultureCode,
                portalId = this.PortalId,
            };

            try
            {
                await this.JsRuntime.InvokeVoidAsync(
                    "dnnSearchSkinObjectInit",
                    settings,
                    "SiteRadioButton",
                    "WebRadioButton",
                    this.UseDropDownList);
            }
            catch (JSException)
            {
                // the search client script is not available on this page; the server side search
                // (the button handlers below) keeps working, which is what the original did too
                // when dnn.searchSkinObject was undefined.
            }
        }

        /// <summary>Handles the Click event of the cmdSearchNew control.</summary>
        /// <returns>A task that completes once the search was routed.</returns>
        /// <remarks>This event is only used when <see cref="UseDropDownList">UseDropDownList</see> is true.</remarks>
        protected async Task CmdSearchNewClickAsync()
        {
            this.SearchType = await this.GetSelectedSearchTypeAsync();
            await this.ExecuteSearch((this.SearchTextNew ?? string.Empty).Trim(), this.SearchType);
        }

        /// <summary>Handles the Click event of the cmdSearch control.</summary>
        /// <returns>A task that completes once the search was routed.</returns>
        /// <remarks>This event is only used when <see cref="UseDropDownList">UseDropDownList</see> is false.</remarks>
        protected async Task CmdSearchClickAsync()
        {
            this.SearchType = "S";
            if (this.WebRadioButtonVisible)
            {
                if (await this.IsWebSearchCheckedAsync())
                {
                    this.SearchType = "W";
                }
            }

            await this.ExecuteSearch((this.SearchText ?? string.Empty).Trim(), this.SearchType);
        }

        /// <summary>Reads the search type the drop-down rendering stored in a DNN client variable.</summary>
        /// <returns>"S" for a site search or "W" for a web search.</returns>
        /// <remarks>
        /// Was <c>ClientAPI.GetClientVariable(this.Page, "SearchIconSelected")</c>. The variable is
        /// maintained by <c>Search.js</c> on the client, so it is read from there when the server
        /// side page seam is not available (which is the case on a live circuit).
        /// </remarks>
        private async Task<string> GetSelectedSearchTypeAsync()
        {
            var page = this.Page;
            if (page != null)
            {
                var serverValue = ClientAPI.GetClientVariable(page, "SearchIconSelected");
                if (!string.IsNullOrEmpty(serverValue))
                {
                    return serverValue;
                }
            }

            try
            {
                var clientValue = await this.JsRuntime.InvokeAsync<string>("dnn.getVar", "SearchIconSelected");
                if (!string.IsNullOrEmpty(clientValue))
                {
                    return clientValue;
                }
            }
            catch (JSException)
            {
                // fall through to the default below
            }

            return "S";
        }

        /// <summary>Determines whether the "web" radio button of the classic rendering is checked.</summary>
        /// <returns><see langword="true"/> when a web search was asked for.</returns>
        /// <remarks>
        /// Was <c>WebRadioButton.Checked</c>, which WebForms restored from the postback. The radios
        /// are plain inputs driven by the search client script, so their state is read from the DOM.
        /// </remarks>
        private async Task<bool> IsWebSearchCheckedAsync()
        {
            try
            {
                return await this.JsRuntime.InvokeAsync<bool>("dnnSearchSkinObjectIsWebChecked", "WebRadioButton");
            }
            catch (JSException)
            {
                return false;
            }
        }

        private int GetSearchTabId()
        {
            int searchTabId = this.PortalSettings.SearchTabId;

            // SENTINEL: Null.NullInteger (-1) means "not configured".
            if (searchTabId == Null.NullInteger)
            {
                ArrayList arrModules = ModuleController.Instance.GetModulesByDefinition(this.PortalSettings.PortalId, "Search Results");
                if (arrModules.Count > 1)
                {
                    foreach (ModuleInfo searchModule in arrModules)
                    {
                        if (searchModule.CultureCode == this.PortalSettings.CultureCode)
                        {
                            searchTabId = searchModule.TabID;
                        }
                    }
                }
                else if (arrModules.Count == 1)
                {
                    searchTabId = ((ModuleInfo)arrModules[0]).TabID;
                }
            }

            return searchTabId;
        }
    }
}
