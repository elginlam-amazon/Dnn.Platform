// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.ExtensionPoints
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A single exported part discovered by <see cref="SafeDirectoryCatalog"/>: the concrete type that
    /// carries an <c>[Export]</c> attribute, the contract it was exported under, and the metadata
    /// supplied through <c>[ExportMetadata]</c> (or through a <c>[MetadataAttribute]</c>-marked
    /// attribute such as <see cref="ExportClientScriptAttribute"/>).
    /// </summary>
    /// <remarks>
    /// This replaces <c>System.ComponentModel.Composition.Primitives.ComposablePartDefinition</c>,
    /// which does not exist in MEF 2 (the <c>System.Composition</c> package). It is deliberately a
    /// plain data object so that discovery does not depend on a composition engine at all.
    /// </remarks>
    public sealed class ExportedPart
    {
        /// <summary>Initializes a new instance of the <see cref="ExportedPart"/> class.</summary>
        /// <param name="implementationType">The concrete type carrying the export attribute.</param>
        /// <param name="contractType">The exported contract type, or <see langword="null"/> when the export only named a contract.</param>
        /// <param name="contractName">The exported contract name, or <see langword="null"/>.</param>
        /// <param name="metadata">The export metadata.</param>
        public ExportedPart(Type implementationType, Type contractType, string contractName, IReadOnlyDictionary<string, object> metadata)
        {
            this.ImplementationType = implementationType ?? throw new ArgumentNullException(nameof(implementationType));
            this.ContractType = contractType;
            this.ContractName = contractName;
            this.Metadata = metadata ?? new Dictionary<string, object>();
        }

        /// <summary>Gets the concrete type that carries the export attribute.</summary>
        public Type ImplementationType { get; }

        /// <summary>Gets the contract type the part was exported under, or <see langword="null"/>.</summary>
        public Type ContractType { get; }

        /// <summary>Gets the contract name the part was exported under, or <see langword="null"/>.</summary>
        public string ContractName { get; }

        /// <summary>Gets the export metadata (the values a metadata view such as <see cref="IExtensionPointData"/> is projected from).</summary>
        public IReadOnlyDictionary<string, object> Metadata { get; }

        /// <inheritdoc/>
        public override string ToString()
            => $"{this.ImplementationType.FullName} -> {this.ContractType?.FullName ?? this.ContractName ?? "(none)"}";
    }
}
