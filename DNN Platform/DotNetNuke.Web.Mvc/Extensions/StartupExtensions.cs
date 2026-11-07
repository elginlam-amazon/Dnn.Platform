// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using DotNetNuke.DependencyInjection.Extensions;
    using DotNetNuke.Entities.Controllers;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Log.EventLog;
    using DotNetNuke.Web.Mvc.Framework;
    using DotNetNuke.Web.Mvc.Framework.Controllers;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ApplicationParts;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    /// <summary>Adds DNN MVC Controller Specific startup extensions to simplify the <see cref="Startup"/> Class.</summary>
    public static class StartupExtensions
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(StartupExtensions));

        /// <summary>
        /// Configures all of the <see cref="DnnController"/>'s to be used with the Service Collection for Dependency Injection
        /// and registers the assemblies that declare them as MVC application parts, so that ASP.NET Core MVC can
        /// discover controllers which live in module assemblies.
        /// </summary>
        /// <param name="services">Service Collection used to registering services in the container.</param>
        public static void AddMvcControllers(this IServiceCollection services)
        {
            var allTypes = DotNetNuke.DependencyInjection.Extensions.TypeExtensions.SafeGetTypes();
            allTypes.LogOtherExceptions(Logger);

            var controllerTypes = allTypes.Types
                .Where(
                    type => typeof(IDnnController).IsAssignableFrom(type) &&
                            type is { IsClass: true, IsAbstract: false })
                .ToList();

            foreach (var controller in controllerTypes)
            {
                services.TryAddTransient(controller);
            }

            var partManager = services.GetApplicationPartManager();
            foreach (var assembly in controllerTypes.Select(type => type.Assembly).Distinct())
            {
                AddApplicationPart(partManager, assembly);
            }
        }

        /// <summary>
        /// Configures all of the <see cref="DnnController"/>'s to be used with the Service Collection for Dependency Injection
        /// and adds the declaring assemblies as MVC application parts of the given builder.
        /// </summary>
        /// <param name="builder">The MVC builder of the host.</param>
        /// <returns>The <paramref name="builder"/>, for chaining.</returns>
        public static IMvcBuilder AddMvcControllers(this IMvcBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddMvcControllers();
            return builder;
        }

        /// <summary>
        /// Registers the view engines which the MVC module host used to install into the static
        /// <c>System.Web.Mvc.ViewEngines.Engines</c> collection. The DNN module delegating view engine takes
        /// precedence over the (framework supplied) Razor view engine, exactly as it did before.
        /// </summary>
        /// <param name="services">Service Collection used to registering services in the container.</param>
        /// <returns>The <paramref name="services"/>, for chaining.</returns>
        public static IServiceCollection AddDnnMvcViewEngines(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton(_ => new ModuleDelegatingViewEngine(
                new HostSettings(
                    new HostController(
#pragma warning disable CS0618 // Type or member is obsolete
                        new EventLogController(),
#pragma warning restore CS0618 // Type or member is obsolete
                        new Lazy<IPortalController>(() => PortalController.Instance)))));

            // The module view locations and the module result capturing filter are part of the same
            // (previously static) view engine setup.
            services.AddDnnModuleExecution();

            services.AddOptions<MvcViewOptions>()
                .Configure<ModuleDelegatingViewEngine>((options, moduleViewEngine) =>
                {
                    if (!options.ViewEngines.Any(engine => engine is ModuleDelegatingViewEngine))
                    {
                        options.ViewEngines.Insert(0, moduleViewEngine);
                    }
                });

            return services;
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

        private static IEnumerable<ApplicationPart> GetApplicationParts(Assembly assembly)
        {
            foreach (var part in ApplicationPartFactory.GetApplicationPartFactory(assembly).GetApplicationParts(assembly))
            {
                yield return part;
            }

            // Precompiled views ship in a related assembly (e.g. MyModule.Views.dll).
            foreach (var related in RelatedAssemblyAttribute.GetRelatedAssemblies(assembly, throwOnError: false))
            {
                foreach (var part in ApplicationPartFactory.GetApplicationPartFactory(related).GetApplicationParts(related))
                {
                    yield return part;
                }
            }
        }
    }
}
