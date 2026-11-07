// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Globalization;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A text box control.</summary>
    /// <remarks>
    /// The <c>TextBox</c> this item created is a <see cref="DnnTextBox"/> now; its
    /// <c>TextChanged</c> postback event is the component's <c>TextChanged</c> event, which still
    /// writes the new value back into the data source. The properties that lived in
    /// <c>ViewState</c> are plain parameters.
    /// </remarks>
    public class DnnFormTextBoxItem : DnnFormItemBase
    {
        private DnnTextBox textBox;

        /// <summary>Initializes a new instance of the <see cref="DnnFormTextBoxItem"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo. Scheduled removal in v12.0.0.")]
        public DnnFormTextBoxItem()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnFormTextBoxItem"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        public DnnFormTextBoxItem(IApplicationStatusInfo appStatus, IEventLogger eventLogger)
            : base(appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>(), eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>())
        {
        }

        /// <summary>Gets or sets a value indicating whether the text box accepts input.</summary>
        /// <remarks>
        /// Replaces <c>System.Web.UI.WebControls.WebControl.Enabled</c>, which this item inherited
        /// when it was a <c>WebControl</c> and which Web Forms propagated down to the rendered
        /// input as <c>disabled="disabled"</c>. <see cref="DnnComponentBase"/> has no such member,
        /// so the value is forwarded explicitly to the child <see cref="DnnTextBox"/>, whose own
        /// <see cref="DnnTextBox.Enabled"/> writes the <c>disabled</c> attribute. Callers such as
        /// Admin/EditExtension use this to make a field read-only for package types that must not
        /// be edited, so it is permission-visible behaviour rather than styling.
        /// </remarks>
        [Parameter]
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets the value of the input's <c>autocomplete</c> attribute.</summary>
        /// <remarks>
        /// This was a <c>System.Web.UI.WebControls.AutoCompleteType</c>, an enumeration of the
        /// vCard tokens Internet Explorer understood. The attribute value is written through
        /// verbatim now; <c>"off"</c> is what DNN relied on.
        /// </remarks>
        [Parameter]
        public string AutoCompleteType { get; set; }

        /// <summary>Gets or sets the max length.</summary>
        [Parameter]
        public int MaxLength { get; set; }

        /// <summary>Gets or sets the columns (when <see cref="TextMode"/> is <see cref="TextBoxMode.MultiLine"/>).</summary>
        [Parameter]
        public int Columns { get; set; }

        /// <summary>Gets or sets the rows (when <see cref="TextMode"/> is <see cref="TextBoxMode.MultiLine"/>).</summary>
        [Parameter]
        public int Rows { get; set; }

        /// <summary>Gets or sets the CSS class for the text box.</summary>
        [Parameter]
        public string TextBoxCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the input mode.</summary>
        [Parameter]
        public TextBoxMode TextMode { get; set; }

        /// <summary>Gets or sets a value indicating whether to clear the field's value after post back when text mode set to password mode.</summary>
        [Parameter]
        public bool ClearContentInPasswordMode { get; set; }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        protected override IDnnControl CreateControlInternal(IDnnControl container)
        {
            this.textBox = new DnnTextBox { ID = this.ID + "_TextBox" };

            this.textBox.Rows = this.Rows;
            this.textBox.Columns = this.Columns;
            this.textBox.TextMode = this.TextMode.ToInputType();
            this.textBox.CssClass = this.TextBoxCssClass;
            this.textBox.Enabled = this.Enabled;
            if (!string.IsNullOrEmpty(this.AutoCompleteType))
            {
                this.textBox.Attributes["autocomplete"] = this.AutoCompleteType;
            }

            this.textBox.TextChanged += this.TextChanged;

            // Load from the item's value (this was the Web Forms ControlState)
            this.textBox.Text = Convert.ToString(this.Value, CultureInfo.InvariantCulture);
            if (this.TextMode == TextBoxMode.Password)
            {
                this.textBox.Attributes["autocomplete"] = "off";
            }

            if (this.MaxLength > 0)
            {
                this.textBox.MaxLength = this.MaxLength;
            }

            container?.Controls.Add(this.textBox);

            return this.textBox;
        }
        #pragma warning restore BL0005

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (this.textBox != null)
            {
                // Enabled is assigned by callers during data binding, i.e. after
                // CreateControlInternal has already built the child, so re-sync it here. This
                // mirrors Web Forms, where WebControl.Enabled was read at render time.
                this.textBox.Enabled = this.Enabled;
            }

            if (this.textBox != null && this.TextMode == TextBoxMode.Password && !this.ClearContentInPasswordMode)
            {
                this.textBox.Text = Convert.ToString(this.Value, CultureInfo.InvariantCulture);
            }
        }
        #pragma warning restore BL0005

        private void TextChanged(object sender, EventArgs e)
        {
            this.UpdateDataSource(this.Value, this.textBox.Text, this.DataField);
        }
    }
}
