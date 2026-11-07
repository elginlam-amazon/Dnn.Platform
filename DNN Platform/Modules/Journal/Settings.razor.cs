// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.Journal
{
    /*
' Copyright (c) 2011  DotNetNuke Corporation
'  All rights reserved.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
' DEALINGS IN THE SOFTWARE.
'
*/

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Modules.Journal.Components;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Journal;
    using DotNetNuke.Services.Journal.Internal;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Http;

    /// <summary>The Settings class manages Module Settings.</summary>
    /// <remarks>
    /// Was <c>Settings.ascx</c> + <c>Settings.ascx.cs</c> + <c>Settings.ascx.designer.cs</c>; the
    /// markup is <c>Settings.razor</c>. The seven designer-generated server controls became the
    /// state below, which <c>Settings.razor</c> binds to:
    /// <list type="bullet">
    /// <item><description>the four <c>asp:CheckBox</c>es are <see cref="EnableEditorChecked"/>, <see cref="AllowFilesChecked"/>, <see cref="AllowPhotosChecked"/> and <see cref="AllowResizeChecked"/>, with their <c>Enabled</c> flags next to them;</description></item>
    /// <item><description>the two <c>asp:DropDownList</c>s are <see cref="PageSizeItems"/>/<see cref="PageSizeSelectedIndex"/> and <see cref="MessageLengthItems"/>/<see cref="MessageLengthSelectedIndex"/>, keeping the item values the markup declared and the <c>SelectedIndex</c>/<c>SelectedItem</c> semantics the code behind relies on;</description></item>
    /// <item><description>the <c>asp:CheckBoxList</c> is <see cref="JournalFilterItems"/>, a list of <see cref="SettingsListItem"/> with the same <c>Value</c>/<c>Text</c>/<c>Selected</c> surface <c>ListItem</c> had.</description></item>
    /// </list>
    /// </remarks>
    public partial class Settings : JournalSettingsBase
    {
        private readonly List<SettingsListItem> pageSizeItems = new List<SettingsListItem>
        {
            new SettingsListItem("5", "5"),
            new SettingsListItem("10", "10"),
            new SettingsListItem("20", "20"),
            new SettingsListItem("30", "30"),
            new SettingsListItem("40", "40"),
            new SettingsListItem("50", "50"),
            new SettingsListItem("75", "75"),
            new SettingsListItem("100", "100"),
        };

        private readonly List<SettingsListItem> messageLengthItems = new List<SettingsListItem>
        {
            new SettingsListItem("140", "140"),
            new SettingsListItem("250", "250"),
            new SettingsListItem("500", "500"),
            new SettingsListItem("1000", "1000"),
            new SettingsListItem("2000", "2000"),

            // the <asp:ListItem Value="-1" resourcekey="MessageLength_Unlimited"> had no inline
            // text; its caption comes from the local resource file, which is set in OnInit.
            new SettingsListItem("-1", string.Empty),
        };

        private readonly List<SettingsListItem> journalFilterItems = new List<SettingsListItem>();

        private bool settingsLoaded;

        /// <summary>Gets the items of the "default page size" drop down list.</summary>
        protected IList<SettingsListItem> PageSizeItems => this.pageSizeItems;

        /// <summary>Gets the items of the "max message length" drop down list.</summary>
        protected IList<SettingsListItem> MessageLengthItems => this.messageLengthItems;

        /// <summary>Gets the items of the journal filters check box list.</summary>
        protected IList<SettingsListItem> JournalFilterItems => this.journalFilterItems;

        /// <summary>Gets or sets the selected index of the "default page size" drop down list.</summary>
        /// <remarks>Replaces <c>drpDefaultPageSize.SelectedIndex</c>; -1 means nothing is selected, as it did there.</remarks>
        protected int PageSizeSelectedIndex { get; set; } = -1;

        /// <summary>Gets or sets the selected index of the "max message length" drop down list.</summary>
        /// <remarks>Replaces <c>drpMaxMessageLength.SelectedIndex</c>.</remarks>
        protected int MessageLengthSelectedIndex { get; set; } = -1;

        /// <summary>Gets or sets a value indicating whether the journal editor is enabled.</summary>
        /// <remarks>Replaces <c>chkEnableEditor.Checked</c>.</remarks>
        protected bool EnableEditorChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether file attachments are allowed.</summary>
        /// <remarks>Replaces <c>chkAllowFiles.Checked</c>.</remarks>
        protected bool AllowFilesChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether photo attachments are allowed.</summary>
        /// <remarks>Replaces <c>chkAllowPhotos.Checked</c>.</remarks>
        protected bool AllowPhotosChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether photos are resized.</summary>
        /// <remarks>Replaces <c>chkAllowResize.Checked</c>.</remarks>
        protected bool AllowResizeChecked { get; set; }

        /// <summary>Gets or sets a value indicating whether the "allow files" check box is enabled.</summary>
        /// <remarks>Replaces <c>chkAllowFiles.Enabled</c>.</remarks>
        protected bool AllowFilesEnabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the "allow photos" check box is enabled.</summary>
        /// <remarks>Replaces <c>chkAllowPhotos.Enabled</c>.</remarks>
        protected bool AllowPhotosEnabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the "allow resize" check box is enabled.</summary>
        /// <remarks>Replaces <c>chkAllowResize.Enabled</c>.</remarks>
        protected bool AllowResizeEnabled { get; set; } = true;

        /// <summary>Gets the selected item of the "default page size" drop down list.</summary>
        /// <remarks>
        /// Replaces <c>drpDefaultPageSize.SelectedItem</c>, including the fact that it is
        /// <see langword="null"/> when nothing is selected.
        /// </remarks>
        protected SettingsListItem PageSizeSelectedItem =>
            this.PageSizeSelectedIndex >= 0 && this.PageSizeSelectedIndex < this.pageSizeItems.Count
                ? this.pageSizeItems[this.PageSizeSelectedIndex]
                : null;

        /// <summary>Gets the selected item of the "max message length" drop down list.</summary>
        /// <remarks>Replaces <c>drpMaxMessageLength.SelectedItem</c>.</remarks>
        protected SettingsListItem MessageLengthSelectedItem =>
            this.MessageLengthSelectedIndex >= 0 && this.MessageLengthSelectedIndex < this.messageLengthItems.Count
                ? this.messageLengthItems[this.MessageLengthSelectedIndex]
                : null;

        /// <summary>LoadSettings loads the settings from the Database and displays them.</summary>
        /// <remarks>
        /// <para>
        /// Replaces the <c>this.Page.IsPostBack == false</c> guard. The host settings page calls
        /// <c>LoadSettings()</c> on every request and <c>UpdateSettings()</c> later in that same
        /// request, so the guard is what stops the editor's unsaved input being overwritten with
        /// the stored values before it is read back.
        /// </para>
        /// <para>
        /// The guard is derived from the request method, not just from an instance flag: if the
        /// component is re-created for the save POST (as it is when the host instantiates it per
        /// request), an instance flag would be <see langword="false"/> again and
        /// <c>LoadSettings</c> would repopulate every field from the database before
        /// <c>UpdateSettings</c> reads it — silently writing the old values back. Testing the
        /// request method reproduces <c>IsPostBack</c> in both hosting modes; the
        /// <c>settingsLoaded</c> flag additionally keeps the load once-only within a persistent
        /// circuit.
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

                    this.BindJournalTypes();

                    // Check for existing settings and use those on this page
                    if (this.Settings.ContainsKey(Constants.DefaultPageSize))
                    {
                        this.PageSizeSelectedIndex = IndexOfValue(this.pageSizeItems, this.Settings[Constants.DefaultPageSize].ToString());
                    }
                    else
                    {
                        this.PageSizeSelectedIndex = IndexOfValue(this.pageSizeItems, "20");
                    }

                    if (this.Settings.ContainsKey(Constants.MaxCharacters))
                    {
                        this.MessageLengthSelectedIndex = IndexOfValue(this.messageLengthItems, this.Settings[Constants.MaxCharacters].ToString());
                    }
                    else
                    {
                        this.MessageLengthSelectedIndex = IndexOfValue(this.messageLengthItems, "250");
                    }

                    if (this.Settings.ContainsKey(Constants.AllowFiles))
                    {
                        this.AllowFilesChecked = Convert.ToBoolean(this.Settings[Constants.AllowFiles].ToString());
                    }
                    else
                    {
                        this.AllowFilesChecked = true;
                    }

                    if (this.Settings.ContainsKey(Constants.AllowPhotos))
                    {
                        this.AllowPhotosChecked = Convert.ToBoolean(this.Settings[Constants.AllowPhotos].ToString());
                    }
                    else
                    {
                        this.AllowPhotosChecked = true;
                    }

                    if (this.Settings.ContainsKey(Constants.AllowResizePhotos))
                    {
                        this.AllowResizeChecked = Convert.ToBoolean(this.Settings[Constants.AllowResizePhotos].ToString());
                    }
                    else
                    {
                        this.AllowResizeChecked = false;
                    }

                    if (this.Settings.ContainsKey(Constants.JournalEditorEnabled))
                    {
                        this.EnableEditorChecked = Convert.ToBoolean(this.Settings[Constants.JournalEditorEnabled].ToString());
                    }
                    else
                    {
                        this.EnableEditorChecked = true;
                    }

                    if (!this.EnableEditorChecked)
                    {
                        this.AllowFilesEnabled = false;
                        this.AllowPhotosEnabled = false;
                    }

                    this.AllowResizeEnabled = this.EnableEditorChecked && this.AllowPhotosChecked;

                    foreach (var li in this.journalFilterItems)
                    {
                        li.Selected = true;
                    }

                    if (this.Settings.ContainsKey(Constants.JournalFilters))
                    {
                        if (string.IsNullOrEmpty(this.Settings[Constants.JournalFilters].ToString()))
                        {
                            foreach (var li in this.journalFilterItems)
                            {
                                li.Selected = true;
                            }
                        }
                        else
                        {
                            foreach (var li in this.journalFilterItems)
                            {
                                li.Selected = false;
                            }

                            foreach (string s in this.Settings[Constants.JournalFilters].ToString().Split(';'))
                            {
                                foreach (var li in this.journalFilterItems)
                                {
                                    if (li.Value == s)
                                    {
                                        li.Selected = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>UpdateSettings saves the modified settings to the Database.</summary>
        public override void UpdateSettings()
        {
            try
            {
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, Constants.DefaultPageSize, this.PageSizeSelectedItem.Value);
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, Constants.AllowFiles, this.AllowFilesChecked.ToString());
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, Constants.AllowPhotos, this.AllowPhotosChecked.ToString());
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, Constants.AllowResizePhotos, this.AllowResizeChecked.ToString());
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, Constants.JournalEditorEnabled, this.EnableEditorChecked.ToString());
                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, Constants.MaxCharacters, this.MessageLengthSelectedItem.Value);
                string journalTypes = string.Empty;
                bool allTypes = true;
                foreach (var li in this.journalFilterItems)
                {
                    if (!li.Selected)
                    {
                        allTypes = false;
                    }
                }

                var jc = InternalJournalController.Instance;
                jc.DeleteFilters(this.PortalId, this.ModuleId);

                foreach (var li in this.journalFilterItems)
                {
                    if (li.Selected)
                    {
                        if (!allTypes)
                        {
                            jc.SaveFilters(this.PortalId, this.ModuleId, Convert.ToInt32(li.Value));
                            journalTypes += li.Value + ";";
                        }
                    }
                }

                ModuleController.Instance.UpdateModuleSetting(this.ModuleId, Constants.JournalFilters, journalTypes);
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Localizes the one drop down item that carried a <c>resourcekey</c> instead of inline
        /// text. In WebForms that was done by the page's control localization pass; a component
        /// localizes its own markup, and the <c>Init</c> phase is the first one that can.
        /// </remarks>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            var unlimited = this.messageLengthItems[this.messageLengthItems.Count - 1];
            if (string.IsNullOrEmpty(unlimited.Text))
            {
                unlimited.Text = this.LocalizeText("MessageLength_Unlimited");
            }
        }

        /// <summary>Applies the "enable editor" check box's effect on the dependent check boxes.</summary>
        /// <remarks>
        /// Replaces the first <c>$('#&lt;%=chkEnableEditor.ClientID %&gt;').click(...)</c> handler
        /// of the inline script: enabling the editor re-enables the file and photo check boxes (and
        /// the resize check box when photos are checked), disabling it disables all three.
        /// </remarks>
        protected void OnEnableEditorChanged()
        {
            if (this.EnableEditorChecked)
            {
                this.AllowFilesEnabled = true;
                this.AllowPhotosEnabled = true;
                if (this.AllowPhotosChecked)
                {
                    this.AllowResizeEnabled = true;
                }
            }
            else
            {
                this.AllowFilesEnabled = false;
                this.AllowPhotosEnabled = false;
                this.AllowResizeEnabled = false;
            }
        }

        /// <summary>Applies the "allow photos" check box's effect on the resize check box.</summary>
        /// <remarks>
        /// Replaces the second <c>$('#&lt;%=chkAllowPhotos.ClientID %&gt;').click(...)</c> handler
        /// of the inline script.
        /// </remarks>
        protected void OnAllowPhotosChanged()
        {
            this.AllowResizeEnabled = this.AllowPhotosChecked;
        }

        /// <summary>Gets the index of the "default page size" item with the given value.</summary>
        /// <param name="value">The posted value of the selected option.</param>
        /// <returns>The index of the item, or -1 when there is none.</returns>
        /// <remarks>Replaces the <c>SelectedIndex</c> the WebForms post back resolved for the drop down.</remarks>
        protected int IndexOfPageSizeValue(object value) => IndexOfValue(this.pageSizeItems, value?.ToString());

        /// <summary>Gets the index of the "max message length" item with the given value.</summary>
        /// <param name="value">The posted value of the selected option.</param>
        /// <returns>The index of the item, or -1 when there is none.</returns>
        /// <remarks>Replaces the <c>SelectedIndex</c> the WebForms post back resolved for the drop down.</remarks>
        protected int IndexOfMessageLengthValue(object value) => IndexOfValue(this.messageLengthItems, value?.ToString());

        private static int IndexOfValue(IList<SettingsListItem> items, string value)
        {
            // This is Items.IndexOf(Items.FindByValue(value)): -1 when there is no such item,
            // which left the drop down with nothing selected.
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Value == value)
                {
                    return i;
                }
            }

            return -1;
        }

        private void BindJournalTypes()
        {
            foreach (JournalTypeInfo journalTypeInfo in JournalController.Instance.GetJournalTypes(this.PortalId))
            {
                this.journalFilterItems.Add(new SettingsListItem(
                    journalTypeInfo.JournalTypeId.ToString(CultureInfo.InvariantCulture),
                    Localization.GetString(journalTypeInfo.JournalType, Constants.SharedResourcesPath)));
            }
        }

        /// <summary>An item of one of the setting lists.</summary>
        /// <remarks>Replaces <c>System.Web.UI.WebControls.ListItem</c>, with the same three members the code behind used.</remarks>
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
        }
    }
}
