// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Localization
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Text.RegularExpressions;

    using DotNetNuke.Common;

    /// <summary>
    /// Resolves <c>dnnLoc</c> localization expressions, which name a key in the
    /// <c>App_LocalResources</c> resource file that sits next to the markup being rendered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was a <c>System.Web.Compilation.ExpressionBuilder</c> registered under the
    /// <c>dnnLoc</c> prefix in <c>web.config</c>'s <c>&lt;expressionBuilders&gt;</c> section, which
    /// let WebForms markup write <c>&lt;%$ dnnLoc:MyKey %&gt;</c>. <c>ExpressionBuilder</c>, the
    /// WebForms page compiler it plugged into, and the <c>System.CodeDom</c> code generation it
    /// performed are all removed on modern .NET, and <c>&lt;expressionBuilders&gt;</c> is no longer
    /// honoured.
    /// </para>
    /// <para>
    /// The type and its resolution logic survive as a plain localization helper: the valuable part
    /// was never the code generation but <see cref="GetLocalizedResource"/>, which maps a markup
    /// path to its local resource file, looks the key up and coerces the result to the target
    /// property's type. Razor/Blazor markup calls <see cref="Evaluate(string, string)"/> (or
    /// <see cref="ReplaceExpressions"/> for markup that still carries <c>[dnnLoc:Key]</c> tokens)
    /// instead of relying on a compile-time expression expansion.
    /// </para>
    /// </remarks>
    public class LocalizationExpressionBuilder
    {
        /// <summary>The markup prefix these expressions are written with.</summary>
        /// <remarks>Replaces the <c>[ExpressionPrefix("dnnLoc")]</c> attribute, which only the removed WebForms page compiler read.</remarks>
        public const string ExpressionPrefix = "dnnLoc";

        /// <summary>Matches a <c>[dnnLoc:Key]</c> token in a block of markup.</summary>
        private static readonly Regex ExpressionRegex = new Regex(
            @"\[" + ExpressionPrefix + @":(?<key>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Gets a value indicating whether this builder can be evaluated at run time.</summary>
        /// <remarks>
        /// Kept for source compatibility with the <c>ExpressionBuilder</c> member of the same name.
        /// Expressions are now <em>only</em> evaluated at run time, so this is always
        /// <see langword="true"/>.
        /// </remarks>
        public virtual bool SupportsEvaluate => true;

        /// <summary>Looks a localized resource up and converts it to the type of the target property.</summary>
        /// <param name="key">The resource key named by the expression.</param>
        /// <param name="targetType">The type declaring the property the value is assigned to, or <see langword="null"/> to skip conversion.</param>
        /// <param name="propertyName">The name of the property the value is assigned to.</param>
        /// <param name="virtualPath">The virtual path of the markup holding the expression, used to locate its local resource file.</param>
        /// <returns>The localized value, converted to the property's type when necessary.</returns>
        /// <exception cref="InvalidOperationException">The key was not found, or its value cannot be converted to the property's type.</exception>
        public static object GetLocalizedResource(string key, Type targetType, string propertyName, string virtualPath)
        {
            if (HttpContextSource.Current == null)
            {
                return null;
            }

            string localResourceFile = string.Empty;
            if (!string.IsNullOrEmpty(virtualPath))
            {
                string filename = Path.GetFileName(virtualPath);
                if (filename != null)
                {
                    localResourceFile = virtualPath.Replace(filename, Localization.LocalResourceDirectory + "/" + filename);
                }
            }

            string value = Localization.GetString(key, localResourceFile);

            if (value == null)
            {
                throw new InvalidOperationException($"Localized Value '{key}' not found.");
            }

            // check if value can be converted to property type
            if (targetType != null)
            {
                PropertyDescriptor propDesc = TypeDescriptor.GetProperties(targetType)[propertyName];
                if (propDesc != null && propDesc.PropertyType != value.GetType())
                {
                    // Type mismatch - make sure that the value can be converted to the Web control property type
                    if (propDesc.Converter != null)
                    {
                        if (!propDesc.Converter.CanConvertFrom(value.GetType()))
                        {
                            throw new InvalidOperationException(
                                $"Localized value '{key}' cannot be converted to type {propDesc.PropertyType}.");
                        }

                        return propDesc.Converter.ConvertFrom(value);
                    }
                }
            }

            // If we reach here, no type mismatch - return the value
            return value;
        }

        /// <summary>Evaluates a <c>dnnLoc</c> expression as a string.</summary>
        /// <param name="expression">The resource key, with or without surrounding whitespace.</param>
        /// <param name="virtualPath">The virtual path of the markup holding the expression.</param>
        /// <returns>The localized text.</returns>
        /// <remarks>
        /// Replaces <c>EvaluateExpression(object, BoundPropertyEntry, object, ExpressionBuilderContext)</c>.
        /// <c>BoundPropertyEntry</c> and <c>ExpressionBuilderContext</c> only carried the expression,
        /// the declaring type, the property name and the virtual path, so the data the override
        /// received is passed explicitly instead.
        /// </remarks>
        public static string Evaluate(string expression, string virtualPath)
        {
            return (string)GetLocalizedResource(expression?.Trim(), null, null, virtualPath);
        }

        /// <summary>Evaluates a <c>dnnLoc</c> expression that is bound to a typed property.</summary>
        /// <param name="expression">The resource key, with or without surrounding whitespace.</param>
        /// <param name="targetType">The type declaring the bound property.</param>
        /// <param name="propertyName">The name of the bound property.</param>
        /// <param name="virtualPath">The virtual path of the markup holding the expression.</param>
        /// <returns>The localized value, converted to the property's type.</returns>
        public static object Evaluate(string expression, Type targetType, string propertyName, string virtualPath)
        {
            return GetLocalizedResource(expression?.Trim(), targetType, propertyName, virtualPath);
        }

        /// <summary>Replaces every <c>[dnnLoc:Key]</c> token in a block of markup with its localized text.</summary>
        /// <param name="markup">The markup to process.</param>
        /// <param name="virtualPath">The virtual path of the markup, used to locate its local resource file.</param>
        /// <returns>The markup with every token replaced.</returns>
        /// <remarks>
        /// The WebForms page compiler expanded <c>&lt;%$ dnnLoc:Key %&gt;</c> while compiling the
        /// page. Razor/Blazor markup and DNN skin files are rendered rather than compiled against an
        /// expression builder, so the same substitution happens here at render time.
        /// </remarks>
        public static string ReplaceExpressions(string markup, string virtualPath)
        {
            if (string.IsNullOrEmpty(markup))
            {
                return markup;
            }

            return ExpressionRegex.Replace(
                markup,
                match =>
                {
                    var localized = Evaluate(match.Groups["key"].Value, virtualPath);

                    // An unresolved key must not silently render as the raw token; GetLocalizedResource
                    // throws in that case, so reaching here with no text means the resource is empty.
                    return localized ?? string.Empty;
                });
        }
    }
}
