// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.DependencyInjection.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Web;

    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>Activates MVC filters from the container.</summary>
    /// <remarks>
    /// <para>
    /// On Web API 2 this was <c>BuildUp(this IServiceProvider container, IFilter filter)</c>: filters were
    /// created by the framework with their parameterless/attribute constructor and the DNN filter provider
    /// then pushed services into every property marked with <see cref="DependencyAttribute"/>.
    /// <c>System.Web.Http.Filters.IFilter</c> does not exist on ASP.NET Core.
    /// </para>
    /// <para>
    /// The modern mechanism is <see cref="ActivatorUtilities"/>: <see cref="CreateFilter{TFilter}"/> and
    /// <see cref="CreateFilter(IServiceProvider, Type, object[])"/> build a filter with full CONSTRUCTOR
    /// injection, which is what <c>[ServiceFilter]</c>/<c>[TypeFilter]</c> do and what
    /// <c>DnnActionFilterProvider</c> and <c>ServicesRoutingManager</c> use.
    /// </para>
    /// <para>
    /// <see cref="BuildUp(IServiceProvider, IFilterMetadata)"/> remains for the one case ASP.NET Core cannot
    /// cover with constructor injection: a filter applied as a plain ATTRIBUTE
    /// (<c>[ApiTokenAuthorize]</c>), which the runtime instantiates itself through the attribute constructor.
    /// Those filters keep declaring their services as <see cref="DependencyAttribute"/> properties, and the
    /// authorization path fills them from the REQUEST container — the same single container everything else
    /// resolves from.
    /// </para>
    /// </remarks>
    internal static class BuildUpExtensions
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(BuildUpExtensions));

        /// <summary>Creates a filter of the given type with constructor injection from the container.</summary>
        /// <typeparam name="TFilter">The filter type.</typeparam>
        /// <param name="container">The service provider (the request container when inside a request).</param>
        /// <param name="parameters">Constructor arguments which are not resolved from the container.</param>
        /// <returns>The activated filter.</returns>
        internal static TFilter CreateFilter<TFilter>(this IServiceProvider container, params object[] parameters)
            where TFilter : IFilterMetadata
        {
            ArgumentNullException.ThrowIfNull(container);

            return ActivatorUtilities.CreateInstance<TFilter>(container, parameters);
        }

        /// <summary>Creates a filter of the given type with constructor injection from the container.</summary>
        /// <param name="container">The service provider (the request container when inside a request).</param>
        /// <param name="filterType">The filter type.</param>
        /// <param name="parameters">Constructor arguments which are not resolved from the container.</param>
        /// <returns>The activated filter.</returns>
        internal static IFilterMetadata CreateFilter(this IServiceProvider container, Type filterType, params object[] parameters)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(filterType);

            return (IFilterMetadata)ActivatorUtilities.CreateInstance(container, filterType, parameters);
        }

        /// <summary>Injects property dependency for properties that are decorated with <see cref="DependencyAttribute"/>.</summary>
        /// <param name="container">The service provider.</param>
        /// <param name="filter">The filter to inject properties into.</param>
        /// <remarks>
        /// Only needed for filters the runtime instantiates as attributes, where constructor injection is not
        /// available. Behaviour is unchanged from the Web API implementation: unresolvable services are left
        /// alone and a failing setter is logged rather than thrown.
        /// </remarks>
        internal static void BuildUp(this IServiceProvider container, IFilterMetadata filter)
        {
            if (container == null || filter == null)
            {
                return;
            }

            var properties = GetDependencyProperties(filter.GetType());
            foreach (var property in properties)
            {
                var service = container.GetService(property.PropertyType);
                if (service != null)
                {
                    try
                    {
                        property.SetValue(filter, service);
                    }
                    catch (Exception exception)
                    {
                        Logger.BuildUpExtensionsSetValueException(exception);
                    }
                }
            }

            IEnumerable<PropertyInfo> GetDependencyProperties(Type type)
            {
                return CBO.Instance.GetCachedObject<IEnumerable<PropertyInfo>>(
                    new CacheItemArgs($"WebApiDependencyProperties_{type.FullName}"),
                    (args) => type
                        .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(propertyInfo => propertyInfo.GetSetMethod(true) != null && propertyInfo.GetCustomAttribute<DependencyAttribute>() != null)
                        .ToList(),
                    saveInDictionary: false);
            }
        }
    }
}
