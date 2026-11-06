// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    /// <summary>A radio button control.</summary>
    /// <remarks>
    /// The <c>RadioButtonList</c> became a <see cref="DnnRadioButtonList"/>, which keeps the same
    /// item/selection object model. The posted value used to be read from
    /// <c>Request.Form[UniqueID]</c> on a postback; the component holds the live selection instead,
    /// so the item's own value seeds the selection the first time the list is populated and the live
    /// selection is honoured afterwards.
    /// </remarks>
    public class DnnFormRadioButtonListItem : DnnFormListItemBase
    {
        private DnnRadioButtonList radioButtonList;

        /// <summary>Initializes a new instance of the <see cref="DnnFormRadioButtonListItem"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo. Scheduled removal in v12.0.0.")]
        public DnnFormRadioButtonListItem()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnFormRadioButtonListItem"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        public DnnFormRadioButtonListItem(IApplicationStatusInfo appStatus, IEventLogger eventLogger)
            : base(appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>(), eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>())
        {
        }

        /// <summary>Gets or sets the JavaScript runtime used to run the item's client click script.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        protected override void BindList()
        {
            if (this.radioButtonList != null)
            {
                string selectedValue = this.radioButtonList.Items.Count == 0
                    ? Convert.ToString(this.Value, CultureInfo.InvariantCulture)
                    : this.radioButtonList.SelectedValue;

                if (this.ListSource is Dictionary<string, string> items)
                {
                    foreach (var item in items)
                    {
                        this.radioButtonList.Items.Add(new DotNetNuke.UI.DnnListItem(item.Key, item.Value));
                    }
                }
                else
                {
                    this.radioButtonList.DataTextField = this.ListTextField;
                    this.radioButtonList.DataValueField = this.ListValueField;
                    this.radioButtonList.DataSource = this.ListSource;

                    this.radioButtonList.DataBind();
                }

                if (string.IsNullOrEmpty(selectedValue))
                {
                    selectedValue = this.DefaultValue;
                }

                // Reset SelectedValue
                var selectedItem = this.radioButtonList.Items.FindByValue(selectedValue);
                if (selectedItem != null)
                {
                    this.radioButtonList.ClearSelection();
                    selectedItem.Selected = true;
                }

                if (selectedValue != Convert.ToString(this.Value, CultureInfo.InvariantCulture))
                {
                    this.UpdateDataSource(this.Value, selectedValue, this.DataField);
                }
            }
        }
        #pragma warning restore BL0005

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        protected override IDnnControl CreateControlInternal(IDnnControl container)
        {
            this.radioButtonList = new DnnRadioButtonList { ID = this.ID + "_RadioButtonList", RepeatDirection = "Vertical", };

            // the click handler the Web Forms version attached to every list item is a single
            // selection-changed handler here, which is what the inline script ultimately triggered.
            this.radioButtonList.SelectedIndexChanged += this.SelectionChanged;

            container?.Controls.Add(this.radioButtonList);

            if (this.ListSource != null)
            {
                this.BindList();
            }

            return this.radioButtonList;
        }
        #pragma warning restore BL0005

        // async void is the standard shape for a synchronous EventHandler that has to await an
        // interop call; the JS invocation must be awaited so that its result/exception is observed.
        private async void SelectionChanged(object sender, EventArgs e)
        {
            this.UpdateDataSource(this.Value, this.radioButtonList.SelectedValue, this.DataField);

            // the Web Forms version put OnClientClicked on every list item's onClick attribute.
            if (!string.IsNullOrEmpty(this.OnClientClicked) && this.JsRuntime != null)
            {
                await this.JsRuntime.InvokeAsync<bool>("dnn.runClientClick", this.OnClientClicked);
            }
        }
    }
}
