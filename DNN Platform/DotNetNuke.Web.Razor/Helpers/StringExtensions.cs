// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor.Helpers
{
    using System;
    using System.Globalization;

    /// <summary>String conversion helpers for module razor scripts.</summary>
    /// <remarks>
    /// Replaces the <c>System.Web.WebPages.StringExtensions</c> surface (shipped with
    /// ASP.NET Web Pages, and surfaced to scripts as <c>System.Web.Helpers</c>), which has no
    /// successor on ASP.NET Core. Module scripts — including third-party scripts installed into a
    /// live site and compiled on first use — call <c>value.AsInt()</c>, <c>value.IsEmpty()</c> and
    /// friends, so the methods are reproduced here with the same names, the same overloads and the
    /// same semantics. All parsing is <see cref="CultureInfo"/>-explicit: the Web Pages originals
    /// parsed with the current culture and fell back to the invariant culture, which is reproduced
    /// exactly rather than left to the ambient culture of the request thread.
    /// </remarks>
    public static class StringExtensions
    {
        /// <summary>Determines whether <paramref name="value"/> is <see langword="null"/> or white space.</summary>
        /// <param name="value">The value to test.</param>
        /// <returns><see langword="true"/> if the value is empty, otherwise <see langword="false"/>.</returns>
        public static bool IsEmpty(this string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        /// <summary>Determines whether <paramref name="value"/> can be converted to an <see cref="int"/>.</summary>
        /// <param name="value">The value to test.</param>
        /// <returns><see langword="true"/> if the value is an integer, otherwise <see langword="false"/>.</returns>
        public static bool IsInt(this string value)
        {
            return TryParseInt(value, out _);
        }

        /// <summary>Converts <paramref name="value"/> to an <see cref="int"/>, or <c>0</c> when it is not a number.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>An integer.</returns>
        public static int AsInt(this string value)
        {
            return value.AsInt(0);
        }

        /// <summary>Converts <paramref name="value"/> to an <see cref="int"/>.</summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="defaultValue">The value to return when <paramref name="value"/> is not a number.</param>
        /// <returns>An integer.</returns>
        public static int AsInt(this string value, int defaultValue)
        {
            return TryParseInt(value, out var result) ? result : defaultValue;
        }

        /// <summary>Determines whether <paramref name="value"/> can be converted to a <see cref="bool"/>.</summary>
        /// <param name="value">The value to test.</param>
        /// <returns><see langword="true"/> if the value is a boolean, otherwise <see langword="false"/>.</returns>
        public static bool IsBool(this string value)
        {
            return bool.TryParse(value?.Trim(), out _);
        }

        /// <summary>Converts <paramref name="value"/> to a <see cref="bool"/>, or <see langword="false"/> when it is not a boolean.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A boolean.</returns>
        public static bool AsBool(this string value)
        {
            return value.AsBool(false);
        }

        /// <summary>Converts <paramref name="value"/> to a <see cref="bool"/>.</summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="defaultValue">The value to return when <paramref name="value"/> is not a boolean.</param>
        /// <returns>A boolean.</returns>
        public static bool AsBool(this string value, bool defaultValue)
        {
            return bool.TryParse(value?.Trim(), out var result) ? result : defaultValue;
        }

        /// <summary>Determines whether <paramref name="value"/> can be converted to a <see cref="float"/>.</summary>
        /// <param name="value">The value to test.</param>
        /// <returns><see langword="true"/> if the value is a number, otherwise <see langword="false"/>.</returns>
        public static bool IsFloat(this string value)
        {
            return TryParseFloat(value, out _);
        }

        /// <summary>Converts <paramref name="value"/> to a <see cref="float"/>, or <c>0</c> when it is not a number.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A number.</returns>
        public static float AsFloat(this string value)
        {
            return value.AsFloat(0f);
        }

        /// <summary>Converts <paramref name="value"/> to a <see cref="float"/>.</summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="defaultValue">The value to return when <paramref name="value"/> is not a number.</param>
        /// <returns>A number.</returns>
        public static float AsFloat(this string value, float defaultValue)
        {
            return TryParseFloat(value, out var result) ? result : defaultValue;
        }

        /// <summary>Determines whether <paramref name="value"/> can be converted to a <see cref="decimal"/>.</summary>
        /// <param name="value">The value to test.</param>
        /// <returns><see langword="true"/> if the value is a number, otherwise <see langword="false"/>.</returns>
        public static bool IsDecimal(this string value)
        {
            return TryParseDecimal(value, out _);
        }

        /// <summary>Converts <paramref name="value"/> to a <see cref="decimal"/>, or <c>0</c> when it is not a number.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A number.</returns>
        public static decimal AsDecimal(this string value)
        {
            return value.AsDecimal(0m);
        }

        /// <summary>Converts <paramref name="value"/> to a <see cref="decimal"/>.</summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="defaultValue">The value to return when <paramref name="value"/> is not a number.</param>
        /// <returns>A number.</returns>
        public static decimal AsDecimal(this string value, decimal defaultValue)
        {
            return TryParseDecimal(value, out var result) ? result : defaultValue;
        }

        /// <summary>Determines whether <paramref name="value"/> can be converted to a <see cref="DateTime"/>.</summary>
        /// <param name="value">The value to test.</param>
        /// <returns><see langword="true"/> if the value is a date, otherwise <see langword="false"/>.</returns>
        public static bool IsDateTime(this string value)
        {
            return TryParseDateTime(value, out _);
        }

        /// <summary>Converts <paramref name="value"/> to a <see cref="DateTime"/>, or <see cref="DateTime.MinValue"/> when it is not a date.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A date.</returns>
        public static DateTime AsDateTime(this string value)
        {
            return value.AsDateTime(DateTime.MinValue);
        }

        /// <summary>Converts <paramref name="value"/> to a <see cref="DateTime"/>.</summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="defaultValue">The value to return when <paramref name="value"/> is not a date.</param>
        /// <returns>A date.</returns>
        public static DateTime AsDateTime(this string value, DateTime defaultValue)
        {
            return TryParseDateTime(value, out var result) ? result : defaultValue;
        }

        private static bool TryParseInt(string value, out int result)
        {
            const NumberStyles Styles = NumberStyles.Integer;
            return int.TryParse(value, Styles, CultureInfo.CurrentCulture, out result)
                || int.TryParse(value, Styles, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryParseFloat(string value, out float result)
        {
            const NumberStyles Styles = NumberStyles.Float | NumberStyles.AllowThousands;
            return float.TryParse(value, Styles, CultureInfo.CurrentCulture, out result)
                || float.TryParse(value, Styles, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryParseDecimal(string value, out decimal result)
        {
            const NumberStyles Styles = NumberStyles.Number;
            return decimal.TryParse(value, Styles, CultureInfo.CurrentCulture, out result)
                || decimal.TryParse(value, Styles, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryParseDateTime(string value, out DateTime result)
        {
            const DateTimeStyles Styles = DateTimeStyles.None;
            return DateTime.TryParse(value, CultureInfo.CurrentCulture, Styles, out result)
                || DateTime.TryParse(value, CultureInfo.InvariantCulture, Styles, out result);
        }
    }
}
