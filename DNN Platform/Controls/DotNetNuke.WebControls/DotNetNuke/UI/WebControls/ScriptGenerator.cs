// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.Globalization;

    /// <summary>
    /// Reflects over the <see cref="ClientPropertyAttribute"/>, <see cref="ClientEventAttribute"/>
    /// and <see cref="ClientPropertyNameAttribute"/> annotations of an object and marshals the
    /// annotated values to the client.
    /// </summary>
    /// <remarks>
    /// The <c>System.Drawing</c> coupling of the original (<c>ColorTranslator.ToHtml</c>) is gone:
    /// colours are recognized and formatted by reflection so that no drawing assembly is needed,
    /// producing the same <c>#rrggbb</c> / named-colour strings. The <c>System.Web.UI</c> coupling
    /// is replaced by this project's <see cref="UrlPropertyAttribute"/> and
    /// <see cref="IUrlResolutionService"/>.
    /// </remarks>
    public static class ScriptGenerator
    {
        /// <summary>Collects the client side properties of an object.</summary>
        /// <param name="instance">The object to reflect over.</param>
        /// <returns>The collected properties.</returns>
        public static Hashtable GetMarshalledProperties(object instance)
        {
            return GetMarshalledProperties(instance, new Hashtable());
        }

        /// <summary>Collects the client side properties of an object into an existing table.</summary>
        /// <param name="instance">The object to reflect over.</param>
        /// <param name="properties">The table to add the properties to.</param>
        /// <returns>The table the properties were added to.</returns>
        public static Hashtable GetMarshalledProperties(object instance, Hashtable properties)
        {
            ArgumentNullException.ThrowIfNull(properties);

            var service = instance as IUrlResolutionService;
            var descriptors = TypeDescriptor.GetProperties(instance);
            foreach (PropertyDescriptor current in descriptors)
            {
                var clientEvent = (ClientEventAttribute)current.Attributes[typeof(ClientEventAttribute)];
                var clientProperty = (ClientPropertyAttribute)current.Attributes[typeof(ClientPropertyAttribute)];
                if (clientProperty is null && clientEvent is null)
                {
                    continue;
                }

                var key = current.Name;
                var nameAttribute = (ClientPropertyNameAttribute)current.Attributes[typeof(ClientPropertyNameAttribute)];
                if (nameAttribute is not null)
                {
                    key = nameAttribute.Name;
                }

                if (!current.ShouldSerializeValue(instance) && !current.IsReadOnly)
                {
                    continue;
                }

                var value = current.GetValue(instance);
                if (clientEvent is not null && current.PropertyType != typeof(string))
                {
                    throw new InvalidOperationException("Events properties can only be of type string");
                }

                if (!current.PropertyType.IsPrimitive && !current.PropertyType.IsEnum && IsColorType(current.PropertyType))
                {
                    value = ToHtmlColor(value);
                }

                if (current.PropertyType == typeof(bool))
                {
                    value = Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? "1" : "0";
                }

                if (current.Attributes[typeof(UrlPropertyAttribute)] is not null && service is not null && value is not null)
                {
                    var url = Convert.ToString(value, CultureInfo.InvariantCulture);
                    value = instance is BaseWebControl control ? control.ResolveUrl(url) : service.ResolveClientUrl(url);
                }

                if (current.PropertyType == typeof(char))
                {
                    // The original used Strings.Asc(...) <> 0, i.e. it skipped the null character.
                    if (value is char character && character != '\0')
                    {
                        properties.Add(key, value);
                    }
                }
                else if (value is not null)
                {
                    properties.Add(key, clientEvent is not null ? Convert.ToString(value, CultureInfo.InvariantCulture) : value);
                }
            }

            return properties;
        }

        /// <summary>Serializes the client side properties of an object to JSON.</summary>
        /// <param name="instance">The object to reflect over.</param>
        /// <returns>The JSON object literal of the properties.</returns>
        public static string GetMarshalledPropertyJSON(object instance)
        {
            return GetMarshalledPropertyJSON(instance, new Hashtable());
        }

        /// <summary>Serializes the client side properties of an object to JSON.</summary>
        /// <param name="instance">The object to reflect over.</param>
        /// <param name="properties">The table to add the properties to first.</param>
        /// <returns>The JSON object literal of the properties.</returns>
        public static string GetMarshalledPropertyJSON(object instance, Hashtable properties)
        {
            return WebControls.ToJSON(GetMarshalledProperties(instance, properties));
        }

        /// <summary>Determines whether the given type is <c>System.Drawing.Color</c>.</summary>
        /// <param name="type">The property type to test.</param>
        /// <returns><see langword="true"/> when the type is a colour.</returns>
        private static bool IsColorType(Type type)
        {
            return string.Equals(type?.FullName, "System.Drawing.Color", StringComparison.Ordinal);
        }

        /// <summary>
        /// Formats a colour the way <c>ColorTranslator.ToHtml</c> did, by reflection.
        /// </summary>
        /// <param name="value">The colour value, which may be <see langword="null"/>.</param>
        /// <returns>The HTML representation of the colour.</returns>
        private static string ToHtmlColor(object value)
        {
            if (value is null)
            {
                // The original substituted the default (empty) colour, which translates to "".
                return string.Empty;
            }

            var type = value.GetType();
            var isKnown = type.GetProperty("IsKnownColor")?.GetValue(value) as bool?;
            if (isKnown == true)
            {
                return Convert.ToString(type.GetProperty("Name")?.GetValue(value), CultureInfo.InvariantCulture);
            }

            // ColorTranslator.ToHtml returns "" only for Color.Empty, NOT for every colour whose
            // alpha channel is zero. Test emptiness via IsEmpty so a defined-but-transparent
            // colour (e.g. Color.FromArgb(0, 255, 0, 0)) still formats as #RRGGBB, as before.
            var isEmpty = type.GetProperty("IsEmpty")?.GetValue(value) as bool?;
            if (isEmpty == true)
            {
                return string.Empty;
            }

            var red = Convert.ToInt32(type.GetProperty("R")?.GetValue(value) ?? 0, CultureInfo.InvariantCulture);
            var green = Convert.ToInt32(type.GetProperty("G")?.GetValue(value) ?? 0, CultureInfo.InvariantCulture);
            var blue = Convert.ToInt32(type.GetProperty("B")?.GetValue(value) ?? 0, CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", red, green, blue);
        }
    }
}
