// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Web.UI.WebControls;

    using Microsoft.AspNetCore.Components;

    /// <summary>This control is only for internal use, please don't reference it in any other place as it may be removed in the future.</summary>
    /// <remarks>
    /// This was a <c>TextBox</c>; it is a <see cref="DnnTextBox"/> now, which renders the same
    /// <c>input</c> and exposes the same <c>Text</c>. The <c>min</c>/<c>max</c>/
    /// <c>data-client-format</c> attributes that <c>OnPreRender</c> added are added to the same
    /// attribute bag, before the element renders.
    /// </remarks>
    [Obsolete("Deprecated in DotNetNuke 10.3.0. Please use DnnTextBox with TextMode=TextBoxMode.Date. Scheduled removal in v12.0.0.")]
    public class DnnDatePicker : DnnTextBox
    {
        /// <summary>Initializes a new instance of the <see cref="DnnDatePicker"/> class.</summary>
        public DnnDatePicker()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnDatePicker"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        public DnnDatePicker(IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger)
        {
            this.TextMode = "date";
        }

        /// <summary>Gets or sets the selected date.</summary>
        public DateTime? SelectedDate
        {
            get
            {
                if (!string.IsNullOrEmpty(this.Text) && DateTime.TryParse(this.Text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var value))
                {
                    return value;
                }

                return null;
            }

            set
            {
                this.Text = value?.ToString(this.Format, CultureInfo.CurrentCulture) ?? string.Empty;
            }
        }

        /// <summary>Gets or sets the minimum date.</summary>
        [Parameter]
        public DateTime MinDate { get; set; } = new DateTime(1900, 1, 1);

        /// <summary>Gets or sets the maximum date.</summary>
        [Parameter]
        public DateTime MaxDate { get; set; } = DateTime.MaxValue;

        /// <summary>Gets the .NET format string for the date.</summary>
        protected virtual string Format => "yyyy-MM-dd";

        /// <summary>Gets the moment.js format string.</summary>
        protected virtual string ClientFormat => "YYYY-MM-DD";

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            this.ApplySettings();
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.ApplySettings();
        }

        /// <summary>Gets the settings.</summary>
        /// <returns>A dictionary of attribute values.</returns>
        protected virtual IDictionary<string, object> GetSettings()
        {
            return new Dictionary<string, object>
            {
                { "min", this.MinDate > DateTime.MinValue ? this.MinDate.ToString(this.Format, CultureInfo.InvariantCulture) : null },
                { "max", this.MaxDate > DateTime.MinValue ? this.MaxDate.ToString(this.Format, CultureInfo.InvariantCulture) : null },
                { "data-client-format", this.ClientFormat },
            };
        }

        /// <summary>Writes the settings into the attribute bag of the rendered input.</summary>
        protected void ApplySettings()
        {
            var settings = this.GetSettings();
            foreach (var setting in settings)
            {
                if (string.Equals(setting.Key, "type", StringComparison.OrdinalIgnoreCase))
                {
                    this.TextMode = setting.Value?.ToString();
                    continue;
                }

                if (setting.Value == null)
                {
                    this.Attributes.Remove(setting.Key);
                }
                else
                {
                    this.Attributes[setting.Key] = setting.Value.ToString();
                }
            }
        }
    }
}
