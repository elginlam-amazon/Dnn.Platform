// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.ExtensionPoints
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Reads the <c>[Export]</c> / <c>[ExportMetadata]</c> attribute contract off a type without
    /// requiring a composition engine, and satisfies <c>[ImportMany]</c> members from a set of
    /// discovered parts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MEF 1 (<c>System.ComponentModel.Composition</c>) is not available on modern .NET and MEF 2
    /// (<c>System.Composition</c>) is not source compatible with it -- there is no
    /// <c>CompositionContainer</c>, <c>DirectoryCatalog</c> or <c>AssemblyCatalog</c>. Rather than
    /// changing the attribute contract that third-party extensions are built against, DNN now reads
    /// the attributes itself, <b>matching them by attribute type name</b>. That means a third-party
    /// extension decorated with either
    /// <c>System.ComponentModel.Composition.ExportAttribute</c> (the original MEF 1 attribute, for
    /// extensions whose source has not been touched) or
    /// <c>System.Composition.ExportAttribute</c> (MEF 2, which is what the platform itself now
    /// compiles against) is discovered identically, as are attributes derived from either of them
    /// such as <see cref="ExportClientScriptAttribute"/>.
    /// </para>
    /// </remarks>
    public static class ExportDiscovery
    {
        private const string Mef1Namespace = "System.ComponentModel.Composition";
        private const string Mef2Namespace = "System.Composition";

        /// <summary>Gets the exports declared by the given type, if any.</summary>
        /// <param name="type">The candidate type.</param>
        /// <returns>The exports declared on <paramref name="type"/>; empty when it declares none.</returns>
        public static IEnumerable<ExportedPart> GetParts(Type type)
        {
            if (type is null || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
            {
                yield break;
            }

            IList<Attribute> attributes;
            try
            {
                attributes = type.GetCustomAttributes(false).OfType<Attribute>().ToList();
            }
            catch (Exception ex) when (ex is TypeLoadException or FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                // An attribute referencing a type that cannot be loaded: this type is not usable.
                yield break;
            }

            var metadata = ReadMetadata(attributes);

            foreach (var attribute in attributes)
            {
                if (!IsAttributeOfKind(attribute.GetType(), "ExportAttribute"))
                {
                    continue;
                }

                var contractType = GetPropertyValue(attribute, "ContractType") as Type;
                var contractName = GetPropertyValue(attribute, "ContractName") as string;
                yield return new ExportedPart(type, contractType, contractName, metadata);
            }
        }

        /// <summary>
        /// Satisfies the <c>[ImportMany]</c> fields and properties of <paramref name="target"/> from
        /// <paramref name="parts"/>.
        /// </summary>
        /// <param name="target">The object whose imports should be satisfied.</param>
        /// <param name="parts">The available parts.</param>
        /// <remarks>
        /// Supported member types are <c>IEnumerable&lt;T&gt;</c>, <c>IEnumerable&lt;Lazy&lt;T&gt;&gt;</c>
        /// and <c>IEnumerable&lt;Lazy&lt;T, TMetadata&gt;&gt;</c> -- the three shapes DNN and its
        /// extensions use. Instances are created lazily, exactly as MEF did, so an extension's
        /// constructor only runs when the extension is actually used.
        /// </remarks>
        public static void Satisfy(object target, IReadOnlyList<ExportedPart> parts)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var type = target.GetType();
            while (type != null && type != typeof(object))
            {
                const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

                foreach (var field in type.GetFields(Flags))
                {
                    if (HasImportMany(field) && TryBuildImport(field.FieldType, parts, out var value))
                    {
                        field.SetValue(target, value);
                    }
                }

                foreach (var property in type.GetProperties(Flags))
                {
                    if (HasImportMany(property) && property.CanWrite && TryBuildImport(property.PropertyType, parts, out var value))
                    {
                        property.SetValue(target, value);
                    }
                }

                type = type.BaseType;
            }
        }

        private static bool HasImportMany(MemberInfo member)
            => member.GetCustomAttributes(false).Any(a => IsAttributeOfKind(a.GetType(), "ImportManyAttribute"));

        private static bool TryBuildImport(Type memberType, IReadOnlyList<ExportedPart> parts, out object value)
        {
            value = null;
            if (!memberType.IsGenericType)
            {
                return false;
            }

            var enumerableDefinition = memberType.GetGenericTypeDefinition();
            if (enumerableDefinition != typeof(IEnumerable<>)
                && enumerableDefinition != typeof(IReadOnlyCollection<>)
                && enumerableDefinition != typeof(IReadOnlyList<>))
            {
                return false;
            }

            var elementType = memberType.GetGenericArguments()[0];
            Type contractType = elementType;
            Type metadataType = null;
            var isLazy = false;

            if (elementType.IsGenericType)
            {
                var elementDefinition = elementType.GetGenericTypeDefinition();
                if (elementDefinition == typeof(Lazy<>))
                {
                    isLazy = true;
                    contractType = elementType.GetGenericArguments()[0];
                }
                else if (elementDefinition == typeof(Lazy<,>))
                {
                    isLazy = true;
                    contractType = elementType.GetGenericArguments()[0];
                    metadataType = elementType.GetGenericArguments()[1];
                }
            }

            var matches = parts.Where(p => Matches(p, contractType)).ToList();
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));

            foreach (var part in matches)
            {
                list.Add(isLazy
                    ? CreateLazy(elementType, contractType, metadataType, part)
                    : CreateInstance(part, contractType));
            }

            value = list;
            return true;
        }

        private static bool Matches(ExportedPart part, Type contractType)
        {
            if (!contractType.IsAssignableFrom(part.ImplementationType))
            {
                return false;
            }

            // An export that named a contract type is only offered for that contract (or a base of
            // it) — this is MEF's contract identity rule. An export that named nothing at all
            // (`[Export]`) is offered for its own type and everything it implements.
            return part.ContractType is null || contractType.IsAssignableFrom(part.ContractType);
        }

        private static object CreateLazy(Type lazyType, Type contractType, Type metadataType, ExportedPart part)
        {
            var valueFactoryType = typeof(Func<>).MakeGenericType(contractType);
            var factory = CreateFactoryDelegate(valueFactoryType, contractType, part);

            if (metadataType is null)
            {
                return Activator.CreateInstance(lazyType, factory);
            }

            var metadataView = MetadataViewProxy.Create(metadataType, part.Metadata);
            return Activator.CreateInstance(lazyType, factory, metadataView);
        }

        private static Delegate CreateFactoryDelegate(Type funcType, Type contractType, ExportedPart part)
        {
            var method = typeof(ExportDiscovery)
                .GetMethod(nameof(CreateInstanceTyped), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(contractType);
            return Delegate.CreateDelegate(funcType, part, method);
        }

        private static TContract CreateInstanceTyped<TContract>(ExportedPart part)
            => (TContract)CreateInstance(part, typeof(TContract));

        private static object CreateInstance(ExportedPart part, Type contractType)
        {
            try
            {
                return Activator.CreateInstance(part.ImplementationType);
            }
            catch (MissingMethodException ex)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The extension '{0}' exported as '{1}' cannot be created because it has no public parameterless constructor.",
                        part.ImplementationType.FullName,
                        contractType.FullName),
                    ex);
            }
        }

        private static IReadOnlyDictionary<string, object> ReadMetadata(IEnumerable<Attribute> attributes)
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (var attribute in attributes)
            {
                var attributeType = attribute.GetType();

                if (IsAttributeOfKind(attributeType, "ExportMetadataAttribute"))
                {
                    var name = GetPropertyValue(attribute, "Name") as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        metadata[name] = GetPropertyValue(attribute, "Value");
                    }

                    continue;
                }

                // A [MetadataAttribute]-marked attribute contributes all of its own public
                // properties as metadata, which is how MEF 1 treated it.
                if (!IsMetadataAttribute(attributeType))
                {
                    continue;
                }

                foreach (var property in attributeType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!property.CanRead || IsExportAttributeProperty(property.Name))
                    {
                        continue;
                    }

                    metadata[property.Name] = property.GetValue(attribute);
                }
            }

            return metadata;
        }

        private static bool IsExportAttributeProperty(string name)
            => name is "ContractName" or "ContractType" or "TypeId";

        private static bool IsMetadataAttribute(Type attributeType)
        {
            for (var type = attributeType; type != null && type != typeof(object); type = type.BaseType)
            {
                if (type.GetCustomAttributes(false).Any(a => IsAttributeOfKind(a.GetType(), "MetadataAttributeAttribute")))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether <paramref name="attributeType"/> is, or derives from, the MEF 1 or MEF 2
        /// attribute with the given simple name.
        /// </summary>
        /// <param name="attributeType">The attribute type to test.</param>
        /// <param name="simpleName">The MEF attribute's simple name, e.g. <c>ExportAttribute</c>.</param>
        /// <returns><see langword="true"/> when it matches either MEF version of the attribute.</returns>
        private static bool IsAttributeOfKind(Type attributeType, string simpleName)
        {
            for (var type = attributeType; type != null && type != typeof(object); type = type.BaseType)
            {
                if (type.Name == simpleName
                    && (type.Namespace == Mef1Namespace || type.Namespace == Mef2Namespace))
                {
                    return true;
                }
            }

            return false;
        }

        private static object GetPropertyValue(object instance, string propertyName)
            => instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);
    }
}
