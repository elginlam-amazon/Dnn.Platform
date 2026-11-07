// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Common.Internal;
    using DotNetNuke.Web.Api.Auth.ApiTokens;
    using DotNetNuke.Web.Api.Auth.ApiTokens.Repositories;
    using DotNetNuke.Web.Api.Internal;
    using DotNetNuke.Web.Common.Internal;
    using DotNetNuke.Web.Components.Controllers;
    using DotNetNuke.Web.ConfigSection;
    using DotNetNuke.Web.Extensions;
    using DotNetNuke.Web.Mvp;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.AspNetCore.Routing.Matching;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;

    using ServicesRoutingManager = DotNetNuke.Web.Api.Internal.ServicesRoutingManager;

    /// <summary>
    /// The host facing wiring of the DNN services framework (web API) infrastructure. These calls replace
    /// the mutation of <c>GlobalConfiguration.Configuration</c> (services, formatters, filter providers,
    /// message handlers and dependency resolver) which the legacy
    /// <c>ServicesRoutingManager.RegisterRoutes</c> performed, and the registration of the DNN routes into
    /// <c>RouteTable.Routes</c>.
    /// </summary>
    /// <remarks>
    /// <para>The DNN host application wires the services framework up like this:</para>
    /// <code>
    /// // ConfigureServices:
    /// builder.Services.AddDnnWebApiInfrastructure();   // folded into AddDnnWebApi(..)
    ///
    /// // Configure:
    /// app.UseRouting();
    /// app.UseDnnWebApiInfrastructure();   // DnnContextMiddleware + controller selection, MUST be after
    ///                                     // UseRouting and BEFORE UseAuthentication/UseAuthorization.
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.UseEndpoints(endpoints =&gt;
    /// {
    ///     endpoints.MapDnnServiceRoutes();  // the named DNN service endpoints, before any catch-all route.
    /// });
    /// </code>
    /// </remarks>
    public static class DnnWebApiHostWiringExtensions
    {
        /// <summary>
        /// The single service-registration entry point of this assembly: the DNN services framework
        /// (web API) infrastructure, the <c>&lt;providers&gt;</c> DI graph, the service controllers, the
        /// authentication-handler options and the application-initialization hosted service.
        /// </summary>
        /// <param name="services">The host's service collection.</param>
        /// <param name="configuration">The host's configuration (the <c>web.config</c> replacement).</param>
        /// <returns>The <paramref name="services"/>, for chaining.</returns>
        /// <remarks>
        /// Called from <c>Website/Program.cs</c>. Everything it registers uses <c>TryAdd</c>, so it is safe
        /// that <c>AddDnnPlatform</c> also runs this assembly's <see cref="DotNetNuke.Web.Startup"/>
        /// (<c>IDnnStartup</c>), which registers the same graph minus the configuration binding.
        /// </remarks>
        public static IServiceCollection AddDnnWebApi(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // <dotnetnuke><authServices><messageHandlers> -> DotNetNuke:authServices:messageHandlers.
            services.Configure<AuthServicesOptions>(configuration.GetSection(AuthServicesOptions.SectionName));

            return services.AddDnnWebApiCore();
        }

        /// <summary>
        /// Adds the services framework request pipeline: the <c>BeginRequest</c>-stage work of the former
        /// <c>DotNetNukeHttpApplication</c>, the DNN request context (portal settings, tab and module), the
        /// namespace-scoped controller selection and the ordered DNN authentication-handler chain.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The <paramref name="app"/>, for chaining.</returns>
        /// <remarks>
        /// Must be called AFTER <c>UseRouting</c> and BEFORE <c>UseAuthentication</c>/<c>UseAuthorization</c>;
        /// the host passes it to <c>UseDnnPlatform</c>'s <c>configureAfterRouting</c> hook, which is exactly
        /// that position.
        /// </remarks>
        public static IApplicationBuilder UseDnnWebApi(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            app.UseMiddleware<DnnWebBeginRequestMiddleware>();

            return app.UseDnnWebApiInfrastructure();
        }

        /// <summary>Registers the services framework infrastructure into the host's service collection.</summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The <paramref name="services"/>, for chaining.</returns>
        public static IServiceCollection AddDnnWebApiInfrastructure(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddHttpContextAccessor();

            // The routing manager: collects the routes of every IServiceRouteMapper (RegisterRoutes) and
            // maps them as endpoints (MapDnnServiceRoutes).
            services.TryAddSingleton<ServicesRoutingManager>();
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IRoutingManager), typeof(ServicesRoutingManager), ServiceLifetime.Singleton));

            // The ordered authentication handler chain, which replaces
            // GlobalConfiguration.Configuration.MessageHandlers.
            services.TryAddSingleton<IDnnAuthMessageHandlerCollection, DnnAuthMessageHandlerCollection>();

            // Namespace scoped controller selection; replaces the IHttpControllerSelector replacement.
            services.TryAddSingleton<DnnControllerSelector>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, DnnControllerNamespaceMatcherPolicy>());

            // Tracing: replaces the ITraceWriter service replacement.
            services.TryAddSingleton<IDnnTraceWriter>(_ => new TraceWriter(ServicesRoutingManager.IsTracingEnabled()));

            // Replaces the ActionDescriptorFilterProvider replacement, which appended the default
            // host-level authorization unless an IOverrideDefaultAuthLevel filter was present.
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IFilterProvider, DnnActionFilterProvider>());

            // The standard tab and module id provider, formerly added through
            // HttpConfiguration.AddTabAndModuleInfoProvider.
            services.AddTabAndModuleInfoProvider(new StandardTabAndModuleInfoProvider());

            // Anti-forgery: replaces the System.Web.Helpers.AntiForgery wrapper behind
            // DotNetNuke.Web.Api.Internal.AntiForgery.Instance. The cookie, header and form field names
            // are pinned to their legacy values so existing DNN client script keeps working, and so the
            // iframe upload flow keeps finding __RequestVerificationToken in the posted form.
            services.AddAntiforgery(options =>
            {
                options.Cookie.Name = AntiForgeryImpl.DefaultCookieName;
                options.FormFieldName = AntiForgeryImpl.DefaultFormFieldName;
                options.HeaderName = AntiForgeryImpl.DefaultHeaderName;
            });
            services.TryAddTransient<IAntiForgery, AntiForgeryImpl>();

            // The media type formatter for text/html and text/plain which passed strings straight through.
            services.Configure<MvcOptions>(options =>
            {
                if (!options.InputFormatters.Any(formatter => formatter is StringPassThroughInputFormatter))
                {
                    options.InputFormatters.Add(new StringPassThroughInputFormatter());
                }

                if (!options.OutputFormatters.Any(formatter => formatter is StringPassThroughOutputFormatter))
                {
                    options.OutputFormatters.Add(new StringPassThroughOutputFormatter());
                }

                // Restores the Web API 2 rule that a complex-typed action parameter is read from the request
                // body. DnnApiController cannot carry [ApiController] (it would force attribute routing onto
                // every conventionally-routed service action), so without this convention every JSON POST in
                // the services framework silently binds a default-valued DTO.
                if (!options.Conventions.Any(convention => convention is DnnApiBodyBindingConvention))
                {
                    options.Conventions.Add(new DnnApiBodyBindingConvention());
                }
            });

            return services;
        }

        /// <summary>
        /// Adds the services framework request pipeline: the DNN request context (portal settings, tab and
        /// module) and the namespace scoped controller selection errors.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The <paramref name="app"/>, for chaining.</returns>
        /// <remarks>Must be called after <c>UseRouting</c> and before <c>UseAuthentication</c>/<c>UseAuthorization</c>.</remarks>
        public static IApplicationBuilder UseDnnWebApiInfrastructure(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            app.UseMiddleware<DnnContextMiddleware>();
            app.UseMiddleware<DnnControllerSelectionMiddleware>();

            // The DNN authentication handler chain (Basic, Digest, API token, ... and the WebForms cookie
            // bridge last), which replaces HttpConfiguration.MessageHandlers. It runs AFTER
            // DnnContextMiddleware because the handlers resolve the current portal
            // (PortalController.Instance.GetCurrentSettings()) to scope credential validation, and BEFORE
            // UseAuthentication/UseAuthorization so that the principal it establishes is the one the
            // authorization filters decide on.
            app.UseMiddleware<DnnAuthMessageHandlerMiddleware>();

            return app;
        }

        /// <summary>
        /// Maps the DNN service routes (the routes contributed by every <see cref="IServiceRouteMapper"/>
        /// of the installed modules, including the <c>-old</c>
        /// <c>DesktopModules/{namespace}/API/{controller}/{action}</c> compatibility routes) as endpoints.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder of the host.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        /// <remarks>Must be called before the host maps its catch-all route.</remarks>
        public static IEndpointRouteBuilder MapDnnServiceRoutes(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            GetRoutingManager(endpoints.ServiceProvider).MapRoutes(endpoints);

            // The MVP handlers and web services, which replace the <httpHandlers> registered .ashx
            // handlers and the .asmx services of the WebForms host.
            endpoints.MapDnnMvpEndpoints();

            return endpoints;
        }

        /// <summary>Registers everything <see cref="AddDnnWebApi"/> adds except the configuration binding.</summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The <paramref name="services"/>, for chaining.</returns>
        internal static IServiceCollection AddDnnWebApiCore(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            // Publish the HOST collection for diagnostics (the Prompt "list-services" command reads it).
            // This is the same collection everything else is registered into — there is no second container.
            DependencyInjectionInitialize.RegisterServiceCollection(services);

            // MVC: the service controllers are MVC controllers now, so the MVC services (controller
            // factory, action invoker, formatters) must exist. AddControllers is idempotent.
            //
            // AddNewtonsoftJson is REQUIRED for wire-format parity, not a preference. On .NET Framework
            // the services framework serialized with Web API 2's JsonMediaTypeFormatter (Newtonsoft),
            // which honours [DataContract]/[DataMember(Name = "...")] and emits PascalCase member names
            // by default. The ASP.NET Core default (System.Text.Json with JsonSerializerDefaults.Web)
            // ignores the DataContract attributes entirely AND camel-cases every name, so every existing
            // client script and third-party caller would silently receive different JSON keys -- a
            // runtime-only break that a green build cannot reveal. The DTOs that depend on this include
            // UI/WebControls/{DnnFileUploadResources,DnnFileUploadOptions,DnnDropDownListOptions,
            // DnnDropDownListState,DnnConfirmPasswordOptions,DnnPaswordStrengthOptions,ItemListOptions,
            // ItemListServicesOptions,Internal/OptionItem,Internal/DnnComboBoxOption,Internal/RenderOption},
            // InternalServices/{ItemListServiceController,FileUploadController} and
            // Models/{SiteDetail,ModuleDetail,ModuleInstance}.
            services.AddControllers().AddNewtonsoftJson();

            // Routing manager, auth handler chain, controller selection, trace writer, filter provider,
            // tab/module info providers, antiforgery and the pass-through formatters.
            services.AddDnnWebApiInfrastructure();

            // Every DnnApiController as a scoped service + its assembly as an MVC application part.
            services.AddWebApi();

            // The DNN <providers> model: 20 ProviderInstaller contracts (singleton, except the HTML editor
            // and navigation control which are transient) + the 3 keyed FolderProviders.
            services.AddDnnProviders();

            services.TryAddTransient<IApiTokenController, ApiTokenController>();
            services.TryAddTransient<IApiTokenRepository, ApiTokenRepository>();
            services.TryAddTransient<IControlBarController, ControlBarController>();
            services.TryAddTransient<ApiTokenSettingsRepository>();

            // The dangerous-file host alert (formerly the DotNetNukeSecurity static class).
            services.TryAddSingleton<DotNetNukeSecurity>();

            // The port of the DotNetNukeHttpApplication static constructor + Application_Start/_End.
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DnnWebApplicationInitializationService>());

            return services;
        }

        private static ServicesRoutingManager GetRoutingManager(IServiceProvider services)
        {
            var fromContainer = services?.GetService<ServicesRoutingManager>()
                ?? services?.GetService<IEnumerable<IRoutingManager>>()?.OfType<ServicesRoutingManager>().FirstOrDefault();

            return fromContainer
                ?? ActivatorUtilities.CreateInstance<ServicesRoutingManager>(
                    services ?? DotNetNuke.Common.Globals.GetCurrentServiceProvider());
        }
    }
}
