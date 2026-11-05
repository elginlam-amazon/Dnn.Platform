// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.ExtensionPoints
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;

    using DotNetNuke.Instrumentation;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Scans a directory of assemblies for exported parts, tolerating assemblies that cannot be
    /// loaded or reflected over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was a <c>System.ComponentModel.Composition.Primitives.ComposablePartCatalog</c> wrapping
    /// an <c>AggregateCatalog</c> of <c>AssemblyCatalog</c>s. Neither type exists in MEF 2
    /// (<c>System.Composition</c>), so the catalog is now a plain list of <see cref="ExportedPart"/>
    /// read by <see cref="ExportDiscovery"/>. The attribute contract third-party extensions are
    /// built against is unchanged.
    /// </para>
    /// <para>
    /// Assemblies are loaded with <see cref="AssemblyLoadContext.Default"/>; there is no shadow
    /// copying and no separate <c>AppDomain</c>. Per-assembly failures are swallowed as before, but
    /// a failure of the scan itself (for example a missing directory) is thrown, and the result is
    /// logged -- a catalog that silently finds nothing would remove every extension point in
    /// the UI without any other symptom.
    /// </para>
    /// </remarks>
    public class SafeDirectoryCatalog
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<SafeDirectoryCatalog>();

        private readonly List<ExportedPart> parts = new List<ExportedPart>();
        private readonly List<Assembly> assemblies = new List<Assembly>();

        /// <summary>Initializes a new instance of the <see cref="SafeDirectoryCatalog"/> class.</summary>
        /// <param name="directory">The directory path in which to search for assemblies (recursively).</param>
        public SafeDirectoryCatalog(string directory)
        {
            // Deliberately not guarded: if the directory we are told to scan does not exist, the
            // installation is broken and every extension point would vanish silently.
            var files = Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories);

            var failed = 0;
            foreach (var file in files)
            {
                try
                {
                    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(file));
                    var assemblyParts = GetParts(assembly);
                    if (assemblyParts.Count > 0)
                    {
                        this.assemblies.Add(assembly);
                        this.parts.AddRange(assemblyParts);
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    failed++;
                }
                catch (BadImageFormatException)
                {
                    // A native or otherwise non-managed DLL sitting in the folder.
                    failed++;
                }
                catch (FileLoadException)
                {
                    // The assembly, or one of its dependencies, could not be loaded.
                    failed++;
                }
                catch (FileNotFoundException)
                {
                    // A dependency of the assembly is missing — much more common on Linux, where
                    // the framework assemblies are not in the application folder.
                    failed++;
                }
            }

            Logger.LogInformation(
                "Extension point scan of '{Directory}' found {PartCount} exported part(s) in {AssemblyCount} assembly(ies); {FailedCount} assembly(ies) could not be inspected.",
                directory,
                this.parts.Count,
                this.assemblies.Count,
                failed);
        }

        /// <summary>Gets the exported parts discovered in the directory.</summary>
        /// <remarks>
        /// Replaces <c>ComposablePartCatalog.Parts</c>, which was
        /// <c>IQueryable&lt;ComposablePartDefinition&gt;</c> — a MEF 1 type that no longer exists.
        /// </remarks>
        public IQueryable<ExportedPart> Parts => this.parts.AsQueryable();

        /// <summary>Gets the assemblies that contributed at least one exported part.</summary>
        public IReadOnlyCollection<Assembly> Assemblies => this.assemblies;

        private static List<ExportedPart> GetParts(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Keep the types that did load, exactly as MEF's "good parts" behaviour did.
                types = Array.FindAll(ex.Types, t => t != null);
            }

            var result = new List<ExportedPart>();
            foreach (var type in types)
            {
                result.AddRange(ExportDiscovery.GetParts(type));
            }

            return result;
        }
    }
}
