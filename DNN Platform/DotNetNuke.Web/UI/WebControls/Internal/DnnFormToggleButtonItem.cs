// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls.Internal
{
    using System;
    using System.Globalization;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.UI;
    using DotNetNuke.Web.UI.WebControls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>This control is only for internal use, please don't reference it in any other place as it may be removed in the future.</summary>
    /// <remarks>
    /// The <c>CheckBox</c> became a <see cref="DnnCheckBox"/>; its <c>CheckedChanged</c> event still
    /// writes the value back into the data source in the mode's own vocabulary
    /// (<c>true/false</c>, <c>Y/N</c> or <c>Yes/No</c>).
    /// </remarks>
    public class DnnFormToggleButtonItem : DnnFormItemBase
    {
        private DnnCheckBox checkBox;

        /// <summary>Initializes a new instance of the <see cref="DnnFormToggleButtonItem"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo. Scheduled removal in v12.0.0.")]
        public DnnFormToggleButtonItem()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnFormToggleButtonItem"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        public DnnFormToggleButtonItem(IApplicationStatusInfo appStatus, IEventLogger eventLogger)
            : base(appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>(), eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>())
        {
            this.Mode = CheckBoxMode.TrueFalse;
        }

        /// <summary>The checkbox mode.</summary>
        public enum CheckBoxMode
        {
            /// <summary>True and False.</summary>
            TrueFalse = 0,

            /// <summary>Y and N.</summary>
            YN = 1,

            /// <summary>Yes and No.</summary>
            YesNo = 2,
        }

        /// <summary>Gets or sets the checkbox mode.</summary>
        [Parameter]
        public CheckBoxMode Mode { get; set; }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        protected override IDnnControl CreateControlInternal(IDnnControl container)
        {
            this.checkBox = new DnnCheckBox { ID = this.ID + "_CheckBox", AutoPostBack = false };

            this.checkBox.CheckedChanged += this.CheckedChanged;
            container?.Controls.Add(this.checkBox);

            switch (this.Mode)
            {
                case CheckBoxMode.YN:
                case CheckBoxMode.YesNo:
                    if (this.Value is string stringValue)
                    {
                        this.checkBox.Checked = stringValue.StartsWith("Y", StringComparison.InvariantCultureIgnoreCase);
                    }

                    break;
                default:
                    this.checkBox.Checked = Convert.ToBoolean(this.Value, CultureInfo.InvariantCulture);
                    break;
            }

            return this.checkBox;
        }
        #pragma warning restore BL0005

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.FormMode = DnnFormMode.Short;
        }

        private void CheckedChanged(object sender, EventArgs e)
        {
            string newValue;
            switch (this.Mode)
            {
                case CheckBoxMode.YN:
                    newValue = this.checkBox.Checked ? "Y" : "N";
                    break;
                case CheckBoxMode.YesNo:
                    newValue = this.checkBox.Checked ? "Yes" : "No";
                    break;
                default:
                    newValue = this.checkBox.Checked ? "true" : "false";
                    break;
            }

            this.UpdateDataSource(this.Value, newValue, this.DataField);
        }
    }
}
