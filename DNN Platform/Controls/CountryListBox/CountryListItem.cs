// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    /// <summary>An item (option) in a <c>CountryListBox</c>, the modern replacement for a Web Forms <c>ListItem</c>.</summary>
    public class CountryListItem
    {
        /// <summary>Initializes a new instance of the <see cref="CountryListItem"/> class.</summary>
        public CountryListItem()
        {
            this.Value = string.Empty;
            this.Text = string.Empty;
        }

        /// <summary>Initializes a new instance of the <see cref="CountryListItem"/> class.</summary>
        /// <param name="value">The item value (an ISO country code).</param>
        /// <param name="text">The item display text (the country name).</param>
        public CountryListItem(string value, string text)
        {
            this.Value = value;
            this.Text = text;
        }

        /// <summary>Gets or sets the item value (an ISO country code).</summary>
        public string Value { get; set; }

        /// <summary>Gets or sets the item display text (the country name).</summary>
        public string Text { get; set; }
    }
}
