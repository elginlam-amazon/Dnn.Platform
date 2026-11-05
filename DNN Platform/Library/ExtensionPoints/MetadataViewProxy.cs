// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.ExtensionPoints
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Reflection;

    /// <summary>
    /// Projects an export's metadata dictionary onto a metadata view interface (for example
    /// <see cref="IExtensionPointData"/>).
    /// </summary>
    /// <remarks>
    /// MEF 1 generated these views internally for <c>Lazy&lt;T, TMetadata&gt;</c> imports. MEF 2
    /// (<c>System.Composition</c>) does not expose a public API to do it, so the projection is done
    /// here with <see cref="DispatchProxy"/>. <see cref="DefaultValueAttribute"/> on the view's
    /// properties is honoured exactly as MEF honoured it, so
    /// <see cref="IExtensionPointData.DisableOnHost"/> and
    /// <see cref="IExtensionPointData.DisableUnauthenticated"/> keep defaulting to
    /// <see langword="false"/> when an extension does not supply them.
    /// </remarks>
    public class MetadataViewProxy : DispatchProxy
    {
        private IReadOnlyDictionary<string, object> metadata;

        /// <summary>Creates a metadata view of the given interface type over the given metadata.</summary>
        /// <param name="viewType">The metadata view interface type.</param>
        /// <param name="metadata">The export metadata.</param>
        /// <returns>An instance implementing <paramref name="viewType"/>.</returns>
        public static object Create(Type viewType, IReadOnlyDictionary<string, object> metadata)
        {
            if (viewType is null)
            {
                throw new ArgumentNullException(nameof(viewType));
            }

            // DispatchProxy.Create is generic; dispatch through a closed generic helper so that the
            // produced proxy actually implements the requested view interface.
            return CreateMethod
                .MakeGenericMethod(viewType)
                .Invoke(null, new object[] { metadata ?? new Dictionary<string, object>() });
        }

        /// <summary>Creates a metadata view of <typeparamref name="TView"/> over the given metadata.</summary>
        /// <typeparam name="TView">The metadata view interface type.</typeparam>
        /// <param name="metadata">The export metadata.</param>
        /// <returns>An instance implementing <typeparamref name="TView"/>.</returns>
        public static TView Create<TView>(IReadOnlyDictionary<string, object> metadata)
        {
            var proxy = DispatchProxy.Create<TView, MetadataViewProxy>();
            ((MetadataViewProxy)(object)proxy).metadata = metadata ?? new Dictionary<string, object>();
            return proxy;
        }

        /// <inheritdoc/>
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod is null)
            {
                throw new ArgumentNullException(nameof(targetMethod));
            }

            var name = targetMethod.Name;
            if (name.StartsWith("get_", StringComparison.Ordinal))
            {
                return this.GetValue(name.Substring(4), targetMethod);
            }

            switch (name)
            {
                case nameof(this.ToString):
                    return "MetadataView";
                case nameof(this.GetHashCode):
                    return this.metadata.GetHashCode();
                case nameof(this.Equals):
                    return ReferenceEquals(this, args?[0]);
                default:
                    throw new NotSupportedException($"A metadata view only supports property getters; '{name}' is not one.");
            }
        }

        private object GetValue(string propertyName, MethodInfo getter)
        {
            if (this.metadata.TryGetValue(propertyName, out var value) && value != null)
            {
                return ConvertTo(value, getter.ReturnType);
            }

            var property = getter.DeclaringType?.GetProperty(propertyName);
            var defaultValue = property?.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultValue != null)
            {
                return ConvertTo(defaultValue.Value, getter.ReturnType);
            }

            return getter.ReturnType.IsValueType ? Activator.CreateInstance(getter.ReturnType) : null;
        }

        private static object ConvertTo(object value, Type targetType)
        {
            if (value is null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlying.IsEnum)
            {
                return value is string enumText
                    ? Enum.Parse(underlying, enumText, true)
                    : Enum.ToObject(underlying, value);
            }

            return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        }

        private static readonly MethodInfo CreateMethod = Array.Find(
            typeof(MetadataViewProxy).GetMethods(BindingFlags.Public | BindingFlags.Static),
            m => m.Name == nameof(Create) && m.IsGenericMethodDefinition);
    }
}
