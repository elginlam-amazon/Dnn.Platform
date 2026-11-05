// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable once CheckNamespace
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Icons;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>The PropertyEditorControl control provides a way to display and edit any properties of any Info class.</summary>
    /// <remarks>
    /// <para>
    /// <c>CreateEditor</c> still builds the editor, one <see cref="FieldEditorControl"/> per visible
    /// property, in exactly the same order and with exactly the same grouping; it just builds an
    /// object graph of <see cref="PropertyEditorTable"/>/<see cref="PropertyEditorPanel"/> instead of
    /// a Web Forms control tree, and <see cref="RenderContent"/> renders that graph.
    /// </para>
    /// <para>
    /// The <c>Table</c>/<c>Panel</c> parameters of the <c>AddEditorRow</c> and <c>AddHeader</c>
    /// overloads became <see cref="PropertyEditorTable"/>/<see cref="PropertyEditorPanel"/>. That is a
    /// signature change, but the overload set is unchanged, so the overrides in
    /// <see cref="CollectionEditorControl"/>, <see cref="SettingsEditorControl"/> and third-party
    /// editors map one-to-one.
    /// </para>
    /// </remarks>
    public class PropertyEditorControl : PropertyEditorComponentBase
    {
        private readonly IServiceProvider serviceProvider;
        private readonly List<object> content = new List<object>();
        private bool itemChanged;
        private Hashtable sections;

        /// <summary>Initializes a new instance of the <see cref="PropertyEditorControl"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.0. Please use overload with IServiceProvider. Scheduled removal in v12.0.0.")]
        public PropertyEditorControl()
            : this(Globals.GetCurrentServiceProvider())
        {
        }

        /// <summary>Initializes a new instance of the <see cref="PropertyEditorControl"/> class.</summary>
        /// <param name="serviceProvider">The DI container.</param>
        public PropertyEditorControl(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            this.Fields = new ArrayList();
            this.ShowRequired = true;
            this.LabelMode = LabelMode.Left;
            this.HelpDisplayMode = HelpDisplayMode.Always;
            this.Groups = Null.NullString;
            this.AutoGenerate = true;
        }

        /// <summary>Occurs when an item is added to a collection type property.</summary>
        public event PropertyChangedEventHandler ItemAdded;

        /// <summary>Occurs when an editor was created.</summary>
        public event EditorCreatedEventHandler ItemCreated;

        /// <summary>Occurs when an item is removed from a collection type property.</summary>
        public event PropertyChangedEventHandler ItemDeleted;

        /// <summary>Gets a value indicating whether any of the properties have been changed.</summary>
        [Browsable(false)]
        public bool IsDirty
        {
            get
            {
                return this.Fields.Cast<FieldEditorControl>().Any(editor => editor.Visible && editor.IsDirty);
            }
        }

        /// <summary>Gets a value indicating whether all of the properties are Valid.</summary>
        [Browsable(false)]
        public bool IsValid
        {
            get
            {
                return this.Fields.Cast<FieldEditorControl>().All(editor => !editor.Visible || editor.IsValid);
            }
        }

        /// <summary>Gets or sets a value indicating whether the editor Autogenerates its editors.</summary>
        [Category("Behavior")]
        [Parameter]
        public bool AutoGenerate { get; set; }

        /// <summary>Gets or sets the DataSource that is bound to this control.</summary>
        [Browsable(false)]
        [Category("Data")]
        [Parameter]
        public object DataSource { get; set; }

        /// <summary>Gets or sets the Edit Mode of the Editor.</summary>
        [Category("Appearance")]
        [Parameter]
        public PropertyEditorMode EditMode { get; set; }

        /// <summary>Gets or sets whether the editors render in a div or in a table.</summary>
        [Parameter]
        public EditorDisplayMode DisplayMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Validators should use client-side
        /// validation.
        /// </summary>
        [Category("Behavior")]
        [Parameter]
        public bool EnableClientValidation { get; set; }

        /// <summary>Gets or sets the grouping mode.</summary>
        [Category("Appearance")]
        [Parameter]
        public GroupByMode GroupByMode { get; set; }

        /// <summary>Gets or sets the grouping order.</summary>
        /// <value>A comma-delimited list of categories/groups.</value>
        [Category("Appearance")]
        [Parameter]
        public string Groups { get; set; }

        /// <summary>Gets or sets whether the control displays Help.</summary>
        [Parameter]
        public HelpDisplayMode HelpDisplayMode { get; set; }

        /// <summary>Gets or sets the label mode.</summary>
        [Parameter]
        public LabelMode LabelMode { get; set; }

        /// <summary>Gets or sets the Local Resource File for the Control.</summary>
        [Parameter]
        public string LocalResourceFile { get; set; }

        /// <summary>Gets or sets the Url of the Required Image.</summary>
        [Parameter]
        public string RequiredUrl { get; set; }

        /// <summary>Gets or sets a value indicating whether the Required icon is used.</summary>
        [Parameter]
        public bool ShowRequired { get; set; }

        /// <summary>Gets or sets a value indicating whether the Visibility control is used.</summary>
        [Category("Appearance")]
        [Parameter]
        public bool ShowVisibility { get; set; }

        /// <summary>Gets or sets whether to sort properties.</summary>
        /// <value>The Sort Mode of the editor.</value>
        /// <remarks>
        /// By default all properties will be sorted.
        /// </remarks>
        [Category("Appearance")]
        [Parameter]
        public PropertySortType SortMode { get; set; }

        /// <summary>Gets or sets the user whose properties are being edited.</summary>
        [Parameter]
        public UserInfo User { get; set; }

        /// <summary>
        /// Gets a collection of fields to display if AutoGenerate is false. Or the
        /// collection of fields generated if AutoGenerate is true.
        /// </summary>
        /// <value>A collection of FieldEditorControl objects.</value>
        [Category("Behavior")]
        public ArrayList Fields { get; private set; }

        /// <summary>Gets the value of the Edit Control Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Edit Control.")]
        public Style EditControlStyle { get; } = new Style();

        /// <summary>Gets or sets the width of the Edit Control Column.</summary>
        [Browsable(true)]
        [Category("Appearance")]
        [Description("Set the Width for the Edit Control.")]
        public Unit EditControlWidth { get; set; }

        /// <summary>Gets the value of the Error Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Error Text.")]
        public Style ErrorStyle { get; } = new Style();

        /// <summary>Gets the value of the Group Header Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Group Header Control.")]
        public Style GroupHeaderStyle { get; } = new Style();

        /// <summary>Gets or sets a value indicating whether to add a &lt;hr&gt; to the Group Header.</summary>
        [Browsable(true)]
        [Category("Appearance")]
        [Description("Set whether to include a rule <hr> in the Group Header.")]
        [Parameter]
        public bool GroupHeaderIncludeRule { get; set; }

        /// <summary>Gets the value of the Help Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Help Text.")]
        public Style HelpStyle { get; } = new Style();

        /// <summary>Gets the value of the Item Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Label Text")]
        public Style ItemStyle { get; } = new Style();

        /// <summary>Gets the value of the Label Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Label Text")]
        public Style LabelStyle { get; } = new Style();

        /// <summary>Gets or sets the width of the Label Column.</summary>
        [Browsable(true)]
        [Category("Appearance")]
        [Description("Set the Width for the Label Control.")]
        public Unit LabelWidth { get; set; }

        /// <summary>Gets the value of the Visibility Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Visibility Control")]
        public Style VisibilityStyle { get; } = new Style();

        /// <summary>Gets the Underlying DataSource.</summary>
        /// <value>An IEnumerable Boolean.</value>
        protected virtual IEnumerable UnderlyingDataSource
        {
            get { return this.GetProperties(); }
        }

        /// <summary>Gets the content built by <see cref="CreateEditor"/>.</summary>
        /// <remarks>Each entry is a <see cref="PropertyEditorTable"/>, a
        /// <see cref="PropertyEditorPanel"/> or a <see cref="PropertyEditorSectionHeader"/>; it
        /// replaces the <c>Controls</c> collection.</remarks>
        protected IList<object> Content => this.content;

        /// <summary>Binds the controls to the DataSource.</summary>
        public override void DataBind()
        {
            // Invoke OnDataBinding so DataBinding Event is raised
            this.OnDataBinding(EventArgs.Empty);

            // Clear Existing Content
            this.content.Clear();

            // Create the Editor
            this.CreateEditor();
        }

        /// <summary>
        /// AddEditorRow builds a single editor row and adds it to the Table, using the
        /// specified adapter.
        /// </summary>
        /// <param name="table">The Table to add the row to.</param>
        /// <param name="name">The name of property being added.</param>
        /// <param name="adapter">An IEditorInfoAdapter.</param>
        protected void AddEditorRow(PropertyEditorTable table, string name, IEditorInfoAdapter adapter)
        {
            ArgumentNullException.ThrowIfNull(table);

            // Create a FieldEditor for this Row
            var editor = this.CreateFieldEditor(name, adapter);
            this.ConfigureEditor(editor);
            table.Rows.Add(editor);

            this.Fields.Add(editor);
        }

        /// <summary>
        /// AddEditorRow builds a single editor and adds it to the container, using the
        /// specified adapter.
        /// </summary>
        /// <param name="container">The container to add the editor to.</param>
        /// <param name="name">The name of property being added.</param>
        /// <param name="adapter">An IEditorInfoAdapter.</param>
        protected void AddEditorRow(PropertyEditorPanel container, string name, IEditorInfoAdapter adapter)
        {
            ArgumentNullException.ThrowIfNull(container);

            var editor = this.CreateFieldEditor(name, adapter);
            this.ConfigureEditor(editor);
            container.Editors.Add(editor);

            this.Fields.Add(editor);
        }

        /// <summary>AddEditorRow builds a single editor row and adds it to the Table.</summary>
        /// <remarks>This method is protected so that classes that inherit from
        /// PropertyEditor can modify how the Row is displayed.</remarks>
        /// <param name="table">The Table to add the row to.</param>
        /// <param name="obj">Row Data Info.</param>
        protected virtual void AddEditorRow(PropertyEditorTable table, object obj)
        {
            var objProperty = (PropertyInfo)obj;
            this.AddEditorRow(table, objProperty.Name, new StandardEditorInfoAdapter(this.DataSource, objProperty.Name));
        }

        /// <summary>AddEditorRow builds a single editor and adds it to the container.</summary>
        /// <param name="container">The container to add the editor to.</param>
        /// <param name="obj">Row Data Info.</param>
        protected virtual void AddEditorRow(PropertyEditorPanel container, object obj)
        {
            var objProperty = (PropertyInfo)obj;
            this.AddEditorRow(container, objProperty.Name, new StandardEditorInfoAdapter(this.DataSource, objProperty.Name));
        }

        /// <summary>AddEditorRow builds a single editor and adds it directly to this control.</summary>
        /// <param name="obj">Row Data Info.</param>
        protected virtual void AddEditorRow(object obj)
        {
            var objProperty = (PropertyInfo)obj;
            this.AddEditorRow(this.GetRootPanel(), objProperty.Name, new StandardEditorInfoAdapter(this.DataSource, objProperty.Name));
        }

        /// <summary>AddFields adds the fields that have been defined in design mode (Autogenerate=false).</summary>
        protected virtual void AddFields()
        {
            var container = this.GetRootPanel();
            foreach (FieldEditorControl editor in this.Fields)
            {
                this.PrepareDeclaredEditor(editor);
                this.ConfigureEditor(editor);
                container.Editors.Add(editor);
            }
        }

        /// <summary>AddFields adds the fields that have been defined in design mode (Autogenerate=false).</summary>
        /// <param name="tbl">The Table to add the rows to.</param>
        protected virtual void AddFields(PropertyEditorTable tbl)
        {
            ArgumentNullException.ThrowIfNull(tbl);

            foreach (FieldEditorControl editor in this.Fields)
            {
                this.PrepareDeclaredEditor(editor);
                this.ConfigureEditor(editor);
                tbl.Rows.Add(editor);
            }
        }

        /// <summary>AddHeader builds a group header.</summary>
        /// <remarks>This method is protected so that classes that inherit from
        /// PropertyEditor can modify how the Header is displayed.</remarks>
        /// <param name="tbl">The Table that contains the group.</param>
        /// <param name="header">Table Header.</param>
        protected virtual void AddHeader(PropertyEditorTable tbl, string header)
        {
            ArgumentNullException.ThrowIfNull(tbl);

            var sectionHeader = new PropertyEditorSectionHeader
            {
                Group = header,
                Text = header,
                ResourceKey = this.ID + "_" + header + ".Header",
                Style = this.GroupHeaderStyle,
                IncludeRule = this.GroupHeaderIncludeRule,
                IconId = "ico" + header,
            };

            tbl.Header = sectionHeader;

            // Get the Hashtable
            if (this.sections == null)
            {
                this.sections = new Hashtable();
            }

            this.sections[sectionHeader] = tbl;
        }

        /// <summary>CreateEditor creates the control collection.</summary>
        protected virtual void CreateEditor()
        {
            PropertyEditorTable table;
            string[] arrGroups = null;

            this.content.Clear();
            this.sections = null;
            if (!string.IsNullOrEmpty(this.Groups))
            {
                arrGroups = this.Groups.Split(',');
            }
            else if (this.GroupByMode != GroupByMode.None)
            {
                arrGroups = this.GetGroups(this.UnderlyingDataSource);
            }

            if (!this.AutoGenerate)
            {
                // Create a new table
                if (this.DisplayMode == EditorDisplayMode.Div)
                {
                    this.AddFields();
                }
                else
                {
                    // Add the Table to the Content
                    table = new PropertyEditorTable { ID = "tbl" };
                    this.AddFields(table);
                    this.content.Add(table);
                }
            }
            else
            {
                this.Fields.Clear();
                if (arrGroups is { Length: > 0 })
                {
                    foreach (string strGroup in arrGroups)
                    {
                        if (this.GroupByMode == GroupByMode.Section)
                        {
                            if (this.DisplayMode == EditorDisplayMode.Div)
                            {
                                var groupData = this.UnderlyingDataSource.Cast<object>().Where(obj => this.GetCategory(obj) == strGroup.Trim() && this.GetRowVisibility(obj));
                                if (groupData.Any())
                                {
                                    var localizedGroupName = Localization.GetString("ProfileProperties_" + strGroup + ".Header", this.LocalResourceFile);
                                    if (string.IsNullOrEmpty(localizedGroupName))
                                    {
                                        localizedGroupName = strGroup;
                                    }

                                    // fieldset to hold properties in group, preceded by its section head
                                    var container = new PropertyEditorPanel
                                    {
                                        WrapInFieldSet = true,
                                        Header = new PropertyEditorSectionHeader
                                        {
                                            Group = strGroup,
                                            Text = localizedGroupName,
                                            IsSectionHead = true,
                                        },
                                    };

                                    foreach (object obj in groupData)
                                    {
                                        this.AddEditorRow(container, obj);
                                    }

                                    this.content.Add(container);
                                }
                            }
                            else
                            {
                                // Create a new table
                                table = new PropertyEditorTable { ID = "tbl" + strGroup };
                                foreach (object obj in this.UnderlyingDataSource)
                                {
                                    if (this.GetCategory(obj) == strGroup.Trim())
                                    {
                                        // Add the Editor Row to the Table
                                        if (this.GetRowVisibility(obj))
                                        {
                                            if (table.Rows.Count == 0)
                                            {
                                                // Add a Header
                                                this.AddHeader(table, strGroup);
                                            }

                                            this.AddEditorRow(table, obj);
                                        }
                                    }
                                }

                                // Add the Table to the Content (if it has any rows)
                                if (table.Rows.Count > 0)
                                {
                                    this.content.Add(table);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Create a new table
                    if (this.DisplayMode == EditorDisplayMode.Div)
                    {
                        foreach (object obj in this.UnderlyingDataSource)
                        {
                            // Add the Editor Row to the Content
                            if (this.GetRowVisibility(obj))
                            {
                                this.AddEditorRow(obj);
                            }
                        }
                    }
                    else
                    {
                        table = new PropertyEditorTable { ID = "tbl" };
                        foreach (object obj in this.UnderlyingDataSource)
                        {
                            if (this.GetRowVisibility(obj))
                            {
                                this.AddEditorRow(table, obj);
                            }
                        }

                        // Add the Table to the Content
                        this.content.Add(table);
                    }
                }
            }
        }

        /// <summary>GetCategory gets the Category of an object.</summary>
        /// <param name="obj">The <see cref="PropertyInfo"/>.</param>
        /// <returns>The category name, or <see cref="Null.NullString"/>.</returns>
        protected virtual string GetCategory(object obj)
        {
            var objProperty = (PropertyInfo)obj;
            var categoryString = Null.NullString;

            // Get Category Field
            var categoryAttributes = objProperty.GetCustomAttributes(typeof(CategoryAttribute), true);
            if (categoryAttributes.Length > 0)
            {
                var category = (CategoryAttribute)categoryAttributes[0];
                categoryString = category.Category;
            }

            return categoryString;
        }

        /// <summary>GetGroups gets an array of Groups/Categories from the DataSource.</summary>
        /// <param name="arrObjects">The <see cref="PropertyInfo"/> instances.</param>
        /// <returns>An array of group/category names.</returns>
        protected virtual string[] GetGroups(IEnumerable arrObjects)
        {
            var arrGroups = new ArrayList();

            foreach (PropertyInfo objProperty in arrObjects)
            {
                var categoryAttributes = objProperty.GetCustomAttributes(typeof(CategoryAttribute), true);
                if (categoryAttributes.Length > 0)
                {
                    var category = (CategoryAttribute)categoryAttributes[0];

                    if (!arrGroups.Contains(category.Category))
                    {
                        arrGroups.Add(category.Category);
                    }
                }
            }

            var strGroups = new string[arrGroups.Count];
            for (int i = 0; i <= arrGroups.Count - 1; i++)
            {
                strGroups[i] = Convert.ToString(arrGroups[i], CultureInfo.InvariantCulture);
            }

            return strGroups;
        }

        /// <summary>GetRowVisibility determines the Visibility of a row in the table.</summary>
        /// <param name="obj">The property.</param>
        /// <returns><see langword="true"/> if the row is visible, otherwise <see langword="false"/>.</returns>
        protected virtual bool GetRowVisibility(object obj)
        {
            var objProperty = (PropertyInfo)obj;

            bool isVisible = true;
            object[] browsableAttributes = objProperty.GetCustomAttributes(typeof(BrowsableAttribute), true);
            if (browsableAttributes.Length > 0)
            {
                var browsable = (BrowsableAttribute)browsableAttributes[0];
                if (!browsable.Browsable)
                {
                    isVisible = false;
                }
            }

            if (!isVisible && this.EditMode == PropertyEditorMode.Edit)
            {
                // Check if property is required - as this will need to override visibility
                object[] requiredAttributes = objProperty.GetCustomAttributes(typeof(RequiredAttribute), true);
                if (requiredAttributes.Length > 0)
                {
                    var required = (RequiredAttribute)requiredAttributes[0];
                    if (required.Required)
                    {
                        isVisible = true;
                    }
                }
            }

            return isVisible;
        }

        /// <summary>Runs when an item is added to a collection type property.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnItemAdded(PropertyEditorEventArgs e)
        {
            this.ItemAdded?.Invoke(this, e);
        }

        /// <summary>Runs when an Editor is Created.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnItemCreated(PropertyEditorItemEventArgs e)
        {
            this.ItemCreated?.Invoke(this, e);
        }

        /// <summary>Runs when an item is removed from a collection type property.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnItemDeleted(PropertyEditorEventArgs e)
        {
            this.ItemDeleted?.Invoke(this, e);
        }

        /// <summary>Runs just before the control is rendered.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnPreRender(EventArgs e)
        {
            if (this.itemChanged)
            {
                // Rebind the control to the DataSource to make sure that the dependent
                // editors are updated
                this.itemChanged = false;
                this.DataBind();
            }

            if (string.IsNullOrEmpty(this.CssClass))
            {
                this.CssClass = "dnnForm";
            }

            // Find the Min/Max buttons
            if (this.GroupByMode == GroupByMode.Section && (this.sections != null))
            {
                foreach (DictionaryEntry key in this.sections)
                {
                    var tbl = (PropertyEditorTable)key.Value;
                    var header = (PropertyEditorSectionHeader)key.Key;
                    DNNClientAPI.EnableMinMax(
                        new PropertyEditorClientApiControl(header.IconId),
                        new PropertyEditorClientApiControl(tbl.ID),
                        false,
                        IconController.IconURL("Minus", "12X15"),
                        IconController.IconURL("Plus", "12X15"),
                        DNNClientAPI.MinMaxPersistanceType.Page);
                }
            }

            base.OnPreRender(e);
        }

        /// <summary>Runs when an item is added to a collection type property.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        protected virtual void CollectionItemAdded(object sender, PropertyEditorEventArgs e)
        {
            this.OnItemAdded(e);
        }

        /// <summary>Runs when an item is removed from a collection type property.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        protected virtual void CollectionItemDeleted(object sender, PropertyEditorEventArgs e)
        {
            this.OnItemDeleted(e);
        }

        /// <summary>Runs when an Editor Is Created.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        protected virtual void EditorItemCreated(object sender, PropertyEditorItemEventArgs e)
        {
            this.OnItemCreated(e);
        }

        /// <summary>Runs when an Item in the List Is Changed.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        protected virtual void ListItemChanged(object sender, PropertyEditorEventArgs e)
        {
            this.itemChanged = true;
        }

        /// <inheritdoc />
        protected override void RenderContent(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (this.content.Count == 0 && this.DataSource != null)
            {
                // The editor is created by DataBind(); create it on demand so that the control also
                // renders when it is used declaratively as a Blazor component.
                this.DataBind();
            }

            builder.OpenElement(0, "div");
            this.AddStyleAttributes(builder, 1);

            builder.OpenRegion(10);
            foreach (var item in this.content)
            {
                switch (item)
                {
                    case PropertyEditorTable table:
                        this.RenderTable(builder, table);
                        break;
                    case PropertyEditorPanel panel:
                        this.RenderPanel(builder, panel);
                        break;
                    case PropertyEditorSectionHeader header:
                        this.RenderHeader(builder, header);
                        break;
                }
            }

            builder.CloseRegion();

            builder.CloseElement();
        }

        /// <summary>Gets (creating it when needed) the panel that holds editors added directly to this control.</summary>
        /// <returns>The root panel.</returns>
        protected PropertyEditorPanel GetRootPanel()
        {
            var panel = this.content.OfType<PropertyEditorPanel>().FirstOrDefault(p => p.ID == "root");
            if (panel == null)
            {
                panel = new PropertyEditorPanel { ID = "root" };
                this.content.Add(panel);
            }

            return panel;
        }

        private void RenderTable(RenderTreeBuilder builder, PropertyEditorTable table)
        {
            builder.OpenRegion(table.ID?.GetHashCode(StringComparison.Ordinal) ?? 0);

            if (table.Header != null)
            {
                this.RenderHeader(builder, table.Header);
            }

            builder.OpenElement(0, "table");
            builder.AddAttribute(1, "id", table.ID);

            builder.OpenRegion(2);
            foreach (var editor in table.Rows)
            {
                builder.OpenElement(0, "tr");
                builder.OpenElement(1, "td");
                builder.AddContent(2, editor.Render());
                builder.CloseElement();
                builder.CloseElement();
            }

            builder.CloseRegion();
            builder.CloseElement();

            builder.CloseRegion();
        }

        private void RenderPanel(RenderTreeBuilder builder, PropertyEditorPanel panel)
        {
            builder.OpenRegion(panel.ID?.GetHashCode(StringComparison.Ordinal) ?? 1);

            if (panel.Header != null)
            {
                this.RenderHeader(builder, panel.Header);
            }

            if (panel.WrapInFieldSet)
            {
                builder.OpenElement(0, "fieldset");
            }

            builder.OpenElement(1, "div");
            builder.AddAttribute(2, "id", panel.ID);
            if (!string.IsNullOrEmpty(panel.CssClass))
            {
                builder.AddAttribute(3, "class", panel.CssClass);
            }

            builder.OpenRegion(4);
            foreach (var editor in panel.Editors)
            {
                builder.AddContent(0, editor.Render());
            }

            builder.CloseRegion();
            builder.CloseElement();

            if (panel.WrapInFieldSet)
            {
                builder.CloseElement();
            }

            builder.CloseRegion();
        }

        private void RenderHeader(RenderTreeBuilder builder, PropertyEditorSectionHeader header)
        {
            if (header.IsSectionHead)
            {
                builder.OpenElement(0, "h2");
                builder.AddAttribute(1, "class", "dnnFormSectionHead");
                builder.AddAttribute(2, "id", header.Group);
                builder.OpenElement(3, "a");
                builder.AddAttribute(4, "href", "#");
                builder.AddContent(5, header.Text);
                builder.CloseElement();
                builder.CloseElement();
                return;
            }

            builder.OpenElement(10, "div");

            // The minimise/maximise icon DNNClientAPI.EnableMinMax binds on.
            builder.OpenElement(11, "img");
            builder.AddAttribute(12, "id", header.IconId);
            builder.AddAttribute(13, "alt", string.Empty);
            builder.AddAttribute(14, "src", IconController.IconURL("Minus", "12X15"));
            builder.CloseElement();

            builder.AddContent(15, " ");

            builder.OpenElement(16, "span");
            builder.AddAttribute(17, "id", "lbl" + header.Group);
            if (!string.IsNullOrEmpty(header.Style?.CssClass))
            {
                builder.AddAttribute(18, "class", header.Style.CssClass);
            }

            if (!string.IsNullOrEmpty(header.ResourceKey))
            {
                builder.AddAttribute(19, "resourcekey", header.ResourceKey);
            }

            builder.AddContent(20, header.Text);
            builder.CloseElement();

            if (header.IncludeRule)
            {
                builder.AddMarkupContent(21, "<hr noshade=\"noshade\" size=\"1\"/>");
            }

            builder.CloseElement();
        }

        private FieldEditorControl CreateFieldEditor(string name, IEditorInfoAdapter adapter)
        {
            return new FieldEditorControl(this.serviceProvider)
            {
                DataSource = this.DataSource,
                EditorInfoAdapter = adapter,
                DataField = name,
                EditorDisplayMode = this.DisplayMode,
                EnableClientValidation = this.EnableClientValidation,
                EditMode = this.EditMode,
                HelpDisplayMode = this.HelpDisplayMode,
                LabelMode = this.LabelMode,
                LabelWidth = this.LabelWidth,
            };
        }

        private void PrepareDeclaredEditor(FieldEditorControl editor)
        {
            editor.DataSource = this.DataSource;
            editor.EditorInfoAdapter = new StandardEditorInfoAdapter(this.DataSource, editor.DataField);
            editor.EditorDisplayMode = this.DisplayMode;
            editor.EnableClientValidation = this.EnableClientValidation;
            if (editor.EditMode != PropertyEditorMode.View)
            {
                editor.EditMode = this.EditMode;
            }

            editor.HelpDisplayMode = this.HelpDisplayMode;
            if (editor.LabelMode == LabelMode.None)
            {
                editor.LabelMode = this.LabelMode;
            }

            if (editor.LabelWidth == Unit.Empty)
            {
                editor.LabelWidth = this.LabelWidth;
            }
        }

        private void ConfigureEditor(FieldEditorControl editor)
        {
            editor.ControlStyle.CopyFrom(this.ItemStyle);
            editor.LabelStyle.CopyFrom(this.LabelStyle);
            editor.HelpStyle.CopyFrom(this.HelpStyle);
            editor.ErrorStyle.CopyFrom(this.ErrorStyle);
            editor.VisibilityStyle.CopyFrom(this.VisibilityStyle);
            editor.EditControlStyle.CopyFrom(this.EditControlStyle);
            if (editor.EditControlWidth == Unit.Empty)
            {
                editor.EditControlWidth = this.EditControlWidth;
            }

            editor.LocalResourceFile = this.LocalResourceFile;
            editor.RequiredUrl = this.RequiredUrl;
            editor.ShowRequired = this.ShowRequired;
            editor.ShowVisibility = this.ShowVisibility;
            editor.User = this.User;
            editor.Width = this.Width;
            editor.ItemAdded += this.CollectionItemAdded;
            editor.ItemChanged += this.ListItemChanged;
            editor.ItemCreated += this.EditorItemCreated;
            editor.ItemDeleted += this.CollectionItemDeleted;

            editor.DataBind();
        }

        /// <summary>GetProperties returns an array of <see cref="System.Reflection.PropertyInfo">PropertyInfo</see>.</summary>
        /// <returns>An array of <see cref="System.Reflection.PropertyInfo">PropertyInfo</see> objects
        /// for the current DataSource object.</returns>
        /// <remarks>
        /// GetProperties will return an array of public properties for the current DataSource
        /// object.  The properties will be sorted according to the SortMode property.
        /// </remarks>
        private PropertyInfo[] GetProperties()
        {
            if (this.DataSource != null)
            {
                const BindingFlags bindings = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

                var properties = this.DataSource.GetType().GetProperties(bindings);

                // Apply sort method
                switch (this.SortMode)
                {
                    case PropertySortType.Alphabetical:
                        Array.Sort(properties, new PropertyNameComparer());
                        break;
                    case PropertySortType.Category:
                        Array.Sort(properties, new PropertyCategoryComparer());
                        break;
                    case PropertySortType.SortOrderAttribute:
                        Array.Sort(properties, new PropertySortOrderComparer());
                        break;
                }

                return properties;
            }

            return null;
        }
    }
}
