// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Lists;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The DNNCountryEditControl control provides a standard UI component for editing Countries.</summary>
    /// <remarks>
    /// <para>
    /// The <c>select</c> is now rendered by the Blazor <see cref="CountryListBox"/> component of the
    /// <c>CountryListBox</c> Razor Class Library, which brings its GeoIP pre-selection with it.
    /// </para>
    /// <para>
    /// <see cref="CountryListBox"/> works in ISO country codes, while this editor stores whatever
    /// <see cref="DNNListEditControl.ValueField"/> says (the DNN <c>Country</c> list entry id by
    /// default). The two value spaces are therefore translated in both directions —
    /// <see cref="BuildDataSource"/>/<see cref="GetSelectedCountryCode"/> on the way in and
    /// <see cref="OnCountryCodeChanged"/> on the way out — so that GeoIP detection can never store a
    /// country code where a list entry id is expected.
    /// </para>
    /// <para>
    /// The region control used to be located by walking the Web Forms control tree
    /// (<c>ControlUtilities.FindControl</c>). There is no control tree on ASP.NET Core, so the link
    /// is now explicit: <see cref="ProfileEditorControl"/> pairs the country and region editors of a
    /// category and assigns <see cref="CoupledRegionControl"/>.
    /// </para>
    /// </remarks>
    public class DNNCountryEditControl : DNNListEditControl
    {
        private readonly ListController listController;

        /// <summary>Initializes a new instance of the <see cref="DNNCountryEditControl"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with ListController. Scheduled removal in v12.0.0.")]
        public DNNCountryEditControl()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNCountryEditControl"/> class.</summary>
        /// <param name="listController">The list controller.</param>
        public DNNCountryEditControl(ListController listController)
            : base(listController, null, null, null)
        {
            this.listController = listController ?? Globals.GetCurrentServiceProvider().GetRequiredService<ListController>();
            this.AutoPostBack = true;
            this.ListName = "Country";
            this.ParentKey = string.Empty;
            this.TextField = ListBoundField.Text;
            this.ValueField = ListBoundField.Id;
            this.ItemChanged += this.OnItemChanged;
            this.SortAlphabetically = true;
        }

        /// <summary>Gets or sets the region editor whose list is driven by the selected country.</summary>
        /// <remarks>Replaces the Web Forms control tree walk; see the remarks on this class.</remarks>
        public DNNRegionEditControl CoupledRegionControl { get; set; }

        /// <summary>Gets or sets the IP address used for the GeoIP country detection.</summary>
        /// <remarks>Forwarded to <see cref="CountryListBox"/>; when it is not set the component reads
        /// the address off the current request, which is only available while the request that serves
        /// the page is being rendered.</remarks>
        [Parameter]
        public string ClientIpAddress { get; set; }

        /// <summary>Gets the stored value as a string for the list entry lookup.</summary>
        private string StringValueForLookup => Convert.ToString(this.Value, CultureInfo.InvariantCulture);

        /// <summary>Builds the option list handed to <see cref="CountryListBox"/>.</summary>
        /// <returns>The countries of the DNN <c>Country</c> list, keyed by their ISO code.</returns>
        protected IEnumerable<CountryListItem> BuildDataSource()
        {
            return this.listController
                .GetListEntryInfoItems("Country", string.Empty, this.PortalId)
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.Text)
                .Select(entry => new CountryListItem(entry.Value, entry.Text))
                .ToList();
        }

        /// <summary>Translates the stored value into the ISO country code <see cref="CountryListBox"/> selects by.</summary>
        /// <returns>The ISO country code, or <see langword="null"/> when nothing is selected.</returns>
        protected string GetSelectedCountryCode()
        {
            var stored = this.StringValueForLookup;
            if (string.IsNullOrEmpty(stored))
            {
                return null;
            }

            var entry = this.FindEntry(stored);
            return entry?.Value;
        }

        /// <inheritdoc />
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            var attributes = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["id"] = this.ClientID,
                ["name"] = this.UniqueID,
                ["data-name"] = this.Name,
                ["data-list"] = "Country",
                ["data-category"] = this.Category,
                ["data-editor"] = "DNNCountryEditControl",
                ["data-required"] = this.Required.ToString().ToLowerInvariant(),
                ["aria-label"] = this.Name,
            };

            builder.OpenComponent<CountryListBox>(0);
            builder.AddComponentParameter(1, nameof(CountryListBox.DataSource), this.BuildDataSource());
            builder.AddComponentParameter(2, nameof(CountryListBox.Value), this.GetSelectedCountryCode());
            builder.AddComponentParameter(
                3,
                nameof(CountryListBox.ValueChanged),
                EventCallback.Factory.Create<string>(this, this.OnCountryCodeChanged));
            builder.AddComponentParameter(4, nameof(CountryListBox.CssClass), this.ControlStyle.CssClass);
            builder.AddComponentParameter(5, nameof(CountryListBox.AdditionalAttributes), (IReadOnlyDictionary<string, object>)attributes);
            if (!string.IsNullOrEmpty(this.ClientIpAddress))
            {
                builder.AddComponentParameter(6, nameof(CountryListBox.ClientIpAddress), this.ClientIpAddress);
            }

            builder.CloseComponent();
        }

        /// <summary>Applies a country code selected in <see cref="CountryListBox"/> to this editor.</summary>
        /// <param name="countryCode">The selected ISO country code.</param>
        private void OnCountryCodeChanged(string countryCode)
        {
            var entry = string.IsNullOrEmpty(countryCode)
                ? null
                : this.listController.GetListEntryInfo("Country", countryCode);

            string postedValue = string.Empty;
            if (entry != null)
            {
                switch (this.ValueField)
                {
                    case ListBoundField.Id:
                        postedValue = entry.EntryID.ToString(CultureInfo.InvariantCulture);
                        break;
                    case ListBoundField.Text:
                        postedValue = entry.Text;
                        break;
                    case ListBoundField.Value:
                        postedValue = entry.Value;
                        break;
                }
            }

            this.SetPostValue(string.Empty, postedValue);
            this.CommitPostData();
            this.RaisePostBackEvent(this.ID);
        }

        /// <summary>Finds the <c>Country</c> list entry matching a stored value.</summary>
        private ListEntryInfo FindEntry(string stored)
        {
            switch (this.ValueField)
            {
                case ListBoundField.Id:
                    return int.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var entryId)
                        ? this.listController.GetListEntryInfo("Country", entryId)
                        : null;
                case ListBoundField.Text:
                    return this.listController
                        .GetListEntryInfoItems("Country", string.Empty, this.PortalId)
                        .FirstOrDefault(e => string.Equals(e.Text, stored, StringComparison.Ordinal));
                default:
                    return this.listController.GetListEntryInfo("Country", stored);
            }
        }

        private void OnItemChanged(object sender, PropertyEditorEventArgs e)
        {
            var regionControl = this.CoupledRegionControl;
            if (regionControl == null)
            {
                return;
            }

            var countries = this.listController.GetListEntryInfoItems("Country");
            foreach (var checkCountry in countries)
            {
                if (checkCountry.EntryID.ToString(CultureInfo.InvariantCulture) == e.StringValue)
                {
                    var attributes = new object[1];
                    attributes[0] = new ListAttribute("Region", "Country." + checkCountry.Value, ListBoundField.Id, ListBoundField.Text);
                    regionControl.CustomAttributes = attributes;
                    break;
                }
            }
        }
    }
}
