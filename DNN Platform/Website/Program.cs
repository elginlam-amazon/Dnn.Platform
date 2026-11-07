// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// -----------------------------------------------------------------------------------------
// The DNN host. This replaces Global.asax (Application_Start/_End/_Error), the web.config
// <system.web>/<system.webServer> module and handler registrations, and the WebForms
// Default.aspx pipeline.
//
// Everything platform-specific lives in the DotNetNuke (Library) RCL and is wired through
// three calls: AddDnnPlatform / UseDnnPlatform / MapRazorComponents(...).
// -----------------------------------------------------------------------------------------
using System;
using System.IO;

using Dnn.ExchangeOnlineAuthProvider;
using Dnn.GoogleMailAuthProvider;
using Dnn.Skins.Aperture;

using DNNConnect.CKEditorProvider;

using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.HttpModules;
using DotNetNuke.Modules.Admin.Sales;
using DotNetNuke.Providers.Caching.SimpleWebFarmCachingProvider;
using DotNetNuke.Services.Install;
using DotNetNuke.UI;
using DotNetNuke.UI.Utilities;
using DotNetNuke.Web;
using DotNetNuke.Web.Api.Extensions;
using DotNetNuke.Web.Client.Providers;
using DotNetNuke.Web.Mvc;
using DotNetNuke.Web.Mvc.Extensions;
using DotNetNuke.Website.Components;
using DotNetNuke.Website.Endpoints;

using log4net.Config;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Registers the entire DNN service graph: every IDnnStartup in the application (including
// DotNetNuke.Startup), the web.config-replacement options, security/membership, syndication,
// the client-API page seam, Razor components, localization, cookie authentication (the
// .DOTNETNUKE cookie, so existing sessions survive), the named HttpClients and the hosted
// services (application initialization, scheduler, disk image store purge).
builder.Services.AddDnnPlatform(builder.Configuration, builder.Environment);

// The DNN services framework (web API): the <providers> DI graph (20 provider contracts + the 3
// keyed folder providers), every DnnApiController and its assembly as an MVC application part, the
// ordered authentication-handler chain and its appsettings.json options
// (DotNetNuke:authServices:messageHandlers), antiforgery with the legacy token names, the DNN
// filter provider (default-deny) and the application-initialization hosted service which replaces
// the DotNetNukeHttpApplication static constructor / Application_Start.
builder.Services.AddDnnWebApi(builder.Configuration);

// ---------------------------------------------------------------------------------------------
// The registrations the referenced projects require from their host. Each one is idempotent
// (TryAdd / AddXxx semantics), so none of them duplicates anything AddDnnPlatform already did.
// ---------------------------------------------------------------------------------------------

// MVC: the DNN MVC module host needs the framework MVC services (IControllerFactory and the
// action invoker are registered by AddControllers, not by DotNetNuke.Web.Mvc.Startup, because the
// ASP.NET Core DefaultControllerFactory is internal), plus every DnnController of every installed
// module as a service and its assembly as an MVC application part. AddControllers is idempotent -
// AddDnnWebApi above already called it for the services framework controllers - and
// AddMvcControllers only ever adds application parts it does not have yet.
builder.Services.AddControllers().AddMvcControllers();

// Session state: the <sessionState> element the legacy web.config relied on by default (in-proc,
// 20 minute idle timeout, the ASP.NET_SessionId cookie). DnnMvcRouteHandler.IsSessionAvailable -
// the replacement for IRequiresSessionState/SetSessionStateBehavior - reads the session feature
// while routing, which is why UseSession() runs BEFORE UseRouting() below.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "ASP.NET_SessionId";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromMinutes(20);
});

// The Client API page seam. AddDnnWebUtility (reached through AddDnnPlatform) registers the
// options, the cache and the context accessor, but the page itself is the HOST's to supply: it is
// what UseDnnWebUtility resolves from the request services to dispatch a Client API callback, and
// what DotNetNuke.WebControls (DNNMultiStateBox/DNNTabStrip), the PropertyEditor client API
// control and UI/Skins/Skin.cs register their keyed client scripts on. Scoped, because it carries
// the registrations of exactly one page/request.
builder.Services.AddScoped<IClientApiPage, HttpContextClientApiPage>();

// The DNN 10 default theme (Dnn.Skins.Aperture). AddApertureTheme registers its defaults with
// TryAdd, so a host-specific IApertureSkinContext / IApertureMenuProvider would have to be
// registered on the line BEFORE this one to win. This host registers neither, so the
// configuration-backed defaults (the "Aperture" section of appsettings.json) are used.
builder.Services.AddApertureTheme();

// Required by the CountryListBox RCL (and by several platform services); both are idempotent.
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

// log4net's ASP.NET Core integration: makes the current request and the host configuration visible
// to the appenders/layouts/pattern converters that used to read HttpContext.Current and
// ConfigurationManager. It installs an IStartupFilter, so it is order-independent.
builder.Services.AddLog4NetAspNetCore();

var app = builder.Build();

// Writable runtime directories must exist before the first request. Under IIS these were created
// by the deployment; running from the content root they are not guaranteed, and the code that
// writes into them (portal file system, upgrade logs, the app-offline switch) assumes they are
// present. wwwroot is the configured web root, so UseStaticFiles would otherwise fail to resolve
// it on a clean checkout; App_Data stays at the CONTENT root and is never web-served.
Directory.CreateDirectory(app.Environment.WebRootPath);
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));

// The static configuration seam, reasserted from the built container (the same IConfiguration
// singleton AddDnnPlatformConfiguration registered). Everything static in the platform reads its
// settings through it.
HostStartupSeams.EnsureConfigurationSource(app);

// The dnn:DOTNETNUKE skin object. DnnControlTypeResolver resolves a stored markup path to the
// component whose type name matches the file name, which cannot work for admin/Skins/dotnetnuke.ascx:
// its class has always been DotNetNukeControl, because "DotNetNuke" is a namespace. The mapping is
// therefore registered explicitly; every other skin object under admin/Skins resolves by convention.
app.Services.GetRequiredService<IDnnControlTypeResolver>().Register(
    "~/admin/Skins/dotnetnuke.ascx",
    typeof(DotNetNuke.UI.Skins.Controls.DotNetNukeControl));

// Static seams + the ordered DNN pipeline (exception handler, request timeouts, static files,
// request localization, routing, authentication, authorization, URL rewriter, client API,
// antiforgery) and every legacy handler URL (Logoff.aspx, *.captcha.aspx, DnnImageHandler.ashx,
// RSS.aspx, ProfilePic.ashx, User.aspx, LinkClick.aspx, Sitemap.aspx).
//
// The two hooks are what used to be the <system.webServer><modules> registrations of
// DotNetNuke.HttpModules: the BeginRequest modules (request DI scope, request filter, services
// framework, mobile redirect) run before routing, and the authentication/cache/EndRequest
// modules (membership, users online, localization, personalization, output caching, analytics)
// run immediately after authorization.
app.UseDnnPlatform(
    configureEarly: early =>
    {
        // BEFORE UseRouting (which UseDnnPlatform calls right after this hook): the DNN MVC
        // module route handler checks session availability while routing, so the session
        // middleware has to be in front of it.
        early.UseSession();
        early.UseDnnEarlyRequestMiddleware();
    },
    configureAfterRouting: afterRouting => afterRouting.UseDnnWebApi(),
    configureAfterAuthentication: afterAuth =>
    {
        // The post-authentication modules must run AFTER UseDnnUrlRewriter() - which is exactly
        // where UseDnnPlatform invokes this hook - because the rewriter is the only thing that
        // publishes the resolved PortalSettings into
        // HttpContext.Items[PortalController.PortalSettingsContextKey], and DNN membership
        // authentication, users online, localization and tab output caching all guard on it.
        afterAuth.UseDnnPostAuthenticationMiddleware();

        // The former MvcHttpModule (DNN initialization + the X-Frame-Options rule for module
        // requests): after routing and authentication, before the endpoints execute.
        afterAuth.UseDnnMvcModuleHost();
    });

// Fail fast if any of the four static seams the platform resolves its ambient state through was
// not wired by AddDnnPlatform/UseDnnPlatform (HttpContextSource, MemoryCacheSource,
// ConfigurationSource and the ServiceLocator/Globals container bridge). Each of them otherwise
// degrades into blank pages or a trace nobody reads.
HostStartupSeams.VerifyStaticSeams(app);

// The DNN MVC module endpoints (the routes contributed by every module's IMvcRouteMapper). Mapped
// FIRST, so that no component catch-all can shadow a module route.
app.MapDnnMvcModuleRoutes();

// The DNN service endpoints (every module's IServiceRouteMapper routes, including the `-old`
// DesktopModules/{namespace}/API/{controller}/{action} compatibility routes). Mapped BEFORE the
// Blazor components so that no catch-all route shadows a service URL.
app.MapDnnServiceRoutes();

// The DNNConnect.CKEditorProvider extension's endpoints: the four URLs it served with
// IHttpHandlers / an ASP.NET AJAX page method (Tabs.ashx, Browser/FileUploader.ashx,
// Browser/ProcessImage.ashx and the Browser.aspx SetFile page method). Mapped BEFORE the Blazor
// components, for the same reason as the service routes above.
app.MapCKEditorProviderEndpoints();

// The composite client resource bundles, which replace the ClientDependency
// DependencyHandler.axd handler that was the only URL the client resource pipeline exposed.
app.MapClientResourceBundles();

// The web farm cache synchronization URL. The pattern is the literal /SimpleWebFarmSync.axd the
// handler was registered under, because the peers of a farm call that exact URL.
app.MapSimpleWebFarmSyncEndpoints();

// The host's own legacy page URLs that are request handlers rather than UI, mapped BEFORE the
// Blazor components so that no catch-all route shadows them. Each pattern is the literal URL the
// .aspx was served under, because each one is addressed from outside the application:
//   /Install/install.aspx            - the installer/upgrader. It streams its progress log to the
//                                     open response (HtmlUtils.WriteHeader/WriteFeedback/
//                                     WriteFooter with buffering disabled), which a component
//                                     cannot do; HtmlUtils.WriteKeepAlive also matches on the
//                                     literal "install.aspx?" substring.
//   /KeepAlive.aspx                 - the application-pool warm-up URL uptime monitors call.
//   /Portals/_default/subhost.aspx  - the IIS sub-host portal entry point; both of its redirects
//                                     are behaviour external links depend on.
// The install/upgrade WIZARDS are routable Blazor components (Install/InstallWizard.razor and
// Install/UpgradeWizard.razor) discovered through Routes.razor's AppAssembly, so they need no
// registration here.
app.MapDnnInstaller();
app.MapDnnKeepAlive();
app.MapDnnSubHost();

// The PayPal IPN callback, on the literal legacy URL PayPal is configured with externally. PayPal
// posts server-to-server to this exact, case-sensitive path and its configuration is not ours to
// change, so the pattern is the original "/admin/Sales/paypalipn.aspx" verbatim (the route string
// keeps the original casing even though the static admin/ asset tree moved to lowercase wwwroot/ —
// this is a routed endpoint, not a static file). It is a POST-only endpoint with no UI, and it must
// be mapped before the component catch-all. The paired checkout page
// (admin/Sales/paypalsubscription.aspx) is a routable component and needs no registration here.
app.MapPayPalIpnEndpoints();

// Attribute-routed controllers (the DNN services framework's own routes come from
// MapDnnServiceRoutes above; this covers controllers that carry their own [Route]).
app.MapControllers();

// Syndication: the ONE syndication handler the pre-migration release.config registered was
//   <add name="RSSHandler" path="RSS.aspx" type="DotNetNuke.Services.Syndication.RssHandler" />
// and DotNetNuke.Services.Syndication.RssHandler IS the generic handler (it derives from
// SyndicationHandlerBase : GenericRssHttpHandlerBase). That URL is served by MapDnnRss() inside
// MapDnnPlatformEndpoints, so every syndication URL that existed at the baseline commit is
// reachable. MapGenericRssHandler("<path>") is deliberately NOT called with an invented path: it
// would publish a feed URL the original application never served.

// The RCLs' routable components are only reachable when BOTH this call and the
// AdditionalAssemblies on the Router in Routes.razor name the assembly.
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddAdditionalAssemblies(
       DnnPlatformComponents.Assembly,
       DnnWebComponents.Assembly,
       CKEditorProviderComponents.Assembly,
       GoogleMailAuthProviderComponents.Assembly,
       ExchangeOnlineAuthProviderComponents.Assembly);

app.Run();
