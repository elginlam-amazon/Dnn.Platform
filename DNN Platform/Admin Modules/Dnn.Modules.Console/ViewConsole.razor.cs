// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Modules.Console;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

using Dnn.Modules.Console.Components;
using DotNetNuke.Abstractions;
using DotNetNuke.Abstractions.Application;
using DotNetNuke.Abstractions.ClientResources;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Tabs;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Instrumentation;
using DotNetNuke.Internal.SourceGenerators;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Security.Roles;
using DotNetNuke.Services.ClientDependency;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Services.Localization;
using DotNetNuke.Services.Personalization;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>Implements the module view logic.</summary>
/// <remarks>
/// Was <c>ViewConsole.ascx</c> + <c>ViewConsole.ascx.cs</c> + <c>ViewConsole.ascx.designer.cs</c>;
/// the markup is <c>ViewConsole.razor</c>. The five designer-generated server controls became the
/// state below, which <c>ViewConsole.razor</c> binds to:
/// <list type="bullet">
/// <item><description>the <c>Console</c> <c>HtmlGenericControl</c> is <see cref="ConsoleElementId"/>, <see cref="ConsoleCssClass"/> and <see cref="ConsoleStyle"/>, which carry the id, the appended mode class and the <c>ConsoleWidth</c> style the code behind assigned to its <c>Attributes</c>;</description></item>
/// <item><description>the <c>IconSize</c> and <c>View</c> <c>DropDownList</c>s are <see cref="IconSizeItems"/>/<see cref="ViewItems"/>, preselected through <see cref="DefaultSize"/>/<see cref="DefaultView"/> as <c>SelectedValue</c> did, and rendered only when <see cref="AllowSizeChange"/>/<see cref="AllowViewChange"/>, which was their <c>Visible</c>;</description></item>
/// <item><description>the <c>SettingsBreak</c> <c>&lt;br&gt;</c> is <see cref="SettingsBreakVisible"/>;</description></item>
/// <item><description>the <c>DetailView</c> <c>Repeater</c> and its recursive <c>ItemDataBound</c> handler are <see cref="DetailViewHtml"/>, built by <see cref="AppendTabHtml"/> in the same depth-first order and with the same (absent) wrapper markup.</description></item>
/// </list>
/// </remarks>
public partial class ViewConsole : PortalModuleBase, IAsyncDisposable
{
    private static readonly ILogger Logger = DnnLoggingController.GetLogger<ViewConsole>();

    private readonly IList<ConsoleListItem> iconSizeItems = new List<ConsoleListItem>();
    private readonly IList<ConsoleListItem> viewItems = new List<ConsoleListItem>();

    private INavigationManager navigationManager;
    private IJavaScriptLibraryHelper javaScript;
    private IClientResourceController clientResourceController;
    private IHostSettings hostSettings;

    private string defaultSize = string.Empty;
    private string defaultView = string.Empty;
    private int groupTabId = -1;
    private IList<TabInfo> tabs;
    private bool loaded;
    private string consoleCssClass = "console";
    private MarkupString detailViewHtml;
    private IJSObjectReference interopModule;
    private bool rendered;

    /// <summary>Initializes a new instance of the <see cref="ViewConsole"/> class.</summary>
    [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
    public ViewConsole()
        : this(null, null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ViewConsole"/> class.</summary>
    /// <param name="navigationManager">The navigation manager.</param>
    /// <param name="javaScript">The JavaScript library helper.</param>
    /// <param name="clientResourceController">The client resources controller.</param>
    [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
    public ViewConsole(INavigationManager navigationManager, IJavaScriptLibraryHelper javaScript, IClientResourceController clientResourceController)
        : this(navigationManager, javaScript, clientResourceController, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ViewConsole"/> class.</summary>
    /// <param name="navigationManager">The navigation manager.</param>
    /// <param name="javaScript">The JavaScript library helper.</param>
    /// <param name="clientResourceController">The client resources controller.</param>
    /// <param name="hostSettings">The host settings.</param>
    /// <remarks>
    /// The Blazor renderer creates components through their parameterless constructor, so the
    /// overloads above are what the DI-aware call sites use; every service they do not supply is
    /// resolved from <see cref="PortalModuleBase.DependencyProvider"/>, exactly as the WebForms
    /// control did. Resolving the unsupplied ones is deferred to <see cref="EnsureServices"/>,
    /// called from <see cref="OnInit"/>, because the renderer - not DI - constructs the component
    /// and <c>DependencyProvider</c> is backed by a request-scoped lazy scope.
    /// </remarks>
    public ViewConsole(INavigationManager navigationManager, IJavaScriptLibraryHelper javaScript, IClientResourceController clientResourceController, IHostSettings hostSettings)
    {
        this.navigationManager = navigationManager;
        this.javaScript = javaScript;
        this.clientResourceController = clientResourceController;
        this.hostSettings = hostSettings;
    }

    /// <summary>Gets a value indicating whether the module settings allow size change.</summary>
    public bool AllowSizeChange => !this.Settings.ContainsKey("AllowSizeChange") || bool.Parse(this.Settings["AllowSizeChange"].ToString());

    /// <summary>Gets a value indicating whether the module settings allow to change view.</summary>
    public bool AllowViewChange => !this.Settings.ContainsKey("AllowViewChange") || bool.Parse(this.Settings["AllowViewChange"].ToString());

    /// <summary>Gets a value indicating whether the module settings indicate to include hidden pages.</summary>
    public bool IncludeHiddenPages => this.Settings.ContainsKey("IncludeHiddenPages") && bool.Parse(this.Settings["IncludeHiddenPages"].ToString());

    /// <summary>Gets the id of the page (tab) for the root node of the console display.</summary>
    public int ConsoleTabID =>
        (this.Mode == "Profile")
            ? this.PortalSettings.UserTabId
            : (this.Settings.ContainsKey("ParentTabID")
                ? int.Parse(this.Settings["ParentTabID"].ToString())
                : this.TabId);

    /// <summary>Gets the configured console width or an empty string if not specified in the settings.</summary>
    public string ConsoleWidth => this.Settings.ContainsKey("ConsoleWidth") ? this.Settings["ConsoleWidth"].ToString() : string.Empty;

    /// <summary>Gets the default size for the console icons.</summary>
    public string DefaultSize
    {
        get
        {
            if (this.defaultSize == string.Empty && this.AllowSizeChange && this.UserId > Null.NullInteger)
            {
                object personalizedValue = this.GetUserSetting("DefaultSize");
                if (personalizedValue != null)
                {
                    this.defaultSize = Convert.ToString(personalizedValue);
                }
            }

            if (this.defaultSize == string.Empty)
            {
                this.defaultSize = this.Settings.ContainsKey("DefaultSize") ? Convert.ToString(this.Settings["DefaultSize"]) : "IconFile";
            }

            return this.defaultSize;
        }
    }

    /// <summary>Gets the default view module for the console.</summary>
    public string DefaultView
    {
        get
        {
            if (this.defaultView == string.Empty && this.AllowViewChange && this.UserId > Null.NullInteger)
            {
                object personalizedValue = this.GetUserSetting("DefaultView");
                if (personalizedValue != null)
                {
                    this.defaultView = Convert.ToString(personalizedValue);
                }
            }

            if (this.defaultView == string.Empty)
            {
                this.defaultView = this.Settings.ContainsKey("DefaultView") ? Convert.ToString(this.Settings["DefaultView"]) : "Hide";
            }

            return this.defaultView;
        }
    }

    /// <summary>Gets the group id, if not displayed in a group, will return <see cref="Null.NullInteger"/>.</summary>
    public int GroupId
    {
        get
        {
            var groupId = Null.NullInteger;
            var value = this.GetRequestParam("GroupId");
            if (!string.IsNullOrEmpty(value))
            {
                groupId = int.Parse(value);
            }

            return groupId;
        }
    }

    /// <summary>Gets a value indicating whether the parent should be shown.</summary>
    public bool IncludeParent => (this.Mode == "Profile") || (this.Settings.ContainsKey("IncludeParent") && bool.Parse(this.Settings["IncludeParent"].ToString()));

    /// <summary>Gets the module display mode.</summary>
    public string Mode => this.Settings.ContainsKey("Mode") ? this.Settings["Mode"].ToString() : "Normal";

    /// <summary>Gets the id of the user when used in a user profile page, if not used on a user profile returns <see cref="Null.NullInteger"/>.</summary>
    public int ProfileUserId
    {
        get
        {
            var userId = Null.NullInteger;
            var value = this.GetRequestParam("UserId");
            if (!string.IsNullOrEmpty(value))
            {
                userId = int.Parse(value);
            }

            return userId;
        }
    }

    /// <summary>Gets a value indicating whether the tooltips should be shown.</summary>
    public bool ShowTooltip => !this.Settings.ContainsKey("ShowTooltip") || bool.Parse(this.Settings["ShowTooltip"].ToString());

    /// <summary>Gets a value indicating whether the pages (tabs) should by ordered by their hierarchy.</summary>
    public bool OrderTabsByHierarchy => this.Settings.ContainsKey("OrderTabsByHierarchy") && bool.Parse(this.Settings["OrderTabsByHierarchy"].ToString());

    /// <summary>Releases the imported JavaScript module.</summary>
    /// <returns>A task representing the operation.</returns>
    /// <remarks>
    /// The module reference is owned by the circuit; disposing it releases the JS side when the
    /// component goes away. A disconnected circuit cannot be called into any more, so the
    /// <see cref="JSDisconnectedException"/> that raises is expected and ignored.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (this.interopModule != null)
        {
            try
            {
                await this.interopModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // the circuit is already gone; there is nothing to release.
            }

            this.interopModule = null;
        }

        this.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Gets or sets the JavaScript runtime used to run the module's client side seam.</summary>
    /// <remarks>Replaces the inline <c>&lt;script&gt;</c> block of <c>ViewConsole.ascx</c>, which a render tree cannot emit.</remarks>
    [Inject]
    protected IJSRuntime JsRuntime { get; set; }

    /// <summary>Gets the <c>id</c> rendered on the console element.</summary>
    /// <remarks>
    /// Replaces <c>Console.ClientID</c>, which the inline script interpolated. WebForms made it
    /// unique per module instance through the naming container; the tab module id does the same
    /// for a component, so several console modules on one page keep distinct ids.
    /// </remarks>
    protected string ConsoleElementId => "Console_" + this.TabModuleId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Gets the css class of the console element.</summary>
    /// <remarks>Replaces <c>Console.Attributes["class"]</c>, which the markup set to "console" and <see cref="OnLoad"/> appended the lower-cased <see cref="Mode"/> to.</remarks>
    protected string ConsoleCssClass => this.consoleCssClass;

    /// <summary>Gets the inline style of the console element, or <see langword="null"/> when no width is configured.</summary>
    /// <remarks>Replaces the <c>Console.Attributes.Add("style", $"width:{ConsoleWidth}")</c> call of <see cref="OnLoad"/>.</remarks>
    protected string ConsoleStyle =>
        this.ConsoleWidth != string.Empty ? $"width:{this.ConsoleWidth}" : null;

    /// <summary>Gets the items of the icon size drop down list.</summary>
    protected IList<ConsoleListItem> IconSizeItems => this.iconSizeItems;

    /// <summary>Gets the items of the view drop down list.</summary>
    protected IList<ConsoleListItem> ViewItems => this.viewItems;

    /// <summary>Gets a value indicating whether the break between the two drop down lists is rendered.</summary>
    /// <remarks>Replaces <c>SettingsBreak.Visible</c>.</remarks>
    protected bool SettingsBreakVisible => this.AllowSizeChange && this.AllowViewChange;

    /// <summary>Gets the rendered tab tree.</summary>
    /// <remarks>Replaces the <c>DetailView</c> repeater's output; see <see cref="BuildDetailView"/>.</remarks>
    protected MarkupString DetailViewHtml => this.detailViewHtml;

    /// <inheritdoc />
    protected override void OnInit(EventArgs e)
    {
        base.OnInit(e);

        try
        {
            this.EnsureServices();

            this.javaScript.RequestRegistration(CommonJs.jQuery);

            this.clientResourceController.RegisterScript("~/desktopmodules/admin/console/scripts/jquery.console.js");

            // The DetailView.ItemDataBound wiring is gone with the repeater; the recursion it
            // performed is BuildDetailView/AppendTabHtml, called from OnLoad.

            // Save User Preferences
            this.SavePersonalizedSettings();
        }
        catch (Exception exc)
        {
            Exceptions.ProcessModuleLoadException(this, exc);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This is the ported <c>OnLoad</c>. The <c>!IsPostBack</c> guard became the
    /// <see cref="loaded"/> flag: <c>OnLoad</c> is raised from <c>OnParametersSet</c>, which runs
    /// again whenever the component's parameters are re-supplied, and the work it guarded (reading
    /// the tab tree and composing its markup) must happen exactly once, as it did on the first,
    /// non-post-back request.
    /// </remarks>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            // IconSize.Visible / View.Visible are the @if guards in the markup; the item
            // collections are rebuilt here because the original added them on every load.
            this.iconSizeItems.Clear();
            this.viewItems.Clear();

            foreach (string val in ConsoleController.GetSizeValues())
            {
                this.iconSizeItems.Add(new ConsoleListItem(val, Localization.GetString(val + ".Text", this.LocalResourceFile)));
            }

            foreach (string val in ConsoleController.GetViewValues())
            {
                this.viewItems.Add(new ConsoleListItem(val, Localization.GetString(val + ".Text", this.LocalResourceFile)));
            }

            // IconSize.SelectedValue = DefaultSize and View.SelectedValue = DefaultView are the
            // `selected` attributes the markup computes from those two properties.
            if (!this.loaded)
            {
                this.loaded = true;

                this.consoleCssClass = this.consoleCssClass + " " + this.Mode.ToLower(CultureInfo.InvariantCulture);

                List<TabInfo> tempTabs = this.IsHostTab()
                    ? TabController.GetTabsBySortOrder(Null.NullInteger).OrderBy(t => t.Level).ThenBy(t => t.LocalizedTabName).ToList()
                    : TabController.GetTabsBySortOrder(this.PortalId).OrderBy(t => t.Level).ThenBy(t => t.LocalizedTabName).ToList();

                this.tabs = new List<TabInfo>();

                IList<int> tabIdList = new List<int>();
                tabIdList.Add(this.ConsoleTabID);

                if (this.IncludeParent)
                {
                    TabInfo consoleTab = TabController.Instance.GetTab(this.ConsoleTabID, this.PortalId);
                    if (consoleTab != null)
                    {
                        this.tabs.Add(consoleTab);
                    }
                }

                foreach (TabInfo tab in tempTabs)
                {
                    if (!this.CanShowTab(tab))
                    {
                        continue;
                    }

                    if (tabIdList.Contains(tab.ParentId))
                    {
                        if (!tabIdList.Contains(tab.TabID))
                        {
                            tabIdList.Add(tab.TabID);
                        }

                        this.tabs.Add(tab);
                    }
                }

                // if OrderTabsByHierarchy set to true, we need reorder the tab list to move tabs which have child tabs to the end of list.
                // so that the list display in UI can show tabs in same level in same area, and not break by child tabs.
                if (this.OrderTabsByHierarchy)
                {
                    this.tabs = this.tabs.OrderBy(t => t.HasChildren).ToList();
                }

                int minLevel = -1;
                if (this.tabs.Count > 0)
                {
                    minLevel = this.tabs.Min(t => t.Level);
                }

                // DetailView.DataSource + DataBind(): the repeater walked these roots and, through
                // its ItemDataBound handler, recursed into each tab's children.
                this.BuildDetailView((minLevel > -1) ? this.tabs.Where(t => t.Level == minLevel) : this.tabs);
            }
        }
        catch (Exception exc)
        {
            Exceptions.ProcessModuleLoadException(this, exc);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Runs the client side seam the <c>.ascx</c> emitted as an inline
    /// <c>jQuery(document).ready(function ($) { $("#&lt;%: Console.ClientID %&gt;").dnnConsole(&lt;%: GetClientSideSettingsObject() %&gt;); });</c>
    /// block. A <c>&lt;script&gt;</c> element produced by a render tree is never executed, so the
    /// block lives in <c>Scripts/consoleInterop.js</c> and is invoked here - the first point at
    /// which JS interop is legal - with the same settings object.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender)
        {
            return;
        }

        try
        {
            IJSObjectReference module;
            try
            {
                module = await this.JsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", Globals.ResolveUrl("~/DesktopModules/Admin/Console/Scripts/consoleInterop.js"));
            }
            catch (JSException)
            {
                // the extension's assets are not reachable; the console degrades to plain markup.
                return;
            }

            this.interopModule = module;

            await module.InvokeVoidAsync("initConsole", this.ConsoleElementId, this.GetClientSideSettingsValues());
        }
        catch (Exception exc)
        {
            Exceptions.ProcessModuleLoadException(this, exc);
        }
        finally
        {
            // jquery.console.js owns this subtree from here on: it adds and removes the
            // console-none/console-small/console-large/active/console-mouseon classes, hides and
            // shows the 16px/32px image pair, rewrites the tooltips and binds click/hover/change
            // handlers on the nodes rendered above. WebForms never re-rendered that markup, but a
            // component re-renders whenever StateHasChanged runs or its parameters are re-supplied
            // (PortalModuleBase.OnParametersSet -> OnLoad is on that path), and the renderer's diff
            // would restore the original attributes and detach the plugin's view state. Latching
            // ShouldRender off after the first render keeps the DOM the plugin's, which is the same
            // guarantee the WebForms control gave it.
            this.rendered = true;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Renders once and then leaves the DOM to <c>jquery.console.js</c>; see the end of
    /// <see cref="OnAfterRenderAsync"/>.
    /// </remarks>
    protected override bool ShouldRender() => !this.rendered;

    /// <summary>Gets the HTML rendering of the console view according to the module settings.</summary>
    /// <param name="tab">The root page to render the console from, <see cref="TabInfo"/>.</param>
    /// <returns>A string containing the rendered HTML.</returns>
    protected string GetHtml(TabInfo tab)
    {
        string returnValue = string.Empty;
        if (this.groupTabId > -1 && this.groupTabId != tab.ParentId)
        {
            this.groupTabId = -1;
            if (!tab.DisableLink)
            {
                returnValue = "<br style=\"clear:both;\" /><br />";
            }
        }

        if (tab.DisableLink)
        {
            returnValue += $"<br style=\"clear:both;\" /><br /><h1><span class=\"TitleHead\">{WebUtility.HtmlEncode(tab.TabName)}</span></h1><br style=\"clear:both\" />";
            this.groupTabId = tab.TabID;
        }
        else
        {
            var sb = new StringBuilder();
            sb.Append(
                tab.TabID == this.PortalSettings.ActiveTab.TabID
                    ? "<div class=\"active console-none \">"
                    : "<div class=\"console-none \">");

            sb.Append("<a href=\"{0}\" aria-label=\"{3}\">");

            if (this.DefaultSize != "IconNone" || this.AllowSizeChange || this.AllowViewChange)
            {
                sb.Append("<img src=\"{1}\" alt=\"{3}\" width=\"16px\" height=\"16px\"/>");
                sb.Append("<img src=\"{2}\" alt=\"{3}\" width=\"32px\" height=\"32px\"/>");
            }

            sb.Append("</a>");
            sb.Append("<h3>{3}</h3>");
            sb.Append("<div>{4}</div>");
            sb.Append("</div>");

            ////const string contentHtml = "<div>" + "<a href=\"{0}\"><img src=\"{1}\" alt=\"{3}\" width=\"16px\" height=\"16px\"/><img src=\"{2}\" alt=\"{3}\" width=\"32px\" height=\"32px\"/></a>" + "<h3>{3}</h3>" + "<div>{4}</div>" + "</div>";
            var tabUrl = tab.FullUrl;
            if (this.ProfileUserId > -1)
            {
                tabUrl = this.navigationManager.NavigateURL(tab.TabID, string.Empty, "UserId=" + this.ProfileUserId.ToString(CultureInfo.InvariantCulture));
            }

            if (this.GroupId > -1)
            {
                tabUrl = this.navigationManager.NavigateURL(tab.TabID, string.Empty, "GroupId=" + this.GroupId.ToString(CultureInfo.InvariantCulture));
            }

            returnValue += string.Format(
                sb.ToString(),
                tabUrl,
                this.GetIconUrl(tab.IconFile, "IconFile"),
                this.GetIconUrl(tab.IconFileLarge, "IconFileLarge"),
                WebUtility.HtmlEncode(tab.LocalizedTabName),
                WebUtility.HtmlEncode(tab.Description));
        }

        return returnValue;
    }

    /// <summary>Gets the client side settings for the module.</summary>
    /// <returns>A settings string ready to use by the .dnnConsole jQuery plugin.</returns>
    [DnnDeprecated(10, 3, 3, "Use GetClientSideSettingsObject")]
    protected partial string GetClientSideSettings()
    {
        string tabModuleId = "-1";
        if (this.UserId > -1)
        {
            tabModuleId = this.TabModuleId.ToString(CultureInfo.InvariantCulture);
        }

        return string.Format(
            "allowIconSizeChange: {0}, allowDetailChange: {1}, selectedSize: '{2}', showDetails: '{3}', tabModuleID: {4}, showTooltip: {5}",
            this.AllowSizeChange.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            this.AllowViewChange.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            JavaScriptEncoder.Default.Encode(this.DefaultSize),
            JavaScriptEncoder.Default.Encode(this.DefaultView),
            tabModuleId,
            this.ShowTooltip.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
    }

    /// <summary>Gets the client side settings for the module.</summary>
    /// <returns>A JSON object with settings for the .dnnConsole jQuery plugin.</returns>
    /// <remarks>The return type was <c>IHtmlString</c>, which is <see cref="IHtmlContent"/> on ASP.NET Core.</remarks>
    protected IHtmlContent GetClientSideSettingsObject()
    {
        return new HtmlString(this.GetClientSideSettingsValues().ToJson());
    }

    /// <summary>Gets the values the <c>.dnnConsole</c> jQuery plugin is initialized with.</summary>
    /// <returns>The settings, as the anonymous object <c>GetClientSideSettingsObject</c> serialized.</returns>
    /// <remarks>
    /// Factored out of <see cref="GetClientSideSettingsObject"/> so the JS interop call in
    /// <see cref="OnAfterRenderAsync"/> passes the plugin the very same values the inline script's
    /// JSON literal did, without serializing and re-parsing them.
    /// </remarks>
    private object GetClientSideSettingsValues()
    {
        var tabModuleId = -1;
        if (this.UserId > -1)
        {
            tabModuleId = this.TabModuleId;
        }

        return new
        {
            allowIconSizeChange = this.AllowSizeChange,
            allowDetailChange = this.AllowViewChange,
            selectedSize = this.DefaultSize,
            showDetails = this.DefaultView,
            tabModuleID = tabModuleId,
            showTooltip = this.ShowTooltip,
        };
    }

    /// <summary>Resolves the services the constructor was not given, as the WebForms control did.</summary>
    private void EnsureServices()
    {
        this.navigationManager ??= this.DependencyProvider.GetRequiredService<INavigationManager>();
        this.javaScript ??= this.DependencyProvider.GetRequiredService<IJavaScriptLibraryHelper>();
        this.clientResourceController ??= this.DependencyProvider.GetRequiredService<IClientResourceController>();
        this.hostSettings ??= this.DependencyProvider.GetRequiredService<IHostSettings>();
    }

    /// <summary>Gets a request value by name, looking in the query string and then the form.</summary>
    /// <param name="name">The name of the value.</param>
    /// <returns>The value, or <see langword="null"/> when there is none.</returns>
    /// <remarks>
    /// Replaces <c>Request.Params[name]</c>, which merged the query string, the form, cookies and
    /// the server variables. The two callers (<see cref="GroupId"/>, <see cref="ProfileUserId"/>)
    /// read values that are supplied as a query string parameter or, on a post, as a form field,
    /// so those are the two collections consulted. <see cref="DotNetNuke.Framework.UserControlBase.Request"/>
    /// is the request captured during the initial render and is <see langword="null"/> when there
    /// is no request in flight.
    /// </remarks>
    private string GetRequestParam(string name)
    {
        var request = this.Request;
        if (request == null)
        {
            return null;
        }

        if (request.Query.TryGetValue(name, out var queryValue))
        {
            return queryValue.ToString();
        }

        if (request.HasFormContentType && request.Form.TryGetValue(name, out var formValue))
        {
            return formValue.ToString();
        }

        return null;
    }

    /// <summary>Composes the markup of the tab tree the <c>DetailView</c> repeater used to render.</summary>
    /// <param name="roots">The root tabs that were the repeater's data source.</param>
    private void BuildDetailView(IEnumerable<TabInfo> roots)
    {
        var sb = new StringBuilder();
        foreach (var tab in roots)
        {
            this.AppendTabHtml(sb, tab);
        }

        this.detailViewHtml = new MarkupString(sb.ToString());
    }

    /// <summary>Appends a tab and its descendants to <paramref name="sb"/>.</summary>
    /// <param name="sb">The builder the markup is appended to.</param>
    /// <param name="tab">The tab to render.</param>
    /// <remarks>
    /// This is <c>RepeaterItemDataBound</c>: it added a <c>Literal</c> with <c>GetHtml(tab)</c> to
    /// the item and, when the tab had children in the bound collection, a nested <c>Repeater</c>
    /// bound to those children with the same handler. Neither a <c>RepeaterItem</c> nor a
    /// <c>Repeater</c> rendered an element of its own, so the result was - and is - the
    /// depth-first concatenation of <see cref="GetHtml"/>, which is the DOM nesting
    /// <c>jquery.console.js</c> addresses.
    /// </remarks>
    private void AppendTabHtml(StringBuilder sb, TabInfo tab)
    {
        sb.Append(this.GetHtml(tab));
        foreach (var child in this.tabs.Where(t => t.ParentId == tab.TabID))
        {
            this.AppendTabHtml(sb, child);
        }
    }

    private bool CanShowTab(TabInfo tab)
    {
        var canShowTab = TabPermissionController.CanViewPage(tab) &&
                         !tab.IsDeleted &&
                         (this.IncludeHiddenPages || tab.IsVisible) &&
                         (tab.StartDate < DateTime.Now || tab.StartDate == Null.NullDate);

        if (!canShowTab)
        {
            return false;
        }

        var key = $"TabVisibility{tab.TabPath.Replace("//", "-")}";
        var visibility = this.Settings.ContainsKey(key) ? this.Settings[key].ToString() : "AllUsers";

        switch (visibility)
        {
            case "Owner":
                canShowTab = this.UserInfo.Social.Roles.SingleOrDefault(ur => ur.RoleID == this.GroupId && ur.IsOwner) != null;
                break;
            case "Members":
                var group = RoleController.Instance.GetRole(this.PortalId, r => r.RoleID == this.GroupId);
                canShowTab = (group != null) && this.UserInfo.IsInRole(group.RoleName);
                break;
            case "Friends":
                var profileUser = UserController.GetUserById(this.hostSettings, this.PortalId, this.ProfileUserId);
                canShowTab = profileUser?.Social.Friend != null || this.UserId == this.ProfileUserId;
                break;
            case "User":
                canShowTab = this.UserId == this.ProfileUserId;
                break;
            case "AllUsers":
                break;
        }

        return canShowTab;
    }

    private string GetIconUrl(string iconURL, string size)
    {
        if (string.IsNullOrEmpty(iconURL))
        {
            iconURL = size == "IconFile"
                ? "~/images/icon_unknown_16px.gif"
                : "~/images/icon_unknown_32px.gif";
        }

        if (!iconURL.Contains("~", StringComparison.Ordinal))
        {
            // Control.ResolveUrl rebased a relative URL on the control's TemplateSourceDirectory,
            // so the result was always app-rooted. Globals.ResolveUrl returns anything that does
            // not start with "~" unchanged, which would leave the browser to resolve the path
            // against the current page's URL - broken on every page that is not at the site root.
            // Making the combined path application-relative preserves the original URL.
            iconURL = "~/" + Path.Combine(this.PortalSettings.HomeDirectory, iconURL)
                .Replace('\\', '/')
                .TrimStart('/');
        }

        // Replaces Control.ResolveUrl, which has no component equivalent; Globals.ResolveUrl
        // applies the same application-root resolution to a "~/" path.
        return Globals.ResolveUrl(iconURL);
    }

    private object GetUserSetting(string key)
    {
        return Personalization.GetProfile(this.ModuleConfiguration.ModuleDefinition.FriendlyName, this.PersonalizationKey(key));
    }

    private bool IsHostTab()
    {
        var returnValue = false;
        if (this.ConsoleTabID != this.TabId)
        {
            if (this.UserInfo is not { IsSuperUser: true, })
            {
                return false;
            }

            var hostTabs = TabController.Instance.GetTabsByPortal(Null.NullInteger);
            if (hostTabs.Keys.Any(key => key == this.ConsoleTabID))
            {
                returnValue = true;
            }
        }
        else
        {
            returnValue = this.PortalSettings.ActiveTab.IsSuperTab;
        }

        return returnValue;
    }

    private string PersonalizationKey(string key)
    {
        return $"{this.PortalId}_{this.TabModuleId}_{key}";
    }

    private void SavePersonalizedSettings()
    {
        if (this.UserId <= -1)
        {
            return;
        }

        var consoleModuleId = -1;
        try
        {
            var consoleModuleIdValue = this.GetQueryStringValue("CTMID");
            if (consoleModuleIdValue != null)
            {
                consoleModuleId = Convert.ToInt32(consoleModuleIdValue);
            }
        }
        catch (Exception exc)
        {
            Logger.ViewConsoleParseConsoleModuleIdException(exc);

            consoleModuleId = -1;
        }

        if (consoleModuleId != this.TabModuleId)
        {
            return;
        }

        var consoleSize = string.Empty;
        var consoleSizeValue = this.GetQueryStringValue("CS");
        if (consoleSizeValue != null)
        {
            consoleSize = consoleSizeValue;
        }

        var consoleView = string.Empty;
        var consoleViewValue = this.GetQueryStringValue("CV");
        if (consoleViewValue != null)
        {
            consoleView = consoleViewValue;
        }

        if (consoleSize != string.Empty && ConsoleController.GetSizeValues().Contains(consoleSize))
        {
            this.SaveUserSetting("DefaultSize", consoleSize);
        }

        if (consoleView != string.Empty && ConsoleController.GetViewValues().Contains(consoleView))
        {
            this.SaveUserSetting("DefaultView", consoleView);
        }
    }

    /// <summary>Gets a query string value by name.</summary>
    /// <param name="name">The name of the value.</param>
    /// <returns>The value, or <see langword="null"/> when the request did not supply it.</returns>
    /// <remarks>Replaces <c>Request.QueryString[name]</c>, which was <see langword="null"/> for an absent key.</remarks>
    private string GetQueryStringValue(string name)
    {
        var request = this.Request;
        return request != null && request.Query.TryGetValue(name, out var value) ? value.ToString() : null;
    }

    private void SaveUserSetting(string key, object val)
    {
        Personalization.SetProfile(this.ModuleConfiguration.ModuleDefinition.FriendlyName, this.PersonalizationKey(key), val);
    }

    /// <summary>An item of one of the console's drop down lists.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.ListItem</c>, with the two members the control used.</remarks>
    protected sealed class ConsoleListItem
    {
        /// <summary>Initializes a new instance of the <see cref="ConsoleListItem"/> class.</summary>
        /// <param name="value">The value of the item.</param>
        /// <param name="text">The caption of the item.</param>
        public ConsoleListItem(string value, string text)
        {
            this.Value = value;
            this.Text = text;
        }

        /// <summary>Gets the value of this item.</summary>
        public string Value { get; }

        /// <summary>Gets the caption of this item.</summary>
        public string Text { get; }
    }
}
