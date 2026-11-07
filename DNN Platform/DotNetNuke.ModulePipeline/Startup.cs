// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.ModulePipeline
{
    using DotNetNuke.DependencyInjection;
    using DotNetNuke.UI.Modules;
    using Microsoft.Extensions.DependencyInjection;

    /// <inheritdoc />
    public class Startup : IDnnStartup
    {
        /// <inheritdoc />
        public void ConfigureServices(IServiceCollection services)
        {
            // The pipeline receives every IModuleControlFactory that the other DNN startups add
            // through TryAddEnumerable (WebForms/Html5/Reflected from DotNetNuke.Library, Razor from
            // DotNetNuke.Web.Razor, MVC from DotNetNuke.Web.Mvc) and selects between them by Priority.
            // Module assemblies that contribute those factories and their controllers are loaded
            // through AssemblyLoadContext.Default and registered as MVC application parts by
            // DotNetNuke.Web.Mvc's Startup (StartupExtensions.AddMvcControllers -> AddApplicationPart),
            // so this project must not repeat that registration.
            services.AddSingleton<IModuleControlPipeline, ModuleControlPipeline>();
        }
    }
}
