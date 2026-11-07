// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor
{
    using DotNetNuke.DependencyInjection;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.FileProviders;

    /// <summary>Registers the services of the razor module rendering pipeline.</summary>
    public class Startup : IDnnStartup
    {
        /// <inheritdoc />
        public void ConfigureServices(IServiceCollection services)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleControlFactory, RazorModuleControlFactory>());
#pragma warning restore CS0618 // Type or member is obsolete

            // The Razor pipeline this project renders through (IRazorPageFactoryProvider,
            // IRazorViewEngine, IRazorPageActivator, IModelMetadataProvider,
            // ITempDataDictionaryFactory) replaces System.Web.WebPages. Runtime compilation
            // replaces System.Web.Compilation.BuildManager: DNN module .cshtml/.vbhtml files are
            // installed into the site after the host was built, so they can only be compiled on
            // first use. Both calls are additive and safe when the host already made them.
            services.AddControllersWithViews().AddRazorRuntimeCompilation();
            services.AddHttpContextAccessor();

            // Module razor scripts live under the site root (e.g. ~/DesktopModules/Foo/_Foo.cshtml),
            // not under the MVC view roots, so the content root is added as a view file provider.
            services.AddOptions<MvcRazorRuntimeCompilationOptions>()
                .Configure<IWebHostEnvironment>((options, environment) =>
                    options.FileProviders.Add(new PhysicalFileProvider(environment.ContentRootPath)));
        }
    }
}
