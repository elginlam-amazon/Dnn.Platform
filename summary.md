# DNN Platform – .NET Framework 4.8 → .NET 10 (iteration 3)

## Outcome

- All 84 requested projects build with **0 errors** on .NET 10 (each project built with its full reference chain; `TreatWarningsAsErrors` stays on).
- The Web Forms stack (DotNetNuke.Library, DotNetNuke.Web, Website, modules, providers, PersonaBar/EditBar) now targets `net10.0`. In iteration 2 it was still on `net48`.
- The only projects not on `net10.0` are `DotNetNuke.Internal.SourceGenerators` and `DotNetNuke.BulkInstall.Encryption`. The source generator stays on `netstandard2.0` because Roslyn requires it. The encryption library multi-targets `net10.0;netstandard2.0` (per the policy below) and is listed only for completeness.
- Libraries that do not depend on Web Forms target `net10.0;netstandard2.0`: Abstractions, DependencyInjection, Instrumentation, Log4Net, ContentSecurityPolicy, Web.Client.ResourceManager and BulkInstall.Encryption. Their unused `net48` leg was removed.
- Tests (`dotnet test`, Linux):

  | Project | Passed | Failed | Skipped |
  | --- | --- | --- | --- |
  | Tests.Core | 1943 | 60 | 12 |
  | Tests.Web | 115 | 0 | 1 |
  | Tests.Web.Mvc | 62 | 0 | 0 |
  | Tests.Content | 126 | 0 | 0 |
  | Tests.Data | 99 | 0 | 0 |
  | BulkInstall.DeployClient | 173 | 0 | 0 |
  | Tests.ContentSecurityPolicy | 57 | 0 | 0 |
  | PersonaBar tests (4 projects) | 101 | 0 | 0 |
  | Tests.Integration | 36 | 97 | 0 |

  - All other test projects pass with 0 failures.
  - The 60 Tests.Core failures are Windows path semantics (`C:\…`, `\` separators in the installer, folder providers and Lucene paths) on Linux. They are not caused by the migration.
  - The 97 Tests.Integration failures are expected: these tests need a running DNN site and a database.

## Approach

Web Forms has no first-party ASP.NET Core equivalent, and rewriting about 500 Web Forms code files and 200 `.aspx`/`.ascx` files as Razor or Blazor was out of reach for one pass. The existing architecture was therefore kept and moved onto compatibility layers that run on ASP.NET Core:

- **System.Web**: `Microsoft.AspNetCore.SystemWebAdapters` 2.3.0 (Microsoft) provides `HttpContext`, `HttpApplication`/`IHttpModule`, `Cache`, `HostingEnvironment`, `MachineKey` and related types.
- **System.Web.UI**: `CoreWebForms` 0.5.0 provides `Page`, `Control`, `WebControls` and `HtmlControls` on ASP.NET Core.
- **New project `DNN Platform/DotNetNuke.SystemWebCompat`** fills the remaining gaps with working implementations. The types keep their `System.*` namespaces so that skins and extensions keep compiling:
  - ScriptManager, UpdatePanel and UpdateProgress, without partial rendering (every postback is a full postback). Page methods (`[WebMethod]` + `PageMethods` client proxy + `UseWebFormsPageMethods` middleware) are included because the install wizard uses them.
  - `BuildManager.GetType`.
  - `FormsAuthentication`, `FormsAuthenticationTicket` and `FormsIdentity`, with tickets protected through `MachineKey` / data protection.
  - `Membership` and `SqlMembershipProvider`, which use the existing `aspnet_Membership_*` stored procedures and SqlMembershipProvider-compatible hashing.
  - `JavaScriptSerializer` on top of Json.NET.
  - `AntiForgery` on top of `IAntiforgery`.
  - `TemplateColumn`, `CustomValidator`, `MimeMapping`, `LoadControl(Type, object[])`, and C# 14 extension members such as `Server.HtmlEncode`, `Request.MapPath` and `Response.Status`.
- **Web API 2 → ASP.NET Core MVC** (DotNetNuke.Web/Api):
  - `DnnApiController` now derives from `ControllerBase`.
  - Each DNN service route is one endpoint, served by `ServicesFrameworkEndpointDataSource` / `MapDnnServicesRoutes()`. The endpoint selects the controller and action like the old namespace-aware controller selector, using the Web API verb conventions.
  - It then runs the DNN context handler, the configured auth message handlers and the forms auth handler, in the same order as before.
  - Kept for module compatibility:
    - `HttpResponseMessage` results, written by an output formatter.
    - `Request.CreateResponse`/`CreateErrorResponse`, plus `Request.RequestUri`/`Request.Content`, which keeps multipart uploads working through `System.Net.Http.Formatting`.
    - `DotNetNuke.Web.Api.HttpResponseException`.
    - Web API parameter binding: complex parameters are read from the request body.
    - The default `RequireHost` authorization level and the `[Dependency]` property injection.
- **MVC 5 → ASP.NET Core MVC** (DotNetNuke.Web.Mvc):
  - Module actions run in-process through `IActionInvokerFactory`, and their output is captured for `MvcHostControl`.
  - View locations come from an `IViewLocationExpander`.
  - `DnnWebViewPage` derives from `RazorPage`.
- **Razor scripts** (DotNetNuke.Web.Razor, deprecated) are rendered through `IRazorViewEngine`.
- **Website** is now an `Microsoft.NET.Sdk.Web` application (`Hosting/Program.cs`), replacing Global.asax:
  - System.Web adapters plus `DotNetNukeHttpApplication`, with the HTTP modules and handlers read from `web.config`.
  - Web Forms, MVC (Json.NET with PascalCase, runtime Razor compilation), anti-forgery, session and data protection.
  - DNN services are registered in the host container (`AddDnnServices`).
  - `WebConfigRuntime` points `System.Configuration` at `App_Data/web.runtime.config`, a copy of `web.config` without the IIS/System.Web sections. It also configures forms authentication and membership from `web.config`.

## Other notable changes

- Packages:
  - `System.Data.SqlClient` → `Microsoft.Data.SqlClient`. `SqlHelper` from Microsoft.ApplicationBlocks.Data was replaced by a local `ExecuteReader`.
  - QuickIO.NET → `System.IO`.
  - The `Dnn.ClientDependency` providers were removed. The DNN 10 `IClientResourceController` already replaces them.
  - `System.ComponentModel.Composition` and `System.Drawing.Common` are added as packages.
  - The obsolete .NET Framework package versions were removed from `Directory.Packages.props`.
  - `System.Security.Cryptography.Pkcs` is pinned to 10.0.13 to clear an NU1903 advisory.
- Removed APIs that have no .NET equivalent:
  - `DotNetNuke.Web.Mvp`: WebFormsMvp has no .NET build, and DNN deprecated it in 9.2. HtmlEditorManager and MemberDirectory were rewritten as plain user controls.
  - The MVC 5 handler, route handler, HTTP module and dependency resolver, the view engine collection and the result-capturing invoker.
  - The Web API controller selector, dependency resolver, trace writer and pass-through formatter.
  - The VS designer classes in WebControls.
  - `AspNetClientCapability.GetHttpBrowserCapabilities`.
- Crypto: `*CryptoServiceProvider` → `X.Create()`, and `Rfc2898DeriveBytes` → `Pbkdf2`. Output bytes are identical, so existing data still decrypts. `CryptoStream` decryption now reads until the end of the stream; previously a partial read could cut off data.
- `Server.Transfer` (unsupported) → `Response.Redirect`. The URL rewriters use `RewritePath` instead, which keeps the 404/500 status codes.
- CAS, `Thread.ResetAbort`, AppDomain shutdown reasons and the .NET Framework 4.8 registry check were removed or replaced.
- Build: `CopyBin` targets now run after the build, and copies of DLLs that no longer exist were removed.
- `Directory.Build.targets` (on the .NET 10 leg only) silences two groups of warnings:
  - analyzer suggestions for APIs that don't exist in .NET Framework or `netstandard2.0`;
  - CLS checks, because the compatibility assemblies are not CLS-annotated.
- Line endings follow `.editorconfig` (CRLF).

## Next steps

- **Runtime validation (main risk)**:
  - The solution compiles. A DNN site has not been run end to end.
  - CoreWebForms 0.5.0 is a preview and only serves precompiled pages (`AddCompiledPages`). Dynamic compilation of the `.aspx`/`.ascx` skins, containers and modules still needs the CoreWebForms page compiler (not yet published).
  - Smoke-test install, login, page rendering, PersonaBar and the services framework on a test site.
- **Decide the long-term UI direction**: stay on CoreWebForms, or rewrite as Razor Pages / Blazor per KB docs 25/26. Behaviours that changed and need checking:
  - No MS AJAX partial rendering: UpdatePanels do full postbacks.
  - `Server.Transfer` is now a redirect.
  - The CKEditor page method is handled in `Page_Load`.
- **Breaking changes for extensions**:
  - DotNetNuke.Web.Mvp removed.
  - Web API controllers must use the ASP.NET Core attributes (`[FromQuery]` instead of `[FromUri]`, `IActionResult` instead of `IHttpActionResult`).
  - `ITabAndModuleInfoProvider` works on `HttpRequest`.
  - `IMapRoute.MapHttpRoute` returns `DnnServiceRoute`.
  - MVC helpers return `IHtmlContent`.
- **MVC module routes**: `MapDnnMvcRoutes()` maps the routes collected at startup, but DNN registers routes on its first request. Give it a dynamic `EndpointDataSource` like the services framework's.
- **Membership**: passwords stored in the `Encrypted` format are not supported (they use the legacy machine-key algorithms). Hashed (the DNN default, SHA256) and Clear are supported.
- **Configuration**:
  - `web.config` changes (DNN `Config.Save`) do not restart the app. Consider a file watcher that calls `StopApplication`.
  - Microsoft.Data.SqlClient encrypts connections by default. Check the connection strings (`TrustServerCertificate`).
  - Share the data-protection keys (`App_Data/DataProtection-Keys`) across web farm nodes.
- **netstandard2.0**: these libraries are `net10.0` only:
  - Library, Web, WebControls, WebUtility, Web.Client, CountryListBox, Syndication, HttpModules, Web.Mvc, Web.Razor, ModulePipeline, Maintenance, PersonaBar/EditBar.Library, DnnExportImportLibrary, ContactList.Api, SystemWebCompat.
  - Most of them reference the Web Forms UI projects (NU1201).
  - CountryListBox, WebUtility, Web.Client and Syndication need CoreWebForms or the SystemWebAdapters core services, which have no `netstandard2.0` build.
- **Platform**:
  - `System.Drawing.Common` is kept, so image handling is Windows/IIS only (CA1416 suppressed). Move to SkiaSharp (KB doc 21) for Linux.
  - Lucene.Net 3.0.3 only ships .NET Framework assets (NU1701 suppressed). Upgrading to 4.8 changes the index format and API.
  - WindowsAzure.Storage is used through sync-over-async calls; migrate to Azure.Storage.Blobs.
- **Tests**:
  - Make the Windows-path tests in Tests.Core platform-neutral, or run them on Windows.
  - Deleted test files, with reasons:
    - MVC 5 internals: ResultCapturingActionInvoker, ModuleDelegatingViewEngine, DnnViewResult/ViewEngineCollection.
    - Web API internals: controller selector, dependency resolver, HttpConfiguration extensions.
    - Mvp presenter tests.
    - SQL CE based PetaPoco integration tests: SQL Server Compact does not exist on .NET.
  - One FileUploadController upload test is `[Ignore]`d.
- **Build/packaging**:
  - The Cake task `PackageWebFormsMvp` still packages a WebFormsMvp component.
  - The `.nuspec` files still list `net48` assets.
  - `WebsitePath` resolves to `<repo>/Website`.
  - Run the full Cake build on Windows.
- **Warnings**: the remaining warnings are the pre-existing SA1600 (missing docs, allowed per project) and CS0618 (DNN's own deprecations, about 1,000). They need DNN's deprecation roadmap rather than mechanical fixes.
