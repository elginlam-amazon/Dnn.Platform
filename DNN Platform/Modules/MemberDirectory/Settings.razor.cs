// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.MemberDirectory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Lists;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Profile;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Modules.MemberDirectory.Presenters;
    using DotNetNuke.Modules.MemberDirectory.ViewModels;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Mvp;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Http;
    using Microsoft.JSInterop;

    /// <summary>Display member directory settings.</summary>
    /// <remarks>
    /// Was <c>Settings.ascx</c> + <c>Settings.ascx.cs</c> + <c>Settings.ascx.designer.cs</c>; the
    /// markup is now <c>Settings.razor</c>. This is still a
    /// <see cref="SettingsView{TModel}"/> bound to <see cref="ModuleSettingsPresenter"/> — the MVP
    /// settings surface is component based on net10.0 — so the presenter and the view model are
    /// unchanged. The twenty designer-generated server controls became the bound state below,
    /// each keeping the control's ID as the rendered element id:
    /// <list type="bullet">
    /// <item><description>the four <c>asp:TextBox</c>es are <see cref="ItemTemplateText"/>, <see cref="AlternateItemTemplateText"/>, <see cref="PopUpTemplateText"/> and <see cref="PropertyValueText"/>, plus <see cref="PageSizeText"/>;</description></item>
    /// <item><description>the three <c>asp:CheckBox</c>es are <see cref="EnablePopUpChecked"/>, <see cref="ExcludeHostUsersChecked"/> and <see cref="DisablePagerChecked"/>;</description></item>
    /// <item><description>the <c>asp:RadioButtonList</c> is <see cref="FilterByItems"/>/<see cref="FilterBySelectedValue"/>, keeping the per-item <c>Enabled</c> flag the "no groups" branch cleared;</description></item>
    /// <item><description>the seven <c>asp:DropDownList</c>s are the item lists and selected values below, with the same <c>Value</c>/<c>Text</c> semantics <c>ListItem</c> had (the <c>SettingsListItem</c> pattern of the migrated Journal settings).</description></item>
    /// </list>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    [PresenterBinding(typeof(ModuleSettingsPresenter))]
    public partial class Settings : SettingsView<MemberDirectorySettingsModel>
    {
        /// <summary>The default page size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>The module's folder, which is where the client script of this control lives.</summary>
        private const string ModulePath = "~/DesktopModules/MemberDirectory/";

        private static string templatePath = "~/DesktopModules/MemberDirectory/Templates/";

        private readonly List<SettingsListItem> filterByItems = new List<SettingsListItem>
        {
            // the five <asp:ListItem>s of the filterBySelector radio button list; the captions come
            // from the resource keys the markup declared (None, User, Group, Relationship,
            // ProfileProperty), which the WebForms control localization pass resolved.
            new SettingsListItem("None", string.Empty),
            new SettingsListItem("User", string.Empty),
            new SettingsListItem("Group", string.Empty),
            new SettingsListItem("Relationship", string.Empty),
            new SettingsListItem("ProfileProperty", string.Empty),
        };

        private readonly List<SettingsListItem> sortOrderItems = new List<SettingsListItem>
        {
            new SettingsListItem("ASC", string.Empty),
            new SettingsListItem("DESC", string.Empty),
        };

        private readonly List<SettingsListItem> displaySearchItems = new List<SettingsListItem>
        {
            new SettingsListItem("None", string.Empty),
            new SettingsListItem("Simple", string.Empty),
            new SettingsListItem("Both", string.Empty),
        };

        private readonly List<SettingsListItem> groupItems = new List<SettingsListItem>();
        private readonly List<SettingsListItem> relationShipItems = new List<SettingsListItem>();
        private readonly List<SettingsListItem> propertyItems = new List<SettingsListItem>();
        private readonly List<SettingsListItem> searchFieldItems = new List<SettingsListItem>();
        private readonly List<SettingsListItem> sortFieldItems = new List<SettingsListItem>();

        private string defaultSearchField1 = "DisplayName";
        private string defaultSearchField2 = "Email";
        private string defaultSearchField3 = "City";
        private string defaultSearchField4 = "Country";

        private string defaultSortField = "DisplayName";
        private string defaultSortOrder = "ASC";

        private string defaultFilterBy = "None";
        private string defaultFilterValue = string.Empty;

        private string defaultDisplaySearch = "Both";
        private string defaultEnablePopUp = "false";

        private string filterBy;
        private string filterValue;

        private bool loadHandled;
        private bool settingsLoadHandled;
        private IJSObjectReference interopModule;

        /// <summary>Gets the default alternate item template.</summary>
        public static string DefaultAlternateItemTemplate
        {
            get
            {
                using var sr = new StreamReader(HttpContextSource.MapPath(templatePath + "AlternateItemTemplate.htm"));
                return sr.ReadToEnd();
            }
        }

        /// <summary>Gets the default item template.</summary>
        public static string DefaultItemTemplate
        {
            get
            {
                // was HttpContext.Current.Server.MapPath: System.Web's static context does not
                // exist on net10.0, HttpContextSource.Current is the DNN accessor for it.
                using var sr = new StreamReader(HttpContextSource.MapPath(templatePath + "ItemTemplate.htm"));
                return sr.ReadToEnd();
            }
        }

        /// <summary>Gets the default pop-up template.</summary>
        public static string DefaultPopUpTemplate
        {
            get
            {
                using var sr = new StreamReader(HttpContextSource.MapPath(templatePath + "PopUpTemplate.htm"));
                return sr.ReadToEnd();
            }
        }

        /// <summary>Gets the items of the "filter by" radio button list.</summary>
        protected IList<SettingsListItem> FilterByItems => this.filterByItems;

        /// <summary>Gets the items of the group drop down list.</summary>
        protected IList<SettingsListItem> GroupItems => this.groupItems;

        /// <summary>Gets the items of the relationship drop down list.</summary>
        protected IList<SettingsListItem> RelationShipItems => this.relationShipItems;

        /// <summary>Gets the items of the profile property drop down list.</summary>
        protected IList<SettingsListItem> PropertyItems => this.propertyItems;

        /// <summary>Gets the items shared by the four search field drop down lists.</summary>
        protected IList<SettingsListItem> SearchFieldItems => this.searchFieldItems;

        /// <summary>Gets the items of the sort field drop down list.</summary>
        protected IList<SettingsListItem> SortFieldItems => this.sortFieldItems;

        /// <summary>Gets the items of the sort order drop down list.</summary>
        protected IList<SettingsListItem> SortOrderItems => this.sortOrderItems;

        /// <summary>Gets the items of the "display search" drop down list.</summary>
        protected IList<SettingsListItem> DisplaySearchItems => this.displaySearchItems;

        /// <summary>Gets or sets the item template. Replaces <c>itemTemplate.Text</c>.</summary>
        protected string ItemTemplateText { get; set; } = string.Empty;

        /// <summary>Gets or sets the alternate item template. Replaces <c>alternateItemTemplate.Text</c>.</summary>
        protected string AlternateItemTemplateText { get; set; } = string.Empty;

        /// <summary>Gets or sets the pop-up template. Replaces <c>popUpTemplate.Text</c>.</summary>
        protected string PopUpTemplateText { get; set; } = string.Empty;

        /// <summary>Gets or sets the profile property filter value. Replaces <c>propertyValue.Text</c>.</summary>
        protected string PropertyValueText { get; set; } = string.Empty;

        /// <summary>Gets or sets the page size. Replaces <c>pageSize.Text</c>.</summary>
        protected string PageSizeText { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the pop-up is enabled. Replaces <c>enablePopUp.Checked</c>.</summary>
        protected bool EnablePopUpChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether host users are excluded. Replaces <c>ExcludeHostUsersCheckBox.Checked</c>.</summary>
        protected bool ExcludeHostUsersChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether paging is disabled. Replaces <c>disablePager.Checked</c>.</summary>
        protected bool DisablePagerChecked { get; set; }

        /// <summary>Gets or sets the selected value of the "filter by" radio button list.</summary>
        protected string FilterBySelectedValue { get; set; }

        /// <summary>Gets or sets the selected value of the group drop down list.</summary>
        protected string GroupSelectedValue { get; set; }

        /// <summary>Gets or sets the selected value of the relationship drop down list.</summary>
        protected string RelationShipSelectedValue { get; set; }

        /// <summary>Gets or sets the selected value of the profile property drop down list.</summary>
        protected string PropertySelectedValue { get; set; }

        /// <summary>Gets or sets the selected value of the sort field drop down list.</summary>
        protected string SortFieldSelectedValue { get; set; }

        /// <summary>Gets or sets the selected value of the sort order drop down list.</summary>
        protected string SortOrderSelectedValue { get; set; }

        /// <summary>Gets or sets the selected value of the "display search" drop down list.</summary>
        protected string DisplaySearchSelectedValue { get; set; }

        /// <summary>Gets or sets the selected value of the first search field drop down list.</summary>
        protected string SearchField1SelectedValue { get; set; }

        /// <summary>Gets or sets the selected value of the second search field drop down list.</summary>
        protected string SearchField2SelectedValue { get; set; }

        /// <summary>Gets or sets the selected value of the third search field drop down list.</summary>
        protected string SearchField3SelectedValue { get; set; }

        /// <summary>Gets or sets the selected value of the fourth search field drop down list.</summary>
        protected string SearchField4SelectedValue { get; set; }

        /// <summary>
        /// Gets the validation message shown next to the page size, which replaces the
        /// <c>asp:RangeValidator</c> on <c>pageSize</c>.
        /// </summary>
        protected string PageSizeErrorMessage { get; private set; }

        /// <summary>Gets or sets the JavaScript runtime the filter visibility script is invoked through.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.AutoDataBind = false;

            // the WebForms control localization pass resolved the resourcekey of every declared
            // <asp:ListItem>; a component localizes its own markup, and Init is the first phase
            // that can.
            this.LocalizeDeclaredItems();
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Replaces "if (!this.IsPostBack)": the settings host calls LoadSettings() and
            // UpdateSettings() in the same request, so the guard is what keeps the editor's
            // unsaved input from being overwritten with the stored values before it is read back.
            if (this.IsPostBackRequest() || this.loadHandled)
            {
                return;
            }

            this.loadHandled = true;

            if (this.Model.Groups.Count > 0)
            {
                foreach (var group in this.Model.Groups)
                {
                    this.groupItems.Add(new SettingsListItem(group.RoleID.ToString(CultureInfo.InvariantCulture), group.RoleName));
                }

                this.groupItems.Insert(0, new SettingsListItem(Null.NullInteger.ToString(), Localization.GetString("None_Specified")));
            }
            else
            {
                FindByValue(this.filterByItems, "Group").Enabled = false;
            }

            foreach (var rel in this.Model.Relationships)
            {
                this.relationShipItems.Add(new SettingsListItem(
                    rel.RelationshipId.ToString(),
                    Localization.GetString(rel.Name, Localization.SharedResourceFile)));
            }

            var profileResourceFile = "~/DesktopModules/Admin/Security/App_LocalResources/Profile.ascx";

            var propertiesCollection = this.GetPropertiesCollection(profileResourceFile);

            // Bind the collection to the list
            this.propertyItems.Clear();
            this.propertyItems.AddRange(propertiesCollection);

            // Insert custom properties to the Search field lists
            propertiesCollection.Insert(0, new SettingsListItem("Username", Localization.GetString("Username", this.LocalResourceFile)));
            propertiesCollection.Insert(1, new SettingsListItem("DisplayName", Localization.GetString("DisplayName", this.LocalResourceFile)));
            propertiesCollection.Insert(2, new SettingsListItem("Email", Localization.GetString("Email", this.LocalResourceFile)));

            // Bind the properties collection in the Search Field Lists (all four lists shared the
            // same ListItemCollection instance in WebForms as well).
            this.searchFieldItems.Clear();
            this.searchFieldItems.AddRange(propertiesCollection);

            this.FilterBySelectedValue = SelectValue(this.filterByItems, this.filterBy, 0);

            switch (this.filterBy)
            {
                case "Group":
                    this.GroupSelectedValue = SelectValue(this.groupItems, this.filterValue, 0);
                    break;
                case "Relationship":
                    this.RelationShipSelectedValue = SelectValue(this.relationShipItems, this.filterValue, 0);
                    break;
                case "ProfileProperty":
                    this.PropertySelectedValue = SelectValue(this.propertyItems, this.filterValue, 0);
                    break;
                case "User":
                    break;
            }

            this.SearchField1SelectedValue = SelectValue(this.searchFieldItems, this.GetTabModuleSetting("SearchField1", this.defaultSearchField1), -1);
            this.SearchField2SelectedValue = SelectValue(this.searchFieldItems, this.GetTabModuleSetting("SearchField2", this.defaultSearchField2), -1);
            this.SearchField3SelectedValue = SelectValue(this.searchFieldItems, this.GetTabModuleSetting("SearchField3", this.defaultSearchField3), -1);
            this.SearchField4SelectedValue = SelectValue(this.searchFieldItems, this.GetTabModuleSetting("SearchField4", this.defaultSearchField4), -1);

            this.ExcludeHostUsersChecked = bool.Parse(this.GetTabModuleSetting("ExcludeHostUsers", "false"));
        }

        /// <inheritdoc />
        protected override void OnSettingsLoaded()
        {
            base.OnSettingsLoaded();

            if (this.IsPostBackRequest() || this.settingsLoadHandled)
            {
                return;
            }

            this.settingsLoadHandled = true;

            this.BindSortList();

            this.ItemTemplateText = this.GetTabModuleSetting("ItemTemplate", DefaultItemTemplate);
            this.AlternateItemTemplateText = this.GetTabModuleSetting("AlternateItemTemplate", DefaultAlternateItemTemplate);
            this.PopUpTemplateText = this.GetTabModuleSetting("PopUpTemplate", DefaultPopUpTemplate);
            this.DisplaySearchSelectedValue = SelectValue(this.displaySearchItems, this.GetTabModuleSetting("DisplaySearch", this.defaultDisplaySearch), -1);
            this.EnablePopUpChecked = bool.Parse(this.GetTabModuleSetting("EnablePopUp", this.defaultEnablePopUp));

            this.filterBy = this.GetModuleSetting("FilterBy", this.defaultFilterBy);
            this.filterValue = this.GetModuleSetting("FilterValue", this.defaultFilterValue);
            this.PropertyValueText = this.GetModuleSetting("FilterPropertyValue", string.Empty);

            this.SortFieldSelectedValue = SelectValue(this.sortFieldItems, this.GetTabModuleSetting("SortField", this.defaultSortField), -1);
            this.SortOrderSelectedValue = SelectValue(this.sortOrderItems, this.GetTabModuleSetting("SortOrder", this.defaultSortOrder), -1);

            this.PageSizeText = this.GetTabModuleSetting("PageSize", DefaultPageSize.ToString(CultureInfo.InvariantCulture));
            this.DisablePagerChecked = bool.Parse(this.GetTabModuleSetting("DisablePaging", "False"));
        }

        /// <inheritdoc />
        protected override void OnSavingSettings()
        {
            // Replaces the asp:RangeValidator on pageSize (Integer, 1-100): an invalid value kept
            // the page from posting back, so it must keep the settings from being written.
            //
            // This MUST throw rather than return: SettingsViewBase.UpdateSettings() calls
            // OnSavingSettings() and then raises OnSaveSettings unconditionally, so simply
            // returning would skip the writes while the host still persisted the presenter's
            // model and reported a successful save -- silently discarding the user's edits with
            // an error message they never see. Throwing aborts UpdateSettings() before
            // OnSaveSettings is raised, which is what the RangeValidator's blocked postback did.
            if (!this.ValidatePageSize())
            {
                this.NotifyTreeChanged();
                throw new InvalidOperationException(this.PageSizeErrorMessage);
            }

            this.Model.TabModuleSettings["ItemTemplate"] = this.ItemTemplateText;
            this.Model.TabModuleSettings["AlternateItemTemplate"] = this.AlternateItemTemplateText;
            this.Model.TabModuleSettings["PopUpTemplate"] = this.PopUpTemplateText;
            this.Model.TabModuleSettings["EnablePopUp"] = this.EnablePopUpChecked.ToString(CultureInfo.InvariantCulture);

            this.filterBy = this.FilterBySelectedValue ?? string.Empty;
            this.Model.ModuleSettings["FilterBy"] = this.filterBy;

            switch (this.filterBy)
            {
                case "Group":
                    this.Model.ModuleSettings["FilterValue"] = this.GroupSelectedValue ?? string.Empty;
                    break;
                case "Relationship":
                    this.Model.ModuleSettings["FilterValue"] = this.RelationShipSelectedValue ?? string.Empty;
                    break;
                case "ProfileProperty":
                    this.Model.ModuleSettings["FilterValue"] = this.PropertySelectedValue ?? string.Empty;
                    break;
            }

            this.Model.ModuleSettings["FilterPropertyValue"] = this.PropertyValueText;

            this.Model.TabModuleSettings["SortField"] = this.SortFieldSelectedValue ?? string.Empty;
            this.Model.TabModuleSettings["SortOrder"] = this.SortOrderSelectedValue ?? string.Empty;

            this.Model.TabModuleSettings["SearchField1"] = this.SearchField1SelectedValue ?? string.Empty;
            this.Model.TabModuleSettings["SearchField2"] = this.SearchField2SelectedValue ?? string.Empty;
            this.Model.TabModuleSettings["SearchField3"] = this.SearchField3SelectedValue ?? string.Empty;
            this.Model.TabModuleSettings["SearchField4"] = this.SearchField4SelectedValue ?? string.Empty;
            this.Model.TabModuleSettings["DisplaySearch"] = this.DisplaySearchSelectedValue ?? string.Empty;

            this.Model.TabModuleSettings["DisablePaging"] = this.DisablePagerChecked.ToString(CultureInfo.InvariantCulture);
            this.Model.TabModuleSettings["PageSize"] = this.PageSizeText;

            this.Model.TabModuleSettings["ExcludeHostUsers"] = this.ExcludeHostUsersChecked.ToString(CultureInfo.InvariantCulture);

            base.OnSavingSettings();
        }

        /// <inheritdoc />
        /// <remarks>
        /// Runs the client side seam of this control: Settings.ascx emitted the filter show/hide
        /// script inline, which a Blazor render cannot execute, so it lives in
        /// Scripts/memberDirectorySettingsInterop.js and is invoked here. The
        /// <c>Sys.WebForms.PageRequestManager.add_endRequest</c> re-registration is gone (there are
        /// no post backs any more); instead the visibility is re-applied after every render that
        /// could have changed the selection.
        /// </remarks>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            try
            {
                if (firstRender)
                {
                    this.interopModule = await this.JsRuntime.InvokeAsync<IJSObjectReference>(
                        "import", Globals.ResolveUrl(ModulePath + "Scripts/memberDirectorySettingsInterop.js"));
                    await this.interopModule.InvokeVoidAsync("setUpSettings");
                }
                else if (this.interopModule != null)
                {
                    await this.interopModule.InvokeVoidAsync("toggleFilter");
                }
            }
            catch (JSException)
            {
                // the extension's assets are not reachable (or there is no browser, as during a
                // plain HTTP render): every filter list simply stays visible.
                this.interopModule = null;
            }
        }

        /// <summary>Applies the "filter by" selection, replacing the radio button list's post back.</summary>
        /// <param name="value">The value of the selected radio button.</param>
        protected void OnFilterByChanged(string value)
        {
            this.FilterBySelectedValue = value;
        }

        private static SettingsListItem FindByValue(IList<SettingsListItem> items, string value)
        {
            foreach (var item in items)
            {
                if (string.Equals(item.Value, value, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Replaces the DNN <c>ListControl.Select(initial, false, fallbackIndex)</c> extension: find
        /// the item with that value, case insensitively, and fall back to the item at
        /// <paramref name="fallbackIndex"/> (nothing, when it is negative).
        /// </summary>
        /// <param name="items">The items of the list.</param>
        /// <param name="initial">The value to select.</param>
        /// <param name="fallbackIndex">The index to select when there is no such value.</param>
        /// <returns>The value that is selected.</returns>
        private static string SelectValue(IList<SettingsListItem> items, string initial, int fallbackIndex)
        {
            foreach (var item in items)
            {
                if (string.Equals(item.Value, initial, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Value;
                }
            }

            return fallbackIndex > -1 && fallbackIndex < items.Count ? items[fallbackIndex].Value : null;
        }

        /// <summary>Gets a value indicating whether this is the equivalent of a WebForms post back.</summary>
        /// <returns><see langword="true"/> when the current request posts the settings form back.</returns>
        private bool IsPostBackRequest()
        {
            return HttpMethods.IsPost(this.Request?.Method ?? HttpMethods.Get);
        }

        /// <summary>Validates the page size the way the <c>asp:RangeValidator</c> did.</summary>
        /// <returns><see langword="true"/> when the page size is an integer between 1 and 100.</returns>
        private bool ValidatePageSize()
        {
            // an empty value never triggered the validator (it has no RequiredFieldValidator).
            if (string.IsNullOrWhiteSpace(this.PageSizeText))
            {
                this.PageSizeErrorMessage = null;
                return true;
            }

            if (int.TryParse(this.PageSizeText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var size)
                && size >= 1 && size <= 100)
            {
                this.PageSizeErrorMessage = null;
                return true;
            }

            this.PageSizeErrorMessage = Localization.GetString("pageSize.Error", this.LocalResourceFile);
            return false;
        }

        /// <summary>Localizes the list items the markup declared with a <c>resourcekey</c>.</summary>
        private void LocalizeDeclaredItems()
        {
            foreach (var item in this.filterByItems)
            {
                item.Text = Localization.GetString(item.Value, this.LocalResourceFile);
            }

            this.sortOrderItems[0].Text = Localization.GetString("Ascending", this.LocalResourceFile);
            this.sortOrderItems[1].Text = Localization.GetString("Descending", this.LocalResourceFile);

            foreach (var item in this.displaySearchItems)
            {
                item.Text = Localization.GetString("DisplaySearch_" + item.Value, this.LocalResourceFile);
            }
        }

        private List<SettingsListItem> GetPropertiesCollection(string profileResourceFile)
        {
            var result = new List<SettingsListItem>();
            foreach (var property in this.Model.ProfileProperties)
            {
                result.Add(new SettingsListItem(property.PropertyName, this.GetLocalizeName(property.PropertyName, profileResourceFile)));
            }

            return result;
        }

        private string GetLocalizeName(string propertyName, string resourceFile)
        {
            var name = Localization.GetString("ProfileProperties_" + propertyName, resourceFile);
            return string.IsNullOrEmpty(name) ? propertyName : name.Trim(':');
        }

        private void BindSortList()
        {
            this.sortFieldItems.Add(this.AddSearchItem("UserId"));
            this.sortFieldItems.Add(this.AddSearchItem("LastName"));
            this.sortFieldItems.Add(this.AddSearchItem("DisplayName"));
            this.sortFieldItems.Add(this.AddSearchItem("CreatedOnDate", "DateCreated"));
            var controller = new ListController(null, null);
            var imageDataType = controller.GetListEntryInfo("DataType", "Image");
            foreach (ProfilePropertyDefinition definition in this.Model.ProfileProperties)
            {
                if (imageDataType != null && definition.DataType != imageDataType.EntryID)
                {
                    this.sortFieldItems.Add(this.AddSearchItem(definition.PropertyName));
                }
            }
        }

        private SettingsListItem AddSearchItem(string name)
        {
            return this.AddSearchItem(name, name);
        }

        private SettingsListItem AddSearchItem(string name, string resourceKey)
        {
            var text = Localization.GetString(resourceKey, this.LocalResourceFile);
            if (string.IsNullOrEmpty(text))
            {
                text = resourceKey;
            }

            return new SettingsListItem(name, text);
        }

        /// <summary>An item of one of the setting lists.</summary>
        /// <remarks>
        /// Replaces <c>System.Web.UI.WebControls.ListItem</c> (and the
        /// <c>ListItemCollection</c> the properties were gathered in), with the same four members
        /// this control used.
        /// </remarks>
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

            /// <summary>Gets or sets a value indicating whether this item is selected.</summary>
            public bool Selected { get; set; }

            /// <summary>Gets or sets a value indicating whether this item can be selected.</summary>
            /// <remarks>Replaces <c>ListItem.Enabled</c>, which the "no groups" branch cleared.</remarks>
            public bool Enabled { get; set; } = true;
        }
    }
}
