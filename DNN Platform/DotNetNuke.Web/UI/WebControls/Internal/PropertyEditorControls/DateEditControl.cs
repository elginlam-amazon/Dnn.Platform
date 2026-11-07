// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls.Internal.PropertyEditorControls
{
    using System;
    using System.Collections.Specialized;
    using System.Data.SqlTypes;
    using System.Globalization;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.UI.WebControls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.Extensions.Logging;

    /// <summary>The DateEditControl control provides a standard UI component for editing date properties.</summary>
    /// <remarks>
    /// This control is only for internal use, please don't reference it in any other place as it may be removed in the future.
    /// It derives from the migrated <see cref="EditControl"/>: the child <c>TextBox</c> with
    /// <c>TextMode=Date</c> became an <c>input type="date"</c> emitted by
    /// <see cref="RenderEditMode"/>, and the posted value is still read by
    /// <see cref="LoadPostData"/> under the <c>_control</c> suffixed field name, so the
    /// culture-invariant parse/format behaviour is unchanged.
    /// </remarks>
    public class DateEditControl : EditControl
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<DateEditControl>();

        /// <inheritdoc />
        public override string EditControlClientId => this.ClientID;

        // NOTE: the Web Forms control overrode `ID` to append "_control" because the editor and its
        // child TextBox were two controls sharing one naming scope. There is no child control any
        // more — the suffix only has to distinguish the posted field name, which is what
        // <see cref="InputSuffix"/> does — so `ID` is left alone and `ClientID` is the id of the
        // rendered input, which keeps <see cref="EditControlClientId"/> correct for labels.

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
        protected virtual string DefaultFormat => "d";

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
                    foreach (var customAttribute in this.CustomAttributes)
                    {
                        if (customAttribute is FormatAttribute formatAtt)
                        {
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
                    if (!string.IsNullOrEmpty(dteString))
                    {
                        dteValue = DateTime.Parse(dteString, CultureInfo.InvariantCulture);
                    }
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
                if (this.DateValue.ToUniversalTime().Date != (DateTime)SqlDateTime.MinValue && this.DateValue != Null.NullDate)
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

        /// <summary>Gets the value rendered into (and read back from) the date input.</summary>
        /// <remarks>Replaces the <c>Text</c> of the child <c>TextBox</c>; the wire format of an
        /// <c>input type="date"</c> is unchanged from what <c>TextBoxMode.Date</c> rendered.</remarks>
        protected virtual string InputValue =>
            this.DateValue != Null.NullDate
                ? this.DateValue.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : string.Empty;

        // Protected API inherited by derived edit controls; making it static would be a breaking change.
        #pragma warning disable CA1822 // Mark members as static
        /// <summary>Gets the field name suffix of the date input.</summary>
        protected string InputSuffix => "_control";
        #pragma warning restore CA1822 // Mark members as static

        /// <summary>Gets the type of the rendered input.</summary>
        protected virtual string InputType => "date";

        /// <inheritdoc />
        public override bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            bool dataChanged = false;
            string presentValue = this.StringValue;
            string postedValue = postCollection[postDataKey + this.InputSuffix];
            if (!presentValue.Equals(postedValue, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(postedValue))
                {
                    this.Value = Null.NullDate;
                    dataChanged = true;
                }
                else
                {
                    this.Value = DateTime.Parse(postedValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                    dataChanged = true;
                }
            }

            return dataChanged;
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

        /// <inheritdoc cref="EditControl.RenderEditMode"/>
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "input");
            this.AddStyleAttributes(builder, 1);
            builder.AddAttribute(5, "type", this.InputType);
            builder.AddAttribute(6, "id", this.ClientID);
            builder.AddAttribute(7, "name", this.UniqueID + this.InputSuffix);
            builder.AddAttribute(8, "value", this.InputValue);
            builder.AddAttribute(9, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnDateInput));
            builder.CloseElement();
        }

        /// <inheritdoc cref="EditControl.RenderViewMode"/>
        protected override void RenderViewMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "span");
            this.AddStyleAttributes(builder, 1);
            builder.AddContent(5, this.StringValue);
            builder.CloseElement();
        }

        /// <summary>Applies the date the user picked.</summary>
        /// <param name="args">The change event arguments.</param>
        private void OnDateInput(ChangeEventArgs args)
        {
            this.SetPostValue(this.InputSuffix, args?.Value?.ToString());
            this.CommitPostData();
        }
    }
}
