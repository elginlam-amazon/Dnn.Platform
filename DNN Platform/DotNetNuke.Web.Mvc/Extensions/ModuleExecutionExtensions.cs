// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Extensions
{
    using System;
    using System.Linq;

    using DotNetNuke.Common;
    using DotNetNuke.ComponentModel;
    using DotNetNuke.Web.Mvc.Framework;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Mvc.Razor;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>Registers the services required to execute DNN MVC modules.</summary>
    public static class ModuleExecutionExtensions
    {
        /// <summary>
        /// Registers the DNN MVC module execution services: the module execution engine, the filter provider
        /// which supplies the result capturing filter, and the view location expander which contributes the
        /// <c>~/DesktopModules/MVC/{module}/Views/...</c> locations (replacing the legacy per-module
        /// <c>RazorViewEngine</c> location formats).
        /// </summary>
        /// <param name="services">Service Collection used to register services in the container.</param>
        /// <returns>The <paramref name="services"/>, for chaining.</returns>
        public static IServiceCollection AddDnnModuleExecution(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<IModuleExecutionEngine, ModuleExecutionEngine>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IFilterProvider, ModuleFilterProvider>());

            services.Configure<RazorViewEngineOptions>(options =>
            {
                if (!options.ViewLocationExpanders.Any(expander => expander is ModuleViewLocationExpander))
                {
                    options.ViewLocationExpanders.Add(new ModuleViewLocationExpander());
                }
            });

            return services;
        }

        /// <summary>Gets the <see cref="IModuleExecutionEngine"/> to execute a DNN MVC module with.</summary>
        /// <param name="services">The service provider of the current request (or <see langword="null"/> to use the application's provider).</param>
        /// <returns>The module execution engine.</returns>
        /// <remarks>
        /// The legacy code located the engine through DNN's <see cref="ComponentFactory"/>. That static
        /// locator is kept working (modules can still register their own engine into it), but it is now
        /// a bridge to the host's container instead of a second container: when nothing was registered the
        /// engine comes out of DI (see <see cref="AddDnnModuleExecution"/>) and that very instance is
        /// published back into the <see cref="ComponentFactory"/> for legacy callers.
        /// </remarks>
        public static IModuleExecutionEngine GetModuleExecutionEngine(IServiceProvider services)
        {
            var engine = ComponentFactory.GetComponent<IModuleExecutionEngine>();
            if (engine != null)
            {
                return engine;
            }

            var provider = services ?? Globals.GetCurrentServiceProvider();
            engine = provider?.GetService<IModuleExecutionEngine>() ?? new ModuleExecutionEngine();
            ComponentFactory.RegisterComponentInstance<IModuleExecutionEngine>(engine);

            return engine;
        }
    }
}
