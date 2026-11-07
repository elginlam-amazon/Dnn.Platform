// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Net;

    using DotNetNuke.UI.WebControls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A time zone edit control.</summary>
    /// <remarks>
    /// The control still edits its value through a <see cref="DnnTimeZoneComboBox"/>. The combo box
    /// used to live in the control's <c>Controls</c> collection and be rendered by
    /// <c>RenderChildren</c>; a <see cref="EditControl"/> has no child collection on ASP.NET Core,
    /// so it is rendered as a child component by <see cref="RenderEditMode"/> instead and its
    /// selection arrives through the component's selection-changed callback, which feeds
    /// <c>LoadPostData</c>/<c>RaisePostDataChangedEvent</c> exactly as the postback used to.
    /// </remarks>
    public class DnnTimeZoneEditControl : TextEditControl
    {
        /// <summary>Initializes a new instance of the <see cref="DnnTimeZoneEditControl"/> class.</summary>
        public DnnTimeZoneEditControl()
        {
        }

        /// <inheritdoc />
        public override string EditControlClientId => this.TimeZonesClientId;

        /// <summary>Gets the identifier emitted onto the time zone combo box.</summary>
        private string TimeZonesClientId => this.ClientID + "_TimeZones";

        /// <summary>Gets or sets the rendered combo box instance.</summary>
        private DnnTimeZoneComboBox TimeZones { get; set; }

        /// <inheritdoc />
        public override bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            bool dataChanged = false;
            string presentValue = this.StringValue;
            string postedValue = postCollection[this.UniqueID];
            if (!string.Equals(presentValue, postedValue, StringComparison.Ordinal))
            {
                this.Value = postedValue;
                dataChanged = true;
            }

            return dataChanged;
        }

        /// <inheritdoc />
        protected override void OnDataChanged(EventArgs e)
        {
            var args = new PropertyEditorEventArgs(this.Name);
            args.Value = TimeZoneInfo.FindSystemTimeZoneById(this.StringValue);
            args.OldValue = this.OldStringValue;
            args.StringValue = this.StringValue;
            this.OnValueChanged(args);
        }

        /// <inheritdoc />
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenComponent<DnnTimeZoneComboBox>(0);
            builder.AddAttribute(1, "ID", this.TimeZonesClientId);
            builder.AddAttribute(2, "InitialValue", this.StringValue);
            builder.AddAttribute(
                3,
                "SelectedIndexChangedCallback",
                EventCallback.Factory.Create<EventArgs>(this, this.OnTimeZoneSelected));
            builder.AddComponentReferenceCapture(4, instance => this.TimeZones = instance as DnnTimeZoneComboBox);
            builder.CloseComponent();
        }

        /// <inheritdoc />
        protected override void RenderViewMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            string propValue = WebUtility.HtmlDecode(Convert.ToString(this.Value, CultureInfo.InvariantCulture));

            builder.OpenElement(0, "span");
            this.AddStyleAttributes(builder, 1);
            builder.AddContent(5, propValue);
            builder.CloseElement();
        }

        private void OnTimeZoneSelected(EventArgs e)
        {
            this.SetPostValue(string.Empty, this.TimeZones?.SelectedValue);
            this.CommitPostData();
        }
    }
}
