// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Lists;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>The DNNListEditControl control provides a standard UI component for selecting from Lists.</summary>
    /// <remarks>
    /// The control implemented <c>IPostBackEventHandler</c> so that <c>AutoPostBack</c> could raise
    /// <see cref="ItemChanged"/>. <see cref="RaisePostBackEvent"/> is preserved — it is now invoked
    /// directly from the <c>select</c>'s change handler instead of through a
    /// <c>__doPostBack</c> round trip.
    /// </remarks>
    public class DNNListEditControl : EditControl
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<DNNListEditControl>();
        private readonly ListController listController;
        private readonly IPortalController portalController;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IPortalGroupController portalGroupController;
        private List<ListEntryInfo> listEntries;
        private string listName = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="DNNListEditControl"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with ListController. Scheduled removal in v12.0.0.")]
        public DNNListEditControl()
            : this(null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNListEditControl"/> class.</summary>
        /// <param name="listController">The list controller.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="portalGroupController">The portal group controller.</param>
        public DNNListEditControl(ListController listController, IPortalController portalController, IApplicationStatusInfo appStatus, IPortalGroupController portalGroupController)
        {
            this.listController = listController ?? Globals.GetCurrentServiceProvider().GetRequiredService<ListController>();
            this.portalController = portalController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>();
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.portalGroupController = portalGroupController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalGroupController>();

            this.ValueField = ListBoundField.Value;
            this.TextField = ListBoundField.Text;
            this.ParentKey = string.Empty;
            this.SortAlphabetically = false;
        }

        /// <summary>Occurs when the selected list item has changed and <see cref="AutoPostBack"/> is set.</summary>
        public event PropertyChangedEventHandler ItemChanged;

        /// <summary>Gets or sets a callback raised in addition to the <see cref="ItemChanged"/> event.</summary>
        [Parameter]
        public EventCallback<PropertyEditorEventArgs> ItemChangedCallback { get; set; }

        /// <summary>Gets integerValue returns the Integer representation of the Value.</summary>
        /// <value>An integer representing the Value.</value>
        protected int IntegerValue
        {
            get
            {
                int intValue = Null.NullInteger;
                if (this.Value == null || string.IsNullOrEmpty((string)this.Value))
                {
                    return intValue;
                }

                try
                {
                    intValue = Convert.ToInt32(this.Value, CultureInfo.InvariantCulture);
                }
                catch (Exception exc)
                {
                    Logger.DnnListEditControlIntegerValueException(exc);
                }

                return intValue;
            }
        }

        /// <summary>Gets the ListEntryInfo objects associated with the control.</summary>
        protected IEnumerable<ListEntryInfo> ListEntries
        {
            get
            {
                if (this.listEntries == null)
                {
                    if (this.SortAlphabetically)
                    {
                        this.listEntries = this.listController.GetListEntryInfoItems(this.ListName, this.ParentKey, this.PortalId).OrderBy(s => s.SortOrder).ThenBy(s => s.Text).ToList();
                    }
                    else
                    {
                        this.listEntries = this.listController.GetListEntryInfoItems(this.ListName, this.ParentKey, this.PortalId).ToList();
                    }
                }

                return this.listEntries;
            }
        }

        /// <summary>Gets oldIntegerValue returns the Integer representation of the OldValue.</summary>
        /// <value>An integer representing the OldValue.</value>
        protected int OldIntegerValue
        {
            get
            {
                int intValue = Null.NullInteger;
                if (this.OldValue == null || string.IsNullOrEmpty(this.OldValue.ToString()))
                {
                    return intValue;
                }

                try
                {
                    // Try and cast the value to an Integer
                    intValue = Convert.ToInt32(this.OldValue, CultureInfo.InvariantCulture);
                }
                catch (Exception exc)
                {
                    Logger.DnnListEditControlOldIntegerValueException(exc);
                }

                return intValue;
            }
        }

        /// <summary>Gets the effective portal id the list entries are read for.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected int PortalId => PortalController.GetEffectivePortalId(this.portalController, this.appStatus, this.portalGroupController, PortalSettings.Current.PortalId);

        /// <summary>Gets oldStringValue returns the Boolean representation of the OldValue.</summary>
        /// <value>A String representing the OldValue.</value>
        protected string OldStringValue => Convert.ToString(this.OldValue, CultureInfo.InvariantCulture);

        /// <summary>Gets or sets a value indicating whether the List Auto Posts Back.</summary>
        protected bool AutoPostBack { get; set; }

        /// <summary>Gets or sets a value indicating whether if true the list will be sorted on the value of Text before rendering.</summary>
        protected bool SortAlphabetically { get; set; }

        /// <summary>Gets or sets listName is the name of the List to display.</summary>
        protected virtual string ListName
        {
            get
            {
                if (this.listName == Null.NullString)
                {
                    this.listName = this.DataField;
                }

                return this.listName;
            }

            set
            {
                this.listName = value;
            }
        }

        /// <summary>Gets or sets the parent key of the List to display.</summary>
        protected virtual string ParentKey { get; set; }

        /// <summary>Gets or sets the field to display in the combo.</summary>
        protected virtual ListBoundField TextField { get; set; }

        /// <summary>Gets or sets the field to use as the combo item values.</summary>
        protected virtual ListBoundField ValueField { get; set; }

        /// <summary>Gets or sets stringValue is the value of the control expressed as a String.</summary>
        /// <value>A string representing the Value.</value>
        protected override string StringValue
        {
            get
            {
                return Convert.ToString(this.Value, CultureInfo.InvariantCulture);
            }

            set
            {
                if (this.ValueField == ListBoundField.Id)
                {
                    // Integer type field
                    this.Value = int.Parse(value, CultureInfo.InvariantCulture);
                }
                else
                {
                    // String type Field
                    this.Value = value;
                }
            }
        }

        /// <summary>Raises <see cref="ItemChanged"/> when <see cref="AutoPostBack"/> is set.</summary>
        /// <param name="eventArgument">The event argument (the control identifier).</param>
        /// <remarks>Replaces <c>IPostBackEventHandler.RaisePostBackEvent</c>; it is now called from
        /// the <c>select</c>'s change handler.</remarks>
        public void RaisePostBackEvent(string eventArgument)
        {
            if (this.AutoPostBack)
            {
                this.OnItemChanged(this.GetEventArgs());
            }
        }

        /// <summary>OnAttributesChanged runs when the CustomAttributes property has changed.</summary>
        protected override void OnAttributesChanged()
        {
            // Get the List settings out of the "Attributes"
            if (this.CustomAttributes != null)
            {
                foreach (Attribute attribute in this.CustomAttributes)
                {
                    if (attribute is ListAttribute)
                    {
                        var listAtt = (ListAttribute)attribute;
                        this.ListName = listAtt.ListName;
                        this.ParentKey = listAtt.ParentKey;
                        this.TextField = listAtt.TextField;
                        this.ValueField = listAtt.ValueField;
                        break;
                    }
                }
            }
        }

        /// <summary>OnDataChanged runs when the PostbackData has changed.  It raises the <see cref="EditControl.ValueChanged"/> Event.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnDataChanged(EventArgs e)
        {
            this.OnValueChanged(this.GetEventArgs());
        }

        /// <summary>OnItemChanged runs when the Item has changed. It raises the <see cref="ItemChanged"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnItemChanged(PropertyEditorEventArgs e)
        {
            this.ItemChanged?.Invoke(this, e);
            if (this.ItemChangedCallback.HasDelegate)
            {
                this.ItemChangedCallback.InvokeAsync(e);
            }
        }

        /// <summary>RenderViewMode renders the View (readonly) mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderViewMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            ListEntryInfo entry = null;
            string entryText = Null.NullString;
            switch (this.ValueField)
            {
                case ListBoundField.Id:
                    entry = this.listController.GetListEntryInfo(this.ListName, Convert.ToInt32(this.Value, CultureInfo.InvariantCulture));
                    break;
                case ListBoundField.Text:
                    entryText = this.StringValue;
                    break;
                case ListBoundField.Value:
                    entry = this.listController.GetListEntryInfo(this.ListName, this.StringValue);
                    break;
            }

            builder.OpenElement(0, "span");
            this.AddStyleAttributes(builder, 1);
            if (entry != null)
            {
                switch (this.TextField)
                {
                    case ListBoundField.Id:
                        builder.AddContent(5, entry.EntryID.ToString(CultureInfo.InvariantCulture));
                        break;
                    case ListBoundField.Text:
                        builder.AddContent(6, entry.Text);
                        break;
                    case ListBoundField.Value:
                        builder.AddContent(7, entry.Value);
                        break;
                }
            }
            else
            {
                builder.AddContent(8, entryText);
            }

            builder.CloseElement();
        }

        /// <summary>RenderEditMode renders the Edit mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Render the Select Tag
            builder.OpenElement(0, "select");
            this.AddStyleAttributes(builder, 1);
            builder.AddAttribute(5, "name", this.UniqueID);
            builder.AddAttribute(6, "id", this.ClientID);
            builder.AddAttribute(7, "data-name", this.Name);
            builder.AddAttribute(8, "data-list", this.ListName);
            builder.AddAttribute(9, "data-category", this.Category);
            builder.AddAttribute(10, "data-editor", "DNNListEditControl");
            builder.AddAttribute(11, "aria-label", this.Name);
            builder.AddAttribute(12, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnSelectionChanged));

            // Add the Not Specified Option
            builder.OpenElement(13, "option");
            builder.AddAttribute(14, "value", Null.NullString);
            if (this.StringValue == Null.NullString)
            {
                // Add the Selected Attribute
                builder.AddAttribute(15, "selected", "selected");
            }

            builder.AddContent(16, "<" + Localization.GetString("Not_Specified", Localization.SharedResourceFile) + ">");
            builder.CloseElement();

            builder.OpenRegion(20);
            foreach (ListEntryInfo item in this.ListEntries)
            {
                string itemValue = Null.NullString;

                // Add the Value Attribute
                switch (this.ValueField)
                {
                    case ListBoundField.Id:
                        itemValue = item.EntryID.ToString(CultureInfo.InvariantCulture);
                        break;
                    case ListBoundField.Text:
                        itemValue = item.Text;
                        break;
                    case ListBoundField.Value:
                        itemValue = item.Value;
                        break;
                }

                builder.OpenElement(0, "option");
                builder.AddAttribute(1, "value", itemValue);
                if (this.StringValue == itemValue)
                {
                    // Add the Selected Attribute
                    builder.AddAttribute(2, "selected", "selected");
                }

                switch (this.TextField)
                {
                    case ListBoundField.Id:
                        builder.AddContent(3, item.EntryID.ToString(CultureInfo.InvariantCulture));
                        break;
                    case ListBoundField.Text:
                        builder.AddContent(4, item.Text);
                        break;
                    case ListBoundField.Value:
                        builder.AddContent(5, item.Value.Trim());
                        break;
                }

                builder.CloseElement();
            }

            builder.CloseRegion();

            // Close Select Tag
            builder.CloseElement();
        }

        private void OnSelectionChanged(ChangeEventArgs args)
        {
            // This is the Blazor equivalent of the AutoPostBack round trip: the framework used to
            // call LoadPostData/RaisePostDataChangedEvent and then RaisePostBackEvent.
            this.OnInputChanged(args);
            this.RaisePostBackEvent(this.ID);
        }

        private PropertyEditorEventArgs GetEventArgs()
        {
            var args = new PropertyEditorEventArgs(this.Name);
            if (this.ValueField == ListBoundField.Id)
            {
                // This is an Integer Value
                args.Value = this.IntegerValue;
                args.OldValue = this.OldIntegerValue;
            }
            else
            {
                // This is a String Value
                args.Value = this.StringValue;
                args.OldValue = this.OldStringValue;
            }

            args.StringValue = this.StringValue;
            return args;
        }
    }
}
