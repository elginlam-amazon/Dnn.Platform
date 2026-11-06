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
using DNNConnect.CKEditorProvider;

using Dnn.ExchangeOnlineAuthProvider;
using Dnn.GoogleMailAuthProvider;

using DotNetNuke.Common;
using DotNetNuke.HttpModules;
using DotNetNuke.Web;
using DotNetNuke.Web.Api.Extensions;
using DotNetNuke.Website.Components;

using Microsoft.AspNetCore.Builder;
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

var app = builder.Build();

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
    configureEarly: early => early.UseDnnEarlyRequestMiddleware(),
    configureAfterRouting: afterRouting => afterRouting.UseDnnWebApi(),
    configureAfterAuthentication: afterAuth => afterAuth.UseDnnPostAuthenticationMiddleware());

// The DNN service endpoints (every module's IServiceRouteMapper routes, including the `-old`
// DesktopModules/{namespace}/API/{controller}/{action} compatibility routes). Mapped BEFORE the
// Blazor components so that no catch-all route shadows a service URL.
app.MapDnnServiceRoutes();

// The DNNConnect.CKEditorProvider extension's endpoints: the four URLs it served with
// IHttpHandlers / an ASP.NET AJAX page method (Tabs.ashx, Browser/FileUploader.ashx,
// Browser/ProcessImage.ashx and the Browser.aspx SetFile page method). Mapped BEFORE the Blazor
// components, for the same reason as the service routes above.
app.MapCKEditorProviderEndpoints();

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
