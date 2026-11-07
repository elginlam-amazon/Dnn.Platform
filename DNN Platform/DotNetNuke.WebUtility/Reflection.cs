// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using System.Runtime.Loader;

    /// <summary>
    /// Helpers that create and invoke types by name through reflection.
    /// </summary>
    /// <remarks>
    /// The WebForms implementation resolved type names through
    /// <c>System.Web.Compilation.BuildManager.GetType</c>, which looked in the compiled-page
    /// assemblies as well. On ASP.NET Core the same resolution is done with
    /// <see cref="Type.GetType(string, bool, bool)"/> followed by a scan over the assemblies loaded
    /// in <see cref="AssemblyLoadContext.Default"/>.
    /// </remarks>
    public class Reflection
    {
        /// <summary>Creates an instance of the type with the given name, caching the resolved type.</summary>
        /// <param name="typeName">The (optionally assembly qualified) name of the type to create.</param>
        /// <param name="cacheKey">The key the resolved type is cached under; the type name is used when empty.</param>
        /// <returns>The newly created object.</returns>
        public static object CreateObject(string typeName, string cacheKey)
        {
            return CreateObject(typeName, cacheKey, true);
        }

        /// <summary>Creates an instance of the type with the given name.</summary>
        /// <param name="typeName">The (optionally assembly qualified) name of the type to create.</param>
        /// <param name="cacheKey">The key the resolved type is cached under; the type name is used when empty.</param>
        /// <param name="useCache">Whether the resolved type may be read from and written to the cache.</param>
        /// <returns>The newly created object.</returns>
        public static object CreateObject(string typeName, string cacheKey, bool useCache)
        {
            // dynamically create the object
            return Activator.CreateInstance(CreateType(typeName, cacheKey, useCache));
        }

        /// <summary>Creates an instance of <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">The type to create.</typeparam>
        /// <returns>The newly created object.</returns>
        public static T CreateObject<T>()
        {
            // dynamically create the object
            return Activator.CreateInstance<T>();
        }

        /// <summary>Resolves the type with the given name, caching the result.</summary>
        /// <param name="typeName">The (optionally assembly qualified) name of the type.</param>
        /// <returns>The resolved type.</returns>
        public static Type CreateType(string typeName)
        {
            return CreateType(typeName, string.Empty, true, false);
        }

        /// <summary>Resolves the type with the given name, caching the result.</summary>
        /// <param name="typeName">The (optionally assembly qualified) name of the type.</param>
        /// <param name="ignoreErrors">Whether a failure to resolve the type is swallowed instead of thrown.</param>
        /// <returns>The resolved type, or <see langword="null"/> when it could not be resolved and errors are ignored.</returns>
        public static Type CreateType(string typeName, bool ignoreErrors)
        {
            return CreateType(typeName, string.Empty, true, ignoreErrors);
        }

        /// <summary>Resolves the type with the given name.</summary>
        /// <param name="typeName">The (optionally assembly qualified) name of the type.</param>
        /// <param name="cacheKey">The key the resolved type is cached under; the type name is used when empty.</param>
        /// <param name="useCache">Whether the resolved type may be read from and written to the cache.</param>
        /// <returns>The resolved type.</returns>
        public static Type CreateType(string typeName, string cacheKey, bool useCache)
        {
            return CreateType(typeName, cacheKey, useCache, false);
        }

        /// <summary>Resolves the type with the given name.</summary>
        /// <param name="typeName">The (optionally assembly qualified) name of the type.</param>
        /// <param name="cacheKey">The key the resolved type is cached under; the type name is used when empty.</param>
        /// <param name="useCache">Whether the resolved type may be read from and written to the cache.</param>
        /// <param name="ignoreErrors">Whether a failure to resolve the type is swallowed instead of thrown.</param>
        /// <returns>The resolved type, or <see langword="null"/> when it could not be resolved and errors are ignored.</returns>
        public static Type CreateType(string typeName, string cacheKey, bool useCache, bool ignoreErrors)
        {
            if (string.IsNullOrEmpty(cacheKey))
            {
                cacheKey = typeName;
            }

            Type objType = null;

            // use the cache for performance
            if (useCache)
            {
                objType = DataCache.GetCache(cacheKey) as Type;
            }

            // is the type in the cache?
            if (objType is null)
            {
                try
                {
                    // use reflection to get the type of the class
                    objType = GetTypeFromLoadedAssemblies(typeName);

                    if (useCache)
                    {
                        // insert the type into the cache
                        DataCache.SetCache(cacheKey, objType);
                    }
                }
                catch (Exception)
                {
                    // could not load the type
                    if (!ignoreErrors)
                    {
                        throw;
                    }
                }
            }

            return objType;
        }

        /// <summary>Creates an instance of the given type through its parameterless constructor.</summary>
        /// <param name="type">The type to instantiate, may be <see langword="null"/>.</param>
        /// <returns>The newly created object, or <see langword="null"/> when <paramref name="type"/> is <see langword="null"/>.</returns>
        public static object CreateInstance(Type type)
        {
            if (type is not null)
            {
                return type.InvokeMember(string.Empty, BindingFlags.CreateInstance, null, null, null, null);
            }

            return null;
        }

        /// <summary>Reads a property of the given target through reflection.</summary>
        /// <param name="type">The type declaring the property, may be <see langword="null"/>.</param>
        /// <param name="propertyName">The name of the property to read.</param>
        /// <param name="target">The instance to read the property from, or <see langword="null"/> for a static property.</param>
        /// <returns>The value of the property, or <see langword="null"/> when <paramref name="type"/> is <see langword="null"/>.</returns>
        public static object GetProperty(Type type, string propertyName, object target)
        {
            if (type is not null)
            {
                return type.InvokeMember(propertyName, BindingFlags.GetProperty, null, target, null, CultureInfo.InvariantCulture);
            }

            return null;
        }

        /// <summary>Writes a property of the given target through reflection.</summary>
        /// <param name="type">The type declaring the property, may be <see langword="null"/>.</param>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <param name="target">The instance to write the property on, or <see langword="null"/> for a static property.</param>
        /// <param name="args">The value to assign (and any index arguments).</param>
        public static void SetProperty(Type type, string propertyName, object target, object[] args)
        {
            if (type is not null)
            {
                type.InvokeMember(propertyName, BindingFlags.SetProperty, null, target, args, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Invokes a non-generic method of the given target through reflection.</summary>
        /// <param name="type">The type declaring the method, may be <see langword="null"/>.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="target">The instance to invoke the method on, or <see langword="null"/> for a static method.</param>
        /// <param name="args">The arguments to pass to the method.</param>
        /// <returns>The return value of the method, or <see langword="null"/> when no matching method was found.</returns>
        public static object InvokeMethod(Type type, string methodName, object target, object[] args)
        {
            if (type is not null)
            {
                // Can't use Type.GetMethod(name, types) here: in case a generic method is defined whose
                // parameters match, an ambiguous match error is raised.
                var match = false;
                MethodInfo method = null;
                foreach (var candidate in type.GetMethods())
                {
                    method = candidate;
                    if (method.Name == methodName && !method.IsGenericMethod && method.GetParameters().Length == args.Length)
                    {
                        if (ParamsSameType(method.GetParameters(), args))
                        {
                            match = true;
                            break;
                        }
                    }
                }

                if (match && method is not null)
                {
                    return method.Invoke(target, args);
                }
            }

            return null;
        }

        /// <summary>Invokes a generic method of the given target through reflection.</summary>
        /// <typeparam name="T">The type argument to close the generic method over, and its return type.</typeparam>
        /// <param name="type">The type declaring the method, may be <see langword="null"/>.</param>
        /// <param name="methodName">The name of the generic method to invoke.</param>
        /// <param name="target">The instance to invoke the method on, or <see langword="null"/> for a static method.</param>
        /// <param name="args">The arguments to pass to the method.</param>
        /// <returns>The return value of the method, or the default of <typeparamref name="T"/> when no matching method was found.</returns>
        public static T InvokeGenericMethod<T>(Type type, string methodName, object target, object[] args)
        {
            if (type is not null)
            {
                MethodInfo method = null;
                foreach (var candidate in type.GetMethods())
                {
                    if (candidate.Name == methodName && candidate.IsGenericMethod)
                    {
                        method = candidate;
                        break;
                    }
                }

                if (method is not null)
                {
                    var genMethod = method.MakeGenericMethod(typeof(T));
                    return (T)genMethod.Invoke(target, args);
                }
            }

            return default;
        }

        private static Type GetTypeFromLoadedAssemblies(string typeName)
        {
            // Mirrors BuildManager.GetType(typeName, throwOnError: True, ignoreCase: True).
            var objType = Type.GetType(typeName, false, true);
            if (objType is not null)
            {
                return objType;
            }

            foreach (var assembly in AssemblyLoadContext.Default.Assemblies)
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }

                try
                {
                    objType = assembly.GetType(typeName, false, true);
                }
                catch (Exception)
                {
                    // A single unloadable assembly must not stop the scan, just as the build manager's
                    // top-level assembly scan tolerated individual failures.
                    continue;
                }

                if (objType is not null)
                {
                    return objType;
                }
            }

            throw new TypeLoadException(
                string.Format(CultureInfo.CurrentCulture, "The type '{0}' could not be found in any loaded assembly.", typeName));
        }

        private static bool ParamsSameType(ParameterInfo[] parameters, object[] args)
        {
            var match = true;
            for (var i = 0; i < args.Length; i++)
            {
                if (!parameters[i].ParameterType.IsAssignableFrom(args[i].GetType()))
                {
                    match = false;
                    break;
                }
            }

            return match;
        }
    }
}
