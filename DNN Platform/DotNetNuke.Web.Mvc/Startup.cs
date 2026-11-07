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
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>Registers the DNN MVC module host services into the host's service collection.</summary>
    public class Startup : IDnnStartup
    {
        /// <summary>The anti-forgery cookie name used by DNN (the <c>System.Web.Helpers.AntiForgeryConfig</c> default).</summary>
        internal const string LegacyAntiForgeryCookieName = "__RequestVerificationToken";

        /// <summary>The anti-forgery form field name used by DNN.</summary>
        internal const string LegacyAntiForgeryFormFieldName = "__RequestVerificationToken";

        /// <summary>The anti-forgery request header name used by the DNN services framework.</summary>
        internal const string LegacyAntiForgeryHeaderName = "RequestVerificationToken";

        /// <inheritdoc />
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IDesktopModuleController, DesktopModuleControllerAdapter>();
            services.AddTransient<IModuleControlController, ModuleControlControllerAdapter>();

            // IControllerFactory is registered by AddControllers()/AddMvcCore() in the host
            // (the ASP.NET Core DefaultControllerFactory is internal), so it is not registered here.
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleControlFactory, MvcModuleControlFactory>());
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IRoutingManager), typeof(MvcRoutingManager), ServiceLifetime.Singleton));

            // Previously located through ComponentFactory.GetComponent<ModuleRoutingProvider>() (which fell
            // back to creating a StandardModuleRoutingProvider). ModuleRoutingProvider.Instance() now
            // resolves this registration, and keeps the ComponentFactory working as a bridge to it.
            services.TryAddSingleton<ModuleRoutingProvider, StandardModuleRoutingProvider>();

            // In ASP.NET Core the MVC controllers are activated from the request's service provider,
            // which replaces the legacy DependencyResolver.SetResolver(new DnnMvcDependencyResolver(...)) call.
            // ServiceBasedControllerActivator makes every controller come out of the container, which is
            // what the DNN dependency resolver used to guarantee.
            services.TryAddSingleton<IControllerActivator, ServiceBasedControllerActivator>();
            services.TryAddSingleton<DnnMvcDependencyResolver>();

            services.AddHttpContextAccessor();

            // The MVC module host controllers (including those living in module assemblies).
            services.AddMvcControllers();

            // View engines previously installed into the static System.Web.Mvc ViewEngines.Engines collection.
            services.AddDnnMvcViewEngines();

            // Tab/module discovery providers previously added through HttpConfiguration.AddTabAndModuleInfoProvider.
            services.AddTabAndModuleInfoProvider(new StandardTabAndModuleInfoProvider());

            // Anti-forgery: replaces the ServiceLocator<IAntiForgery, AntiForgery> static locator.
            // The cookie, header and form field names are kept as they were under
            // System.Web.Helpers.AntiForgeryConfig so existing clients keep working.
            services.AddAntiforgery(options =>
            {
                options.Cookie.Name = LegacyAntiForgeryCookieName;
                options.FormFieldName = LegacyAntiForgeryFormFieldName;
                options.HeaderName = LegacyAntiForgeryHeaderName;
            });
            services.TryAddTransient<IAntiForgery, AntiForgeryImpl>();
        }
    }
}
