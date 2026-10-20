// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc
{
    using DotNetNuke.Common.Internal;
    using DotNetNuke.DependencyInjection;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Common;
    using DotNetNuke.Web.Mvc.Extensions;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>The DNN startup for the MVC module pipeline.</summary>
    public class Startup : IDnnStartup
    {
        /// <inheritdoc />
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IDesktopModuleController, DesktopModuleControllerAdapter>();
            services.AddTransient<IModuleControlController, ModuleControlControllerAdapter>();

            services.TryAddSingleton<IModuleExecutionEngine, ModuleExecutionEngine>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleControlFactory, MvcModuleControlFactory>());
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IRoutingManager), typeof(MvcRoutingManager), ServiceLifetime.Singleton));

            services.AddDnnMvc();
        }
    }
}
