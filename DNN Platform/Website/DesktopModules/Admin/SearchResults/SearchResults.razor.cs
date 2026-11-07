// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.SearchResults
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Search.Internals;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A control which displays search results.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>SearchResults.ascx</c> + <c>SearchResults.ascx.cs</c> +
    /// <c>SearchResults.ascx.designer.cs</c>; the markup now lives in <c>SearchResults.razor</c>.
    /// The designer fields map as follows:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>ResultsPerPageList</c> (a <c>DnnComboBox</c> with four literal items) is <see cref="ResultsPerPageItems"/>, whose selection is still made by looking the current <see cref="PageSize"/> up in the list (<c>FindItemByValue</c>).</description></item>
    /// <item><description><c>AdvnacedDatesList</c> (six resource-keyed items) is <see cref="AdvancedDatesItems"/>, selected by <see cref="SetLastModifiedFilter"/> exactly as before.</description></item>
    /// <item><description><c>SearchScopeList</c> (filled from <see cref="SearchContentSources"/>) is <see cref="SearchScopeItems"/>, with the same <see cref="CheckedScopeItem"/> rule.</description></item>
    /// <item><description><c>lblAdvancedTags</c>/<c>lblAdvancedDates</c>/<c>lblAdvancedScope</c>/<c>lblAdvancedExactSearch</c> are the four <c>LabelControl</c> components in the markup, pointed at this control's own resource file.</description></item>
    /// </list>
    /// <para>
    /// Nothing else changed: the query string is read through the same keys with the same defaults
    /// (<c>Page</c> is 1-based, as <see cref="DefaultPageIndex"/> says, and <c>Size</c> defaults to
    /// 15), the module settings are read through the same <c>Settings</c> hashtable, and the
    /// permission trimming of the result list stays where it always was — in the search Web API the
    /// client module queries, plus the <c>IsPrivate</c> content-source filter below.
    /// </para>
    /// </remarks>
    public partial class SearchResults : PortalModuleBase
    {
        private const int DefaultPageIndex = 1;
        private const int DefaultPageSize = 15;
        private const int DefaultSortOption = 0;

        private const string MyFileName = "SearchResults.ascx";

        private readonly IJavaScriptLibraryHelper javaScript;
        private readonly IClientResourceController clientResourceController;
        private readonly IServicesFramework servicesFramework;
        private readonly IApplicationStatusInfo appStatus;
        private readonly List<SearchResultsListItem> resultsPerPageItems = new List<SearchResultsListItem>
        {
            // the four <asp:ListItem>s declared inside <dnn:DnnComboBox ID="ResultsPerPageList">.
            new SearchResultsListItem("15", "15"),
            new SearchResultsListItem("25", "25"),
            new SearchResultsListItem("50", "50"),
            new SearchResultsListItem("100", "100"),
        };

        private readonly List<SearchResultsListItem> advancedDatesItems = new List<SearchResultsListItem>();
        private readonly List<SearchResultsListItem> searchScopeItems = new List<SearchResultsListItem>();
        private IList<string> searchContentSources;
        private IList<int> searchPortalIds;
        private bool loaded;

        /// <summary>Initializes a new instance of the <see cref="SearchResults"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public SearchResults()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="SearchResults"/> class.</summary>
        /// <param name="javaScript">The JavaScript library helper.</param>
        /// <param name="clientResourceController">The client resources controller.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public SearchResults(IJavaScriptLibraryHelper javaScript, IClientResourceController clientResourceController)
            : this(javaScript, clientResourceController, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="SearchResults"/> class.</summary>
        /// <param name="javaScript">The JavaScript library helper.</param>
        /// <param name="clientResourceController">The client resources controller.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        public SearchResults(IJavaScriptLibraryHelper javaScript, IClientResourceController clientResourceController, IServicesFramework servicesFramework)
        {
            this.javaScript = javaScript ?? Globals.GetCurrentServiceProvider().GetRequiredService<IJavaScriptLibraryHelper>();
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
            this.servicesFramework = servicesFramework ?? Globals.GetCurrentServiceProvider().GetRequiredService<IServicesFramework>();
            this.appStatus = Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
        }

        /// <summary>Gets the virtual path of this control's resource file.</summary>
        /// <remarks>
        /// Replaces <c>Localization.GetResourceFile(this, MyFileName)</c>, which built
        /// <c>TemplateSourceDirectory + "/App_LocalResources/SearchResults.ascx"</c>; the same
        /// string, so every resource key resolves out of the same <c>.resx</c> as before (and so
        /// per-portal Language Editor overrides of the search prompts keep applying).
        /// </remarks>
        protected string ResourceFile =>
            this.ControlPath + Localization.LocalResourceDirectory + "/" + MyFileName;

        protected string SearchTerm
        {
            get { return this.QueryValue("Search"); }
        }

        protected string TagsQuery
        {
            get { return this.QueryValue("Tag"); }
        }

        protected string SearchScopeParam
        {
            get { return this.QueryValue("Scope"); }
        }

        protected string[] SearchScope
        {
            get
            {
                var searchScopeParam = this.SearchScopeParam;
                return string.IsNullOrEmpty(searchScopeParam) ? [] : searchScopeParam.Split(',');
            }
        }

        protected string LastModifiedParam
        {
            get { return this.QueryValue("LastModified"); }
        }

        protected int PageIndex
        {
            get
            {
                if (string.IsNullOrEmpty(this.QueryValue("Page")))
                {
                    return DefaultPageIndex;
                }

                int pageIndex;
                if (int.TryParse(this.QueryValue("Page"), out pageIndex))
                {
                    return pageIndex;
                }

                return DefaultPageIndex;
            }
        }

        protected int PageSize
        {
            get
            {
                if (string.IsNullOrEmpty(this.QueryValue("Size")))
                {
                    return DefaultPageSize;
                }

                int pageSize;
                if (int.TryParse(this.QueryValue("Size"), out pageSize))
                {
                    return pageSize;
                }

                return DefaultPageSize;
            }
        }

        protected int SortOption
        {
            get
            {
                if (string.IsNullOrEmpty(this.QueryValue("Sort")))
                {
                    return DefaultSortOption;
                }

                int sortOption;
                if (int.TryParse(this.QueryValue("Sort"), out sortOption))
                {
                    return sortOption;
                }

                return DefaultSortOption;
            }
        }

        /// <summary>Gets a value indicating whether the exact-search box is checked.</summary>
        /// <remarks>
        /// Was <c>CheckedExactSearch</c>, which emitted the literal <c>checked="true"</c> attribute
        /// text; the markup renders the attribute from this boolean instead, which produces the
        /// same rendered checkbox for the same query string.
        /// </remarks>
        protected bool ExactSearchChecked
        {
            get
            {
                var paramExactSearch = this.QueryValue("ExactSearch");

                return !string.IsNullOrEmpty(paramExactSearch) && paramExactSearch.ToLowerInvariant() == "y";
            }
        }

        protected string LinkTarget
        {
            get
            {
                string settings = Convert.ToString(this.Settings["LinkTarget"]);
                return string.IsNullOrEmpty(settings) || settings == "0" ? string.Empty : " target=\"_blank\" ";
            }
        }

        protected bool ShowDescription => this.GetBooleanSetting("ShowDescription", true);

        protected bool ShowSnippet => this.GetBooleanSetting("ShowSnippet", true);

        protected bool ShowSource => this.GetBooleanSetting("ShowSource", true);

        protected bool ShowLastUpdated => this.GetBooleanSetting("ShowLastUpdated", true);

        protected bool ShowTags => this.GetBooleanSetting("ShowTags", true);

        protected int MaxDescriptionLength => this.GetIntegerSetting("MaxDescriptionLength", 100);

        protected IList<string> SearchContentSources
        {
            get
            {
                if (this.searchContentSources == null)
                {
                    IList<int> portalIds = this.SearchPortalIds;
                    var list = new List<SearchContentSource>();
                    foreach (int portalId in portalIds)
                    {
                        IEnumerable<SearchContentSource> crawlerList =
                            InternalSearchController.Instance.GetSearchContentSourceList(portalId);
                        foreach (SearchContentSource src in crawlerList)
                        {
                            // a private content source is never offered as a scope filter: this is
                            // part of what keeps the result list trimmed, so it is preserved as is.
                            if (src.IsPrivate)
                            {
                                continue;
                            }

                            if (list.All(r => r.LocalizedName != src.LocalizedName))
                            {
                                list.Add(src);
                            }
                        }
                    }

                    List<string> configuredList = null;

                    if (!string.IsNullOrEmpty(Convert.ToString(this.Settings["ScopeForFilters"])))
                    {
                        configuredList = Convert.ToString(this.Settings["ScopeForFilters"]).Split('|').ToList();
                    }

                    this.searchContentSources = new List<string>();

                    // add other searchable module defs
                    foreach (SearchContentSource contentSource in list)
                    {
                        if (configuredList == null ||
                            configuredList.Any(l => l.Equals(contentSource.LocalizedName)))
                        {
                            if (!this.searchContentSources.Equals(contentSource.LocalizedName))
                            {
                                this.searchContentSources.Add(contentSource.LocalizedName);
                            }
                        }
                    }
                }

                return this.searchContentSources;
            }
        }

        protected string DefaultText
        {
            get { return Localization.GetSafeJSString("DefaultText", this.ResourceFile); }
        }

        protected string NoResultsText
        {
            get { return Localization.GetSafeJSString("NoResults", this.ResourceFile); }
        }

        protected string AdvancedText
        {
            get { return Localization.GetSafeJSString("Advanced", this.ResourceFile); }
        }

        protected string SourceText
        {
            get { return Localization.GetSafeJSString("Source", this.ResourceFile); }
        }

        protected string TagsText
        {
            get { return Localization.GetSafeJSString("Tags", this.ResourceFile); }
        }

        protected string AuthorText
        {
            get { return Localization.GetSafeJSString("Author", this.ResourceFile); }
        }

        protected string LikesText
        {
            get { return Localization.GetSafeJSString("Likes", this.ResourceFile); }
        }

        protected string ViewsText
        {
            get { return Localization.GetSafeJSString("Views", this.ResourceFile); }
        }

        protected string CommentsText
        {
            get { return Localization.GetSafeJSString("Comments", this.ResourceFile); }
        }

        protected string RelevanceText
        {
            get { return Localization.GetSafeJSString("Relevance", this.ResourceFile); }
        }

        protected string DateText
        {
            get { return Localization.GetSafeJSString("Date", this.ResourceFile); }
        }

        protected string SearchButtonText
        {
            get { return Localization.GetSafeJSString("btnSearch", this.ResourceFile); }
        }

        protected string ClearButtonText
        {
            get { return Localization.GetSafeJSString("btnClear", this.ResourceFile); }
        }

        protected string AdvancedSearchHintText
        {
            get { return Localization.GetString("AdvancedSearchHint", this.ResourceFile); }
        }

        protected string LinkAdvancedTipText
        {
            get { return Localization.GetString("linkAdvancedTip", this.ResourceFile); }
        }

        protected string LastModifiedText
        {
            get { return Localization.GetString("LastModified", this.ResourceFile); }
        }

        protected string ResultsPerPageText
        {
            get { return Localization.GetString("ResultPerPage", this.ResourceFile); }
        }

        protected string AddTagText
        {
            get { return Localization.GetSafeJSString("AddTag", this.ResourceFile); }
        }

        protected string ResultsCountText
        {
            get { return Localization.GetSafeJSString("ResultsCount", this.ResourceFile); }
        }

        protected string CurrentPageIndexText
        {
            get
            {
                return Localization.GetSafeJSString("CurrentPageIndex", this.ResourceFile);
            }
        }

        protected string CultureCode { get; set; }

        /// <summary>Gets the items of the <c>ResultsPerPageList</c> combo box.</summary>
        protected IReadOnlyList<SearchResultsListItem> ResultsPerPageItems => this.resultsPerPageItems;

        /// <summary>Gets the items of the <c>AdvnacedDatesList</c> combo box.</summary>
        protected IReadOnlyList<SearchResultsListItem> AdvancedDatesItems => this.advancedDatesItems;

        /// <summary>Gets the items of the <c>SearchScopeList</c> combo box.</summary>
        protected IReadOnlyList<SearchResultsListItem> SearchScopeItems => this.searchScopeItems;

        /// <summary>Gets the JSON the client module is initialized from.</summary>
        /// <remarks>
        /// Replaces the inline <c>&lt;script&gt;</c> block of the <c>.ascx</c>: every member below
        /// is the value that block interpolated, under the same name and of the same JavaScript
        /// type (the five <c>show*</c> flags and <c>maxDescriptionLength</c> were emitted as bare
        /// <c>true</c>/<c>false</c>/number literals, so they are real JSON booleans/numbers here).
        /// It is rendered into a <c>data-</c> attribute, which Razor HTML-encodes, and parsed by
        /// the bootstrap block of <c>dnn.searchResult.js</c>.
        /// </remarks>
        protected string ClientOptionsJson
        {
            get
            {
                var options = new
                {
                    moduleId = this.ModuleId,

                    // the ids of the three selects the bootstrap block turns into combo boxes.
                    comboResultsPerPage = "ResultsPerPageList",
                    comboAdvancedDates = "AdvnacedDatesList",
                    comboAdvancedScope = "SearchScopeList",

                    // DnnComboBox.Options.Localization, with the module's own AllFeaturesSelected
                    // override the code behind assigned to the scope list.
                    comboLocalization = new
                    {
                        ItemsChecked = Web.UI.Utilities.GetLocalizedString("ItemsCheckedString"),
                        AllItemsChecked = Localization.GetString("AllFeaturesSelected", this.ResourceFile),
                    },

                    queryOptions = new
                    {
                        searchTerm = this.SearchTerm,
                        sortOption = this.SortOption,
                        pageIndex = this.PageIndex,
                        pageSize = this.PageSize,
                    },

                    settings = new
                    {
                        defaultText = this.DefaultText,
                        comboAdvancedDates = "AdvnacedDatesList",
                        comboAdvancedScope = "SearchScopeList",
                        noresultsText = this.NoResultsText,
                        advancedText = this.AdvancedText,
                        sourceText = this.SourceText,
                        authorText = this.AuthorText,
                        likesText = this.LikesText,
                        viewsText = this.ViewsText,
                        commentsText = this.CommentsText,
                        tagsText = this.TagsText,
                        addTagText = this.AddTagText,
                        lastModifiedText = this.LastModifiedText,
                        resultsCountText = this.ResultsCountText,
                        currentPageIndexText = this.CurrentPageIndexText,
                        linkTarget = this.LinkTarget,
                        showDescription = this.ShowDescription,
                        maxDescriptionLength = this.MaxDescriptionLength,
                        showSnippet = this.ShowSnippet,
                        showSource = this.ShowSource,
                        showLastUpdated = this.ShowLastUpdated,
                        showTags = this.ShowTags,
                        cultureCode = this.CultureCode,
                    },
                };

                return JsonSerializer.Serialize(options);
            }
        }

        private IList<int> SearchPortalIds
        {
            get
            {
                if (this.searchPortalIds == null)
                {
                    this.searchPortalIds = new List<int>();
                    if (!string.IsNullOrEmpty(Convert.ToString(this.Settings["ScopeForPortals"])))
                    {
                        List<string> list = Convert.ToString(this.Settings["ScopeForPortals"]).Split('|').ToList();
                        foreach (string l in list)
                        {
                            this.searchPortalIds.Add(Convert.ToInt32(l));
                        }
                    }
                    else
                    {
                        this.searchPortalIds.Add(this.PortalId); // no setting, just search current portal by default
                    }
                }

                return this.searchPortalIds;
            }
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // OnLoad can be raised again when the component's parameters change; the Web Forms
            // version ran once per request, so the list building is guarded.
            if (this.loaded)
            {
                return;
            }

            this.loaded = true;

            this.servicesFramework.RequestAjaxAntiForgerySupport();
            this.javaScript.RequestRegistration(CommonJs.DnnPlugins);
            this.clientResourceController.RegisterScript("~/resources/Shared/scripts/dnn.searchBox.js");
            this.clientResourceController.RegisterStylesheet("~/resources/Shared/stylesheets/dnn.searchBox.css", Abstractions.ClientResources.FileOrder.Css.ModuleCss);
            this.clientResourceController.RegisterScript("~/desktopmodules/Admin/SearchResults/dnn.searchResult.js");

            // The three combo boxes used to register the Selectize client component for
            // themselves; this control renders the plain selects, so it registers it.
            this.RegisterComboBoxResources();

            this.CultureCode = Thread.CurrentThread.CurrentCulture.ToString();

            this.BuildAdvancedDatesItems();

            foreach (string o in this.SearchContentSources)
            {
                this.searchScopeItems.Add(new SearchResultsListItem(o, o) { Selected = this.CheckedScopeItem(o) });
            }

            var pageSizeItem = this.resultsPerPageItems.FirstOrDefault(i => i.Value == this.PageSize.ToString());
            if (pageSizeItem != null)
            {
                pageSizeItem.Selected = true;
            }

            this.SetLastModifiedFilter();
        }

        private void BuildAdvancedDatesItems()
        {
            // the six <asp:ListItem>s of <dnn:DnnComboBox ID="AdvnacedDatesList">, whose captions
            // came from their ResourceKey and which had the "all" option selected by default.
            this.advancedDatesItems.Clear();
            this.advancedDatesItems.Add(new SearchResultsListItem(Localization.GetString("optionAll", this.ResourceFile), string.Empty) { Selected = true });
            this.advancedDatesItems.Add(new SearchResultsListItem(Localization.GetString("optionDay", this.ResourceFile), "day"));
            this.advancedDatesItems.Add(new SearchResultsListItem(Localization.GetString("optionWeek", this.ResourceFile), "week"));
            this.advancedDatesItems.Add(new SearchResultsListItem(Localization.GetString("optionMonth", this.ResourceFile), "month"));
            this.advancedDatesItems.Add(new SearchResultsListItem(Localization.GetString("optionQuarter", this.ResourceFile), "quarter"));
            this.advancedDatesItems.Add(new SearchResultsListItem(Localization.GetString("optionYear", this.ResourceFile), "year"));
        }

        /// <summary>Registers the client resources <c>DnnComboBox</c> used to register for itself.</summary>
        /// <remarks>
        /// This is <c>DnnComboBox.RegisterRequestResources</c>: without it the three selects would
        /// render as bare drop downs and the advanced-search form would lose the checkbox scope
        /// list the client module reads through <c>selectize</c>.
        /// </remarks>
        private void RegisterComboBoxResources()
        {
            if (this.appStatus.Status != UpgradeStatus.None)
            {
                return;
            }

            var package = JavaScriptLibraryController.Instance.GetLibrary(l => l.LibraryName == "Selectize");
            if (package == null)
            {
                return;
            }

            this.javaScript.RequestRegistration("Selectize");

            var libraryPath =
                $"~/resources/Libraries/{package.LibraryName}/{Globals.FormatVersion(package.Version, "00", 3, "_")}/";

            this.clientResourceController.RegisterScript($"{libraryPath}dnn.combobox.js");
            this.clientResourceController.RegisterStylesheet($"{libraryPath}selectize.css");
            this.clientResourceController.RegisterStylesheet($"{libraryPath}selectize.default.css");
            this.clientResourceController.RegisterScript("~/resources/Shared/scripts/dnn.ControlsInterop.js");
        }

        /// <summary>Reads a query string value, replacing <c>Request.QueryString[key]</c>.</summary>
        /// <param name="key">The query string key.</param>
        /// <returns>The value, or an empty string when it is absent.</returns>
        private string QueryValue(string key)
        {
            var query = this.Request?.Query;
            if (query == null || !query.ContainsKey(key))
            {
                return string.Empty;
            }

            return query[key].ToString() ?? string.Empty;
        }

        private bool CheckedScopeItem(string scopeItemName)
        {
            var searchScope = this.SearchScope;
            return searchScope.Length == 0 || searchScope.Any(x => x == scopeItemName);
        }

        private void SetLastModifiedFilter()
        {
            var lastModifiedParam = this.LastModifiedParam;

            if (!string.IsNullOrEmpty(lastModifiedParam))
            {
                var item = this.advancedDatesItems.FirstOrDefault(x => x.Value == lastModifiedParam);
                if (item != null)
                {
                    item.Selected = true;
                }
            }
        }

        private bool GetBooleanSetting(string settingName, bool defaultValue)
        {
            if (this.Settings.ContainsKey(settingName) && !string.IsNullOrEmpty(Convert.ToString(this.Settings[settingName])))
            {
                return Convert.ToBoolean(this.Settings[settingName]);
            }

            return defaultValue;
        }

        private int GetIntegerSetting(string settingName, int defaultValue)
        {
            var settingValue = Convert.ToString(this.Settings[settingName]);
            if (!string.IsNullOrEmpty(settingValue) && Regex.IsMatch(settingValue, "^\\d+$"))
            {
                return Convert.ToInt32(settingValue);
            }

            return defaultValue;
        }

        /// <summary>An item of one of this control's three combo boxes.</summary>
        /// <remarks>Replaces <c>System.Web.UI.WebControls.ListItem</c>, with the same three members.</remarks>
        protected sealed class SearchResultsListItem
        {
            /// <summary>Initializes a new instance of the <see cref="SearchResultsListItem"/> class.</summary>
            /// <param name="text">The item's caption.</param>
            /// <param name="value">The item's value.</param>
            public SearchResultsListItem(string text, string value)
            {
                this.Text = text;
                this.Value = value;
            }

            /// <summary>Gets the item's caption.</summary>
            public string Text { get; }

            /// <summary>Gets the item's value.</summary>
            public string Value { get; }

            /// <summary>Gets or sets a value indicating whether the item is selected.</summary>
            public bool Selected { get; set; }
        }
    }
}
