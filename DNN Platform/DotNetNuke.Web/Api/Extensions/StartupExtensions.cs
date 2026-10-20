// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Extensions
{
    using System.Linq;

    using DotNetNuke.DependencyInjection.Extensions;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Web.Api;
    using DotNetNuke.Web.Api.Internal;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    /// <summary>Service registration extensions of the services framework.</summary>
    internal static class StartupExtensions
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(StartupExtensions));

        /// <summary>Registers the services framework controllers and the ASP.NET Core MVC extensions used by the services framework.</summary>
        /// <param name="services">The services collection.</param>
        public static void AddWebApi(this IServiceCollection services)
        {
            var allTypes = TypeExtensions.SafeGetTypes();
            allTypes.LogOtherExceptions(Logger);

            var controllerTypes = allTypes.Types
                .Where(
                    type => typeof(DnnApiController).IsAssignableFrom(type) &&
                            type is { IsClass: true, IsAbstract: false });
            foreach (var controller in controllerTypes)
            {
                services.TryAddScoped(controller);
            }

            services.TryAddEnumerable(ServiceDescriptor.Singleton<IFilterProvider, DnnActionFilterProvider>());
            services.Configure<MvcOptions>(options =>
            {
                // HttpResponseMessage results (Request.CreateResponse) and the ASP.NET Web API parameter binding rules
                if (!options.OutputFormatters.OfType<HttpResponseMessageOutputFormatter>().Any())
                {
                    options.OutputFormatters.Insert(0, new HttpResponseMessageOutputFormatter());
                }

                if (!options.Conventions.OfType<DnnApiControllerConvention>().Any())
                {
                    options.Conventions.Add(new DnnApiControllerConvention());
                }
            });
        }
    }
}
