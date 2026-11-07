// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Sites.Components.Dto
{
    using System.Runtime.Serialization;

    /// <summary>
    /// A simple text/value pair. This replaces the WebForms
    /// <c>System.Web.UI.WebControls.ListItem</c> the portal template helpers used to return; the
    /// members carry exactly the same data (<c>Text</c> and <c>Value</c>) and the same serialized
    /// <c>text</c>/<c>value</c> names, so anything reaching a client is unchanged.
    /// </summary>
    [DataContract]
    public class ListItemInfo
    {
        /// <summary>Initializes a new instance of the <see cref="ListItemInfo"/> class.</summary>
        public ListItemInfo()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ListItemInfo"/> class.</summary>
        /// <param name="text">The display text.</param>
        /// <param name="value">The value.</param>
        public ListItemInfo(string text, string value)
        {
            this.Text = text;
            this.Value = value;
        }

        /// <summary>Gets or sets the display text.</summary>
        [DataMember(Name = "text")]
        public string Text { get; set; }

        /// <summary>Gets or sets the value.</summary>
        [DataMember(Name = "value")]
        public string Value { get; set; }
    }
}
