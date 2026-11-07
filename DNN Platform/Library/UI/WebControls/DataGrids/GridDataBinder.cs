// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Reflection;

    /// <summary>Evaluates a field of a data item by name.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.DataBinder.Eval</c>, which lives in the WebForms assembly. The
    /// resolution order mirrors it: <see cref="DataRowView"/>/<see cref="DataRow"/> columns first
    /// (DNN binds grids to <see cref="DataTable"/>s), then dictionaries, then public properties and
    /// fields, walking dotted paths one segment at a time.
    /// </remarks>
    public static class GridDataBinder
    {
        /// <summary>Evaluates a field of a data item.</summary>
        /// <param name="container">The data item.</param>
        /// <param name="expression">The field name, which may be a dotted path.</param>
        /// <returns>The value of the field, or <see langword="null"/>.</returns>
        /// <exception cref="ArgumentException">The field does not exist on <paramref name="container"/>.</exception>
        public static object Eval(object container, string expression)
        {
            if (container == null || string.IsNullOrEmpty(expression))
            {
                return null;
            }

            var current = container;
            foreach (var segment in expression.Split('.'))
            {
                if (current == null)
                {
                    return null;
                }

                current = EvalSegment(current, segment);
            }

            return current;
        }

        /// <summary>Evaluates a field of a data item and formats it.</summary>
        /// <param name="container">The data item.</param>
        /// <param name="expression">The field name, which may be a dotted path.</param>
        /// <param name="format">The format string, which may be empty.</param>
        /// <returns>The formatted value.</returns>
        public static string Eval(object container, string expression, string format)
        {
            var value = Eval(container, expression);
            if (string.IsNullOrEmpty(format))
            {
                return value == null ? string.Empty : Convert.ToString(value, CultureInfo.CurrentCulture);
            }

            return string.Format(CultureInfo.CurrentCulture, format, value);
        }

        private static object EvalSegment(object current, string name)
        {
            switch (current)
            {
                case DataRowView rowView:
                    return rowView[name];
                case DataRow row:
                    return row[name];
                case IDictionary<string, object> typedDictionary:
                    return typedDictionary.TryGetValue(name, out var typedValue) ? typedValue : null;
                case IDictionary dictionary:
                    return dictionary[name];
            }

            var type = current.GetType();
            const BindingFlags Bindings = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            var property = type.GetProperty(name, Bindings);
            if (property != null)
            {
                return property.GetValue(current, null);
            }

            var field = type.GetField(name, Bindings);
            if (field != null)
            {
                return field.GetValue(current);
            }

            throw new ArgumentException(
                string.Format(CultureInfo.CurrentCulture, "'{0}' does not contain a field or property named '{1}'.", type.FullName, name),
                nameof(name));
        }
    }
}
