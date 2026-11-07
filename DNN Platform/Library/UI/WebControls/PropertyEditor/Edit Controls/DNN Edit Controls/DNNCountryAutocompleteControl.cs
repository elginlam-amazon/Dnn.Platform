// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
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
    using DotNetNuke.Web.Client.ResourceManager;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Lets the user type a country name, looking the matching countries up as they type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>TextBox</c>/<c>HiddenField</c> child controls are now rendered directly, keeping their
    /// <c>{ID}_name</c> and <c>{ID}_id</c> identifiers and their <c>data-*</c> attributes so that
    /// <c>dnn.CountriesRegions.js</c> — which provides the autocomplete and writes the resolved
    /// country id into the hidden field — keeps working unchanged. Both inputs also raise the value
    /// change natively, so the editor still works without the script.
    /// </para>
    /// <para>
    /// The country code lookup still uses <see cref="CountryLookup"/> from the <c>CountryListBox</c>
    /// Razor Class Library. The drop-down flavour of this editor is
    /// <see cref="DNNCountryEditControl"/>, which renders the <see cref="CountryListBox"/> component
    /// itself.
    /// </para>
    /// <para>
    /// The coupled region control used to be located by walking the page's control tree. There is no
    /// control tree on ASP.NET Core, so the link is now explicit through
    /// <see cref="CoupledRegionControl"/>, which <see cref="ProfileEditorControl"/> assigns when it
    /// pairs the country and region editors of a category.
    /// </para>
    /// </remarks>
    public class DnnCountryAutocompleteControl : EditControl
    {
        private readonly IServicesFramework servicesFramework;
        private readonly ListController listController;
        private readonly IClientResourceController clientResourceController;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;
        private readonly IPortalController portalController;

        /// <summary>Initializes a new instance of the <see cref="DnnCountryAutocompleteControl"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public DnnCountryAutocompleteControl()
            : this(null, null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnCountryAutocompleteControl"/> class.</summary>
        /// <param name="servicesFramework">The web API service framework.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with ListController. Scheduled removal in v12.0.0.")]
        public DnnCountryAutocompleteControl(IServicesFramework servicesFramework)
            : this(servicesFramework, null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnCountryAutocompleteControl"/> class.</summary>
        /// <param name="servicesFramework">The web API service framework.</param>
        /// <param name="listController">The list controller.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="portalController">The portal controller.</param>
        public DnnCountryAutocompleteControl(IServicesFramework servicesFramework, ListController listController, IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IPortalController portalController)
        {
            this.servicesFramework = servicesFramework ?? Globals.GetCurrentServiceProvider().GetRequiredService<IServicesFramework>();
            this.listController = listController ?? Globals.GetCurrentServiceProvider().GetRequiredService<ListController>();
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            this.portalController = portalController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>();
        }

        /// <summary>Initializes a new instance of the <see cref="DnnCountryAutocompleteControl"/> class.</summary>
        /// <param name="type">A string representing the <see cref="Type"/> being edited.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public DnnCountryAutocompleteControl(string type)
            : this(type, null, null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnCountryAutocompleteControl"/> class.</summary>
        /// <param name="type">A string representing the <see cref="Type"/> being edited.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with ListController. Scheduled removal in v12.0.0.")]
        public DnnCountryAutocompleteControl(string type, IServicesFramework servicesFramework)
            : this(type, servicesFramework, null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnCountryAutocompleteControl"/> class.</summary>
        /// <param name="type">A string representing the <see cref="Type"/> being edited.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        /// <param name="listController">The list controller.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="portalController">The portal controller.</param>
        public DnnCountryAutocompleteControl(string type, IServicesFramework servicesFramework, ListController listController, IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IPortalController portalController)
            : this(servicesFramework, listController, clientResourceController, appStatus, eventLogger, portalController)
        {
            this.SystemType = type;
        }

        /// <inheritdoc />
        public override string EditControlClientId => this.ClientID + "_name";

        /// <summary>Gets or sets the region editor whose list is driven by the selected country.</summary>
        public DNNRegionEditControl CoupledRegionControl { get; set; }

        /// <summary>Gets the old value expressed as a string.</summary>
        protected string OldStringValue => Convert.ToString(this.OldValue, CultureInfo.InvariantCulture);

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

        /// <summary>Gets the country name rendered into the autocomplete input.</summary>
        private string CountryText
        {
            get
            {
                if (!string.IsNullOrEmpty(this.StringValue) && int.TryParse(this.StringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var countryId))
                {
                    var entry = this.listController.GetListEntryInfo(countryId);
                    if (entry != null)
                    {
                        return entry.Text;
                    }
                }

                return this.StringValue;
            }
        }

        /// <inheritdoc />
        public override bool LoadPostData(string postDataKey, System.Collections.Specialized.NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            bool dataChanged = false;
            string presentValue = this.StringValue;
            string postedValue = postCollection[postDataKey + "_id"];
            if (!presentValue.Equals(postedValue, StringComparison.Ordinal))
            {
                this.Value = postedValue;
                dataChanged = true;
            }

            return dataChanged;
        }

        /// <inheritdoc />
        protected override void OnDataChanged(EventArgs e)
        {
            PropertyEditorEventArgs args = new PropertyEditorEventArgs(this.Name);
            args.Value = this.StringValue;
            args.OldValue = this.OldStringValue;
            args.StringValue = this.StringValue;
            this.OnValueChanged(args);
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.servicesFramework.RequestAjaxAntiForgerySupport();
            this.clientResourceController.RegisterScript("~/resources/Shared/components/CountriesRegions/dnn.CountriesRegions.js");
            this.clientResourceController.CreateStylesheet("~/resources/Shared/components/CountriesRegions/dnn.CountriesRegions.css").SetPriority(FileOrder.Css.FeatureCss).Register();
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.portalController.GetCurrentSettings(), CommonJs.jQuery);
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.portalController.GetCurrentSettings(), CommonJs.jQueryUI);
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            this.LoadControls();
        }

        /// <inheritdoc />
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // The country name the autocomplete operates on.
            builder.OpenElement(0, "input");
            this.AddStyleAttributes(builder, 1);
            builder.AddAttribute(5, "type", "text");
            builder.AddAttribute(6, "id", this.ClientID + "_name");
            builder.AddAttribute(7, "name", this.UniqueID + "_name");
            builder.AddAttribute(8, "value", this.CountryText);
            builder.AddAttribute(9, "data-name", this.Name);
            builder.AddAttribute(10, "data-list", "Country");
            builder.AddAttribute(11, "data-category", this.Category);
            builder.AddAttribute(12, "data-editor", "DnnCountryAutocompleteControl");
            builder.AddAttribute(13, "data-required", this.Required.ToString().ToLowerInvariant());
            builder.AddAttribute(14, "aria-label", this.Name);
            builder.AddAttribute(15, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnCountryNameChanged));
            builder.CloseElement();

            // The hidden field holding the resolved country id, which is the stored value.
            builder.OpenElement(20, "input");
            builder.AddAttribute(21, "type", "hidden");
            builder.AddAttribute(22, "id", this.ClientID + "_id");
            builder.AddAttribute(23, "name", this.UniqueID + "_id");
            builder.AddAttribute(24, "value", this.StringValue);
            builder.AddAttribute(25, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnCountryIdChanged));
            builder.CloseElement();
        }

        /// <summary>Points the coupled region editor at the region list of the selected country.</summary>
        /// <remarks>Replaces the <c>LoadControls</c> method, which assigned the text of the
        /// <c>TextBox</c>, the value of the <c>HiddenField</c> (now both rendered directly) and the
        /// <c>ParentKey</c> of the region control it found in the page's control tree.</remarks>
        private void LoadControls()
        {
            var regionControl = this.CoupledRegionControl;
            if (regionControl == null)
            {
                return;
            }

            string countryCode = CountryLookup.CodeByName(this.StringValue);
            if (!string.IsNullOrEmpty(this.StringValue) && int.TryParse(this.StringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var countryId))
            {
                var entry = this.listController.GetListEntryInfo(countryId);
                if (entry != null)
                {
                    countryCode = entry.Value;
                }
            }

            regionControl.ParentKey = "Country." + countryCode;
        }

        private void OnCountryIdChanged(ChangeEventArgs args)
        {
            this.SetPostValue("_id", args?.Value?.ToString());
            this.CommitPostData();
        }

        private void OnCountryNameChanged(ChangeEventArgs args)
        {
            // Without the autocomplete script the user types a name, so resolve it to the list entry
            // id that is stored, exactly as the script does before writing the hidden field.
            var countryName = args?.Value?.ToString();
            var entry = string.IsNullOrEmpty(countryName)
                ? null
                : this.listController.GetListEntryInfoItems("Country")
                    .FirstOrDefault(item => string.Equals(item.Text, countryName, StringComparison.OrdinalIgnoreCase));

            this.SetPostValue("_id", entry == null ? string.Empty : entry.EntryID.ToString(CultureInfo.InvariantCulture));
            this.CommitPostData();
        }
    }
}
