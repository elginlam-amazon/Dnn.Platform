// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.ExtensionPoints
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;

    using DotNetNuke.Instrumentation;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Resolves the <c>UserControlSrc</c> an extension point declares (historically the virtual path
    /// of an <c>.ascx</c> user control) to the Blazor component type that replaces it.
    /// </summary>
    /// <remarks>
    /// On WebForms the extension controls called <c>Page.LoadControl(extension.UserControlSrc)</c>,
    /// which compiled and instantiated the <c>.ascx</c>. There is no <c>LoadControl</c> on ASP.NET
    /// Core, so the control source is resolved to a component type and rendered through
    /// <see cref="DynamicComponent"/>. Extensions may keep declaring the old
    /// <c>"~/DesktopModules/Foo/Bar.ascx"</c> path -- the file name is matched against the component
    /// types that replaced it -- or may declare an assembly-qualified component type name directly,
    /// which is the preferred form going forward.
    /// </remarks>
    public static class ExtensionControlResolver
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(ExtensionControlResolver));

        private static readonly ConcurrentDictionary<string, Type> Cache = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Resolves an extension point's control source to a component type.</summary>
        /// <param name="controlSrc">The value of the extension's <c>UserControlSrc</c>.</param>
        /// <returns>The component type to render.</returns>
        /// <exception cref="ArgumentException"><paramref name="controlSrc"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">No component could be found for <paramref name="controlSrc"/>.</exception>
        public static Type Resolve(string controlSrc)
        {
            if (string.IsNullOrWhiteSpace(controlSrc))
            {
                throw new ArgumentException("An extension point must declare a control source.", nameof(controlSrc));
            }

            return Cache.GetOrAdd(controlSrc, ResolveCore);
        }

        private static Type ResolveCore(string controlSrc)
        {
            // An assembly-qualified or namespace-qualified component type name.
            if (!HasControlFileExtension(controlSrc))
            {
                var declared = Type.GetType(controlSrc, false, true) ?? FindByName(controlSrc, matchFullName: true);
                if (declared != null)
                {
                    return declared;
                }
            }

            var name = Path.GetFileNameWithoutExtension(controlSrc.Replace('\\', '/').TrimEnd('/'));
            var byName = FindByName(name, matchFullName: false);
            if (byName != null)
            {
                Logger.LogDebug(
                    "Extension point control source '{ControlSrc}' resolved to component {ComponentType}.",
                    controlSrc,
                    byName.FullName);
                return byName;
            }

            throw new InvalidOperationException(
                $"The extension point control source '{controlSrc}' could not be resolved to a component. " +
                "On ASP.NET Core there is no Page.LoadControl: the control must be a Blazor component, " +
                "either named after the original control file or declared as a type name.");
        }

        private static bool HasControlFileExtension(string controlSrc)
            => controlSrc.EndsWith(".ascx", StringComparison.OrdinalIgnoreCase)
            || controlSrc.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)
            || controlSrc.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);

        private static Type FindByName(string name, bool matchFullName)
        {
            foreach (var assembly in AssemblyLoadContext.Default.Assemblies.ToList())
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }

                foreach (var type in GetTypes(assembly))
                {
                    if (!typeof(IComponent).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                    {
                        continue;
                    }

                    var candidate = matchFullName ? type.FullName : type.Name;
                    if (string.Equals(candidate, name, StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<Type> GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return Array.FindAll(ex.Types, t => t != null);
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException or TypeLoadException)
            {
                return Array.Empty<Type>();
            }
        }
    }
}
