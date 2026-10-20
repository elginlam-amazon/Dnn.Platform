// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Website.Hosting
{
    using System;
    using System.IO;
    using System.Web.Helpers;
    using System.Web.UI;

    using DotNetNuke.Web;
    using DotNetNuke.Web.Common.Internal;
    using DotNetNuke.Web.Extensions;
    using DotNetNuke.Web.Mvc.Extensions;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Hosting;
    using Newtonsoft.Json.Serialization;

    /// <summary>The ASP.NET Core host of the DNN Platform (replaces the IIS integrated pipeline / <c>Global.asax</c>).</summary>
    public static class Program
    {
        /// <summary>The entry point of the application.</summary>
        /// <param name="args">The command line arguments.</param>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = Directory.GetCurrentDirectory(),

                // the site root holds the static files (skins, portals, resources), like in IIS
                WebRootPath = Directory.GetCurrentDirectory(),
            });

            var contentRoot = builder.Environment.ContentRootPath;
            var webConfig = WebConfigRuntime.Initialize(contentRoot);
            webConfig.ConfigureFormsAuthentication();

            ConfigureServices(builder.Services, webConfig, contentRoot);

            var app = builder.Build();

            // the DNN services are part of the host container (see DotNetNukeHttpApplication)
            DependencyInjectionInitialize.HostServiceProvider = app.Services;
            webConfig.ConfigureMembership();

            Configure(app, webConfig);
            app.Run();
        }

        private static void ConfigureServices(IServiceCollection services, WebConfigRuntime webConfig, string contentRoot)
        {
            // forms authentication tickets, anti-forgery tokens and MachineKey.Protect use ASP.NET Core data protection
            services.AddDataProtection()
                .SetApplicationName("DotNetNuke")
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(contentRoot, "App_Data", "DataProtection-Keys")));

            services.AddAntiforgery(options =>
            {
                options.Cookie.Name = AntiForgeryConfig.CookieName;
                options.FormFieldName = AntiForgeryConfig.FormFieldName;
                options.HeaderName = "RequestVerificationToken";
                options.SuppressXFrameOptionsHeader = true;
            });

            services.AddDistributedMemoryCache();
            services.AddSession();

            // System.Web (HttpContext, HttpApplication + the DNN HTTP modules of the web.config) and Web Forms
            services.AddSystemWebAdapters()
                .AddHttpApplication<DotNetNukeHttpApplication>(options =>
                {
                    foreach (var module in webConfig.GetHttpModules())
                    {
                        options.RegisterModule(module.Value, module.Key);
                    }
                })
                .AddWrappedAspNetCoreSession()
                .AddWebForms(options => { })
                .AddCompiledPages();

            // services framework (Web API) and MVC modules; the module views (.cshtml) are compiled at runtime from DesktopModules
            var mvc = services.AddControllersWithViews()
                .AddNewtonsoftJson(options => options.SerializerSettings.ContractResolver = new DefaultContractResolver());
#pragma warning disable ASPDEPR003 // the views of the installed modules are files deployed under DesktopModules and must be compiled at runtime
            mvc.AddRazorRuntimeCompilation(options => options.FileProviders.Add(new PhysicalFileProvider(contentRoot)));
#pragma warning restore ASPDEPR003

            // the DNN services (all IDnnStartup implementations) are registered in the host container
            services.AddDnnServices();
        }

        private static void Configure(WebApplication app, WebConfigRuntime webConfig)
        {
            if (!app.Environment.IsDevelopment())
            {
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseSession();
            app.UseSystemWebAdapters();
            app.UseWebFormsPageMethods();

            app.MapDnnServicesRoutes();
            app.MapDnnMvcRoutes();
            foreach (var handler in webConfig.GetHttpHandlers())
            {
                app.MapHttpHandler(handler.Key, handler.Value);
            }

            app.MapHttpHandlers();
        }
    }
}
