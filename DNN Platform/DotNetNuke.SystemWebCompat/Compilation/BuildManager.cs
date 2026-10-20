// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Compilation
{
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Replacement for the type resolution features of the System.Web <c>BuildManager</c> on .NET 10.
    /// </summary>
    /// <remarks>
    /// There is no <c>App_Code</c> / <c>bin</c> dynamic compilation on ASP.NET Core; types are resolved from the
    /// assemblies that are loaded into the application.
    /// </remarks>
    public static class BuildManager
    {
        private static readonly ConcurrentDictionary<(string TypeName, bool IgnoreCase), Type> TypeCache = new ConcurrentDictionary<(string TypeName, bool IgnoreCase), Type>();

        /// <summary>Finds a type in the application assemblies.</summary>
        /// <param name="typeName">The (assembly qualified) name of the type.</param>
        /// <param name="throwOnError">Whether to throw an exception when the type cannot be found.</param>
        /// <returns>The type, or <see langword="null"/>.</returns>
        public static Type GetType(string typeName, bool throwOnError)
        {
            return GetType(typeName, throwOnError, false);
        }

        /// <summary>Finds a type in the application assemblies.</summary>
        /// <param name="typeName">The (assembly qualified) name of the type.</param>
        /// <param name="throwOnError">Whether to throw an exception when the type cannot be found.</param>
        /// <param name="ignoreCase">Whether the type name is case-insensitive.</param>
        /// <returns>The type, or <see langword="null"/>.</returns>
        public static Type GetType(string typeName, bool throwOnError, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                if (throwOnError)
                {
                    throw new ArgumentNullException(nameof(typeName));
                }

                return null;
            }

            var type = TypeCache.GetOrAdd((typeName, ignoreCase), key => FindType(key.TypeName, key.IgnoreCase));
            if (type == null)
            {
                // do not cache misses, the assembly may be loaded later (e.g. extension installation)
                TypeCache.TryRemove((typeName, ignoreCase), out _);
                if (throwOnError)
                {
                    throw new TypeLoadException($"Could not load type '{typeName}'.");
                }
            }

            return type;
        }

        /// <summary>Gets the assemblies that are loaded into the application.</summary>
        /// <returns>A collection of <see cref="Assembly"/> instances.</returns>
        public static ICollection GetReferencedAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).ToArray();
        }

        private static Type FindType(string typeName, bool ignoreCase)
        {
            var type = Type.GetType(typeName, false, ignoreCase);
            if (type != null)
            {
                return type;
            }

            // an assembly qualified name whose assembly is not loaded yet
            var commaIndex = typeName.IndexOf(',', StringComparison.Ordinal);
            if (commaIndex > 0)
            {
                var assemblyName = typeName.Substring(commaIndex + 1).Trim();
                try
                {
                    var assembly = Assembly.Load(new AssemblyName(assemblyName));
                    return assembly.GetType(typeName.Substring(0, commaIndex).Trim(), false, ignoreCase);
                }
                catch (Exception ex) when (ex is IO.FileNotFoundException || ex is IO.FileLoadException || ex is BadImageFormatException || ex is ArgumentException)
                {
                    return null;
                }
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }

                type = assembly.GetType(typeName, false, ignoreCase);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
