// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.MemberDirectory
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Web;
    using System.Web.UI.WebControls;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Lists;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Profile;
    using DotNetNuke.Entities.Users.Social;
    using DotNetNuke.Modules.MemberDirectory.ViewModels;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.UI.WebControls.Extensions;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Display member directory settings.</summary>
    /// <remarks>
    /// This control was originally implemented using the WebFormsMvp based <c>DotNetNuke.Web.Mvp</c>
    /// pattern (a <c>SettingsView&lt;MemberDirectorySettingsModel&gt;</c> plus a <c>ModuleSettingsPresenter</c>).
    /// That framework was removed, so the presenter logic has been folded directly into this
    /// <see cref="ModuleSettingsBase"/> code-behind while preserving the original behavior.
    /// </remarks>
    public partial class Settings : ModuleSettingsBase
    {
        public const int DefaultPageSize = 20;

        private static string templatePath = "~/DesktopModules/MemberDirectory/Templates/";

        private readonly MemberDirectorySettingsModel model = new MemberDirectorySettingsModel();

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

        public static string DefaultAlternateItemTemplate
        {
            get
            {
                using var sr = new StreamReader(HttpContextSource.Current.Server.MapPath(templatePath + "AlternateItemTemplate.htm"));
                return sr.ReadToEnd();
            }
        }

        public static string DefaultItemTemplate
        {
            get
            {
                using var sr = new StreamReader(HttpContext.Current.Server.MapPath(templatePath + "ItemTemplate.htm"));
                return sr.ReadToEnd();
            }
        }

        public static string DefaultPopUpTemplate
        {
            get
            {
                using var sr = new StreamReader(HttpContext.Current.Server.MapPath(templatePath + "PopUpTemplate.htm"));
                return sr.ReadToEnd();
            }
        }

        /// <summary>Gets the settings view model.</summary>
        protected MemberDirectorySettingsModel Model => this.model;

        /// <inheritdoc />
        public override void LoadSettings()
        {
            base.LoadSettings();

            if (this.IsPostBack)
            {
                return;
            }

            this.LoadModel();

            // Originally handled by SettingsViewBase.OnSettingsLoaded.
            this.BindSortList();

            this.itemTemplate.Text = this.GetTabModuleSetting("ItemTemplate", DefaultItemTemplate);
            this.alternateItemTemplate.Text = this.GetTabModuleSetting("AlternateItemTemplate", DefaultAlternateItemTemplate);
            this.popUpTemplate.Text = this.GetTabModuleSetting("PopUpTemplate", DefaultPopUpTemplate);
            this.displaySearchList.Select(this.GetTabModuleSetting("DisplaySearch", this.defaultDisplaySearch));
            this.enablePopUp.Checked = bool.Parse(this.GetTabModuleSetting("EnablePopUp", this.defaultEnablePopUp));

            this.filterBy = this.GetModuleSetting("FilterBy", this.defaultFilterBy);
            this.filterValue = this.GetModuleSetting("FilterValue", this.defaultFilterValue);
            this.propertyValue.Text = this.GetModuleSetting("FilterPropertyValue", string.Empty);

            this.sortFieldList.Select(this.GetTabModuleSetting("SortField", this.defaultSortField));
            this.sortOrderList.Select(this.GetTabModuleSetting("SortOrder", this.defaultSortOrder));

            this.pageSize.Text = this.GetTabModuleSetting("PageSize", DefaultPageSize.ToString(CultureInfo.InvariantCulture));
            this.disablePager.Checked = bool.Parse(this.GetTabModuleSetting("DisablePaging", "False"));

            // Originally handled by the view's OnLoad.
            if (this.Model.Groups.Count > 0)
            {
                this.groupList.DataSource = this.Model.Groups;
                this.groupList.DataBind();
                this.groupList.Items.Insert(0, new ListItem(Localization.GetString("None_Specified"), Null.NullInteger.ToString()));
            }
            else
            {
                this.filterBySelector.Items.FindByValue("Group").Enabled = false;
            }

            foreach (var rel in this.Model.Relationships)
            {
                this.relationShipList.AddItem(Localization.GetString(rel.Name, Localization.SharedResourceFile), rel.RelationshipId.ToString());
            }

            var profileResourceFile = "~/DesktopModules/Admin/Security/App_LocalResources/Profile.ascx";

            System.Web.UI.WebControls.ListItemCollection propertiesCollection = this.GetPropertiesCollection(profileResourceFile);

            // Bind the ListItemCollection to the list
            this.propertyList.DataSource = propertiesCollection;
            this.propertyList.DataBind();

            // Insert custom properties to the Search field lists
            propertiesCollection.Insert(0, new ListItem(Localization.GetString("Username", this.LocalResourceFile), "Username"));
            propertiesCollection.Insert(1, new ListItem(Localization.GetString("DisplayName", this.LocalResourceFile), "DisplayName"));
            propertiesCollection.Insert(2, new ListItem(Localization.GetString("Email", this.LocalResourceFile), "Email"));

            // Bind the properties collection in the Search Field Lists
            this.searchField1List.DataSource = propertiesCollection;
            this.searchField1List.DataBind();

            this.searchField2List.DataSource = propertiesCollection;
            this.searchField2List.DataBind();

            this.searchField3List.DataSource = propertiesCollection;
            this.searchField3List.DataBind();

            this.searchField4List.DataSource = propertiesCollection;
            this.searchField4List.DataBind();

            this.filterBySelector.Select(this.filterBy, false, 0);

            switch (this.filterBy)
            {
                case "Group":
                    this.groupList.Select(this.filterValue, false, 0);
                    break;
                case "Relationship":
                    this.relationShipList.Select(this.filterValue, false, 0);
                    break;
                case "ProfileProperty":
                    this.propertyList.Select(this.filterValue, false, 0);
                    break;
                case "User":
                    break;
            }

            this.searchField1List.Select(this.GetTabModuleSetting("SearchField1", this.defaultSearchField1));
            this.searchField2List.Select(this.GetTabModuleSetting("SearchField2", this.defaultSearchField2));
            this.searchField3List.Select(this.GetTabModuleSetting("SearchField3", this.defaultSearchField3));
            this.searchField4List.Select(this.GetTabModuleSetting("SearchField4", this.defaultSearchField4));

            this.ExcludeHostUsersCheckBox.Checked = bool.Parse(this.GetTabModuleSetting("ExcludeHostUsers", "false"));
        }

        /// <inheritdoc />
        public override void UpdateSettings()
        {
            this.EnsureModelSettings();

            this.Model.TabModuleSettings["ItemTemplate"] = this.itemTemplate.Text;
            this.Model.TabModuleSettings["AlternateItemTemplate"] = this.alternateItemTemplate.Text;
            this.Model.TabModuleSettings["PopUpTemplate"] = this.popUpTemplate.Text;
            this.Model.TabModuleSettings["EnablePopUp"] = this.enablePopUp.Checked.ToString(CultureInfo.InvariantCulture);

            this.filterBy = this.filterBySelector.SelectedValue;
            this.Model.ModuleSettings["FilterBy"] = this.filterBy;

            switch (this.filterBy)
            {
                case "Group":
                    this.Model.ModuleSettings["FilterValue"] = this.groupList.SelectedValue;
                    break;
                case "Relationship":
                    this.Model.ModuleSettings["FilterValue"] = this.relationShipList.SelectedValue;
                    break;
                case "ProfileProperty":
                    this.Model.ModuleSettings["FilterValue"] = this.propertyList.SelectedValue;
                    break;
            }

            this.Model.ModuleSettings["FilterPropertyValue"] = this.propertyValue.Text;

            this.Model.TabModuleSettings["SortField"] = this.sortFieldList.SelectedValue;
            this.Model.TabModuleSettings["SortOrder"] = this.sortOrderList.SelectedValue;

            this.Model.TabModuleSettings["SearchField1"] = this.searchField1List.SelectedValue;
            this.Model.TabModuleSettings["SearchField2"] = this.searchField2List.SelectedValue;
            this.Model.TabModuleSettings["SearchField3"] = this.searchField3List.SelectedValue;
            this.Model.TabModuleSettings["SearchField4"] = this.searchField4List.SelectedValue;
            this.Model.TabModuleSettings["DisplaySearch"] = this.displaySearchList.SelectedValue;

            this.Model.TabModuleSettings["DisablePaging"] = this.disablePager.Checked.ToString(CultureInfo.InvariantCulture);
            this.Model.TabModuleSettings["PageSize"] = this.pageSize.Text;

            this.Model.TabModuleSettings["ExcludeHostUsers"] = this.ExcludeHostUsersCheckBox.Checked.ToString(CultureInfo.InvariantCulture);

            // Persist the settings (originally handled by ModuleSettingsPresenter.SaveSettings).
            foreach (var setting in this.Model.ModuleSettings)
            {
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, setting.Key, setting.Value);
            }

            foreach (var setting in this.Model.TabModuleSettings)
            {
                ModuleController.Instance.UpdateTabModuleSetting(this.ModuleContext.TabModuleId, setting.Key, setting.Value);
            }

            base.UpdateSettings();
        }

        /// <summary>Gets the module setting.</summary>
        /// <param name="key">The setting name.</param>
        /// <param name="defaultValue">The default setting value.</param>
        /// <returns>The setting value or <paramref name="defaultValue"/>.</returns>
        protected string GetModuleSetting(string key, string defaultValue)
        {
            return this.Model.ModuleSettings.TryGetValue(key, out var settingValue) ? settingValue : defaultValue;
        }

        /// <summary>Gets the tab-module setting.</summary>
        /// <param name="key">The setting name.</param>
        /// <param name="defaultValue">The default setting value.</param>
        /// <returns>The setting value or <paramref name="defaultValue"/>.</returns>
        protected string GetTabModuleSetting(string key, string defaultValue)
        {
            return this.Model.TabModuleSettings.TryGetValue(key, out var settingValue) ? settingValue : defaultValue;
        }

        /// <summary>Loads the view model from the current module context.</summary>
        private void LoadModel()
        {
            this.Model.ModuleSettings = this.ModuleContext.Configuration.ModuleSettings
                .Cast<DictionaryEntry>()
                .ToDictionary(kvp => (string)kvp.Key, kvp => (string)kvp.Value);

            this.Model.TabModuleSettings = this.ModuleContext.Configuration.TabModuleSettings
                .Cast<DictionaryEntry>()
                .ToDictionary(kvp => (string)kvp.Key, kvp => (string)kvp.Value);

            this.Model.Groups = RoleController.Instance.GetRoles(this.PortalId, r => r.Status == RoleStatus.Approved);
            this.Model.Relationships = RelationshipController.Instance.GetRelationshipsByPortalId(this.PortalId);

            this.Model.ProfileProperties = new List<ProfilePropertyDefinition>();
            foreach (ProfilePropertyDefinition definition in ProfileController.GetPropertyDefinitionsByPortal(
                this.DependencyProvider.GetRequiredService<IHostSettings>(),
                this.DependencyProvider.GetRequiredService<IPortalController>(),
                this.DependencyProvider.GetRequiredService<IApplicationStatusInfo>(),
                this.DependencyProvider.GetRequiredService<IPortalGroupController>(),
                this.PortalId,
                true,
                true))
            {
                var controller = new ListController(null, null);
                ListEntryInfo textType = controller.GetListEntryInfo("DataType", "Text");
                ListEntryInfo regionType = controller.GetListEntryInfo("DataType", "Region");
                ListEntryInfo countryType = controller.GetListEntryInfo("DataType", "Country");
                if (definition.DataType == textType.EntryID || definition.DataType == regionType.EntryID || definition.DataType == countryType.EntryID)
                {
                    this.Model.ProfileProperties.Add(definition);
                }
            }
        }

        /// <summary>Ensures the model has settings dictionaries available during a post-back save.</summary>
        private void EnsureModelSettings()
        {
            this.Model.ModuleSettings ??= new Dictionary<string, string>();
            this.Model.TabModuleSettings ??= new Dictionary<string, string>();
        }

        private System.Web.UI.WebControls.ListItemCollection GetPropertiesCollection(string profileResourceFile)
        {
            var result = new System.Web.UI.WebControls.ListItemCollection();
            foreach (var property in this.Model.ProfileProperties)
            {
                result.Add(new ListItem(this.GetLocalizeName(property.PropertyName, profileResourceFile), property.PropertyName));
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
            this.sortFieldList.Items.Add(this.AddSearchItem("UserId"));
            this.sortFieldList.Items.Add(this.AddSearchItem("LastName"));
            this.sortFieldList.Items.Add(this.AddSearchItem("DisplayName"));
            this.sortFieldList.Items.Add(this.AddSearchItem("CreatedOnDate", "DateCreated"));
            var controller = new ListController(null, null);
            var imageDataType = controller.GetListEntryInfo("DataType", "Image");
            foreach (ProfilePropertyDefinition definition in this.Model.ProfileProperties)
            {
                if (imageDataType != null && definition.DataType != imageDataType.EntryID)
                {
                    this.sortFieldList.Items.Add(this.AddSearchItem(definition.PropertyName));
                }
            }
        }

        private ListItem AddSearchItem(string name)
        {
            return this.AddSearchItem(name, name);
        }

        private ListItem AddSearchItem(string name, string resourceKey)
        {
            var text = Localization.GetString(resourceKey, this.LocalResourceFile);
            if (string.IsNullOrEmpty(text))
            {
                text = resourceKey;
            }

            var item = new ListItem(text, name);
            return item;
        }
    }
}
