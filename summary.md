# net10.0 migration – verification cycle summary

## State
- AWS Transform Custom left the working tree clean at commit `e8f8925600`.
- Fixed in this cycle: `DNN Platform/Website/DotNetNuke.Website.csproj`. The `**/App_LocalResources/**/*.resx` content glob matched the `bin/` output, which nested output as `bin/bin/...` and made clean builds fail with 1,232 `MSB3030` errors. It now has `Exclude="$(DefaultItemExcludes)"`.
- `dotnet build DNN_Platform.sln --no-incremental` (Debug): exit code 0, 0 errors, 13,720 warnings. The largest groups are SA1600 (8,190), CS0618 (1,874), CA1305 (443) and ASP0006 (328).
- All 87 solution projects are SDK-style. 84 target `net10.0`. Three stay on `netstandard2.0` as they did originally: `DotNetNuke.Abstractions`, `DotNetNuke.BulkInstall.Encryption`, and `DotNetNuke.Internal.SourceGenerators` (a Roslyn source generator has to target netstandard2.0).
- The two VB projects in scope (`DotNetNuke.WebUtility.vbproj`, `DotNetNuke.WebControls.vbproj`) were converted to C# `.csproj` by atx (commits `d1d55cba43`, `ef5981aa59`). They build and are kept as they are.
- There are no `System.Web*` / `System.Data.Entity` usings, `packages.config`, `Global.asax`, `PreApplicationStartMethod`, `web.config`, `HintPath` references or `<Compile Remove>` entries left.
- `dotnet test DNN_Platform.sln`: 20 test assemblies pass with 0 failures (Tests.Core: 2067 passed, 12 skipped). The two assemblies that fail are covered under "Next steps".

## Next steps
1. **Release builds fail on the ImageSharp licence check (needs a decision).** `SixLabors.ImageSharp` 3.2.0 and `SixLabors.ImageSharp.Drawing` 2.2.0 (set in `Directory.Packages.props`) run a licence-key check before compile. In Debug it only warns. In Release it is an error, so `dotnet build -c Release` fails in `DotNetNuke.Library` and every project downstream of it. Options:
   - Get a Six Labors licence key and provide it to CI as `SixLaborsLicenseKey` (an MSBuild property or environment variable). DNN is MIT-licensed open source, so it may qualify for a free community key. This needs no code change.
   - Downgrading is not an option. I tried ImageSharp 3.1.12 / Drawing 2.1.7, which have no licence check, and NuGet audit raised high-severity advisories that are treated as errors (GHSA-j3p4-wp97-rph4, GHSA-j9gm-c75j-xc9q, GHSA-jjfr-hcj7-qf5w, GHSA-v76p-62qx-wwq2, and moderate ones), so I reverted it.
   - Moving the imaging code to a library under a permissive licence (for example SkiaSharp) is possible, but it is a larger refactor of the Library, Web, ResourceManager, CKEditor and PersonaBar imaging code.
2. **Tests that need a database cannot run on Linux.** `DotNetNuke.Tests.Urls` (2029 failures) and `DotNetNuke.Tests.Integration` (156 failures) depend on SQL Server LocalDB (`(localdb)\MSSQLLocalDB` in their `appsettings.json`) and a hosted DNN site. LocalDB is Windows-only, so they fail here with `PlatformNotSupportedException` or the portal lookup failing in setup. To get a real result, run them on Windows with LocalDB, or point the connection strings at a reachable SQL Server.
