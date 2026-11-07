// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Globalization;

    /// <summary>Extension methods for <see cref="TextBoxMode"/>.</summary>
    public static class TextBoxModeExtensions
    {
        /// <summary>Gets the HTML <c>input</c> type (or <c>textarea</c>) a mode renders as.</summary>
        /// <param name="mode">The text box mode.</param>
        /// <returns>The value of the <c>type</c> attribute, or <c>"multiline"</c> for a text area.</returns>
        public static string ToInputType(this TextBoxMode mode)
        {
            switch (mode)
            {
                case TextBoxMode.MultiLine:
                    return "multiline";
                case TextBoxMode.Password:
                    return "password";
                case TextBoxMode.Color:
                    return "color";
                case TextBoxMode.Date:
                    return "date";
                case TextBoxMode.DateTime:
                case TextBoxMode.DateTimeLocal:
                    return "datetime-local";
                case TextBoxMode.Email:
                    return "email";
                case TextBoxMode.Month:
                    return "month";
                case TextBoxMode.Number:
                    return "number";
                case TextBoxMode.Range:
                    return "range";
                case TextBoxMode.Search:
                    return "search";
                case TextBoxMode.Phone:
                    return "tel";
                case TextBoxMode.Time:
                    return "time";
                case TextBoxMode.Url:
                    return "url";
                case TextBoxMode.Week:
                    return "week";
                default:
                    return "text";
            }
        }

        /// <summary>Gets the mode an HTML input type corresponds to.</summary>
        /// <param name="inputType">The value of the <c>type</c> attribute.</param>
        /// <returns>The matching mode.</returns>
        public static TextBoxMode ToTextBoxMode(string inputType)
        {
            if (string.IsNullOrEmpty(inputType))
            {
                return TextBoxMode.SingleLine;
            }

            foreach (TextBoxMode mode in Enum.GetValues<TextBoxMode>())
            {
                if (string.Equals(mode.ToInputType(), inputType, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mode.ToString(), inputType, StringComparison.OrdinalIgnoreCase))
                {
                    return mode;
                }
            }

            return TextBoxMode.SingleLine;
        }

        /// <summary>Formats a pixel count as a CSS length.</summary>
        /// <param name="pixels">The number of pixels.</param>
        /// <returns>The CSS length, or <see langword="null"/> when <paramref name="pixels"/> is not positive.</returns>
        internal static string ToCssPixels(this int pixels)
        {
            return pixels > 0 ? pixels.ToString(CultureInfo.InvariantCulture) + "px" : null;
        }
    }
}
