// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.Extensions.Logging;

    using Calendar = DotNetNuke.Common.Utilities.Calendar;

    /// <summary>
    /// The DateEditControl control provides a standard UI component for editing
    /// date properties.
    /// </summary>
    /// <remarks>
    /// The <c>TextBox</c> + calendar <c>HyperLink</c> child controls are now rendered directly. The
    /// text input keeps its <c>{UniqueID}date</c> form field name, so <see cref="LoadPostData"/> and
    /// any third-party override of it are unchanged, and the calendar link still uses
    /// <see cref="Calendar.InvokePopupCal(DotNetNuke.UI.Utilities.IClientApiControl)"/> — the element
    /// is presented to it through <see cref="PropertyEditorClientApiControl"/>.
    /// </remarks>
    public class DateEditControl : EditControl
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<DateEditControl>();

        /// <summary>Gets dateValue returns the Date representation of the Value.</summary>
        /// <value>A Date representing the Value.</value>
        protected DateTime DateValue
        {
            get
            {
                DateTime dteValue = Null.NullDate;
                try
                {
                    var dteString = Convert.ToString(this.Value, CultureInfo.InvariantCulture);
                    DateTime.TryParse(dteString, CultureInfo.InvariantCulture, DateTimeStyles.None, out dteValue);
                }
                catch (Exception exc)
                {
                    Logger.DateEditControlDateValueException(exc);
                }

                return dteValue;
            }
        }

        /// <summary>
        /// Gets defaultDateFormat is a string that will be used to format the date in the absence of a
        /// FormatAttribute.
        /// </summary>
        /// <value>A String representing the default format to use to render the date.</value>
        /// <returns>A Format String.</returns>
        protected virtual string DefaultFormat
        {
            get
            {
                return "d";
            }
        }

        /// <summary>Gets format is a string that will be used to format the date in View mode.</summary>
        /// <value>A String representing the format to use to render the date.</value>
        /// <returns>A Format String.</returns>
        protected virtual string Format
        {
            get
            {
                string format = this.DefaultFormat;
                if (this.CustomAttributes != null)
                {
                    foreach (Attribute attribute in this.CustomAttributes)
                    {
                        if (attribute is FormatAttribute)
                        {
                            var formatAtt = (FormatAttribute)attribute;
                            format = formatAtt.Format;
                            break;
                        }
                    }
                }

                return format;
            }
        }

        /// <summary>Gets oldDateValue returns the Date representation of the OldValue.</summary>
        /// <value>A Date representing the OldValue.</value>
        protected DateTime OldDateValue
        {
            get
            {
                DateTime dteValue = Null.NullDate;
                try
                {
                    // Try and cast the value to an DateTime
                    var dteString = this.OldValue as string;
                    dteValue = DateTime.Parse(dteString, CultureInfo.InvariantCulture);
                }
                catch (Exception exc)
                {
                    Logger.DateEditControlOldDateValueException(exc);
                }

                return dteValue;
            }
        }

        /// <summary>Gets or sets the Value expressed as a String.</summary>
        protected override string StringValue
        {
            get
            {
                string stringValue = Null.NullString;
                if (this.DateValue.ToUniversalTime().Date != new DateTime(1754, 1, 1) && this.DateValue != Null.NullDate)
                {
                    stringValue = this.DateValue.ToString(this.Format, CultureInfo.InvariantCulture);
                }

                return stringValue;
            }

            set
            {
                this.Value = DateTime.Parse(value, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Gets the <c>id</c> of the rendered date text input.</summary>
        protected string DateFieldClientId => this.ClientID + "date";

        /// <inheritdoc />
        public override string EditControlClientId => this.DateFieldClientId;

        /// <inheritdoc />
        public override bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            bool dataChanged = false;
            string presentValue = this.StringValue;
            string postedValue = postCollection[postDataKey + "date"];
            if (!presentValue.Equals(postedValue, StringComparison.Ordinal))
            {
                this.Value = DateTime.Parse(postedValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                dataChanged = true;
            }

            return dataChanged;
        }

        /// <summary>Gets the text rendered into the date input.</summary>
        /// <returns>The short date, or an empty string when no date is set.</returns>
        /// <remarks>Replaces the <c>LoadDateControls</c> assignment to <c>dateField.Text</c>.</remarks>
        protected virtual string GetDateText()
        {
            return this.DateValue != Null.NullDate
                ? this.DateValue.Date.ToString("d", CultureInfo.CurrentCulture)
                : string.Empty;
        }

        /// <summary>OnDataChanged is called by the PostBack Handler when the Data has changed.</summary>
        /// <param name="e">An EventArgs object.</param>
        protected override void OnDataChanged(EventArgs e)
        {
            var args = new PropertyEditorEventArgs(this.Name);
            args.Value = this.DateValue;
            args.OldValue = this.OldDateValue;
            args.StringValue = this.DateValue.ToString(CultureInfo.InvariantCulture);
            this.OnValueChanged(args);
        }

        /// <summary>Renders the date text input and the calendar popup link.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        /// <param name="sequence">The render tree sequence number to use.</param>
        protected void RenderDateField(RenderTreeBuilder builder, int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // The date is one of several fields of this editor, so the untouched fields have to be
            // present in the post data when LoadPostData reassembles the value.
            this.SetPostValue("date", this.GetDateText());

            builder.OpenElement(sequence, "input");
            this.AddStyleAttributes(builder, sequence + 1);
            builder.AddAttribute(sequence + 5, "type", "text");
            builder.AddAttribute(sequence + 6, "name", this.UniqueID + "date");
            builder.AddAttribute(sequence + 7, "id", this.DateFieldClientId);
            builder.AddAttribute(sequence + 8, "value", this.GetDateText());
            builder.AddAttribute(sequence + 9, "aria-label", "date");
            builder.AddAttribute(
                sequence + 10,
                "onchange",
                EventCallback.Factory.Create<ChangeEventArgs>(this, args => this.OnFieldChanged("date", args)));
            builder.CloseElement();

            builder.AddMarkupContent(sequence + 15, "&nbsp;");

            builder.OpenElement(sequence + 16, "a");
            builder.AddAttribute(sequence + 17, "class", "CommandButton");
            builder.AddAttribute(sequence + 18, "href", Calendar.InvokePopupCal(new PropertyEditorClientApiControl(this.DateFieldClientId)));
            builder.AddMarkupContent(
                sequence + 19,
                "<img src=\"" + Globals.ApplicationPath + "/images/calendar.png\" border=\"0\" alt=\"\" />&nbsp;&nbsp;" + Localization.GetString("Calendar"));
            builder.CloseElement();
        }

        /// <summary>Records a changed field value and applies it through <see cref="LoadPostData"/>.</summary>
        /// <param name="suffix">The field name suffix.</param>
        /// <param name="args">The change event arguments.</param>
        protected void OnFieldChanged(string suffix, ChangeEventArgs args)
        {
            this.SetPostValue(suffix, args?.Value?.ToString());
            this.CommitPostData();
        }

        /// <summary>RenderEditMode is called by the base control to render the control in Edit Mode.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            this.RenderDateField(builder, 0);
        }

        /// <summary>RenderViewMode renders the View (readonly) mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderViewMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "span");
            this.AddStyleAttributes(builder, 1);
            builder.AddContent(5, this.StringValue);
            builder.CloseElement();
        }
    }
}
