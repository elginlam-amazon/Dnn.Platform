// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;

    using DotNetNuke.DependencyInjection;
    using DotNetNuke.Entities.Urls;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Authentication;
    using DotNetNuke.Services.Authentication;
    using DotNetNuke.Services.Cache;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.GeneratedImage;
    using DotNetNuke.Services.Sitemap;
    using DotNetNuke.Services.Syndication;
    using DotNetNuke.Services.UserProfile;
    using DotNetNuke.UI.Utilities;
    using DotNetNuke.UI.WebControls;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    using CookieOptions = DotNetNuke.Common.Options.AuthenticationCookieOptions;
    using DnnHost = DotNetNuke.Entities.Host.Host;

    /// <summary>
    /// The host application's entry point into the DNN platform: one call to register every
    /// platform service, one call to build the request pipeline, and one call to map the legacy
    /// handler URLs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On .NET Framework these three concerns lived in <c>web.config</c> (modules, handlers,
    /// providers, membership, globalization) and in <c>Global.asax</c>. There is exactly ONE
    /// service container: the host's. <see cref="AddDnnPlatform"/> runs every
    /// <see cref="IDnnStartup"/> in the application — including the platform's own
    /// <see cref="DotNetNuke.Startup"/> — against the host's <see cref="IServiceCollection"/>,
    /// so nothing builds a second provider.
    /// </para>
    /// <para>
    /// Typical <c>Program.cs</c>:
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.Services.AddDnnPlatform(builder.Configuration, builder.Environment);
    /// var app = builder.Build();
    /// app.UseDnnPlatform();
    /// app.MapRazorComponents&lt;App&gt;()
    ///    .AddInteractiveServerRenderMode()
    ///    .AddAdditionalAssemblies(DnnPlatformComponents.Assembly);
    /// app.Run();
    /// </code>
    /// </para>
    /// </remarks>
    public static class DnnPlatformApplicationExtensions
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(DnnPlatformApplicationExtensions));

        /// <summary>
        /// Registers the whole DNN platform service graph, the framework services it depends on,
        /// cookie authentication, authorization, Razor components and localization.
        /// </summary>
        /// <param name="services">The host's service collection.</param>
        /// <param name="configuration">The host's configuration (replaces <c>web.config</c>).</param>
        /// <param name="environment">The host's environment; registered with <see cref="HttpContextSource"/> so that virtual-path mapping works from the very first registration onwards.</param>
        /// <returns>The same <paramref name="services"/>, for chaining.</returns>
        public static IServiceCollection AddDnnPlatform(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
        {
            Requires.NotNull(nameof(services), services);
            Requires.NotNull(nameof(configuration), configuration);
            Requires.NotNull(nameof(environment), environment);

            // Must happen before any IDnnStartup runs: the platform resolves "~" during
            // registration (DotNetNuke.Startup passes the application path to Serilog).
            HttpContextSource.RegisterWebHostEnvironment(environment);

            // web.config replacements: MachineKey, HttpRuntime, CustomErrors, MobileRedirect and
            // ImageHandler options, plus the request-size limits.
            services.AddDnnPlatformConfiguration(configuration);

            // <membership>, <roleManager>, <profile> and data protection.
            services.AddDnnPlatformSecurity(configuration);

            // The RSS endpoint's feed readers/writers.
            services.AddDnnSyndication();

            // Scoped IClientApiPage: without it DNN client variables and keyed client script
            // blocks cannot be emitted.
            services.AddDnnWebUtility(configuration);

            // Blazor Server hosting for the migrated .ascx/.aspx UI.
            services.AddRazorComponents().AddInteractiveServerComponents();
            services.AddCascadingAuthenticationState();

            // Request timeouts: FolderManager/FileManager call
            // IHttpRequestTimeoutFeature.DisableTimeout() for long imports, which is the
            // replacement for Server.ScriptTimeout. The feature only exists when the
            // middleware is in the pipeline.
            services.AddRequestTimeouts();

            AddCookieAuthentication(services, configuration);

            // Every IDnnStartup in the application, into THIS collection.
            AddDnnStartupServices(services);

            return services;
        }

        /// <summary>
        /// Builds the DNN request pipeline and maps the legacy handler URLs.
        /// </summary>
        /// <param name="app">The host's application builder.</param>
        /// <returns>The same <paramref name="app"/>, for chaining.</returns>
        /// <remarks>
        /// The order below is not arbitrary:
        /// <list type="bullet">
        /// <item><description>the URL rewriter reads <c>HttpContext.User</c> and the DNN auth-token claim, so it must come after <c>UseAuthentication</c>;</description></item>
        /// <item><description>it mutates <c>Request.Path</c>/<c>QueryString</c>, so it must come before the endpoint middleware;</description></item>
        /// <item><description>it publishes the resolved <c>PortalSettings</c> into <c>HttpContext.Items</c>, which is the only slot <c>PortalSettings.Current</c> reads.</description></item>
        /// </list>
        /// </remarks>
        public static WebApplication UseDnnPlatform(this WebApplication app)
        {
            return app.UseDnnPlatform(configureEarly: null, configureAfterRouting: null, configureAfterAuthentication: null);
        }

        /// <summary>
        /// Builds the DNN request pipeline and maps the legacy handler URLs, letting the host insert
        /// middleware at the <c>BeginRequest</c> and <c>AuthenticateRequest</c> positions.
        /// </summary>
        /// <param name="app">The host's application builder.</param>
        /// <param name="configureEarly">Invoked BEFORE <c>UseRouting</c>. May be <see langword="null"/>.</param>
        /// <param name="configureAfterAuthentication">Invoked after the URL rewriter. May be <see langword="null"/>.</param>
        /// <returns>The same <paramref name="app"/>, for chaining.</returns>
        public static WebApplication UseDnnPlatform(
            this WebApplication app,
            Action<IApplicationBuilder> configureEarly,
            Action<IApplicationBuilder> configureAfterAuthentication)
        {
            return app.UseDnnPlatform(configureEarly, configureAfterRouting: null, configureAfterAuthentication);
        }

        /// <summary>
        /// Builds the DNN request pipeline and maps the legacy handler URLs, letting the host
        /// insert the middleware that live outside this assembly at the two positions where the
        /// platform needs them.
        /// </summary>
        /// <param name="app">The host's application builder.</param>
        /// <param name="configureEarly">
        /// Invoked BEFORE <c>UseRouting</c>, i.e. at the position of the <c>BeginRequest</c>
        /// <c>IHttpModule</c> stage. <c>DotNetNuke.HttpModules</c> fills this with
        /// <c>UseDnnEarlyRequestMiddleware()</c> (request DI scope, request filter, services
        /// framework, mobile redirect). May be <see langword="null"/>.
        /// </param>
        /// <param name="configureAfterRouting">
        /// Invoked immediately AFTER <c>UseRouting</c> and BEFORE
        /// <c>UseAuthentication</c>/<c>UseAuthorization</c>. <c>DotNetNuke.Web</c> fills this with
        /// <c>UseDnnWebApi()</c> (the services-framework BeginRequest work, the DNN request
        /// context, namespace-scoped controller selection and the ordered DNN authentication
        /// handler chain), all of which need the matched endpoint and must establish the principal
        /// the authorization filters then decide on. May be <see langword="null"/>.
        /// </param>
        /// <param name="configureAfterAuthentication">
        /// Invoked immediately AFTER the URL rewriter (which itself runs after
        /// <c>UseAuthorization</c>), i.e. at the position of the
        /// <c>AuthenticateRequest</c>/<c>AuthorizeRequest</c> stage — on ASP.NET that stage ran
        /// after every <c>BeginRequest</c> module, the rewriter included, and these middleware
        /// need the <c>PortalSettings</c> the rewriter publishes.
        /// <c>DotNetNuke.HttpModules</c> fills this with
        /// <c>UseDnnPostAuthenticationMiddleware()</c> (membership, users online, mobile
        /// redirect, localization, personalization, output caching, analytics). May be
        /// <see langword="null"/>.
        /// </param>
        /// <returns>The same <paramref name="app"/>, for chaining.</returns>
        /// <remarks>
        /// These are delegates rather than direct calls because <c>DotNetNuke.Library</c> cannot
        /// reference <c>DotNetNuke.HttpModules</c> (the dependency runs the other way), yet the
        /// ported modules must sit at an exact position in this pipeline. The hooks never add
        /// routing or authentication themselves — those stay owned by this method.
        /// </remarks>
        public static WebApplication UseDnnPlatform(
            this WebApplication app,
            Action<IApplicationBuilder> configureEarly,
            Action<IApplicationBuilder> configureAfterRouting,
            Action<IApplicationBuilder> configureAfterAuthentication)
        {
            Requires.NotNull(nameof(app), app);

            // ---- static seams -----------------------------------------------------------
            // Each of these throws/logs loudly when unset, so they are wired before anything
            // can serve a request.
            HttpContextSource.RegisterAccessor(app.Services.GetRequiredService<IHttpContextAccessor>());
            HttpContextSource.RegisterWebHostEnvironment(app.Services.GetRequiredService<IWebHostEnvironment>());
            MemoryCacheSource.RegisterInstance(app.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>());

            // Bridges Framework.ServiceLocator and Globals.DependencyProvider to the SAME container.
            app.Services.UseDnnPlatformServices();

            // ---- pipeline ---------------------------------------------------------------
            app.UseDnnExceptionHandler();
            app.UseRequestTimeouts();
            app.UseStaticFiles();
            app.UseRequestLocalization();

            // BeginRequest-stage modules: before routing, so a filtered or redirected request
            // never reaches an endpoint, and so the per-request DI scope exists for everything
            // that follows.
            configureEarly?.Invoke(app);

            app.UseRouting();

            // Post-routing, pre-authentication stage: the DNN services framework
            // (DotNetNuke.Web's UseDnnWebApi). It needs the matched endpoint, and the principal
            // its authentication-handler chain establishes is the one UseAuthorization decides on.
            configureAfterRouting?.Invoke(app);

            app.UseAuthentication();
            app.UseAuthorization();

            // AFTER auth (reads the user), BEFORE endpoints (rewrites the path).
            app.UseDnnUrlRewriter();

            // AuthenticateRequest/AuthorizeRequest-stage modules. They must run AFTER the URL
            // rewriter even though their legacy events fired "before" it, because on ASP.NET the
            // rewriter was a BeginRequest module and the runtime raises events by STAGE: every
            // AuthenticateRequest/ResolveRequestCache/PreRequestHandlerExecute handler therefore
            // ran once all BeginRequest handlers had completed. The rewriter is what publishes
            // the resolved PortalSettings into HttpContext.Items, and membership, users online,
            // localization and output caching all read it — placing this hook before the rewriter
            // would make every one of them a silent no-op. This is still before the endpoint
            // executes, which the output-cache and analytics response rewriting depend on.
            configureAfterAuthentication?.Invoke(app);

            // Emits the DNN client variables / keyed client scripts for the current request.
            app.UseDnnWebUtility();

            app.UseAntiforgery();

            // ---- legacy handler URLs ----------------------------------------------------
            app.MapDnnPlatformEndpoints();

            return app;
        }

        /// <summary>
        /// Maps every URL that used to be served by an <c>IHttpHandler</c> registered in
        /// <c>web.config</c>. A missing call here is an unreachable legacy URL.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The same <paramref name="endpoints"/>, for chaining.</returns>
        /// <remarks>
        /// None of these endpoints requires authorization: each one performs DNN's own
        /// per-portal/per-tab/per-folder permission check and redirects to
        /// <c>AccessDeniedURL</c> itself. Adding <c>RequireAuthorization()</c> would change
        /// anonymous behaviour that portals depend on (public RSS, public files, sitemaps).
        /// </remarks>
        public static IEndpointRouteBuilder MapDnnPlatformEndpoints(this IEndpointRouteBuilder endpoints)
        {
            Requires.NotNull(nameof(endpoints), endpoints);

            endpoints.MapDnnLogOff();           // Logoff.aspx
            endpoints.MapCaptchaHandler();      // ImageChallenge.captcha.aspx + *.captcha.aspx
            endpoints.MapDnnImageHandler();     // DnnImageHandler.ashx
            endpoints.MapDnnRss();              // RSS.aspx
            endpoints.MapDnnUserProfilePic();   // ProfilePic.ashx
            endpoints.MapDnnUserProfilePage();  // User.aspx
            endpoints.MapDnnFileServer();       // LinkClick.aspx
            endpoints.MapDnnSitemap();          // Sitemap.aspx
            endpoints.MapDnnWebUtilityScripts();// the DNN client-script resources

            return endpoints;
        }

        /// <summary>
        /// Installs the DNN error handling that replaces <c>&lt;customErrors&gt;</c> and
        /// <c>Application_Error</c>.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The same <paramref name="app"/>, for chaining.</returns>
        /// <remarks>
        /// <c>DBLoggingProvider</c> publishes the failure on <c>IExceptionHandlerFeature</c> and
        /// sets a 500; without an exception-handler branch in the pipeline that 500 renders as a
        /// blank page. The behaviour here matches
        /// <see cref="Exceptions.ProcessPageLoadException(Exception, string)"/>: log, then either
        /// show DNN's own error page or let the raw error surface, according to the
        /// <c>UseCustomErrorMessages</c> host setting.
        /// </remarks>
        public static IApplicationBuilder UseDnnExceptionHandler(this IApplicationBuilder app)
        {
            Requires.NotNull(nameof(app), app);

            return app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
            {
                var feature = context.Features.Get<IExceptionHandlerPathFeature>();
                var exception = feature?.Error;
                if (exception != null)
                {
                    Exceptions.LogException(exception);
                }

                var useCustomErrors = true;
                try
                {
                    useCustomErrors = DnnHost.UseCustomErrorMessages;
                }
                catch (Exception settingsException)
                {
                    // The host settings live in the database; a failure to read them (for
                    // instance when the database itself is what failed) must not mask the
                    // original exception. Fall back to the friendly page.
                    Logger.LogError(settingsException, "Reading the UseCustomErrorMessages host setting while handling an unhandled exception failed.");
                }

                if (useCustomErrors)
                {
                    context.Response.Redirect(Globals.ResolveUrl(DotNetNuke.Common.ErrorPage.Path));
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(
                    "<html><head><title>Error</title></head><body><h2>An error has occurred.</h2></body></html>").ConfigureAwait(false);
            }));
        }

        /// <summary>
        /// Runs every <see cref="IDnnStartup"/> in the application against
        /// <paramref name="services"/>.
        /// </summary>
        /// <param name="services">The host's service collection.</param>
        /// <remarks>
        /// This is the DNN extensibility convention (modules and providers contribute their own
        /// registrations through an <see cref="IDnnStartup"/>), kept intact — but writing into the
        /// host's collection instead of building a second container.
        /// </remarks>
        private static void AddDnnStartupServices(IServiceCollection services)
        {
            foreach (var startupType in GetDnnStartupTypes())
            {
                IDnnStartup startup;
                try
                {
                    startup = (IDnnStartup)Activator.CreateInstance(startupType);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Unable to instantiate DNN startup type {StartupType}.", startupType.FullName);
                    continue;
                }

                try
                {
                    startup.ConfigureServices(services);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Unable to configure services for DNN startup type {StartupType}.", startupType.FullName);
                }
            }
        }

        private static IEnumerable<Type> GetDnnStartupTypes()
        {
            // ASP.NET loaded every assembly in bin/ eagerly; on modern .NET assemblies load
            // lazily, so an extension whose types are not referenced yet would never be seen.
            LoadApplicationAssemblies();

            return AssemblyLoadContext.Default.Assemblies
                .Where(assembly => !assembly.IsDynamic)
                .SelectMany(GetLoadableTypes)
                .Where(type => typeof(IDnnStartup).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false })
                .ToList();
        }

        private static void LoadApplicationAssemblies()
        {
            var loaded = AssemblyLoadContext.Default.Assemblies
                .Where(assembly => !assembly.IsDynamic)
                .Select(assembly => assembly.GetName().Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string[] files;
            try
            {
                files = Directory.GetFiles(AppContext.BaseDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            }
            catch (IOException ex)
            {
                Logger.LogError(ex, "Unable to enumerate the application directory while discovering DNN startup types.");
                return;
            }

            foreach (var file in files)
            {
                var simpleName = Path.GetFileNameWithoutExtension(file);
                if (!loaded.Add(simpleName))
                {
                    continue;
                }

                try
                {
                    AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
                }
                catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException)
                {
                    // Native/unmanaged or mismatched assemblies in the output folder are expected
                    // and simply carry no startup logic; anything else is worth knowing about.
                    Logger.LogDebug(ex, "Skipping {Assembly} while discovering DNN startup types.", simpleName);
                }
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Logger.LogWarning(ex, "Unable to load all types from {Assembly} while discovering DNN startup types.", assembly.FullName);
                return ex.Types.Where(type => type != null);
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                Logger.LogWarning(ex, "Unable to load types from {Assembly} while discovering DNN startup types.", assembly.FullName);
                return Enumerable.Empty<Type>();
            }
        }

        private static void AddCookieAuthentication(IServiceCollection services, IConfiguration configuration)
        {
            // The values that used to live in <authentication mode="Forms"><forms …>. The defaults
            // are DNN's shipped values, so an existing installation keeps its .DOTNETNUKE cookie —
            // and therefore its live sessions — across the migration.
            var cookie = new CookieOptions();
            configuration.GetSection(CookieOptions.SectionName).Bind(cookie);

            services
                .AddAuthentication(DnnAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(DnnAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.Name = cookie.Name;
                    options.Cookie.Path = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path;
                    if (!string.IsNullOrEmpty(cookie.Domain))
                    {
                        options.Cookie.Domain = cookie.Domain;
                    }

                    options.Cookie.SecurePolicy = cookie.RequireSsl
                        ? Microsoft.AspNetCore.Http.CookieSecurePolicy.Always
                        : Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
                    options.Cookie.HttpOnly = true;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(cookie.TimeoutMinutes);
                    options.SlidingExpiration = cookie.SlidingExpiration;
                    options.LoginPath = ToPathString(cookie.LoginUrl, "/Login");
                    options.AccessDeniedPath = ToPathString(cookie.LoginUrl, "/Login");
                    options.ReturnUrlParameter = "returnurl";
                });

            // No FallbackPolicy on purpose: DNN performs its own per-tab/per-module permission
            // checks, and a global fallback policy would lock anonymous users out of public
            // portals, the RSS feed, LinkClick and the sitemap.
            services.AddAuthorization();
        }

        private static PathString ToPathString(string url, string fallback)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return new PathString(fallback);
            }

            var path = url.Trim();
            if (path.StartsWith("~", StringComparison.Ordinal))
            {
                path = path.Substring(1);
            }

            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            return new PathString(path);
        }
    }
}
