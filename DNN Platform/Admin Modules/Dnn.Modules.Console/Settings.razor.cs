// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.Modules.Console
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Dnn.Modules.Console.Components;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Common;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    /// <summary>Loads and saves the module settings.</summary>
    /// <remarks>
    /// Was <c>Settings.ascx</c> + <c>Settings.ascx.cs</c> + <c>Settings.ascx.designer.cs</c>; the
    /// markup is <c>Settings.razor</c>. The designer-generated server controls became the state
    /// below, which <c>Settings.razor</c> binds to:
    /// <list type="bullet">
    /// <item><description>the <c>modeList</c>, <c>DefaultSize</c> and <c>DefaultView</c> <c>DnnComboBox</c>es are <see cref="ModeItems"/>/<see cref="ModeSelectedValue"/>, <see cref="DefaultSizeItems"/>/<see cref="DefaultSizeSelectedValue"/> and <see cref="DefaultViewItems"/>/<see cref="DefaultViewSelectedValue"/>, keeping <c>ListItem</c>'s Value/Text and the "nothing selected" state <c>ClearSelection</c> produced (a <see langword="null"/> selected value);</description></item>
    /// <item><description>the <c>ParentTab</c> <c>DnnPageDropDownList</c> is <see cref="ParentTabItems"/>/<see cref="ParentTabSelectedValue"/>, with <see cref="SelectedPage"/>, <see cref="SelectedItemValueAsInt"/> and <see cref="SelectedItemValue"/> preserving the three members the code behind used;</description></item>
    /// <item><description>the six <c>asp:CheckBox</c>es and the <c>ConsoleWidth</c> <c>asp:TextBox</c> are the <c>*Checked</c> properties and <see cref="ConsoleWidthText"/>, with the markup's <c>Checked="true"</c> defaults;</description></item>
    /// <item><description>the <c>tabs</c> <c>Repeater</c> and its <c>ItemDataBound</c> handler are <see cref="TabRows"/> and <see cref="BindTabs"/>, which build the row state (label, tab path and per-row visibility list) the handler used to assign to the three controls of each item;</description></item>
    /// <item><description>the three <c>runat="server"</c> row <c>div</c>s are <see cref="ParentTabRowVisible"/>, <see cref="IncludeParentRowVisible"/> and <see cref="TabVisibilityRowVisible"/>.</description></item>
    /// </list>
    /// </remarks>
    public partial class Settings : ModuleSettingsBase
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<Settings>();

        private readonly List<SettingsListItem> modeItems = new List<SettingsListItem>
        {
            // The three <asp:ListItem>s declared inside <dnn:DnnComboBox ID="modeList">. Their
            // captions came from the ResourceKey attribute, which the page's localization pass
            // resolved; a component localizes its own markup, so that happens in OnInit.
            new SettingsListItem("Normal", "Normal"),
            new SettingsListItem("Profile", "Profile"),
            new SettingsListItem("Group", "Group"),
        };

        private readonly List<SettingsListItem> defaultSizeItems = new List<SettingsListItem>();
        private readonly List<SettingsListItem> defaultViewItems = new List<SettingsListItem>();
        private readonly List<SettingsListItem> parentTabItems = new List<SettingsListItem>();
        private readonly List<TabRow> tabRows = new List<TabRow>();

        private bool settingsLoaded;

        /// <summary>Gets or sets the host settings, needed to enumerate the portal's pages.</summary>
        /// <remarks>
        /// <c>DnnPageDropDownList</c> listed the pages through a Web API service, which resolved
        /// these two services for itself; <see cref="BindParentTabs"/> calls the same
        /// <c>TabController.GetPortalTabs</c> overload directly and so needs them here.
        /// </remarks>
        [Inject]
        protected DotNetNuke.Abstractions.Application.IHostSettings HostSettings { get; set; }

        /// <summary>Gets or sets the application status, needed to enumerate the portal's pages.</summary>
        [Inject]
        protected DotNetNuke.Abstractions.Application.IApplicationStatusInfo AppStatus { get; set; }

        /// <summary>Gets the items of the mode drop down list.</summary>
        protected IList<SettingsListItem> ModeItems => this.modeItems;

        /// <summary>Gets the items of the default size drop down list.</summary>
        protected IList<SettingsListItem> DefaultSizeItems => this.defaultSizeItems;

        /// <summary>Gets the items of the default view drop down list.</summary>
        protected IList<SettingsListItem> DefaultViewItems => this.defaultViewItems;

        /// <summary>Gets the items of the parent page drop down list.</summary>
        protected IList<SettingsListItem> ParentTabItems => this.parentTabItems;

        /// <summary>Gets the rows of the tab visibility list.</summary>
        protected IList<TabRow> TabRows => this.tabRows;

        /// <summary>Gets or sets the selected value of the mode drop down list.</summary>
        /// <remarks>
        /// Replaces <c>modeList.SelectedValue</c>. The combo box had no explicit selection, so the
        /// first declared item was the selected one, which is why this starts at "Normal".
        /// </remarks>
        protected string ModeSelectedValue { get; set; } = "Normal";

        /// <summary>Gets or sets the selected value of the default size drop down list.</summary>
        /// <remarks>Replaces <c>DefaultSize.SelectedValue</c>.</remarks>
        protected string DefaultSizeSelectedValue { get; set; }

        /// <summary>Gets or sets the selected value of the default view drop down list.</summary>
        /// <remarks>Replaces <c>DefaultView.SelectedValue</c>.</remarks>
        protected string DefaultViewSelectedValue { get; set; }

        /// <summary>Gets or sets the selected value of the parent page drop down list.</summary>
        /// <remarks>Replaces the selection of <c>ParentTab</c>; the empty string is the <c>UndefinedItem</c>.</remarks>
        protected string ParentTabSelectedValue { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the parent page is included.</summary>
        /// <remarks>Replaces <c>IncludeParent.Checked</c>.</remarks>
        protected bool IncludeParentChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether the console icons can be resized.</summary>
        /// <remarks>Replaces <c>AllowResize.Checked</c>, including the markup's <c>Checked="true"</c>.</remarks>
        protected bool AllowResizeChecked { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the console view can be changed.</summary>
        /// <remarks>Replaces <c>AllowViewChange.Checked</c>, including the markup's <c>Checked="true"</c>.</remarks>
        protected bool AllowViewChangeChecked { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether tooltips are shown.</summary>
        /// <remarks>Replaces <c>ShowTooltip.Checked</c>, including the markup's <c>Checked="true"</c>.</remarks>
        protected bool ShowTooltipChecked { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the pages are ordered by hierarchy.</summary>
        /// <remarks>Replaces <c>OrderTabsByHierarchy.Checked</c>.</remarks>
        protected bool OrderTabsByHierarchyChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether hidden pages are included.</summary>
        /// <remarks>Replaces <c>IncludeHiddenPages.Checked</c>.</remarks>
        protected bool IncludeHiddenPagesChecked { get; set; }

        /// <summary>Gets or sets the console width.</summary>
        /// <remarks>Replaces <c>ConsoleWidth.Text</c>, including the markup's <c>Text=""</c>.</remarks>
        protected string ConsoleWidthText { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the parent page row is rendered.</summary>
        /// <remarks>Replaces <c>parentTabRow.Visible</c>, which <see cref="SwitchMode"/> assigned.</remarks>
        protected bool ParentTabRowVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the "include parent" row is rendered.</summary>
        /// <remarks>Replaces <c>includeParentRow.Visible</c>.</remarks>
        protected bool IncludeParentRowVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the tab visibility row is rendered.</summary>
        /// <remarks>Replaces <c>tabVisibilityRow.Visible</c>.</remarks>
        protected bool TabVisibilityRowVisible { get; set; }

        /// <summary>Gets or sets the selected page of the parent page drop down list.</summary>
        /// <remarks>Replaces <c>ParentTab.SelectedPage</c>, including the fact that assigning <see langword="null"/> cleared the selection.</remarks>
        protected TabInfo SelectedPage
        {
            get
            {
                var pageId = this.SelectedItemValueAsInt;
                return pageId == Null.NullInteger ? null : TabController.Instance.GetTab(pageId, this.PortalId);
            }

            set
            {
                this.ParentTabSelectedValue = value != null
                    ? value.TabID.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
            }
        }

        /// <summary>Gets the selected parent page id, or <see cref="Null.NullInteger"/> when nothing is selected.</summary>
        /// <remarks>Replaces <c>ParentTab.SelectedItemValueAsInt</c>.</remarks>
        protected int SelectedItemValueAsInt =>
            int.TryParse(this.ParentTabSelectedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : Null.NullInteger;

        /// <summary>Gets the value of the selected parent page item.</summary>
        /// <remarks>Replaces <c>ParentTab.SelectedItem.Value</c>.</remarks>
        protected string SelectedItemValue => this.ParentTabSelectedValue;

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// Replaces the <c>this.Page.IsPostBack == false</c> guard. The host settings page calls
        /// <c>LoadSettings()</c> on every request and <c>UpdateSettings()</c> later in that same
        /// request, so the guard is what stops the editor's unsaved input being overwritten with
        /// the stored values before it is read back.
        /// </para>
        /// <para>
        /// The guard is derived from the request method, not just from an instance flag: if the
        /// component is re-created for the save POST, an instance flag would be
        /// <see langword="false"/> again and <c>LoadSettings</c> would repopulate every field from
        /// the database before <c>UpdateSettings</c> reads it - silently writing the old values
        /// back. Testing the request method reproduces <c>IsPostBack</c> in both hosting modes;
        /// the <c>settingsLoaded</c> flag additionally keeps the load once-only within a
        /// persistent circuit. This is the convention the migrated
        /// <c>DotNetNuke.Modules.Journal</c> settings component established.
        /// </para>
        /// </remarks>
        public override void LoadSettings()
        {
            try
            {
                var isPostBack = HttpMethods.IsPost(this.Request?.Method ?? HttpMethods.Get);
                if (!this.settingsLoaded && !isPostBack)
                {
                    this.settingsLoaded = true;

                    this.BindParentTabs();

                    if (this.Settings.ContainsKey("ParentTabId") && !string.IsNullOrEmpty(Convert.ToString(this.Settings["ParentTabId"])))
                    {
                        var tabId = Convert.ToInt32(this.Settings["ParentTabId"]);
                        this.SelectedPage = TabController.Instance.GetTab(tabId, this.PortalId);
                    }

                    foreach (string val in ConsoleController.GetSizeValues())
                    {
                        AddItem(this.defaultSizeItems, Localization.GetString(val, this.LocalResourceFile), val, v => this.DefaultSizeSelectedValue = v);
                    }

                    this.SelectDropDownListItem(this.defaultSizeItems, "DefaultSize", v => this.DefaultSizeSelectedValue = v);

                    this.SelectDropDownListItem(this.modeItems, "Mode", v => this.ModeSelectedValue = v);

                    if (this.Settings.ContainsKey("AllowSizeChange"))
                    {
                        this.AllowResizeChecked = Convert.ToBoolean(this.Settings["AllowSizeChange"]);
                    }

                    foreach (var val in ConsoleController.GetViewValues())
                    {
                        AddItem(this.defaultViewItems, Localization.GetString(val, this.LocalResourceFile), val, v => this.DefaultViewSelectedValue = v);
                    }

                    this.SelectDropDownListItem(this.defaultViewItems, "DefaultView", v => this.DefaultViewSelectedValue = v);
                    if (this.Settings.ContainsKey("IncludeParent"))
                    {
                        this.IncludeParentChecked = Convert.ToBoolean(this.Settings["IncludeParent"]);
                    }

                    if (this.Settings.ContainsKey("AllowViewChange"))
                    {
                        this.AllowViewChangeChecked = Convert.ToBoolean(this.Settings["AllowViewChange"]);
                    }

                    if (this.Settings.ContainsKey("ShowTooltip"))
                    {
                        this.ShowTooltipChecked = Convert.ToBoolean(this.Settings["ShowTooltip"]);
                    }

                    if (this.Settings.ContainsKey("OrderTabsByHierarchy"))
                    {
                        this.OrderTabsByHierarchyChecked = Convert.ToBoolean(this.Settings["OrderTabsByHierarchy"]);
                    }

                    if (this.Settings.ContainsKey("IncludeHiddenPages"))
                    {
                        this.IncludeHiddenPagesChecked = Convert.ToBoolean(this.Settings["IncludeHiddenPages"]);
                    }

                    if (this.Settings.ContainsKey("ConsoleWidth"))
                    {
                        this.ConsoleWidthText = Convert.ToString(this.Settings["ConsoleWidth"]);
                    }

                    this.SwitchMode();
                }
            }
            catch (Exception ex)
            {
                Exceptions.ProcessModuleLoadException(this, ex);
            }
        }

        /// <inheritdoc />
        public override void UpdateSettings()
        {
            try
            {
                // validate console width value
                var width = string.Empty;
                if (this.ConsoleWidthText.Trim().Length > 0)
                {
                    try
                    {
                        width = ParseCssUnit(this.ConsoleWidthText.Trim());
                    }
                    catch (Exception exc)
                    {
                        Logger.SettingsParseWidthException(exc);

                        throw new Exception("ConsoleWidth value is invalid. Value must be numeric.");
                    }
                }

                if (this.SelectedItemValueAsInt == Null.NullInteger)
                {
                    ModuleController.Instance.DeleteModuleSetting(this.ModuleId, "ParentTabID");
                }
                else
                {
                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ParentTabID", this.SelectedItemValue);
                }

                // A WebForms <select> with nothing selected still DISPLAYED its first option and
                // posted that option's value back, so SelectedValue was never null here. A
                // component's state stays null until the editor actually changes the control, so
                // each selection is coalesced to the value the browser showed - otherwise a
                // never-touched list (or one whose stored value no longer matches an item, which
                // SelectDropDownListItem deliberately leaves unselected) would silently save null.
                // The render-time null is left alone, so "nothing selected" still renders as such.
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "Mode", Displayed(this.ModeSelectedValue, this.modeItems));
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "DefaultSize", Displayed(this.DefaultSizeSelectedValue, this.defaultSizeItems));
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "AllowSizeChange", this.AllowResizeChecked.ToString(CultureInfo.InvariantCulture));
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "DefaultView", Displayed(this.DefaultViewSelectedValue, this.defaultViewItems));
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "AllowViewChange", this.AllowViewChangeChecked.ToString(CultureInfo.InvariantCulture));
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ShowTooltip", this.ShowTooltipChecked.ToString(CultureInfo.InvariantCulture));
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "OrderTabsByHierarchy", this.OrderTabsByHierarchyChecked.ToString(CultureInfo.InvariantCulture));
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "IncludeHiddenPages", this.IncludeHiddenPagesChecked.ToString(CultureInfo.InvariantCulture));
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "IncludeParent", this.IncludeParentChecked.ToString(CultureInfo.InvariantCulture));
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ConsoleWidth", width);

                // This was the walk over this.tabs.Items, skipping the header/footer item types;
                // TabRows only ever holds the data items the repeater's ItemTemplate rendered, so
                // the type test has nothing left to exclude.
                foreach (var row in this.tabRows)
                {
                    var tabPath = row.TabPath;
                    var visibility = Displayed(row.VisibilitySelectedValue, row.VisibilityItems);

                    var key = string.Format(CultureInfo.InvariantCulture, "TabVisibility{0}", tabPath.Replace("//", "-"));
                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, key, visibility);
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// The <c>tabs.ItemDataBound</c> and <c>modeList.SelectedIndexChanged</c> wiring is gone
        /// with the two server controls: the first is <see cref="BindTabs"/>, the second the
        /// <c>@onchange</c> handler <see cref="ModeListSelectedIndexChanged"/>. What remains is the
        /// <c>ParentTab.UndefinedItem</c> assignment and the localization of the three
        /// <c>modeList</c> items, which carried a <c>ResourceKey</c> instead of final text and
        /// which the WebForms page's control localization pass resolved.
        /// </remarks>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            foreach (var item in this.modeItems)
            {
                var text = this.LocalizeText(item.Value);
                if (!string.IsNullOrEmpty(text))
                {
                    item.Text = text;
                }
            }
        }

        /// <summary>Gets the value a WebForms list control would have posted for a selection.</summary>
        /// <param name="selectedValue">The component's selected value, which may be <see langword="null"/>.</param>
        /// <param name="items">The items of the list.</param>
        /// <returns>The selected value, or the first item's value when nothing is selected.</returns>
        /// <remarks>
        /// A <c>DnnComboBox</c> with no selection rendered its first option as the selected one and
        /// posted that value back, so <c>SelectedValue</c> was never <see langword="null"/> when
        /// <c>UpdateSettings</c> read it. This restores that value for the save path only.
        /// </remarks>
        private static string Displayed(string selectedValue, IList<SettingsListItem> items)
        {
            if (selectedValue != null)
            {
                return selectedValue;
            }

            return items.Count > 0 ? items[0].Value : string.Empty;
        }

        /// <summary>Parses and normalizes a CSS length, as <c>System.Web.UI.WebControls.Unit</c> did.</summary>
        /// <param name="value">The value typed into the console width box.</param>
        /// <returns>The normalized length, e.g. <c>200px</c> or <c>50%</c>.</returns>
        /// <exception cref="FormatException">The value is not a valid CSS length.</exception>
        /// <remarks>
        /// Replaces <c>Unit.Parse(text).ToString()</c>. <c>Unit</c> lives in
        /// <c>System.Web.UI.WebControls</c>, which does not exist on modern .NET, so its two
        /// behaviors the setting relies on are reproduced here: a bare number means pixels, and an
        /// unparseable number or an unknown suffix throws - which is what the caller turns into
        /// the "value must be numeric" error. Pixel and point values are truncated to whole
        /// numbers, as <c>Unit</c>'s constructor did.
        /// </remarks>
        private static string ParseCssUnit(string value)
        {
            var text = value.Trim();
            if (text.Length == 0)
            {
                return string.Empty;
            }

            var index = 0;
            while (index < text.Length &&
                   (char.IsDigit(text[index]) || text[index] == '.' || text[index] == '-' || text[index] == '+'))
            {
                index++;
            }

            var numberPart = text.Substring(0, index).Trim();
            var unitPart = text.Substring(index).Trim();

            if (numberPart.Length == 0)
            {
                throw new FormatException($"'{value}' is not a valid CSS length.");
            }

            // Unit.Parse(string) used the current culture for the number.
            var number = double.Parse(numberPart, NumberStyles.Float, CultureInfo.CurrentCulture);

            // An omitted unit meant UnitType.Pixel.
            var suffix = unitPart.Length == 0 ? "px" : unitPart.ToLowerInvariant();
            switch (suffix)
            {
                case "px":
                case "pt":
                    // Unit stored pixel and point values as an integer.
                    return ((int)number).ToString(CultureInfo.CurrentCulture) + suffix;
                case "%":
                case "pc":
                case "in":
                case "mm":
                case "cm":
                case "em":
                case "ex":
                    return number.ToString(CultureInfo.CurrentCulture) + suffix;
                default:
                    throw new FormatException($"'{value}' has an unrecognized CSS unit '{unitPart}'.");
            }
        }

        /// <summary>Adds an item to one of the setting lists, selecting it when it is the first one.</summary>
        /// <param name="items">The list to add to.</param>
        /// <param name="text">The caption of the item.</param>
        /// <param name="value">The value of the item.</param>
        /// <param name="setSelected">Assigns the list's selected value.</param>
        /// <remarks>
        /// Replaces <c>DnnComboBox.AddItem(text, value)</c>, whose first item became the selected
        /// one because a list control with no explicit selection selects its first item.
        /// </remarks>
        private static void AddItem(List<SettingsListItem> items, string text, string value, Action<string> setSelected)
        {
            items.Add(new SettingsListItem(value, text));
            if (items.Count == 1)
            {
                setSelected(value);
            }
        }

        /// <summary>Fills the parent page drop down list.</summary>
        /// <remarks>
        /// Replaces the item source of <c>&lt;dnn:DnnPageDropDownList IncludeHostPages="true"&gt;</c>,
        /// which populated itself from <c>InternalServices/ItemListService/GetPages</c>. That
        /// service's <c>GetPortalPages</c> is reproduced here exactly, so the selectable set is
        /// unchanged: the control posted <c>includeDisabled=false</c>, <c>includeAllTypes=false</c>,
        /// <c>includeActive=false</c> and <c>includeHostPages=(IncludeHostPages &amp;&amp; the
        /// current user is a super user)</c>, which the service turned into
        /// <c>TabController.GetPortalTabs(..., excludeTabId: ActiveTab.TabID, includeNoneSpecified:
        /// false, noneSpecifiedText: null, includeHidden: true, includeDeleted: false, includeUrl:
        /// false, checkViewPermission: true, checkEditPermission: false)</c> filtered by
        /// <c>!DisableLink &amp;&amp; !IsSystem</c>, plus - for a super user - the host pages
        /// filtered by <c>!IsDeleted &amp;&amp; !DisableLink &amp;&amp; !IsSystem</c>. Dropping any
        /// of those filters would offer system, heading-only, URL/file/redirect or self-parenting
        /// pages that could never be stored as <c>ParentTabID</c> before. The captions are
        /// <c>IndentedTabName</c>, which is what the control's <c>SelectedPage</c> setter used, and
        /// the <c>UndefinedItem</c> stays first.
        /// </remarks>
        private void BindParentTabs()
        {
            this.parentTabItems.Clear();

            // ParentTab.UndefinedItem = new ListItem(DynamicSharedConstants.Unspecified, string.Empty)
            this.parentTabItems.Add(new SettingsListItem(string.Empty, DynamicSharedConstants.Unspecified));

            var pages = TabController.GetPortalTabs(
                    this.HostSettings,
                    this.AppStatus,
                    this.PortalId,
                    this.PortalSettings.ActiveTab.TabID,
                    false,
                    null,
                    true,
                    false,
                    false,
                    true,
                    false)
                .Where(t => !t.DisableLink && !t.IsSystem)
                .ToList();

            if (UserController.Instance.GetCurrentUserInfo().IsSuperUser)
            {
                pages.AddRange(TabController.Instance.GetTabsByPortal(Null.NullInteger).AsList()
                    .Where(t => !t.IsDeleted && !t.DisableLink && !t.IsSystem));
            }

            foreach (var tab in pages)
            {
                this.parentTabItems.Add(new SettingsListItem(
                    tab.TabID.ToString(CultureInfo.InvariantCulture),
                    tab.IndentedTabName));
            }
        }

        /// <summary>Binds the tab visibility rows.</summary>
        /// <param name="tabId">The parent page the rows are collected under.</param>
        /// <param name="includeParent">Whether the parent page itself is one of the rows.</param>
        /// <remarks>
        /// This is <c>BindTabs</c> plus <c>TabsItemDataBound</c>: the repeater's handler ran per
        /// bound item and built exactly the per-row state created here (the label text, the hidden
        /// tab path and the visibility list, preselected from the stored
        /// <c>TabVisibility&lt;tabPath&gt;</c> setting).
        /// </remarks>
        private void BindTabs(int tabId, bool includeParent)
        {
            List<TabInfo> tempTabs = TabController.GetTabsBySortOrder(this.PortalId).OrderBy(t => t.Level).ThenBy(t => t.HasChildren).ToList();

            IList<TabInfo> tabList = new List<TabInfo>();

            IList<int> tabIdList = new List<int>();
            tabIdList.Add(tabId);

            if (includeParent)
            {
                TabInfo consoleTab = TabController.Instance.GetTab(tabId, this.PortalId);
                if (consoleTab != null)
                {
                    tabList.Add(consoleTab);
                }
            }

            foreach (TabInfo tab in tempTabs)
            {
                bool canShowTab = TabPermissionController.CanViewPage(tab) &&
                                  !tab.IsDeleted &&
                                  (tab.StartDate < DateTime.Now || tab.StartDate == Null.NullDate);

                if (!canShowTab)
                {
                    continue;
                }

                if (tabIdList.Contains(tab.ParentId))
                {
                    if (!tabIdList.Contains(tab.TabID))
                    {
                        tabIdList.Add(tab.TabID);
                    }

                    tabList.Add(tab);
                }
            }

            // this.tabs.DataSource = tabList; this.tabs.DataBind();
            this.tabRows.Clear();
            foreach (var tab in tabList)
            {
                var row = new TabRow
                {
                    TabName = tab.TabName,
                    TabPath = tab.TabPath,
                    ElementSuffix = tab.TabID.ToString(CultureInfo.InvariantCulture),
                };

                // visibilityDropDown.Items.Clear() then the AddItem calls of TabsItemDataBound.
                row.VisibilityItems.Add(new SettingsListItem("AllUsers", this.LocalizeText("AllUsers")));
                if (this.ModeSelectedValue == "Profile")
                {
                    row.VisibilityItems.Add(new SettingsListItem("Friends", this.LocalizeText("Friends")));
                    row.VisibilityItems.Add(new SettingsListItem("User", this.LocalizeText("User")));
                }
                else
                {
                    row.VisibilityItems.Add(new SettingsListItem("Owner", this.LocalizeText("Owner")));
                    row.VisibilityItems.Add(new SettingsListItem("Members", this.LocalizeText("Members")));
                }

                // The first added item was the selected one until SelectDropDownListItem ran.
                row.VisibilitySelectedValue = row.VisibilityItems[0].Value;

                var key = string.Format(CultureInfo.InvariantCulture, "TabVisibility{0}", tab.TabPath.Replace("//", "-"));
                this.SelectDropDownListItem(row.VisibilityItems, key, v => row.VisibilitySelectedValue = v);

                this.tabRows.Add(row);
            }
        }

        /// <summary>Applies the mode to the form, as <c>SwitchMode</c> did.</summary>
        private void SwitchMode()
        {
            int parentTabId = -1;
            if (this.Settings.ContainsKey("ParentTabID") && !string.IsNullOrEmpty(Convert.ToString(this.Settings["ParentTabID"])))
            {
                parentTabId = Convert.ToInt32(this.Settings["ParentTabID"]);
            }

            switch (this.ModeSelectedValue)
            {
                case "Normal":
                    this.ParentTabRowVisible = true;
                    this.IncludeParentRowVisible = true;
                    this.TabVisibilityRowVisible = false;
                    break;
                case "Profile":
                    this.ParentTabRowVisible = false;
                    this.IncludeParentRowVisible = false;
                    this.TabVisibilityRowVisible = true;
                    parentTabId = this.PortalSettings.UserTabId;
                    break;
                case "Group":
                    this.ParentTabRowVisible = true;
                    this.IncludeParentRowVisible = true;
                    this.TabVisibilityRowVisible = true;
                    break;
            }

            this.SelectedPage = TabController.Instance.GetTab(parentTabId, this.PortalId);
            this.BindTabs(parentTabId, this.IncludeParentChecked);
        }

        /// <summary>Re-applies the mode when the mode drop down changes.</summary>
        /// <param name="args">The change event args.</param>
        /// <remarks>Replaces <c>ModeListSelectedIndexChanged</c>, which the combo box's <c>AutoPostBack="true"</c> raised.</remarks>
        private void ModeListSelectedIndexChanged(ChangeEventArgs args)
        {
            this.ModeSelectedValue = args.Value?.ToString();
            this.SwitchMode();
        }

        /// <summary>Records the parent page the editor picked.</summary>
        /// <param name="args">The change event args.</param>
        /// <remarks>
        /// Replaces the post back that carried <c>ParentTab</c>'s selection back to the server;
        /// the control raised no server side change event, so nothing else happens here.
        /// </remarks>
        private void ParentTabSelectedIndexChanged(ChangeEventArgs args)
        {
            this.ParentTabSelectedValue = args.Value?.ToString() ?? string.Empty;
        }

        /// <summary>Preselects the stored value in one of the setting lists.</summary>
        /// <param name="items">The items of the list.</param>
        /// <param name="key">The module setting holding the stored value.</param>
        /// <param name="setSelected">Assigns the list's selected value.</param>
        /// <remarks>
        /// Replaces <c>SelectDropDownListItem(ref DnnComboBox, string)</c>: when the setting
        /// exists it cleared the selection and then selected the item with that value - so a
        /// stored value that no longer matches an item leaves the list with nothing selected,
        /// which is why the selection is cleared before the lookup.
        /// </remarks>
        private void SelectDropDownListItem(IList<SettingsListItem> items, string key, Action<string> setSelected)
        {
            if (!this.Settings.ContainsKey(key))
            {
                return;
            }

            // ddl.ClearSelection()
            setSelected(null);

            // ddl.FindItemByValue(...)
            var storedValue = Convert.ToString(this.Settings[key]);
            var selItem = items.FirstOrDefault(i => i.Value == storedValue);
            if (selItem != null)
            {
                // selItem.Selected = true
                setSelected(selItem.Value);
            }
        }

        /// <summary>An item of one of the setting lists.</summary>
        /// <remarks>Replaces <c>System.Web.UI.WebControls.ListItem</c> / <c>DnnListItem</c>, with the members the code behind used.</remarks>
        protected sealed class SettingsListItem
        {
            /// <summary>Initializes a new instance of the <see cref="SettingsListItem"/> class.</summary>
            /// <param name="value">The value of the item.</param>
            /// <param name="text">The caption of the item.</param>
            public SettingsListItem(string value, string text)
            {
                this.Value = value;
                this.Text = text;
            }

            /// <summary>Gets the value of this item.</summary>
            public string Value { get; }

            /// <summary>Gets or sets the caption of this item.</summary>
            public string Text { get; set; }
        }

        /// <summary>A row of the tab visibility list.</summary>
        /// <remarks>
        /// Replaces one <c>RepeaterItem</c> of the <c>tabs</c> repeater, holding what its three
        /// controls carried: the <c>tabLabel</c> text, the <c>tabPath</c> hidden field value and
        /// the <c>tabVisibility</c> combo box.
        /// </remarks>
        protected sealed class TabRow
        {
            /// <summary>Gets or sets the name of the page, which was <c>tabLabel.Text</c>.</summary>
            public string TabName { get; set; }

            /// <summary>Gets or sets the path of the page, which was <c>tabPath.Value</c>.</summary>
            public string TabPath { get; set; }

            /// <summary>Gets or sets the suffix that makes this row's element ids unique, as the repeater's naming container did.</summary>
            public string ElementSuffix { get; set; }

            /// <summary>Gets the items of this row's visibility drop down list.</summary>
            public IList<SettingsListItem> VisibilityItems { get; } = new List<SettingsListItem>();

            /// <summary>Gets or sets the selected value of this row's visibility drop down list.</summary>
            public string VisibilitySelectedValue { get; set; }
        }
    }
}
