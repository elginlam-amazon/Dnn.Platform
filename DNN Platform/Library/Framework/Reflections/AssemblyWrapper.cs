// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Framework.Reflections
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;

    using DotNetNuke.Framework.Internal.Reflection;

    /// <summary>Wraps a <see cref="Assembly"/> so that type scanning can be mocked.</summary>
    /// <remarks>
    /// The assembly is one that is already loaded into <see cref="AssemblyLoadContext.Default"/> (see
    /// <see cref="TypeLocator"/>), which replaces the <c>AppDomain</c> the WebForms implementation
    /// enumerated.
    /// </remarks>
    public class AssemblyWrapper : IAssembly
    {
        private readonly Assembly assembly;

        /// <summary>Initializes a new instance of the <see cref="AssemblyWrapper"/> class.</summary>
        /// <param name="assembly">The assembly.</param>
        public AssemblyWrapper(Assembly assembly)
        {
            this.assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        }

        /// <inheritdoc />
        /// <remarks>
        /// Returns the types the assembly does expose when some of them cannot be loaded, rather than
        /// letting a single unresolvable dependency abort the caller's scan. <see cref="FileNotFoundException"/>
        /// and <see cref="FileLoadException"/> are caught alongside <see cref="BadImageFormatException"/>
        /// because on Linux a missing or mismatched dependency surfaces as one of those instead.
        /// </remarks>
        public Type[] GetTypes()
        {
            try
            {
                return this.assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types?.Where(type => type is not null).ToArray() ?? [];
            }
            catch (FileNotFoundException)
            {
                return [];
            }
            catch (FileLoadException)
            {
                return [];
            }
            catch (BadImageFormatException)
            {
                return [];
            }
        }
    }
}
