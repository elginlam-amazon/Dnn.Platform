// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Maintenance.Telerik
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Metadata;
    using System.Reflection.PortableExecutable;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Instrumentation;

    using Microsoft.Extensions.Logging;

    /// <inheritdoc />
    public class TelerikUtils : ITelerikUtils
    {
        /// <summary>The file name of the Telerik Web UI assembly.</summary>
        public static readonly string TelerikWebUIFileName = "Telerik.Web.UI.dll";

        private static readonly string[] Vendors =
        [
            "Microsoft",
            "System",
        ];

        private static readonly HashSet<string> WellKnownAssemblies = new(StringComparer.OrdinalIgnoreCase)
        {
            "Telerik.Web.UI.Skins.dll",
            "DotNetNuke.Website.Deprecated.dll",
            "DotNetNuke.Web.Deprecated.dll",
            "DotNetNuke.Modules.DigitalAssets.dll",
        };

        private readonly IApplicationStatusInfo applicationStatusInfo;
        private readonly ILogger log;

        /// <summary>Initializes a new instance of the <see cref="TelerikUtils"/> class.</summary>
        /// <param name="applicationStatusInfo">An instance of <see cref="IApplicationStatusInfo"/>.</param>
        /// <param name="logger">An instance of <see cref="ILogger"/>.</param>
        public TelerikUtils(IApplicationStatusInfo applicationStatusInfo, ILogger<TelerikUtils> logger)
        {
            this.applicationStatusInfo = applicationStatusInfo ?? throw new ArgumentNullException(nameof(applicationStatusInfo));
            this.log = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public string BinPath => Path.Combine(this.applicationStatusInfo.ApplicationMapPath, "bin");

        /// <inheritdoc />
        public IEnumerable<string> GetAssembliesThatDependOnTelerik()
        {
            // The assemblies are inspected through their metadata only, which reads the file and
            // never loads it into the runtime - so, like the temporary AppDomain this replaces, it
            // does not lock the files in the bin folder.
            return DirectoryGetFiles(this.BinPath, "*.dll", SearchOption.AllDirectories)
                .Where(IsNotWellKnownAssembly)
                .Where(this.AssemblyDependsOnTelerik)
                .ToList();
        }

        /// <inheritdoc />
        public bool TelerikIsInstalled()
        {
            return FileExists(Path.Combine(this.BinPath, TelerikWebUIFileName));
        }

        /// <inheritdoc />
        public Version GetTelerikVersion()
        {
            try
            {
                var path = Path.Combine(this.BinPath, TelerikWebUIFileName);
                using var stream = new MemoryStream(File.ReadAllBytes(path));
                using var peReader = new PEReader(stream);
                return peReader.GetMetadataReader().GetAssemblyDefinition().Version;
            }
            catch (Exception ex)
            {
                throw new IOException("Could not get Telerik version number.", ex);
            }
        }

        /// <inheritdoc />
        public bool IsTelerikVersionVulnerable(Version version)
        {
            return version < new Version(2014, 0);
        }

        private static string[] DirectoryGetFiles(string path, string searchPattern, SearchOption searchOption) =>
            Directory.GetFiles(path, searchPattern, searchOption);

        private static bool FileExists(string path) => File.Exists(path);

        /// <summary>Gets the full names of the assemblies the given assembly references.</summary>
        /// <param name="assemblyFilePath">The path of the assembly to inspect.</param>
        /// <returns>The full name of each referenced assembly.</returns>
        /// <remarks>
        /// This used to be <c>domain.Load(File.ReadAllBytes(path)).GetReferencedAssemblies()</c> in
        /// a temporary <c>AppDomain</c>. Creating and unloading an <c>AppDomain</c> is not
        /// supported on modern .NET and throws (SYSLIB0024), so the assembly reference table is
        /// read straight out of the file's metadata instead. That is strictly better for this use:
        /// it needs none of the referenced assemblies to be resolvable, and it never loads code
        /// into the process. The returned value keeps the <see cref="AssemblyName.FullName"/>
        /// format the callers match against.
        /// </remarks>
        private static List<string> GetReferencedAssemblyNames(string assemblyFilePath)
        {
            try
            {
                using var stream = new MemoryStream(File.ReadAllBytes(assemblyFilePath));
                using var peReader = new PEReader(stream);
                var metadata = peReader.GetMetadataReader();

                var names = new List<string>();
                foreach (var handle in metadata.AssemblyReferences)
                {
                    var reference = metadata.GetAssemblyReference(handle);

                    var assemblyName = new AssemblyName
                    {
                        Name = metadata.GetString(reference.Name),
                        Version = reference.Version,
                        CultureName = reference.Culture.IsNil
                            ? string.Empty
                            : metadata.GetString(reference.Culture),
                    };

                    if (!reference.PublicKeyOrToken.IsNil)
                    {
                        assemblyName.SetPublicKeyToken(
                            metadata.GetBlobBytes(reference.PublicKeyOrToken));
                    }

                    names.Add(assemblyName.FullName);
                }

                return names;
            }
            catch (Exception ex)
            {
                throw new IOException($"Could not load assembly '{assemblyFilePath}'", ex);
            }
        }

        private static bool IsNotWellKnownAssembly(string path)
        {
            var fileName = Path.GetFileName(path);
            return !WellKnownAssemblies.Contains(fileName) &&
                !Vendors.Any(vendor => fileName.StartsWith($"{vendor}.", StringComparison.OrdinalIgnoreCase));
        }

        private bool AssemblyDependsOnTelerik(string path)
        {
            try
            {
                return GetReferencedAssemblyNames(path)
                    .Any(assemblyName => assemblyName.StartsWith("Telerik", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                // If we can't get the referenced assemblies, then it can't depend on Telerik.
                this.log.TelerikUtilsCountNotDetermineTelerikDependenciesOnSomeAssemblies(ex);
                return false;
            }
        }
    }
}
