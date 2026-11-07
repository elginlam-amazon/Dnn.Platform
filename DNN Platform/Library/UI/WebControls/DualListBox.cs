// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>Two list boxes with buttons to move items between them.</summary>
    /// <remarks>
    /// Was a <c>WebControl</c> implementing <c>IPostBackEventHandler</c>/<c>IPostBackDataHandler</c>
    /// that rendered itself onto an <c>HtmlTextWriter</c>. It is now a
    /// <see cref="DnnComponentBase"/> that renders the same table through the render tree. The
    /// post-back protocol collapses into direct event handling: the multiple selects report their
    /// selection through <c>onchange</c> (which is what <c>LoadPostData</c> used to read out of the
    /// post collection) and the buttons call <see cref="RaisePostBackEvent"/> on click, which still
    /// raises the same four events with the same arguments.
    /// </remarks>
    public class DualListBox : DnnComponentBase
    {
        private readonly Style availableListBoxStyle = new Style();
        private readonly Style buttonStyle = new Style();
        private readonly TableItemStyle containerStyle = new TableItemStyle();
        private readonly Style headerStyle = new Style();
        private readonly Style selectedListBoxStyle = new Style();
        private List<string> addValues;
        private List<string> removeValues;

        /// <summary>Initializes a new instance of the <see cref="DualListBox"/> class.</summary>
        public DualListBox()
        {
            this.ShowAddButton = true;
            this.ShowAddAllButton = true;
            this.ShowRemoveButton = true;
            this.ShowRemoveAllButton = true;
        }

        /// <summary>Occurs when the user moves the selected available items to the selected list.</summary>
        public event DualListBoxEventHandler AddButtonClick;

        /// <summary>Occurs when the user moves every available item to the selected list.</summary>
        public event EventHandler AddAllButtonClick;

        /// <summary>Occurs when the user removes the selected items from the selected list.</summary>
        public event DualListBoxEventHandler RemoveButtonClick;

        /// <summary>Occurs when the user removes every item from the selected list.</summary>
        public event EventHandler RemoveAllButtonClick;

        /// <summary>Gets the value of the Available List Box Style.</summary>
        /// <value>A Style object.</value>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Available List Box.")]
        public Style AvailableListBoxStyle
        {
            get
            {
                return this.availableListBoxStyle;
            }
        }

        /// <summary>Gets the value of the Button Style.</summary>
        /// <value>A Style object.</value>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Button.")]
        public Style ButtonStyle
        {
            get
            {
                return this.buttonStyle;
            }
        }

        /// <summary>Gets the value of the Container Style.</summary>
        /// <value>A Style object.</value>
        /// <remarks>Was a <c>TableStyle</c>, which has no ASP.NET Core equivalent.</remarks>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Container.")]
        public TableItemStyle ContainerStyle
        {
            get
            {
                return this.containerStyle;
            }
        }

        /// <summary>Gets the value of the Header Style.</summary>
        /// <value>A Style object.</value>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Header.")]
        public Style HeaderStyle
        {
            get
            {
                return this.headerStyle;
            }
        }

        /// <summary>Gets the value of the Selected List Box Style.</summary>
        /// <value>A Style object.</value>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Selected List Box.")]
        public Style SelectedListBoxStyle
        {
            get
            {
                return this.selectedListBoxStyle;
            }
        }

        /// <summary>Gets or sets the image of the "add all" button.</summary>
        [Parameter]
        public string AddAllImageURL { get; set; }

        /// <summary>Gets or sets the localization key of the "add all" button.</summary>
        [Parameter]
        public string AddAllKey { get; set; }

        /// <summary>Gets or sets the text of the "add all" button.</summary>
        [Parameter]
        public string AddAllText { get; set; }

        /// <summary>Gets or sets the image of the "add" button.</summary>
        [Parameter]
        public string AddImageURL { get; set; }

        /// <summary>Gets or sets the localization key of the "add" button.</summary>
        [Parameter]
        public string AddKey { get; set; }

        /// <summary>Gets or sets the text of the "add" button.</summary>
        [Parameter]
        public string AddText { get; set; }

        /// <summary>Gets or sets the items offered in the available list.</summary>
        [Parameter]
        public object AvailableDataSource { get; set; }

        /// <summary>Gets or sets the property of an item that provides its text.</summary>
        [Parameter]
        public string DataTextField { private get; set; }

        /// <summary>Gets or sets the property of an item that provides its value.</summary>
        [Parameter]
        public string DataValueField { private get; set; }

        /// <summary>Gets or sets the resource file used to localize the buttons and the headers.</summary>
        [Parameter]
        public string LocalResourceFile { get; set; }

        /// <summary>Gets or sets the image of the "remove all" button.</summary>
        [Parameter]
        public string RemoveAllImageURL { get; set; }

        /// <summary>Gets or sets the localization key of the "remove all" button.</summary>
        [Parameter]
        public string RemoveAllKey { get; set; }

        /// <summary>Gets or sets the text of the "remove all" button.</summary>
        [Parameter]
        public string RemoveAllText { get; set; }

        /// <summary>Gets or sets the image of the "remove" button.</summary>
        [Parameter]
        public string RemoveImageURL { get; set; }

        /// <summary>Gets or sets the localization key of the "remove" button.</summary>
        [Parameter]
        public string RemoveKey { get; set; }

        /// <summary>Gets or sets the text of the "remove" button.</summary>
        [Parameter]
        public string RemoveText { get; set; }

        /// <summary>Gets or sets the items shown in the selected list.</summary>
        [Parameter]
        public object SelectedDataSource { get; set; }

        /// <summary>Gets or sets a value indicating whether the "add" button renders.</summary>
        [Parameter]
        public bool ShowAddButton { get; set; }

        /// <summary>Gets or sets a value indicating whether the "add all" button renders.</summary>
        [Parameter]
        public bool ShowAddAllButton { get; set; }

        /// <summary>Gets or sets a value indicating whether the "remove" button renders.</summary>
        [Parameter]
        public bool ShowRemoveButton { get; set; }

        /// <summary>Gets or sets a value indicating whether the "remove all" button renders.</summary>
        [Parameter]
        public bool ShowRemoveAllButton { get; set; }

        /// <summary>Gets or sets a value indicating whether the buttons cause validation to occur.</summary>
        [Parameter]
        public bool CausesValidation { get; set; }

        /// <summary>Gets or sets the validation group the buttons validate.</summary>
        [Parameter]
        public string ValidationGroup { get; set; }

        /// <inheritdoc />
        protected override string TagName => "div";

        /// <summary>Reads the selections out of a post collection.</summary>
        /// <param name="postDataKey">A key to the PostBack Data to load.</param>
        /// <param name="postCollection">A name value collection of postback data.</param>
        /// <returns><see langword="true"/> when either selection was posted.</returns>
        /// <remarks>
        /// Replaces <c>IPostBackDataHandler.LoadPostData</c>. It is kept public because it is the
        /// documented way to feed this control from a classic form post; an interactive render
        /// feeds the very same fields through the selects' <c>onchange</c> instead.
        /// </remarks>
        public bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            bool retValue = Null.NullBoolean;
            string addItems = postCollection[postDataKey + "_Available"];
            if (!string.IsNullOrEmpty(addItems))
            {
                this.addValues = new List<string>();
                foreach (string addItem in addItems.Split(','))
                {
                    this.addValues.Add(addItem);
                }

                retValue = true;
            }

            string removeItems = postCollection[postDataKey + "_Selected"];
            if (!string.IsNullOrEmpty(removeItems))
            {
                this.removeValues = new List<string>();
                foreach (string removeItem in removeItems.Split(','))
                {
                    this.removeValues.Add(removeItem);
                }

                retValue = true;
            }

            return retValue;
        }

        /// <summary>Notifies this control that the posted selection changed.</summary>
        /// <remarks>Replaces <c>IPostBackDataHandler.RaisePostDataChangedEvent</c>.</remarks>
        public void RaisePostDataChangedEvent()
        {
        }

        /// <summary>Raises the event matching the clicked button.</summary>
        /// <param name="eventArgument">The button, which is <c>Add</c>, <c>AddAll</c>, <c>Remove</c> or <c>RemoveAll</c>.</param>
        /// <remarks>Replaces <c>IPostBackEventHandler.RaisePostBackEvent</c>.</remarks>
        public void RaisePostBackEvent(string eventArgument)
        {
            switch (eventArgument)
            {
                case "Add":
                    this.OnAddButtonClick(new DualListBoxEventArgs(this.addValues));
                    break;
                case "AddAll":
                    this.OnAddAllButtonClick(new EventArgs());
                    break;
                case "Remove":
                    this.OnRemoveButtonClick(new DualListBoxEventArgs(this.removeValues));
                    break;
                case "RemoveAll":
                    this.OnRemoveAllButtonClick(new EventArgs());
                    break;
            }
        }

        /// <summary>Raises the <see cref="AddButtonClick"/> event.</summary>
        /// <param name="e">The items to add.</param>
        protected void OnAddButtonClick(DualListBoxEventArgs e)
        {
            if (this.AddButtonClick != null)
            {
                this.AddButtonClick(this, e);
            }
        }

        /// <summary>Raises the <see cref="AddAllButtonClick"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected void OnAddAllButtonClick(EventArgs e)
        {
            if (this.AddAllButtonClick != null)
            {
                this.AddAllButtonClick(this, e);
            }
        }

        /// <summary>Raises the <see cref="RemoveButtonClick"/> event.</summary>
        /// <param name="e">The items to remove.</param>
        protected void OnRemoveButtonClick(DualListBoxEventArgs e)
        {
            if (this.RemoveButtonClick != null)
            {
                this.RemoveButtonClick(this, e);
            }
        }

        /// <summary>Raises the <see cref="RemoveAllButtonClick"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected void OnRemoveAllButtonClick(EventArgs e)
        {
            if (this.RemoveAllButtonClick != null)
            {
                this.RemoveAllButtonClick(this, e);
            }
        }

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // render table
            builder.OpenElement(sequence++, "table");
            if (this.ContainerStyle != null)
            {
                builder.AddMultipleAttributes(sequence++, this.ContainerStyle.GetAttributes());
            }

            // Render Header Row
            this.RenderHeader(builder, ref sequence);

            // Render ListBox row
            this.RenderListBoxes(builder, ref sequence);

            // Render end of table
            builder.CloseElement();

            base.RenderContents(builder, ref sequence);
        }

        private static IEnumerable<string> ReadSelection(ChangeEventArgs args)
        {
            switch (args?.Value)
            {
                case string[] values:
                    return values;
                case string value:
                    return value.Split(',');
                default:
                    return Enumerable.Empty<string>();
            }
        }

        private NameValueCollection GetList(string listType, object dataSource)
        {
            var dataList = dataSource as IEnumerable;
            var list = new NameValueCollection();
            if (dataList == null)
            {
                throw new ArgumentException("The " + listType + "DataSource must implement the IEnumerable Interface");
            }
            else
            {
                foreach (object item in dataList)
                {
                    const BindingFlags Bindings = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
                    PropertyInfo objTextProperty = item.GetType().GetProperty(this.DataTextField, Bindings);
                    PropertyInfo objValueProperty = item.GetType().GetProperty(this.DataValueField, Bindings);
                    string objValue = Convert.ToString(objValueProperty.GetValue(item, null), CultureInfo.InvariantCulture);
                    string objText = Convert.ToString(objTextProperty.GetValue(item, null), CultureInfo.InvariantCulture);

                    list.Add(objText, objValue);
                }
            }

            return list;
        }

        private void RenderButton(string buttonType, RenderTreeBuilder builder, ref int sequence)
        {
            string buttonText = Null.NullString;
            string imageUrl = Null.NullString;

            // Begin Button Row
            builder.OpenElement(sequence++, "tr");

            // Begin Button Cell
            builder.OpenElement(sequence++, "td");
            switch (buttonType)
            {
                case "Add":
                    buttonText = string.IsNullOrEmpty(this.AddKey)
                                    ? this.AddText
                                    : Localization.GetString(this.AddKey, this.LocalResourceFile);
                    imageUrl = this.AddImageURL;
                    break;
                case "AddAll":
                    buttonText = string.IsNullOrEmpty(this.AddAllKey)
                                    ? this.AddAllText
                                    : Localization.GetString(this.AddAllKey, this.LocalResourceFile);
                    imageUrl = this.AddAllImageURL;
                    break;
                case "Remove":
                    buttonText = string.IsNullOrEmpty(this.RemoveKey)
                                    ? this.RemoveText
                                    : Localization.GetString(this.RemoveKey, this.LocalResourceFile);
                    imageUrl = this.RemoveImageURL;
                    break;
                case "RemoveAll":
                    buttonText = string.IsNullOrEmpty(this.RemoveAllKey)
                                    ? this.RemoveAllText
                                    : Localization.GetString(this.RemoveAllKey, this.LocalResourceFile);
                    imageUrl = this.RemoveAllImageURL;
                    break;
            }

            // Render Hyperlink. The WebForms control rendered the __doPostBack script of
            // GetPostBackOptions into the href; the click is handled on the server directly now.
            var argument = buttonType;
            builder.OpenElement(sequence++, "a");
            builder.AddAttribute(sequence++, "href", "javascript:void(0);");
            builder.AddAttribute(sequence++, "title", buttonText);
            builder.AddAttribute(sequence++, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => this.RaisePostBackEvent(argument)));
            if (this.ButtonStyle != null)
            {
                this.ButtonStyle.AddAttributesToRender(builder, sequence);
                sequence += 2;
            }

            // Render Image
            if (!string.IsNullOrEmpty(imageUrl))
            {
                builder.OpenElement(sequence++, "img");
                builder.AddAttribute(sequence++, "src", Common.Globals.ResolveUrl(imageUrl));
                builder.AddAttribute(sequence++, "title", buttonText);
                builder.AddAttribute(sequence++, "border", "0");
                builder.AddAttribute(sequence++, "alt", buttonText);
                builder.CloseElement();
            }
            else
            {
                builder.AddContent(sequence++, buttonText);
            }

            // End of Hyperlink
            builder.CloseElement();

            // End of Button Cell
            builder.CloseElement();

            // Render end of Button Row
            builder.CloseElement();
        }

        private void RenderButtons(RenderTreeBuilder builder, ref int sequence)
        {
            // render table
            builder.OpenElement(sequence++, "table");

            if (this.ShowAddButton)
            {
                this.RenderButton("Add", builder, ref sequence);
            }

            if (this.ShowAddAllButton)
            {
                this.RenderButton("AddAll", builder, ref sequence);
            }

            // Begin Button Row
            builder.OpenElement(sequence++, "tr");

            // Begin Button Cell
            builder.OpenElement(sequence++, "td");

            builder.AddMarkupContent(sequence++, "&nbsp;");

            // End of Button Cell
            builder.CloseElement();

            // Render end of Button Row
            builder.CloseElement();

            if (this.ShowRemoveButton)
            {
                this.RenderButton("Remove", builder, ref sequence);
            }

            if (this.ShowRemoveAllButton)
            {
                this.RenderButton("RemoveAll", builder, ref sequence);
            }

            // Render end of table
            builder.CloseElement();
        }

        private void RenderListBox(string listType, object dataSource, Style style, RenderTreeBuilder builder, ref int sequence)
        {
            if (dataSource == null)
            {
                return;
            }

            NameValueCollection list = this.GetList(listType, dataSource);
            if (list == null)
            {
                return;
            }

            // Render ListBox
            builder.OpenElement(sequence++, "select");
            if (style != null)
            {
                style.AddAttributesToRender(builder, sequence);
                sequence += 2;
            }

            builder.AddAttribute(sequence++, "multiple", "multiple");
            builder.AddAttribute(sequence++, "name", this.UniqueId + "_" + listType);
            var isAvailable = listType == "Available";
            builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(
                this,
                args => this.SelectionChanged(isAvailable, args)));
            for (int i = 0; i <= list.Count - 1; i++)
            {
                // Render option tags for each item
                builder.OpenElement(sequence++, "option");
                builder.AddAttribute(sequence++, "value", list.Get(i));
                builder.AddContent(sequence++, list.GetKey(i));
                builder.CloseElement();
            }

            // Render ListBox end
            builder.CloseElement();
        }

        private void SelectionChanged(bool isAvailable, ChangeEventArgs args)
        {
            var selection = ReadSelection(args).Where(value => !string.IsNullOrEmpty(value)).ToList();
            if (isAvailable)
            {
                this.addValues = selection;
            }
            else
            {
                this.removeValues = selection;
            }

            this.RaisePostDataChangedEvent();
        }

        private void RenderHeader(RenderTreeBuilder builder, ref int sequence)
        {
            // render Header row
            builder.OpenElement(sequence++, "tr");

            builder.OpenElement(sequence++, "td");
            if (this.HeaderStyle != null)
            {
                this.HeaderStyle.AddAttributesToRender(builder, sequence);
                sequence += 2;
            }

            builder.AddContent(sequence++, Localization.GetString(this.ID + "_Available", this.LocalResourceFile));
            builder.CloseElement();

            builder.OpenElement(sequence++, "td");
            builder.CloseElement();

            builder.OpenElement(sequence++, "td");
            if (this.HeaderStyle != null)
            {
                this.HeaderStyle.AddAttributesToRender(builder, sequence);
                sequence += 2;
            }

            builder.AddContent(sequence++, Localization.GetString(this.ID + "_Selected", this.LocalResourceFile));
            builder.CloseElement();

            // Render end of Header Row
            builder.CloseElement();
        }

        private void RenderListBoxes(RenderTreeBuilder builder, ref int sequence)
        {
            // render List Boxes row
            builder.OpenElement(sequence++, "tr");

            builder.OpenElement(sequence++, "td");
            this.RenderListBox("Available", this.AvailableDataSource, this.AvailableListBoxStyle, builder, ref sequence);
            builder.CloseElement();

            builder.OpenElement(sequence++, "td");
            this.RenderButtons(builder, ref sequence);
            builder.CloseElement();

            builder.OpenElement(sequence++, "td");
            this.RenderListBox("Selected", this.SelectedDataSource, this.SelectedListBoxStyle, builder, ref sequence);
            builder.CloseElement();

            // Render end of List Boxes Row
            builder.CloseElement();
        }
    }
}
