// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.SearchResults
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Search.Internals;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A settings control for the search results module.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>ResultsSettings.ascx</c> + <c>ResultsSettings.ascx.cs</c> +
    /// <c>ResultsSettings.ascx.designer.cs</c>; the markup now lives in
    /// <c>ResultsSettings.razor</c>. The designer-generated server controls became the state below:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>comboBoxLinkTarget</c> is <see cref="LinkTargetItems"/>/<see cref="LinkTargetSelectedValue"/> (the two literal items, values <c>"0"</c>/<c>"1"</c>);</description></item>
    /// <item><description><c>comboBoxPortals</c>, <c>comboBoxFilters</c> and <c>comboBoxRoles</c> are <see cref="PortalItems"/>, <see cref="FilterItems"/> and <see cref="RoleItems"/>; <c>DnnComboBox.Value</c> (the comma separated list of the selected values) is <see cref="SelectedValuesOf"/>;</description></item>
    /// <item><description><c>divPortalGroup.Visible</c> is <see cref="PortalGroupRowVisible"/>;</description></item>
    /// <item><description>the seven <c>asp:CheckBox</c>es and <c>txtMaxDescriptionLength</c> are the <c>*Checked</c> properties and <see cref="MaxDescriptionLengthText"/>;</description></item>
    /// <item><description>the two <c>asp:RequiredFieldValidator</c>s are <see cref="PortalsRequiredMessageVisible"/>/<see cref="FiltersRequiredMessageVisible"/>, and <c>Page.IsValid</c> is <see cref="IsValid"/>.</description></item>
    /// </list>
    /// <para>
    /// Every setting is read and written under its original name and in its original format, so a
    /// site upgraded to this build keeps its configuration: the two multi-select scopes are still
    /// persisted as <c>|</c> separated lists (the <c>Value.Replace(",", "|")</c> of the Web Forms
    /// control) and <c>SearchResult_ScopeForRoles</c> is still the comma separated portal setting
    /// the search service reads when it trims results by role.
    /// </para>
    /// </remarks>
    public partial class ResultsSettings : ModuleSettingsBase
    {
        private readonly IPortalController portalController;

        private readonly List<ResultsSettingsListItem> linkTargetItems = new List<ResultsSettingsListItem>();
        private readonly List<ResultsSettingsListItem> portalItems = new List<ResultsSettingsListItem>();
        private readonly List<ResultsSettingsListItem> filterItems = new List<ResultsSettingsListItem>();
        private readonly List<ResultsSettingsListItem> roleItems = new List<ResultsSettingsListItem>();

        private bool settingsLoaded;

        /// <summary>Whether <see cref="UpdateSettings"/> has run its validation, which is when the two required-field messages may show (the Web Forms validators only rendered their text after a validated post).</summary>
        private bool validated;

        /// <summary>Initializes a new instance of the <see cref="ResultsSettings"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.2. Please use overload with IPortalController. Scheduled removal in v12.0.0.")]
        public ResultsSettings()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ResultsSettings"/> class.</summary>
        /// <param name="portalController">The portal controller.</param>
        public ResultsSettings(IPortalController portalController)
        {
            this.portalController = portalController ?? this.DependencyProvider.GetRequiredService<IPortalController>();
        }

        /// <summary>Gets the items of the <c>comboBoxLinkTarget</c> combo box.</summary>
        protected IReadOnlyList<ResultsSettingsListItem> LinkTargetItems => this.linkTargetItems;

        /// <summary>Gets the items of the <c>comboBoxPortals</c> combo box.</summary>
        protected IReadOnlyList<ResultsSettingsListItem> PortalItems => this.portalItems;

        /// <summary>Gets the items of the <c>comboBoxFilters</c> combo box.</summary>
        protected IReadOnlyList<ResultsSettingsListItem> FilterItems => this.filterItems;

        /// <summary>Gets the items of the <c>comboBoxRoles</c> combo box.</summary>
        protected IReadOnlyList<ResultsSettingsListItem> RoleItems => this.roleItems;

        /// <summary>Gets or sets the selected value of <c>comboBoxLinkTarget</c>.</summary>
        protected string LinkTargetSelectedValue { get; set; }

        /// <summary>Gets or sets a value indicating whether the portals row renders (was <c>divPortalGroup.Visible</c>).</summary>
        protected bool PortalGroupRowVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether <c>chkEnableWildSearch</c> is checked.</summary>
        protected bool EnableWildSearchChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether <c>chkShowDescription</c> is checked.</summary>
        protected bool ShowDescriptionChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether <c>chkShowFriendlyTitle</c> is checked.</summary>
        protected bool ShowFriendlyTitleChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether <c>chkShowSnippet</c> is checked.</summary>
        protected bool ShowSnippetChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether <c>chkShowLastUpdated</c> is checked.</summary>
        protected bool ShowLastUpdatedChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether <c>chkShowSource</c> is checked.</summary>
        protected bool ShowSourceChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether <c>chkShowTags</c> is checked.</summary>
        protected bool ShowTagsChecked { get; set; }

        /// <summary>Gets or sets the text of <c>txtMaxDescriptionLength</c>.</summary>
        protected string MaxDescriptionLengthText { get; set; }

        /// <summary>
        /// Gets a value indicating whether the <c>maxDescriptionLengthRow</c> renders, which the
        /// inline script toggled from <c>chkShowDescription</c>.
        /// </summary>
        protected bool MaxDescriptionLengthRowVisible => this.ShowDescriptionChecked;

        /// <summary>
        /// Gets a value indicating whether the <c>scopeForRolesRow</c> renders, which the inline
        /// script toggled on when the "Users" filter was among the selected ones (the same
        /// case-insensitive comparison).
        /// </summary>
        protected bool ScopeForRolesRowVisible =>
            this.filterItems.Any(item => item.Selected && string.Equals(item.Value, "Users", StringComparison.OrdinalIgnoreCase));

        /// <summary>Gets a value indicating whether the <c>portalsRequied</c> message shows.</summary>
        /// <remarks>
        /// The <c>RequiredFieldValidator</c> of <c>comboBoxPortals</c> lived inside
        /// <c>divPortalGroup</c>, so it did not validate while that row was hidden (an ASP.NET
        /// validator inside an invisible container is skipped). That condition is preserved: a
        /// portal group that does not exist must not block saving the settings.
        /// </remarks>
        protected bool PortalsRequiredMessageVisible =>
            this.validated && this.PortalGroupRowVisible && !this.portalItems.Any(item => item.Selected);

        /// <summary>Gets a value indicating whether the <c>filtersRequired</c> message shows.</summary>
        protected bool FiltersRequiredMessageVisible =>
            this.validated && !this.filterItems.Any(item => item.Selected);

        /// <summary>Gets a value indicating whether the form is valid (was <c>Page.IsValid</c>).</summary>
        protected bool IsValid =>
            (!this.PortalGroupRowVisible || this.portalItems.Any(item => item.Selected))
            && this.filterItems.Any(item => item.Selected);

        /// <inheritdoc />
        /// <remarks>
        /// The <c>if (this.Page.IsPostBack == false)</c> guard of the Web Forms control becomes the
        /// same pair of conditions the platform's other migrated settings controls use: the host
        /// calls <c>LoadSettings()</c> on every request and <c>UpdateSettings()</c> later in the
        /// same one, and re-loading in between would overwrite the edited values with the stored
        /// ones - silently saving the old settings.
        /// </remarks>
        public override void LoadSettings()
        {
            try
            {
                var isPostBack = HttpMethods.IsPost(this.Request?.Method ?? HttpMethods.Get);
                if (!this.settingsLoaded && !isPostBack)
                {
                    this.settingsLoaded = true;

                    this.BuildLinkTargetItems();

                    if (!string.IsNullOrEmpty(Convert.ToString(this.Settings["LinkTarget"])))
                    {
                        this.SelectLinkTarget(Convert.ToString(this.Settings["LinkTarget"]));
                    }

                    if (!string.IsNullOrEmpty(Convert.ToString(this.Settings["ScopeForPortals"])))
                    {
                        var list = Convert.ToString(this.Settings["ScopeForPortals"]).Split('|').ToList();
                        var portalList = this.LoadPortalsList().ToList();
                        if (portalList.Any())
                        {
                            foreach (var portal in portalList)
                            {
                                this.portalItems.Add(new ResultsSettingsListItem(portal[0], portal[1]) { Selected = list.Contains(portal[1]) });
                            }
                        }
                        else
                        {
                            this.PortalGroupRowVisible = false;
                        }
                    }
                    else
                    {
                        var portalList = this.LoadPortalsList().ToList();
                        if (portalList.Any())
                        {
                            foreach (var portal in portalList)
                            {
                                this.portalItems.Add(new ResultsSettingsListItem(portal[0], portal[1]) { Selected = this.PortalId.ToString() == portal[1] });
                            }
                        }
                        else
                        {
                            this.PortalGroupRowVisible = false;
                        }
                    }

                    if (!string.IsNullOrEmpty(Convert.ToString(this.Settings["ScopeForFilters"])))
                    {
                        var list = Convert.ToString(this.Settings["ScopeForFilters"]).Split('|').ToList();
                        var filterList = this.LoadSeachContentSourcesList();
                        foreach (var filter in filterList)
                        {
                            this.filterItems.Add(new ResultsSettingsListItem(filter, filter) { Selected = list.Contains(filter) });
                        }
                    }
                    else
                    {
                        var filterList = this.LoadSeachContentSourcesList();
                        foreach (var filter in filterList)
                        {
                            this.filterItems.Add(new ResultsSettingsListItem(filter, filter) { Selected = true });
                        }
                    }

                    var scopeForRoles =
                        PortalController.GetPortalSetting(this.portalController, "SearchResult_ScopeForRoles", this.PortalId, string.Empty)
                        .Split([','], StringSplitOptions.RemoveEmptyEntries);
                    var roles = RoleController.Instance.GetRoles(this.PortalId, r => !r.IsSystemRole || r.RoleName == "Registered Users");
                    roles.Insert(0, new RoleInfo() { RoleName = "Superusers" });

                    foreach (var role in roles)
                    {
                        this.roleItems.Add(new ResultsSettingsListItem(role.RoleName, role.RoleName) { Selected = scopeForRoles.Length == 0 || scopeForRoles.Contains(role.RoleName) });
                    }

                    this.EnableWildSearchChecked = this.GetBooleanSetting("EnableWildSearch", true);
                    this.ShowDescriptionChecked = this.GetBooleanSetting("ShowDescription", true);
                    this.ShowFriendlyTitleChecked = this.GetBooleanSetting("ShowFriendlyTitle", true);
                    this.ShowSnippetChecked = this.GetBooleanSetting("ShowSnippet", true);
                    this.ShowLastUpdatedChecked = this.GetBooleanSetting("ShowLastUpdated", true);
                    this.ShowSourceChecked = this.GetBooleanSetting("ShowSource", true);
                    this.ShowTagsChecked = this.GetBooleanSetting("ShowTags", true);

                    this.MaxDescriptionLengthText = this.GetStringSetting("MaxDescriptionLength", "100");
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
                this.validated = true;
                if (this.IsValid)
                {
                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "LinkTarget", this.LinkTargetSelectedValue);

                    var selectedPortals = SelectedValuesOf(this.portalItems).Replace(",", "|");

                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ScopeForPortals", selectedPortals);

                    var selectedFilters = SelectedValuesOf(this.filterItems).Replace(",", "|");

                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ScopeForFilters", selectedFilters.ToString());

                    var selectedRoles = SelectedValuesOf(this.roleItems);
                    PortalController.UpdatePortalSetting(this.portalController, this.PortalId, "SearchResult_ScopeForRoles", selectedRoles);

                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "EnableWildSearch", this.EnableWildSearchChecked.ToString());
                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ShowDescription", this.ShowDescriptionChecked.ToString());
                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ShowFriendlyTitle", this.ShowFriendlyTitleChecked.ToString());
                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ShowSnippet", this.ShowSnippetChecked.ToString());
                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ShowLastUpdated", this.ShowLastUpdatedChecked.ToString());
                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ShowSource", this.ShowSourceChecked.ToString());
                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ShowTags", this.ShowTagsChecked.ToString());

                    var maxDescriptionLength = this.MaxDescriptionLengthText;
                    if (string.IsNullOrEmpty(maxDescriptionLength) || !Regex.IsMatch(maxDescriptionLength, "^\\d+$"))
                    {
                        maxDescriptionLength = "100";
                    }

                    ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "MaxDescriptionLength", maxDescriptionLength);
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }

            DataCache.RemoveCache(string.Format("ModuleInfos{0}", this.PortalSettings.PortalId));
        }

        protected IEnumerable<string[]> LoadPortalsList()
        {
            var groups = PortalGroupController.Instance.GetPortalGroups().ToArray();
            var myGroup = (
                from @group in groups
                select PortalGroupController.Instance.GetPortalsByGroup(@group.PortalGroupId) into portals
                where portals.Any((IPortalInfo x) => x.PortalId == PortalSettings.Current.PortalId)
                select portals.ToArray())
                .FirstOrDefault();

            var result = new List<string[]>();
            if (myGroup != null && myGroup.Any())
            {
                result.AddRange(myGroup.Select(
                    (IPortalInfo pi) => new[] { pi.PortalName, pi.PortalId.ToString(CultureInfo.InvariantCulture) }));
            }

            return result;
        }

        protected IEnumerable<string> LoadSeachContentSourcesList()
        {
            var portals = PortalController.Instance.GetPortals();

            var result = new List<string>();
            foreach (var portal in portals)
            {
                var pi = portal as IPortalInfo;
                if (pi != null)
                {
                    var list = InternalSearchController.Instance.GetSearchContentSourceList(pi.PortalId);
                    foreach (var src in list)
                    {
                        if (!src.IsPrivate && !result.Contains(src.LocalizedName))
                        {
                            result.Add(src.LocalizedName);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>Replaces <c>DnnComboBox.Value</c> for a multi-select list.</summary>
        /// <param name="items">The list's items.</param>
        /// <returns>The comma separated values of the selected items.</returns>
        private static string SelectedValuesOf(IEnumerable<ResultsSettingsListItem> items)
        {
            return string.Join(",", items.Where(item => item.Selected).Select(item => item.Value));
        }

        /// <summary>Applies the selection of a multi-select to its item list.</summary>
        /// <param name="items">The list's items.</param>
        /// <param name="value">The value posted by the <c>select</c> element.</param>
        private static void ApplySelection(List<ResultsSettingsListItem> items, object value)
        {
            var selected = value switch
            {
                string[] values => values,
                string single => string.IsNullOrEmpty(single) ? [] : single.Split(','),
                _ => [],
            };

            foreach (var item in items)
            {
                item.Selected = selected.Contains(item.Value);
            }
        }

        private void BuildLinkTargetItems()
        {
            // the two <asp:ListItem>s of <dnn:DnnComboBox ID="comboBoxLinkTarget">, whose captions
            // came from their ResourceKey.
            this.linkTargetItems.Clear();
            this.linkTargetItems.Add(new ResultsSettingsListItem(Localization.GetString("linkTargetOnSamePage", this.LocalResourceFile), "0"));
            this.linkTargetItems.Add(new ResultsSettingsListItem(Localization.GetString("linkTargetOpenNewPage", this.LocalResourceFile), "1"));

            // DnnComboBox rendered its first option as the selected one when nothing was selected.
            this.LinkTargetSelectedValue = this.linkTargetItems[0].Value;
        }

        /// <summary>Replaces <c>comboBoxLinkTarget.SelectedValue = value</c>, which only took values it had an item for.</summary>
        /// <param name="value">The value to select.</param>
        private void SelectLinkTarget(string value)
        {
            if (this.linkTargetItems.Any(item => item.Value == value))
            {
                this.LinkTargetSelectedValue = value;
            }
        }

        private void PortalsSelectionChanged(ChangeEventArgs args)
        {
            ApplySelection(this.portalItems, args.Value);
        }

        private void FiltersSelectionChanged(ChangeEventArgs args)
        {
            ApplySelection(this.filterItems, args.Value);
        }

        private void RolesSelectionChanged(ChangeEventArgs args)
        {
            ApplySelection(this.roleItems, args.Value);
        }

        private bool GetBooleanSetting(string settingName, bool defaultValue)
        {
            if (!string.IsNullOrEmpty(Convert.ToString(this.Settings[settingName])))
            {
                return Convert.ToBoolean(this.Settings[settingName]);
            }

            return defaultValue;
        }

        private string GetStringSetting(string settingName, string defaultValue)
        {
            if (!string.IsNullOrEmpty(Convert.ToString(this.Settings[settingName])))
            {
                return Convert.ToString(this.Settings[settingName]);
            }

            return defaultValue;
        }

        /// <summary>An item of one of this control's four combo boxes.</summary>
        /// <remarks>Replaces <c>System.Web.UI.WebControls.ListItem</c>.</remarks>
        protected sealed class ResultsSettingsListItem
        {
            /// <summary>Initializes a new instance of the <see cref="ResultsSettingsListItem"/> class.</summary>
            /// <param name="text">The item's caption.</param>
            /// <param name="value">The item's value.</param>
            public ResultsSettingsListItem(string text, string value)
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
