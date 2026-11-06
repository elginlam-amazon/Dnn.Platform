// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using DotNetNuke.DependencyInjection.Extensions;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc.ApplicationParts;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    /// <summary>Adds DNN Web API Specific startup extensions to simplify the <see cref="Startup"/> Class.</summary>
    internal static class StartupExtensions
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(StartupExtensions));

        /// <summary>
        /// Configures all of the <see cref="DnnApiController"/>'s to be used with the Service Collection for
        /// Dependency Injection, and registers the assemblies that declare them as MVC application parts so
        /// that ASP.NET Core's controller factory and action discovery find the service controllers of every
        /// installed module (the legacy <c>DnnHttpControllerSelector</c>/<c>DnnDependencyResolver</c> pair did
        /// both jobs on Web API).
        /// </summary>
        /// <param name="services">Service Collection used to registering services in the container.</param>
        public static void AddWebApi(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            var allTypes = DotNetNuke.DependencyInjection.Extensions.TypeExtensions.SafeGetTypes();
            allTypes.LogOtherExceptions(Logger);

            var controllerTypes = allTypes.Types
                .Where(
                    type => typeof(DnnApiController).IsAssignableFrom(type) &&
                            type is { IsClass: true, IsAbstract: false })
                .ToList();
            foreach (var controller in controllerTypes)
            {
                services.TryAddScoped(controller);
            }

            var partManager = services.GetApplicationPartManager();
            foreach (var assembly in controllerTypes.Select(type => type.Assembly).Distinct())
            {
                AddApplicationPart(partManager, assembly);
            }
        }

        private static ApplicationPartManager GetApplicationPartManager(this IServiceCollection services)
        {
            var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(ApplicationPartManager));
            if (descriptor?.ImplementationInstance is ApplicationPartManager existing)
            {
                return existing;
            }

            var partManager = new ApplicationPartManager();
            services.TryAddSingleton(partManager);
            return partManager;
        }

        private static void AddApplicationPart(ApplicationPartManager partManager, Assembly assembly)
        {
            foreach (var part in GetApplicationParts(assembly))
            {
                if (!partManager.ApplicationParts.Any(existing => string.Equals(existing.Name, part.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    partManager.ApplicationParts.Add(part);
                }
            }
        }

        private static ApplicationPart[] GetApplicationParts(Assembly assembly)
        {
            ApplicationPart[] parts;
            try
            {
                parts = ApplicationPartFactory.GetApplicationPartFactory(assembly)
                    .GetApplicationParts(assembly)
                    .ToArray();
            }
            catch (Exception exception) when (
                exception is BadImageFormatException

                // On Linux an assembly whose dependencies are missing or whose file name casing
                // differs from the assembly name throws these instead of BadImageFormatException.
                or FileNotFoundException
                or FileLoadException
                or ReflectionTypeLoadException)
            {
                Logger.StartupExtensionsUnableToAddApplicationPart(exception, assembly.FullName);
                parts = [];
            }

            return parts;
        }
    }
}
