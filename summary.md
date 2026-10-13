# .NET Framework → net10.0 upgrade – cycle summary

## Outcome

- Every requested project builds with **0 compilation errors** (`dotnet build` per project, plus `dotnet build DNN_Platform.sln`).
  The only solution-level failure is `DNN Platform/Dnn.ClientSide` (a JavaScript `.esproj`, not part of the request): its `npm run build` needs the yarn workspace installed (`tsx` not found).
- **Projects that can genuinely run on .NET 10 now target `net10.0`** (libraries also keep `netstandard2.0`, plus `net48` where .NET Framework callers need it).
- **The System.Web / Web Forms tier still targets `net48`.** It is now all SDK-style and builds cross-platform (Linux included). It was **not** moved to `net10.0`. See "Why the Web Forms tier is still net48".

## Target framework per project

| Project(s) | Before | After |
|---|---|---|
| DotNetNuke.Abstractions | netstandard2.0 | **net10.0;netstandard2.0** |
| DotNetNuke.Log4Net (legacy csproj) | net48 (non SDK) | **SDK-style, net10.0;netstandard2.0;net48** |
| log4net.vs2010 (orphan upstream csproj) | net40 (non SDK) | **SDK-style, net10.0;netstandard2.0**, own obj/bin folders |
| DotNetNuke.Instrumentation, DotNetNuke.DependencyInjection, DotNetNuke.ContentSecurityPolicy, DotNetNuke.Web.Client.ResourceManager | netstandard2.0;net48 | **net10.0;netstandard2.0;net48** |
| DotNetNuke.BulkInstall.Encryption | netstandard2.0 | **net10.0;netstandard2.0** |
| DotNetNuke.Tests.ContentSecurityPolicy | net48 | **net10.0** (57/57 tests pass) |
| Build, BulkInstall.DeployClient (+Tests), Dnn.Skins.Aperture, DotNetNuke.Tests.SourceGenerators | net10.0 | unchanged (builds; SourceGenerators tests 16/16 pass) |
| DotNetNuke.Internal.SourceGenerators | netstandard2.0 | unchanged: Roslyn analyzers/source generators must target netstandard2.0 |
| DotNetNuke.Maintenance | netstandard2.0 | unchanged: it references the net48 DotNetNuke.Library (see below) |
| 21 legacy (non SDK) projects: Dnn.Modules.Console, Dnn.AuthServices.Jwt, CoreMessaging, DDRMenu, Groups, HTML, HtmlEditorManager, Journal, MemberDirectory, RazorHost, ResourceManager, TelerikRemoval, 4 authentication providers, FolderProviders, CKEditor provider, 2 SMTP OAuth providers, DotNetNuke.Website | net48 (non SDK, VS Web Application targets, Windows only) | **SDK-style net48**, builds on Linux |
| All other projects in the request (DotNetNuke.Library, DotNetNuke.Web, Web.Mvc, Web.Razor, Web.Client, WebUtility, WebControls, HttpModules, ModulePipeline, Syndication, CountryListBox, connectors, remaining providers/modules/samples, Dnn.PersonaBar.*, Dnn.EditBar.*, all DNN Platform/AdminExperience test projects) | net48 | net48 (unchanged TFM, build verified) |

## Changes made

Project system / build
- Converted the 21 legacy csproj files to SDK-style net48. The conversion keeps the original compile items (files never part of a project get an explicit `Compile Remove`), embedded resources (default resx globbing is off, so no new satellite assemblies), references, project references (`Private=False` kept), and the `Module.build` / `Provider.build` / `Library.build` imports. `AfterBuild` became `AfterTargets="Build"`. The `Cloud_Debug`/`Cloud_Release` defines (`CLOUD`, `CLOUD_DEBUG`, `CLOUD_RELEASE`) are kept in FolderProviders and the SMTP OAuth providers. HintPath references to the bin folders of sibling projects (`DotNetNuke.Instrumentation.dll`, `DotNetNuke.WebControls.dll`) are now `ProjectReference`s. Removed the dead `AllRules.ruleset` setting (MSB3884).
- Replaced the Windows-only `XCOPY` post-build steps with the MSBuild `Copy` task (Abstractions, DependencyInjection, ContentSecurityPolicy, Web.Client.ResourceManager, Maintenance, ModulePipeline).
- Fixed path casing that breaks on case-sensitive file systems: `Package.Targets` → `Package.targets` and `Module.Build` → `Module.build` in all `*.build` files and in `AEPackage.targets`; `DotNetNuke.Log4Net\` → `DotNetNuke.Log4net\` in the Instrumentation project reference; `Bin` → `bin` output path in Groups.
- New `Directory.Build.targets`: on the **modern leg only** of projects that also target netstandard2.0/net48, it turns off the analyzers whose fixes need APIs missing on those older targets (CA1305 for interpolated `StringBuilder` handlers, CA1510-CA1513, CA1845-CA1847, CA1864-CA1868, CA2249). The netstandard2.0 and net48 legs still run the full analyzer set on the same code.
- Reverted the earlier automated pass on Dnn.PersonaBar.Library, Dnn.EditBar.Library/UI, Dnn.PersonaBar.UI/Extensions and the 4 PersonaBar test projects. It had set them to net10.0/netstandard2.0 on top of the net48-only DotNetNuke.Library, added SystemWebAdapters, and inserted `using Microsoft.AspNetCore.Mvc;` above the license headers. That cannot build or run, so these projects are back on net48. Also removed the now-unused SystemWebAdapters entries from `Directory.Packages.props` and reverted the cosmetic-only changes to `DNN_Platform.sln` and to the SourceGenerators csproj.

Code
- `log4net/Config/XmlConfigurator.cs`: the `#if NETSTANDARD` branch of the remote config download had never been compiled and had unbalanced braces. The `using` statement is now shared by both branches; behaviour is the same as the .NET Framework path. The log4net copy uses its own `NETSTANDARD` symbols, so the net10.0 leg defines `NETSTANDARD;NETSTANDARD2_0` and uses the same System.Web-free code as the netstandard2.0 build. The ASP.NET appenders and pattern converters stay net48-only. Added `System.Configuration.ConfigurationManager` (all non-net48 legs) and `System.Security.Principal.Windows` (netstandard2.0). Suppressed obsolete-API and XML-doc warnings for this vendored third-party code.
- `Abstractions/Portals/StyleColor.cs`: span-based hex parsing on .NET 10, substring parsing on netstandard2.0. Same result.
- `DependencyInjection/Extensions/TypeExtensions.cs`: loader-exception diagnostics are now formatted with `FormattableString.Invariant` (CA1305).
- `BulkInstall.Encryption` (crypto migration, KB 22). Before: AES-256-CBC with a PBKDF2 key from `new Rfc2898DeriveBytes(pass, salt, 1000)`, `AesManaged`, `RNGCryptoServiceProvider` and `SHA256Managed`. After:
  - The factory APIs `Aes.Create()`, `RandomNumberGenerator.Create()` and `SHA256.Create()`.
  - Key derivation uses `Rfc2898DeriveBytes.Pbkdf2(..., HashAlgorithmName.SHA1, 32)` on .NET 10 and the original constructor on netstandard2.0. Both use the same algorithm and iteration count, so ciphertexts stay compatible with existing servers.
  - Behaviour fix: decryption now reads in a loop. Since .NET 6, `CryptoStream.Read` may return fewer bytes than requested, and the old single `Read` call could truncate the payload on .NET 10.
- Removed the designer-only `$this.TrayLargeIcon` / `$this.TrayAutoArrange` / `$this.DefaultModifiers` entries from 3 `.resx` files (HtmlEditorManager `InvalidConfiguration.ascx.resx`, CKEditor `Browser.aspx.resx` and `CKEditorOptions.ascx.resx`). Nothing reads them at runtime, and as non-string resources they make the SDK resource generator require System.Resources.Extensions.

## Why the Web Forms tier is still net48

DotNetNuke.Library (about 1,400 files) and everything built on it is an ASP.NET Web Forms / Web API 2 / MVC 5 application framework:
- About 260 files use `System.Web.UI`, 170 use `System.Web.UI.WebControls`, and 93 use `System.Web.Http`.
- Its skin, container and module model derives from `Page`, `UserControl` and `Control`.

None of this exists on .NET 10. `Microsoft.AspNetCore.SystemWebAdapters` covers `HttpContext`/`Request`/`Response` but not Web Forms. Moving this tier means re-architecting DNN's rendering pipeline (KB 25/26: Razor Pages/MVC or Blazor), not porting it.

The only ways to get a "green" net10.0 build in this cycle were:
- compiling against the .NET Framework System.Web reference assemblies, which fails at runtime, or
- removing most of the code with `#if`.

Both would ship non-functional binaries, so I did neither. The tier stays a consistent, buildable net48 SDK-style stack, ready for an incremental migration.

## Verification

- `dotnet build` succeeded with 0 errors for every requested project, both individually and through `DNN_Platform.sln` (only the out-of-scope `.esproj` fails).
- Tests run on .NET 10:
  - DotNetNuke.Tests.ContentSecurityPolicy: 57/57 pass.
  - DotNetNuke.Tests.SourceGenerators: 16/16 pass.
  - DotNetNuke.Tests.BulkInstall.DeployClient: 167/173 pass. The 6 failures are Windows-path assumptions in the tests (`modules\file.zip`, `/test` being a valid absolute URI on Linux). None of them involve the encryption code, and the Encryptor tests pass.
- I could not run the net48 test projects on this Linux host (no .NET Framework/Mono runtime).

## Next steps (for review)

1. **Web Forms → ASP.NET Core re-architecture (product decision).**
   - Decide the target UI model (Razor Pages/MVC or Blazor, KB 25/26) for DotNetNuke.Library, DotNetNuke.Web, Website, the modules, PersonaBar and EditBar.
   - A practical first step is to pull the System.Web-free parts of DotNetNuke.Library (entities, data, services) into a multi-targeted library. DotNetNuke.Maintenance, the connectors and the tests could then move to net10.0.
2. **DotNetNuke.Maintenance** is netstandard2.0 but references net48 DotNetNuke.Library. This is the upstream design and causes the NU1702/MSB3277 warnings. Re-target it once the Library split exists.
3. **Syndication** could be multi-targeted (RSS/OPML model and parsing) once a replacement for `HttpContext.Current.Server.MapPath` / `VirtualPathUtility` is chosen. The handler and data-source controls stay Web Forms.
4. **NuGet packaging:** `Build/Tools/NuGet/*.nuspec` still publishes only the `lib/net48` / `lib/netstandard2.0` assets from `Website\bin`. Add `lib/net10.0` entries for Abstractions, Instrumentation/Log4Net, DependencyInjection, ContentSecurityPolicy and Web.Client.ResourceManager if you want to ship the new targets.
5. **Warnings:** the full solution build reports about 11.8k warnings. All of them are in projects whose analyzer settings were not changed: SA1600 is explicitly non-fatal, CS0618 comes from DNN's own `[DnnDeprecated]` scheme, plus StyleCop in the samples and MSB3277/MSB3836 binding conflicts in the net48 test projects. Clearing them is a code-style/product decision outside this upgrade.
6. **`Dnn.ClientSide.esproj`** needs `yarn install` (Node toolchain) before `dotnet build` of the whole solution is fully green.
7. **BulkInstall tests:** make the 6 path-dependent DeployClient tests platform-neutral if CI runs on Linux.
