// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>The items of a <see cref="DnnListControl"/>.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.ListItemCollection</c>.</remarks>
    public class DnnListItemCollection : Collection<DnnListItem>
    {
        /// <summary>Finds the first item whose text matches exactly.</summary>
        /// <param name="text">The text to look for.</param>
        /// <returns>The item, or <see langword="null"/>.</returns>
        public DnnListItem FindByText(string text)
        {
            return this.FirstOrDefault(item => string.Equals(item.Text, text, StringComparison.Ordinal));
        }

        /// <summary>Finds the first item whose value matches exactly.</summary>
        /// <param name="value">The value to look for.</param>
        /// <returns>The item, or <see langword="null"/>.</returns>
        public DnnListItem FindByValue(string value)
        {
            return this.FirstOrDefault(item => string.Equals(item.Value, value, StringComparison.Ordinal));
        }

        /// <summary>Adds an item with the given text and value.</summary>
        /// <param name="text">The item text.</param>
        /// <param name="value">The item value.</param>
        public void Add(string text, string value)
        {
            this.Add(new DnnListItem(text, value));
        }
    }
}
