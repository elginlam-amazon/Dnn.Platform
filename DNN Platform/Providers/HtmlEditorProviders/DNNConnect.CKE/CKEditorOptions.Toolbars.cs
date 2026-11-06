// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using DNNConnect.CKEditorProvider.Objects;
    using DNNConnect.CKEditorProvider.Utilities;

    using DotNetNuke.Services.Localization;

    using Microsoft.JSInterop;

    using Globals = DotNetNuke.Common.Globals;

    /// <content>
    /// The toolbar set editor of <c>CKEditorOptions.ascx.cs</c>: the two repeaters that rendered
    /// the available buttons and the toolbar groups, the four <c>asp:ImageButton</c> handlers
    /// (<c>IbAddClick</c>, <c>IbCancelClick</c>, <c>IbEditClick</c>, <c>IbDeleteClick</c>) and the
    /// <c>js/Options.js</c> drag and drop contract.
    /// </content>
    /// <remarks>
    /// <para>
    /// The two <c>asp:Repeater</c>s bound <see cref="System.Data.DataTable"/>s whose rows held one
    /// string column each (<c>GroupName</c>; <c>Button</c>/<c>Icon</c>). They are the row models
    /// <see cref="ToolbarGroupRow"/> and <see cref="ToolbarButtonRow"/> the markup iterates, so
    /// <c>ToolbarGroupsRepeater_ItemDataBound</c> - which filled the nested
    /// <c>ToolbarButtonsRepeater</c> per group - is <see cref="BuildGroupButtons"/>, called while
    /// the groups are built.
    /// </para>
    /// <para>
    /// <c>js/Options.js</c> mutates the <c>ToolbarSet</c> hidden input directly with
    /// <c>jQuery.val()</c>, which raises no <c>change</c> event, so the bound field is not updated
    /// by the browser. Every command that needs the serialized toolbar reads the live value back
    /// through <c>getElementValue</c> in js/optionsInterop.js first - this is what the WebForms
    /// postback did when it posted the hidden field.
    /// </para>
    /// </remarks>
    public partial class CKEditorOptions
    {
        /// <summary>The element id of the <c>ToolbarSet</c> hidden input js/Options.js serializes into.</summary>
        private const string ToolbarSetFieldId = "CKEditor_Options_ToolbarSet";

        /// <summary>The rows of the toolbar groups repeater.</summary>
        private readonly List<ToolbarGroupRow> toolbarGroupRows = new List<ToolbarGroupRow>();

        /// <summary>The rows of the available toolbar buttons repeater.</summary>
        private readonly List<ToolbarButtonRow> availableButtonRows = new List<ToolbarButtonRow>();

        /// <summary>Whether the toolbar editor currently edits an existing set.</summary>
        /// <remarks>Replaces the <c>iBAdd.ImageUrl.Contains("save.gif")</c> test of the original.</remarks>
        private bool isToolbarEditMode;

        /// <summary>Whether the "cancel toolbar" image button renders. Replaces <c>iBCancel.Visible</c>.</summary>
        private bool cancelToolbarVisible;

        /// <summary>Whether the toolbar name text box is editable. Replaces <c>dnnTxtToolBName.Enabled</c>.</summary>
        private bool toolbarNameEnabled = true;

        /// <summary>The value of the <c>HiddenMessage</c> hidden field of the export dialog.</summary>
        private string hiddenMessage = string.Empty;

        /// <summary>Gets the image of the add/save toolbar image button.</summary>
        /// <remarks>Replaces <c>iBAdd.ImageUrl</c>, which the original switched between the two images.</remarks>
        internal string AddToolbarImageUrl =>
            Globals.ResolveUrl(this.isToolbarEditMode ? "~/images/save.gif" : "~/images/add.gif");

        /// <summary>Gets the image of the cancel toolbar image button.</summary>
        internal string CancelToolbarImageUrl => Globals.ResolveUrl("~/images/cancel.gif");

        /// <summary>Gets the image of the edit toolbar image button.</summary>
        internal string EditToolbarImageUrl => Globals.ResolveUrl("~/images/edit.gif");

        /// <summary>Gets the image of the delete toolbar image button.</summary>
        internal string DeleteToolbarImageUrl => Globals.ResolveUrl("~/images/delete.gif");

        /// <summary>Gets the alternate text and tooltip of the add/save toolbar image button.</summary>
        /// <remarks>
        /// Replaces <c>iBAdd.AlternateText</c>/<c>ToolTip</c>, which the original reassigned between
        /// <c>AddToolbar.Text</c> and <c>SaveToolbar.Text</c>.
        /// </remarks>
        internal string AddOrSaveToolbarText =>
            this.isToolbarEditMode
                ? Localization.GetString("SaveToolbar.Text", this.ResXFile, this.LangCode)
                : this.AddToolbarText;

        /// <summary>Builds the icon URL of a toolbar button.</summary>
        /// <param name="icon">The icon file name.</param>
        /// <returns>The resolved URL.</returns>
        /// <remarks>Replaces the <c>ResolveUrl(...icons/{0}...)</c> data binding expression of the repeaters.</remarks>
        internal string ToolbarIconUrl(string icon) =>
            Globals.ResolveUrl($"~/Providers/HtmlEditorProviders/DNNConnect.CKE/icons/{icon}");

        /// <summary>Builds the CSS class of one toolbar group button list item.</summary>
        /// <param name="groupButton">The button row.</param>
        /// <returns>The class attribute value.</returns>
        /// <remarks>
        /// The three classes the <c>ToolbarButtonsRepeater</c>'s item template emitted, and which
        /// js/Options.js selects on when it drags buttons between groups: the separator ("-") and
        /// the row break ("/") pseudo buttons carry an extra class.
        /// </remarks>
        private static string GroupButtonCssClass(ToolbarButtonRow groupButton)
        {
            var cssClass = "groupButton ui-state-default ui-corner-all";
            if (groupButton is null)
            {
                return cssClass;
            }

            if (string.Equals(groupButton.Button, "-", StringComparison.Ordinal))
            {
                cssClass += " separator";
            }

            if (string.Equals(groupButton.Button, "/", StringComparison.Ordinal))
            {
                cssClass += " rowBreak";
            }

            return cssClass;
        }

        /// <summary>Hides the "create new group"/"add row break" helpers when nothing is edited.</summary>
        /// <returns>The current toolbar set serialized the way js/Options.js does.</returns>
        /// <remarks>Reads the <c>ToolbarSet</c> hidden input the client script maintains.</remarks>
        private async Task<string> ReadToolbarSetValueAsync()
        {
            try
            {
                var module = await this.JsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "/Providers/HtmlEditorProviders/DNNConnect.CKE/js/optionsInterop.js");

                var value = await module.InvokeAsync<string>("getElementValue", ToolbarSetFieldId);

                if (value != null)
                {
                    this.toolbarSetValue = value;
                }
            }
            catch (Microsoft.JSInterop.JSException)
            {
                // the client side is not reachable; fall back on the last value the browser posted.
            }

            return this.toolbarSetValue;
        }

        /// <summary>Fills the toolbar groups repeater.</summary>
        /// <param name="toolbarSet">The toolbar set.</param>
        /// <param name="excludeButtons">The exclude buttons list.</param>
        /// <remarks>This is <c>FillToolbarGroupsRepeater</c> plus <c>ToolbarGroupsRepeater_ItemDataBound</c>.</remarks>
        private void FillToolbarGroupsRepeater(ToolbarSet toolbarSet, out List<string> excludeButtons)
        {
            excludeButtons = new List<string>();

            this.toolbarGroupRows.Clear();

            this.ToolbarSets = toolbarSet;

            foreach (var group in this.ToolbarSets.ToolbarGroups)
            {
                this.toolbarGroupRows.Add(new ToolbarGroupRow
                {
                    GroupName = group.name,
                    Buttons = this.BuildGroupButtons(group.name),
                });

                // exclude existing buttons in the available list
                excludeButtons.AddRange(group.items);
            }
        }

        /// <summary>Builds the buttons of one toolbar group.</summary>
        /// <param name="groupName">The name of the group.</param>
        /// <returns>The rows of the nested <c>ToolbarButtonsRepeater</c>.</returns>
        /// <remarks>This is the body of <c>ToolbarGroupsRepeater_ItemDataBound</c>, which read the group name off the <c>GroupListItem</c> hidden field.</remarks>
        private List<ToolbarButtonRow> BuildGroupButtons(string groupName)
        {
            var rows = new List<ToolbarButtonRow>();

            if (this.AllListButtons == null)
            {
                this.AllListButtons = ToolbarUtil.LoadToolBarButtons(
                    !string.IsNullOrEmpty(this.configFolder)
                        ? Path.Combine(this.HomeDirectory, this.configFolder)
                        : this.HomeDirectory);
            }

            foreach (var button in this.ToolbarSets.ToolbarGroups
                .Where(group => group.name.Equals(groupName, StringComparison.Ordinal))
                .SelectMany(group => group.items))
            {
                if (this.AllListButtons.Find(availButton => availButton.ToolbarName.Equals(button, StringComparison.Ordinal)) == null
                    && !button.Equals("/", StringComparison.Ordinal))
                {
                    continue;
                }

                var buttonItem = this.AllListButtons.Find(b => b.ToolbarName.Equals(button, StringComparison.Ordinal));

                rows.Add(new ToolbarButtonRow
                {
                    Button = button,
                    Icon = buttonItem != null ? buttonItem.ToolbarIcon : button,
                });
            }

            return rows;
        }

        /// <summary>Fills the available toolbar buttons.</summary>
        /// <param name="excludeItems">The exclude items.</param>
        private void FillAvailableToolbarButtons(ICollection<string> excludeItems)
        {
            this.availableButtonRows.Clear();

            if (this.ListButtons == null)
            {
                this.ListButtons = ToolbarUtil.LoadToolBarButtons(
                    !string.IsNullOrEmpty(this.configFolder)
                        ? Path.Combine(this.HomeDirectory, this.configFolder)
                        : this.HomeDirectory);
            }

            var buttons = this.ListButtons;

            if (excludeItems != null && excludeItems.Count > 0)
            {
                foreach (var excludeItem in excludeItems.Where(excludeItem => !excludeItem.Equals("-", StringComparison.Ordinal)))
                {
                    buttons.RemoveAll(button => button.ToolbarName.Equals(excludeItem, StringComparison.Ordinal));
                }
            }

            foreach (var button in buttons)
            {
                this.availableButtonRows.Add(new ToolbarButtonRow
                {
                    Button = button.ToolbarName,
                    Icon = button.ToolbarIcon,
                });
            }
        }

        /// <summary>Resets the toolbar editor to an empty, unnamed toolbar set.</summary>
        /// <remarks>
        /// This is the tail of <c>InsertToolbars</c>: an empty set holding a single group named
        /// after <c>NewGroupName.Text</c>, plus the full list of available buttons.
        /// </remarks>
        private void FillEmptyToolbarEditor()
        {
            var emptyToolbarSet = new ToolbarSet();

            // Empty Toolbar
            emptyToolbarSet.ToolbarGroups.Add(
                new ToolbarGroup { name = Localization.GetString("NewGroupName.Text", this.ResXFile, this.LangCode) });

            this.FillToolbarGroupsRepeater(emptyToolbarSet, out _);

            // Load Toolbar Buttons
            this.FillAvailableToolbarButtons(null);
        }

        /// <summary>Add new/Save Toolbar Set.</summary>
        /// <returns>A task.</returns>
        /// <remarks>Replaces <c>IbAddClick(object, ImageClickEventArgs)</c>.</remarks>
        private async Task IbAddClick()
        {
            if (string.IsNullOrEmpty(this.toolbarName))
            {
                this.ShowNotification(Localization.GetString("ToolbarNameMissing.Text", this.ResXFile, this.LangCode), "error");

                return;
            }

            var serializedToolbar = await this.ReadToolbarSetValueAsync();

            if (string.IsNullOrEmpty(serializedToolbar))
            {
                return;
            }

            var modifiedSet = ToolbarUtil.ConvertStringToToolbarSet(serializedToolbar);

            // Save modified Toolbar Set
            if (this.isToolbarEditMode)
            {
                var toolbarEdit = this.listToolbars.Find(
                    toolbarSel => toolbarSel.Name.Equals(this.toolbarName, StringComparison.Ordinal));

                toolbarEdit.ToolbarGroups = modifiedSet.ToolbarGroups;
                toolbarEdit.Priority = int.Parse(this.toolbarPriority, CultureInfo.InvariantCulture);

                var homeDirectory = this.portalSettings?.HomeDirectoryMapPath ?? Globals.HostMapPath;
                var homeDirPath = !string.IsNullOrEmpty(this.configFolder)
                                      ? Path.Combine(homeDirectory, this.configFolder)
                                      : homeDirectory;
                ToolbarUtil.SaveToolbarSets(this.listToolbars, homeDirPath);

                this.ShowNotification(
                    Localization.GetString("ToolbarSetSaved.Text", this.ResXFile, this.LangCode),
                    "success");
            }
            else
            {
                // Add New Toolbar Set
                var newToolbar = new ToolbarSet(this.toolbarName, int.Parse(this.toolbarPriority, CultureInfo.InvariantCulture))
                {
                    ToolbarGroups = modifiedSet.ToolbarGroups,
                };

                this.listToolbars.Add(newToolbar);
                var homeDirPath = !string.IsNullOrEmpty(this.configFolder)
                                      ? Path.Combine(this.HomeDirectory, this.configFolder)
                                      : this.HomeDirectory;
                ToolbarUtil.SaveToolbarSets(this.listToolbars, homeDirPath);

                this.ShowNotification(
                    string.Format(
                        Localization.GetString("ToolbarSetCreated.Text", this.ResXFile, this.LangCode),
                        this.toolbarName),
                    "success");
            }

            // Hide Priority
            var selectedPriority = this.toolbarPriorityItems.FirstOrDefault(
                item => string.Equals(item.Value, this.toolbarPriority, StringComparison.Ordinal));
            if (selectedPriority != null)
            {
                selectedPriority.Enabled = false;
            }

            this.BindUserGroupsGridView();

            this.toolbarName = string.Empty;
            this.toolbarSetValue = string.Empty;

            this.FillEmptyToolbarEditor();

            this.toolbarNameEnabled = true;

            this.isToolbarEditMode = false;

            this.cancelToolbarVisible = false;

            this.HideAddToolbar();
        }

        /// <summary>Cancel Edit Toolbar.</summary>
        /// <remarks>Replaces <c>IbCancelClick(object, ImageClickEventArgs)</c>.</remarks>
        private void IbCancelClick()
        {
            this.toolbarName = string.Empty;
            this.toolbarSetValue = string.Empty;

            this.FillEmptyToolbarEditor();

            this.toolbarNameEnabled = true;

            var editedToolbar = this.listToolbars.Find(
                toolbarSel => toolbarSel.Name.Equals(this.customToolbar, StringComparison.Ordinal));

            if (editedToolbar != null)
            {
                var priorityItem = this.toolbarPriorityItems.FirstOrDefault(
                    item => item.Text == editedToolbar.Priority.ToString(CultureInfo.InvariantCulture));

                if (priorityItem != null)
                {
                    priorityItem.Enabled = false;
                }
            }

            this.isToolbarEditMode = false;

            this.cancelToolbarVisible = false;

            this.HideAddToolbar();
        }

        /// <summary>Delete Selected Toolbar Set.</summary>
        /// <remarks>Replaces <c>IbDeleteClick(object, ImageClickEventArgs)</c>.</remarks>
        private void IbDeleteClick()
        {
            if (this.customToolbar == null)
            {
                return;
            }

            var toolbarDelete = this.listToolbars.Find(
                toolbarSel => toolbarSel.Name.Equals(this.customToolbar, StringComparison.Ordinal));

            if (toolbarDelete == null)
            {
                return;
            }

            var priority = toolbarDelete.Priority.ToString(CultureInfo.InvariantCulture);

            if (priority.Length.Equals(1))
            {
                priority = $"0{priority}";
            }

            var priorityItem = this.toolbarPriorityItems.FirstOrDefault(item => item.Text == priority);
            if (priorityItem != null)
            {
                priorityItem.Enabled = true;
            }

            this.listToolbars.RemoveAll(
                toolbarSel => toolbarSel.Name.Equals(this.customToolbar, StringComparison.Ordinal));

            var homeDirPath = !string.IsNullOrEmpty(this.configFolder)
                                  ? Path.Combine(this.HomeDirectory, this.configFolder)
                                  : this.HomeDirectory;
            ToolbarUtil.SaveToolbarSets(this.listToolbars, homeDirPath);

            this.BindUserGroupsGridView();

            this.toolbarNameEnabled = true;

            this.isToolbarEditMode = false;

            this.ShowNotification(
                Localization.GetString("ToolbarSetDeleted.Text", this.ResXFile, this.LangCode),
                "success");
        }

        /// <summary>Edit Selected Toolbar.</summary>
        /// <remarks>Replaces <c>IbEditClick(object, ImageClickEventArgs)</c>.</remarks>
        private void IbEditClick()
        {
            if (this.customToolbar == null)
            {
                return;
            }

            this.AddToolbarVisible = true;

            var toolbarEdit = this.listToolbars.Find(
                toolbarSel => toolbarSel.Name.Equals(this.customToolbar, StringComparison.Ordinal));

            if (toolbarEdit != null)
            {
                this.toolbarName = toolbarEdit.Name;
                this.toolbarSetValue = ToolbarUtil.ConvertToolbarSetToString(toolbarEdit);

                this.FillToolbarGroupsRepeater(toolbarEdit, out var excludeButtons);

                this.FillAvailableToolbarButtons(excludeButtons);

                var priority = toolbarEdit.Priority.ToString(CultureInfo.InvariantCulture);

                if (priority.Length.Equals(1))
                {
                    priority = $"0{priority}";
                }

                var priorityItem = this.toolbarPriorityItems.FirstOrDefault(item => item.Text == priority);
                if (priorityItem != null)
                {
                    priorityItem.Enabled = true;
                }

                this.toolbarPriority = priority;

                this.toolbarNameEnabled = false;

                this.isToolbarEditMode = true;

                this.cancelToolbarVisible = true;
            }

            this.HideAddToolbar();
        }

        /// <summary>A row of the toolbar groups repeater.</summary>
        /// <remarks>Replaces the <c>GroupName</c> row of the <see cref="System.Data.DataTable"/> the repeater bound.</remarks>
        public sealed class ToolbarGroupRow
        {
            /// <summary>Gets or sets the name of the group.</summary>
            public string GroupName { get; set; }

            /// <summary>Gets or sets the buttons of the group, which the nested repeater rendered.</summary>
            public List<ToolbarButtonRow> Buttons { get; set; } = new List<ToolbarButtonRow>();
        }

        /// <summary>A row of one of the two toolbar button repeaters.</summary>
        /// <remarks>Replaces the <c>Button</c>/<c>Icon</c> row of the <see cref="System.Data.DataTable"/>s the repeaters bound.</remarks>
        public sealed class ToolbarButtonRow
        {
            /// <summary>Gets or sets the name of the button.</summary>
            public string Button { get; set; }

            /// <summary>Gets or sets the icon file name of the button.</summary>
            public string Icon { get; set; }
        }
    }
}
