// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;

    /// <summary>Specifies the unit of measure of a <see cref="Unit"/>.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.UnitType</c>, which does not exist on ASP.NET Core.
    /// The member names and numeric values are unchanged so persisted/designer values keep working.
    /// </remarks>
    public enum UnitType
    {
        /// <summary>A measurement in pixels.</summary>
        Pixel = 1,

        /// <summary>A measurement in points (1 point == 1/72 inch).</summary>
        Point = 2,

        /// <summary>A measurement in picas (1 pica == 12 points).</summary>
        Pica = 3,

        /// <summary>A measurement in inches.</summary>
        Inch = 4,

        /// <summary>A measurement in millimeters.</summary>
        Mm = 5,

        /// <summary>A measurement in centimeters.</summary>
        Cm = 6,

        /// <summary>A measurement as a percentage of the parent element.</summary>
        Percentage = 7,

        /// <summary>A measurement relative to the height of the parent element's font.</summary>
        Em = 8,

        /// <summary>A measurement relative to the height of the lowercase letter <c>x</c> of the parent element's font.</summary>
        Ex = 9,
    }

    /// <summary>Represents a CSS length (a numeric value plus a unit of measure).</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.Unit</c>, which does not exist on ASP.NET Core. The public
    /// surface that DNN relied upon — <see cref="Empty"/>, <see cref="IsEmpty"/>, <see cref="Value"/>,
    /// <see cref="Type"/>, <see cref="Parse(string, CultureInfo)"/>, <see cref="Pixel"/>,
    /// <see cref="Point"/>, <see cref="Percentage"/>, the <see cref="int"/>/<see cref="double"/>
    /// constructors, equality and <see cref="ToString()"/> — is preserved so that call sites and the
    /// stored CSS strings are unchanged.
    /// </remarks>
    public readonly struct Unit : IEquatable<Unit>
    {
        private readonly double value;
        private readonly UnitType type;

        /// <summary>Initializes a new instance of the <see cref="Unit"/> struct, in pixels.</summary>
        /// <param name="value">The length, in pixels.</param>
        public Unit(int value)
            : this(value, UnitType.Pixel)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Unit"/> struct, in pixels.</summary>
        /// <param name="value">The length, in pixels.</param>
        public Unit(double value)
            : this(value, UnitType.Pixel)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Unit"/> struct.</summary>
        /// <param name="value">The length.</param>
        /// <param name="type">The unit of measure.</param>
        public Unit(double value, UnitType type)
        {
            if (type == UnitType.Pixel || type == UnitType.Point || type == UnitType.Pica)
            {
                this.value = (int)value;
            }
            else
            {
                this.value = value;
            }

            this.type = type;
        }

        /// <summary>Initializes a new instance of the <see cref="Unit"/> struct from a CSS length string.</summary>
        /// <param name="value">The CSS length (for example <c>150px</c> or <c>50%</c>).</param>
        public Unit(string value)
            : this(value, CultureInfo.CurrentCulture)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Unit"/> struct from a CSS length string.</summary>
        /// <param name="value">The CSS length (for example <c>150px</c> or <c>50%</c>).</param>
        /// <param name="culture">The culture used to parse the numeric part.</param>
        public Unit(string value, CultureInfo culture)
        {
            var parsed = Parse(value, culture);
            this.value = parsed.value;
            this.type = parsed.type;
        }

        /// <summary>Gets an empty (unspecified) <see cref="Unit"/>.</summary>
        public static Unit Empty => default;

        /// <summary>Gets a value indicating whether this instance does not specify a length.</summary>
        public bool IsEmpty => this.type == 0;

        /// <summary>Gets the numeric part of the length.</summary>
        public double Value => this.value;

        /// <summary>Gets the unit of measure of the length.</summary>
        public UnitType Type => this.type == 0 ? UnitType.Pixel : this.type;

        /// <summary>Creates a <see cref="Unit"/> measured in pixels.</summary>
        /// <param name="n">The number of pixels.</param>
        /// <returns>A new <see cref="Unit"/>.</returns>
        public static Unit Pixel(int n) => new Unit(n, UnitType.Pixel);

        /// <summary>Creates a <see cref="Unit"/> measured in points.</summary>
        /// <param name="n">The number of points.</param>
        /// <returns>A new <see cref="Unit"/>.</returns>
        public static Unit Point(int n) => new Unit(n, UnitType.Point);

        /// <summary>Creates a <see cref="Unit"/> measured as a percentage.</summary>
        /// <param name="n">The percentage.</param>
        /// <returns>A new <see cref="Unit"/>.</returns>
        public static Unit Percentage(double n) => new Unit(n, UnitType.Percentage);

        /// <summary>Parses a CSS length string.</summary>
        /// <param name="s">The CSS length (for example <c>150px</c> or <c>50%</c>).</param>
        /// <param name="culture">The culture used to parse the numeric part.</param>
        /// <returns>The parsed <see cref="Unit"/>, or <see cref="Empty"/> when <paramref name="s"/> is blank.</returns>
        /// <exception cref="FormatException">The value is not a valid CSS length.</exception>
        public static Unit Parse(string s, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return Empty;
            }

            var trimmed = s.Trim();
            var unitType = UnitType.Pixel;
            var numberLength = trimmed.Length;
            while (numberLength > 0 && !char.IsDigit(trimmed[numberLength - 1]) && trimmed[numberLength - 1] != '.')
            {
                numberLength--;
            }

            if (numberLength == 0)
            {
                throw new FormatException($"'{s}' is not a valid CSS length.");
            }

            var suffix = trimmed.Substring(numberLength).Trim();
            if (suffix.Length > 0)
            {
                unitType = ParseUnitType(suffix, s);
            }

            var number = double.Parse(trimmed.Substring(0, numberLength), NumberStyles.Float, culture ?? CultureInfo.CurrentCulture);
            return new Unit(number, unitType);
        }

        /// <summary>Parses a CSS length string using the current culture.</summary>
        /// <param name="s">The CSS length.</param>
        /// <returns>The parsed <see cref="Unit"/>.</returns>
        public static Unit Parse(string s) => Parse(s, CultureInfo.CurrentCulture);

        /// <summary>Implicitly converts an <see cref="int"/> number of pixels to a <see cref="Unit"/>.</summary>
        /// <param name="n">The number of pixels.</param>
        public static implicit operator Unit(int n) => new Unit(n, UnitType.Pixel);

        /// <summary>Determines whether two lengths are equal.</summary>
        /// <param name="left">The first length.</param>
        /// <param name="right">The second length.</param>
        /// <returns><see langword="true"/> when the lengths are equal.</returns>
        public static bool operator ==(Unit left, Unit right) => left.Equals(right);

        /// <summary>Determines whether two lengths differ.</summary>
        /// <param name="left">The first length.</param>
        /// <param name="right">The second length.</param>
        /// <returns><see langword="true"/> when the lengths differ.</returns>
        public static bool operator !=(Unit left, Unit right) => !left.Equals(right);

        /// <inheritdoc />
        public bool Equals(Unit other) => this.type == other.type && this.value.Equals(other.value);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Unit other && this.Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(this.value, this.type);

        /// <summary>Renders the length as a CSS length string.</summary>
        /// <returns>The CSS length, or an empty string when <see cref="IsEmpty"/>.</returns>
        public override string ToString() => this.ToString(CultureInfo.CurrentCulture);

        /// <summary>Renders the length as a CSS length string.</summary>
        /// <param name="culture">The culture used to format the numeric part.</param>
        /// <returns>The CSS length, or an empty string when <see cref="IsEmpty"/>.</returns>
        public string ToString(CultureInfo culture)
        {
            if (this.IsEmpty)
            {
                return string.Empty;
            }

            culture ??= CultureInfo.CurrentCulture;
            var number = this.type == UnitType.Pixel || this.type == UnitType.Point || this.type == UnitType.Pica
                ? ((int)this.value).ToString(culture)
                : this.value.ToString(culture);
            return number + GetSuffix(this.type);
        }

        private static UnitType ParseUnitType(string suffix, string original)
        {
            switch (suffix.ToLowerInvariant())
            {
                case "px": return UnitType.Pixel;
                case "pt": return UnitType.Point;
                case "pc": return UnitType.Pica;
                case "in": return UnitType.Inch;
                case "mm": return UnitType.Mm;
                case "cm": return UnitType.Cm;
                case "%": return UnitType.Percentage;
                case "em": return UnitType.Em;
                case "ex": return UnitType.Ex;
                default:
                    throw new FormatException($"'{original}' is not a valid CSS length.");
            }
        }

        private static string GetSuffix(UnitType type)
        {
            switch (type)
            {
                case UnitType.Point: return "pt";
                case UnitType.Pica: return "pc";
                case UnitType.Inch: return "in";
                case UnitType.Mm: return "mm";
                case UnitType.Cm: return "cm";
                case UnitType.Percentage: return "%";
                case UnitType.Em: return "em";
                case UnitType.Ex: return "ex";
                default: return "px";
            }
        }
    }
}
