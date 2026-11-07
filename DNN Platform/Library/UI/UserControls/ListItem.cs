// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.UserControls
{
    using System;

    /// <summary>An item (option) rendered by one of the DNN user controls.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.ListItem</c>, which does not exist on ASP.NET Core.
    /// The member shape (<see cref="Text"/>, <see cref="Value"/>, <see cref="Selected"/>,
    /// <see cref="Enabled"/> and the two constructors) is kept source compatible with it so the
    /// control logic that was written against <c>ListItem</c> is preserved verbatim.
    /// </remarks>
    public class ListItem
    {
        /// <summary>Initializes a new instance of the <see cref="ListItem"/> class.</summary>
        public ListItem()
            : this(string.Empty, string.Empty)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ListItem"/> class.</summary>
        /// <param name="text">The display text of the item.</param>
        public ListItem(string text)
            : this(text, text)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ListItem"/> class.</summary>
        /// <param name="text">The display text of the item.</param>
        /// <param name="value">The value of the item.</param>
        public ListItem(string text, string value)
        {
            this.Text = text;
            this.Value = value;
            this.Enabled = true;
        }

        /// <summary>Gets or sets the display text of the item.</summary>
        public string Text { get; set; }

        /// <summary>Gets or sets the value of the item.</summary>
        public string Value { get; set; }

        /// <summary>Gets or sets a value indicating whether the item is selected.</summary>
        public bool Selected { get; set; }

        /// <summary>Gets or sets a value indicating whether the item can be selected.</summary>
        public bool Enabled { get; set; }

        /// <inheritdoc/>
        public override string ToString() => this.Text ?? string.Empty;
    }

    /// <summary>Extension methods that replace the <c>ListItemCollection</c> lookup helpers.</summary>
    /// <remarks>
    /// <c>System.Web.UI.WebControls.ListItemCollection</c> is gone; the control logic used its
    /// <c>FindByText</c>/<c>FindByValue</c> members, which are provided here over a plain list so
    /// that the ported logic reads the same.
    /// </remarks>
    public static class ListItemCollectionExtensions
    {
        /// <summary>Finds the first item with the given display text.</summary>
        /// <param name="items">The items to search.</param>
        /// <param name="text">The display text to look for.</param>
        /// <returns>The matching item, or <see langword="null"/> when there is none.</returns>
        public static ListItem FindByText(this System.Collections.Generic.IEnumerable<ListItem> items, string text)
        {
            if (items is null)
            {
                return null;
            }

            foreach (var item in items)
            {
                if (string.Equals(item.Text, text, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>Finds the first item with the given value.</summary>
        /// <param name="items">The items to search.</param>
        /// <param name="value">The value to look for.</param>
        /// <returns>The matching item, or <see langword="null"/> when there is none.</returns>
        public static ListItem FindByValue(this System.Collections.Generic.IEnumerable<ListItem> items, string value)
        {
            if (items is null)
            {
                return null;
            }

            foreach (var item in items)
            {
                if (string.Equals(item.Value, value, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>Clears the selection of every item in the collection.</summary>
        /// <param name="items">The items to clear the selection of.</param>
        public static void ClearSelection(this System.Collections.Generic.IEnumerable<ListItem> items)
        {
            if (items is null)
            {
                return;
            }

            foreach (var item in items)
            {
                item.Selected = false;
            }
        }
    }
}
