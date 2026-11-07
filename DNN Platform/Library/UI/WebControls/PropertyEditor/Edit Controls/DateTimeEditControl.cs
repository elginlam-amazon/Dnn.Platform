// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;

    using DotNetNuke.Common.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>
    /// The DateTimeEditControl control provides a standard UI component for editing
    /// date and time properties.
    /// </summary>
    /// <remarks>
    /// The <c>hours</c>/<c>minutes</c>/<c>ampm</c> <c>DropDownList</c> child controls are now
    /// rendered directly, keeping their <c>{UniqueID}hours</c>, <c>{UniqueID}minutes</c> and
    /// <c>{UniqueID}ampm</c> form field names so that <see cref="LoadPostData"/> is unchanged.
    /// </remarks>
    public class DateTimeEditControl : DateEditControl
    {
        private bool is24HourClock;

        /// <summary>
        /// Gets defaultFormat is a string that will be used to format the date in the absence of a
        /// FormatAttribute.
        /// </summary>
        /// <value>A String representing the default format to use to render the date.</value>
        /// <returns>A Format String.</returns>
        protected override string DefaultFormat
        {
            get
            {
                return "g";
            }
        }

        /// <summary>Gets a value indicating whether the current culture uses a 24 hour clock.</summary>
        protected bool Is24HourClock => this.is24HourClock;

        /// <inheritdoc />
        public override bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            bool dataChanged = false;
            DateTime presentValue = this.OldDateValue;
            string postedDate = postCollection[postDataKey + "date"];
            string postedHours = postCollection[postDataKey + "hours"];
            string postedMinutes = postCollection[postDataKey + "minutes"];
            string postedAMPM = postCollection[postDataKey + "ampm"];
            DateTime postedValue = Null.NullDate;
            if (!string.IsNullOrEmpty(postedDate))
            {
                if (!DateTime.TryParse(postedDate, out postedValue))
                {
                    postedValue = Null.NullDate;
                }
            }

            if (postedHours != "12" || this.is24HourClock)
            {
                if (int.TryParse(postedHours, out var hours))
                {
                    postedValue = postedValue.AddHours(hours);
                }
            }

            postedValue = postedValue.AddMinutes(int.Parse(postedMinutes, CultureInfo.InvariantCulture));
            if (!this.is24HourClock && postedAMPM.Equals("PM", StringComparison.Ordinal))
            {
                postedValue = postedValue.AddHours(12);
            }

            if (!presentValue.Equals(postedValue))
            {
                this.Value = postedValue.ToString(CultureInfo.InvariantCulture);
                dataChanged = true;
            }

            return dataChanged;
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            if (string.IsNullOrEmpty(CultureInfo.CurrentCulture.DateTimeFormat.AMDesignator))
            {
                this.is24HourClock = true;
            }
        }

        /// <inheritdoc />
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.GetTimeParts(out var hour, out var minute, out var isAM);

            // Every part of the value has to be present in the post data, because LoadPostData
            // reassembles the whole DateTime from all four fields.
            this.SetPostValue("hours", hour.ToString(CultureInfo.InvariantCulture));
            this.SetPostValue("minutes", minute.ToString(CultureInfo.InvariantCulture));
            if (!this.is24HourClock)
            {
                this.SetPostValue("ampm", isAM ? "AM" : "PM");
            }

            this.RenderDateField(builder, 0);

            builder.AddMarkupContent(100, "<br/>");

            int maxHour = 12;
            int minHour = 1;
            if (this.is24HourClock)
            {
                minHour = 0;
                maxHour = 23;
            }

            builder.OpenElement(110, "select");
            this.AddStyleAttributes(builder, 111);
            builder.AddAttribute(115, "name", this.UniqueID + "hours");
            builder.AddAttribute(116, "id", this.ClientID + "hours");
            builder.AddAttribute(117, "aria-label", "hours");
            builder.AddAttribute(
                118,
                "onchange",
                EventCallback.Factory.Create<ChangeEventArgs>(this, args => this.OnFieldChanged("hours", args)));
            builder.OpenRegion(119);
            for (int i = minHour; i <= maxHour; i++)
            {
                RenderOption(builder, i.ToString(CultureInfo.InvariantCulture), i.ToString("00", CultureInfo.CurrentCulture), i == hour);
            }

            builder.CloseRegion();
            builder.CloseElement();

            builder.AddMarkupContent(130, "&nbsp");

            builder.OpenElement(140, "select");
            this.AddStyleAttributes(builder, 141);
            builder.AddAttribute(145, "name", this.UniqueID + "minutes");
            builder.AddAttribute(146, "id", this.ClientID + "minutes");
            builder.AddAttribute(147, "aria-label", "minutes");
            builder.AddAttribute(
                148,
                "onchange",
                EventCallback.Factory.Create<ChangeEventArgs>(this, args => this.OnFieldChanged("minutes", args)));
            builder.OpenRegion(149);
            for (int i = 0; i <= 59; i++)
            {
                RenderOption(builder, i.ToString(CultureInfo.InvariantCulture), i.ToString("00", CultureInfo.CurrentCulture), i == minute);
            }

            builder.CloseRegion();
            builder.CloseElement();

            if (!this.is24HourClock)
            {
                builder.AddMarkupContent(160, "&nbsp");

                builder.OpenElement(170, "select");
                this.AddStyleAttributes(builder, 171);
                builder.AddAttribute(175, "name", this.UniqueID + "ampm");
                builder.AddAttribute(176, "id", this.ClientID + "ampm");
                builder.AddAttribute(177, "aria-label", "ampm");
                builder.AddAttribute(
                    178,
                    "onchange",
                    EventCallback.Factory.Create<ChangeEventArgs>(this, args => this.OnFieldChanged("ampm", args)));
                builder.OpenRegion(179);
                RenderOption(builder, "AM", "AM", isAM);
                RenderOption(builder, "PM", "PM", !isAM);
                builder.CloseRegion();
                builder.CloseElement();
            }
        }

        /// <summary>Splits the current value into the hour, minute and AM/PM parts that are rendered.</summary>
        /// <param name="hour">The hour to pre-select.</param>
        /// <param name="minute">The minute to pre-select.</param>
        /// <param name="isAM">Whether the AM option is pre-selected.</param>
        /// <remarks>Replaces the <c>LoadDateControls</c> override, which assigned the selections onto
        /// the <c>DropDownList</c> child controls.</remarks>
        protected void GetTimeParts(out int hour, out int minute, out bool isAM)
        {
            hour = this.DateValue.Hour;
            minute = this.DateValue.Minute;
            isAM = true;
            if (!this.is24HourClock)
            {
                if (hour >= 12)
                {
                    hour -= 12;
                    isAM = false;
                }

                if (hour == 0)
                {
                    hour = 12;
                }
            }
        }

        private static void RenderOption(RenderTreeBuilder builder, string value, string text, bool isSelected)
        {
            builder.OpenElement(0, "option");
            builder.AddAttribute(1, "value", value);
            if (isSelected)
            {
                builder.AddAttribute(2, "selected", "selected");
            }

            builder.AddContent(3, text);
            builder.CloseElement();
        }
    }
}
