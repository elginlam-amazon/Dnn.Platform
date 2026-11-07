// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls.Extensions
{
    using System;
    using System.Linq;

    using DotNetNuke.UI;

    /// <summary>Extension methods for <see cref="DnnListControl"/> instances.</summary>
    public static class ListControlExtensions
    {
        /// <summary>Adds an item into the list.</summary>
        /// <param name="control">The list control.</param>
        /// <param name="text">The item text.</param>
        /// <param name="value">The item value.</param>
        public static void AddItem(this DnnListControl control, string text, string value)
        {
            control.Items.Add(new DnnListItem(text, value));
        }

        /// <summary>Inserts an item into the list.</summary>
        /// <param name="control">The list control.</param>
        /// <param name="index">The location in the collection to insert the item.</param>
        /// <param name="text">The item text.</param>
        /// <param name="value">The item value.</param>
        public static void InsertItem(this DnnListControl control, int index, string text, string value)
        {
            control.Items.Insert(index, new DnnListItem(text, value));
        }

        /// <summary>Binds a data source to the invoked server control and all its child controls.</summary>
        /// <param name="control">The list control.</param>
        /// <param name="initialValue">The initial value.</param>
        public static void DataBind(this DnnListControl control, string initialValue)
        {
            DataBind(control, initialValue, false);
        }

        /// <summary>Binds a data source to the invoked server control and all its child controls.</summary>
        /// <param name="control">The list control.</param>
        /// <param name="initial">The initial value or text.</param>
        /// <param name="findByText">Whether <paramref name="initial"/> is the text or value.</param>
        public static void DataBind(this DnnListControl control, string initial, bool findByText)
        {
            control.DataBind();

            Select(control, initial, findByText);
        }

        /// <summary>Selects an item.</summary>
        /// <param name="control">The list control.</param>
        /// <param name="initial">The item's value.</param>
        public static void Select(this DnnListControl control, string initial)
        {
            Select(control, initial, false, -1);
        }

        /// <summary>Selects an item.</summary>
        /// <param name="control">The list control.</param>
        /// <param name="initial">The item's value or text.</param>
        /// <param name="findByText">Whether <paramref name="initial"/> is the text or value.</param>
        public static void Select(this DnnListControl control, string initial, bool findByText)
        {
            Select(control, initial, findByText, -1);
        }

        /// <summary>Selects an item.</summary>
        /// <param name="control">The list control.</param>
        /// <param name="initial">The item's value or text.</param>
        /// <param name="findByText">Whether <paramref name="initial"/> is the text or value.</param>
        /// <param name="fallbackIndex">The index to select if the item isn't found.</param>
        public static void Select(this DnnListControl control, string initial, bool findByText, int fallbackIndex)
        {
            control.ClearSelection();
            if (findByText)
            {
                if (control.Items.FindByTextWithIgnoreCase(initial) != null)
                {
                    control.Items.FindByTextWithIgnoreCase(initial).Selected = true;
                }
                else if (fallbackIndex > -1)
                {
                    control.Items[fallbackIndex].Selected = true;
                }
            }
            else
            {
                if (control.Items.FindByValueWithIgnoreCase(initial) != null)
                {
                    control.Items.FindByValueWithIgnoreCase(initial).Selected = true;
                }
                else if (fallbackIndex > -1)
                {
                    control.Items[fallbackIndex].Selected = true;
                }
            }
        }

        /// <summary>Use this method instead of DnnListItemCollection.FindByText to find the specific item with case-insensitive.</summary>
        /// <param name="listItems">the items.</param>
        /// <param name="text">the item with this text want to find.</param>
        /// <returns>the specific item or null if didn't match the text with any item.</returns>
        public static DnnListItem FindByTextWithIgnoreCase(this DnnListItemCollection listItems, string text)
        {
            return listItems.FirstOrDefault(item => item.Text != null && item.Text.Equals(text, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Use this method instead of DnnListItemCollection.FindByValue to find the specific item with case-insensitive.</summary>
        /// <param name="listItems">the items.</param>
        /// <param name="value">the item with this value want to find.</param>
        /// <returns>the specific item or null if didn't match the value with any item.</returns>
        public static DnnListItem FindByValueWithIgnoreCase(this DnnListItemCollection listItems, string value)
        {
            return listItems.FirstOrDefault(item => item.Value != null && item.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
        }
    }
}
