// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.ContactList.Mvc.Models
{
    using System.ComponentModel;

    using Newtonsoft.Json;

    /// <summary>
    /// Settings class manages the settings for the module instance.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Settings"/> class.
        /// </summary>
        public Settings()
        {
            this.PageSize = 10;
            this.AllowContactCreation = true;
        }

        /// <summary>
        /// Gets or sets the number of contacts to show per page.
        /// </summary>
        [DisplayName("Page Size")]
        [JsonProperty(nameof(PageSize))]
        public int PageSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether users can add/edit the contact.
        /// </summary>
        [DisplayName("Allow contact creation.")]
        [JsonProperty(nameof(AllowContactCreation))]
        public bool AllowContactCreation { get; set; }
    }
}
