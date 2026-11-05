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
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Lists;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Client.ResourceManager;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The DNNRegionEditControl control provides a standard UI component for editing Regions.</summary>
    /// <remarks>
    /// The <c>DropDownList</c>/<c>TextBox</c>/<c>HtmlInputHidden</c> child controls are now rendered
    /// directly, keeping their <c>{ID}_dropdown</c>, <c>{ID}_text</c> and <c>{ID}_value</c>
    /// identifiers and their <c>data-editor</c> attributes, so that
    /// <c>dnn.CountriesRegions.js</c> — which swaps the drop down for the text box depending on
    /// whether the selected country has regions — keeps working unchanged. The drop down and the
    /// text box additionally raise the value change natively, so the editor also works without the
    /// script.
    /// </remarks>
    public class DNNRegionEditControl : EditControl
    {
        private readonly IClientResourceController clientResourceController;
        private readonly IServicesFramework servicesFramework;
        private readonly ListController listController;
        private readonly IPortalController portalController;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IPortalGroupController portalGroupController;
        private readonly IEventLogger eventLogger;
        private List<ListEntryInfo> listEntries;

        /// <summary>Initializes a new instance of the <see cref="DNNRegionEditControl"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public DNNRegionEditControl()
            : this(null, null, null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNRegionEditControl"/> class.</summary>
        /// <param name="servicesFramework">The web API service framework.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with ListController. Scheduled removal in v12.0.0.")]
        public DNNRegionEditControl(IServicesFramework servicesFramework)
            : this(servicesFramework, null, null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNRegionEditControl"/> class.</summary>
        /// <param name="servicesFramework">The web API service framework.</param>
        /// <param name="listController">The list controller.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="portalGroupController">The portal group controller.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        public DNNRegionEditControl(IServicesFramework servicesFramework, ListController listController, IPortalController portalController, IApplicationStatusInfo appStatus, IPortalGroupController portalGroupController, IEventLogger eventLogger, IClientResourceController clientResourceController)
        {
            this.servicesFramework = servicesFramework ?? Globals.GetCurrentServiceProvider().GetRequiredService<IServicesFramework>();
            this.listController = listController ?? Globals.GetCurrentServiceProvider().GetRequiredService<ListController>();
            this.portalController = portalController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>();
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.portalGroupController = portalGroupController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalGroupController>();
            this.eventLogger = eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
        }

        /// <summary>Initializes a new instance of the <see cref="DNNRegionEditControl"/> class.</summary>
        /// <param name="type">A string representing the <see cref="Type"/> being edited.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public DNNRegionEditControl(string type)
            : this(type, null, null, null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNRegionEditControl"/> class.</summary>
        /// <param name="type">A string representing the <see cref="Type"/> being edited.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with ListController. Scheduled removal in v12.0.0.")]
        public DNNRegionEditControl(string type, IServicesFramework servicesFramework)
            : this(type, servicesFramework, null, null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNRegionEditControl"/> class.</summary>
        /// <param name="type">A string representing the <see cref="Type"/> being edited.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        /// <param name="listController">The list controller.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="portalGroupController">The portal group controller.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        public DNNRegionEditControl(string type, IServicesFramework servicesFramework, ListController listController, IPortalController portalController, IApplicationStatusInfo appStatus, IPortalGroupController portalGroupController, IEventLogger eventLogger, IClientResourceController clientResourceController)
            : this(servicesFramework, listController, portalController, appStatus, portalGroupController, eventLogger, clientResourceController)
        {
            this.SystemType = type;
        }

        /// <summary>Gets or sets the parent key of the List to display.</summary>
        public string ParentKey { get; set; }

        /// <inheritdoc />
        public override string EditControlClientId => this.ClientID + "_dropdown";

        /// <summary>Gets the old value expressed as a string.</summary>
        protected string OldStringValue => Convert.ToString(this.OldValue, CultureInfo.InvariantCulture);

        /// <summary>Gets the ListEntryInfo objects associated with the control.</summary>
        protected IEnumerable<ListEntryInfo> ListEntries
        {
            get
            {
                if (this.listEntries == null)
                {
                    this.listEntries = this.listController.GetListEntryInfoItems("Region", this.ParentKey, this.PortalId).OrderBy(s => s.SortOrder).ThenBy(s => s.Text).ToList();
                }

                return this.listEntries;
            }
        }

        /// <summary>Gets the effective portal id the region list is read for.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected int PortalId => PortalController.GetEffectivePortalId(this.portalController, this.appStatus, this.portalGroupController, PortalSettings.Current.PortalId);

        /// <inheritdoc />
        protected override string StringValue
        {
            get
            {
                string strValue = Null.NullString;
                if (this.Value != null)
                {
                    strValue = Convert.ToString(this.Value, CultureInfo.InvariantCulture);
                }

                return strValue;
            }

            set
            {
                this.Value = value;
            }
        }

        /// <inheritdoc />
        public override bool LoadPostData(string postDataKey, System.Collections.Specialized.NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            bool dataChanged = false;
            string presentValue = this.StringValue;
            string postedValue = postCollection[postDataKey + "_value"];
            if (!presentValue.Equals(postedValue, StringComparison.Ordinal))
            {
                this.Value = postedValue;
                dataChanged = true;
            }

            return dataChanged;
        }

        /// <summary>OnAttributesChanged runs when the CustomAttributes property has changed.</summary>
        protected override void OnAttributesChanged()
        {
            // Get the List settings out of the "Attributes"
            if (this.CustomAttributes != null)
            {
                foreach (Attribute attribute in this.CustomAttributes)
                {
                    if (attribute is ListAttribute listAtt)
                    {
                        this.ParentKey = listAtt.ParentKey;
                        this.listEntries = null;
                        break;
                    }
                }
            }
        }

        /// <inheritdoc />
        protected override void OnDataChanged(EventArgs e)
        {
            PropertyEditorEventArgs args = new PropertyEditorEventArgs(this.Name)
            {
                Value = this.StringValue,
                OldValue = this.OldStringValue,
                StringValue = this.StringValue,
            };
            this.OnValueChanged(args);
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.servicesFramework.RequestAjaxAntiForgerySupport();
            this.clientResourceController.RegisterScript("~/Resources/Shared/components/CountriesRegions/dnn.CountriesRegions.js");
            this.clientResourceController
                .CreateStylesheet("~/Resources/Shared/components/CountriesRegions/dnn.CountriesRegions.css")
                .SetPriority(FileOrder.Css.FeatureCss)
                .Register();
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.portalController.GetCurrentSettings(), CommonJs.jQuery);
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.portalController.GetCurrentSettings(), CommonJs.jQueryUI);
        }

        /// <inheritdoc />
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "div");
            this.AddStyleAttributes(builder, 1);
            builder.AddAttribute(5, "data-name", this.Name);
            builder.AddAttribute(6, "data-list", "Region");
            builder.AddAttribute(7, "data-category", this.Category);
            builder.AddAttribute(8, "data-required", this.Required.ToString().ToLowerInvariant());

            // The region drop down, shown when the selected country has a region list.
            builder.OpenElement(10, "select");
            this.AddStyleAttributes(builder, 11);
            builder.AddAttribute(15, "id", this.ClientID + "_dropdown");
            builder.AddAttribute(16, "name", this.UniqueID + "_dropdown");
            builder.AddAttribute(17, "data-editor", "DNNRegionEditControl_DropDown");
            builder.AddAttribute(18, "aria-label", "Region");
            builder.AddAttribute(19, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnRegionChanged));

            builder.OpenElement(20, "option");
            builder.AddAttribute(21, "value", string.Empty);
            if (string.IsNullOrEmpty(this.StringValue))
            {
                builder.AddAttribute(22, "selected", "selected");
            }

            builder.AddContent(23, "<" + Localization.GetString("Not_Specified", Localization.SharedResourceFile) + ">");
            builder.CloseElement();

            builder.OpenRegion(30);
            if (this.ListEntries != null && this.ListEntries.Any())
            {
                foreach (ListEntryInfo item in this.ListEntries)
                {
                    var itemValue = item.EntryID.ToString(CultureInfo.InvariantCulture);
                    builder.OpenElement(0, "option");
                    builder.AddAttribute(1, "value", itemValue);
                    if (string.Equals(this.StringValue, itemValue, StringComparison.Ordinal))
                    {
                        builder.AddAttribute(2, "selected", "selected");
                    }

                    builder.AddContent(3, item.Text);
                    builder.CloseElement();
                }
            }

            builder.CloseRegion();
            builder.CloseElement();

            // The free-text region, shown when the selected country has no region list.
            builder.OpenElement(40, "input");
            this.AddStyleAttributes(builder, 41);
            builder.AddAttribute(45, "type", "text");
            builder.AddAttribute(46, "id", this.ClientID + "_text");
            builder.AddAttribute(47, "name", this.UniqueID + "_text");
            builder.AddAttribute(48, "data-editor", "DNNRegionEditControl_Text");
            builder.AddAttribute(49, "aria-label", "Region");
            builder.AddAttribute(50, "value", this.StringValue);
            builder.AddAttribute(51, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnRegionChanged));
            builder.CloseElement();

            // The hidden field that dnn.CountriesRegions.js writes the resolved value into.
            builder.OpenElement(60, "input");
            builder.AddAttribute(61, "type", "hidden");
            builder.AddAttribute(62, "id", this.ClientID + "_value");
            builder.AddAttribute(63, "name", this.UniqueID + "_value");
            builder.AddAttribute(64, "data-editor", "DNNRegionEditControl_Hidden");
            builder.AddAttribute(65, "value", this.StringValue);
            builder.AddAttribute(66, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnRegionChanged));
            builder.CloseElement();

            builder.CloseElement();
        }

        private void OnRegionChanged(ChangeEventArgs args)
        {
            this.SetPostValue("_value", args?.Value?.ToString());
            this.CommitPostData();
        }
    }
}
