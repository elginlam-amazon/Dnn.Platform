// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Extensions
{
    using System.Linq;

    using DotNetNuke.DependencyInjection.Extensions;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Web.Mvc.Framework;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Mvc.Razor;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    /// <summary>Adds DNN MVC specific startup extensions.</summary>
    public static class StartupExtensions
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(StartupExtensions));

        /// <summary>Configures all of the <see cref="DnnController"/>'s to be used with the Service Collection for Dependency Injection.</summary>
        /// <param name="services">Service Collection used to registering services in the container.</param>
        public static void AddMvcControllers(this IServiceCollection services)
        {
            var allTypes = TypeExtensions.SafeGetTypes();
            allTypes.LogOtherExceptions(Logger);

            var controllerTypes = allTypes.Types
                .Where(
                    type => typeof(IDnnController).IsAssignableFrom(type) &&
                            type is { IsClass: true, IsAbstract: false });
            foreach (var controller in controllerTypes)
            {
                services.TryAddTransient(controller);
            }
        }

        /// <summary>Registers the DNN MVC services (view location expander and MVC controllers) with the service collection.</summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The same service collection for chaining.</returns>
        public static IServiceCollection AddDnnMvc(this IServiceCollection services)
        {
            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.ViewLocationExpanders.Add(new DnnModuleViewLocationExpander());
            });

            services.AddMvcControllers();

            return services;
        }

        /// <summary>Maps the DNN MVC module routes (collected from <see cref="IMvcRouteMapper"/> implementations) as ASP.NET Core endpoints.</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The same endpoint route builder for chaining.</returns>
        public static IEndpointRouteBuilder MapDnnMvcRoutes(this IEndpointRouteBuilder endpoints)
        {
            foreach (var route in MvcRoutingManager.GetCollectedRoutes())
            {
                var defaults = route.Defaults ?? new RouteValueDictionary();
                var constraints = route.Constraints ?? new RouteValueDictionary();

                endpoints.MapControllerRoute(
                    name: route.Name,
                    pattern: route.Url,
                    defaults: defaults,
                    constraints: constraints);
            }

            return endpoints;
        }
    }
}
