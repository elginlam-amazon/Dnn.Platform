# Aperture

This is the new minimalist default theme for DNN 10. This theme uses no CSS frameworks and has a
small list of dependencies (e.g., TypeScript, SASS, esbuild).

> [!WARNING]
> This is the DNN 10 **default theme** — do not modify the distributed Aperture files directly;
> they **will be overwritten on upgrade**.
> Need a custom version? Copy this project and customize the copy instead.

## Project shape

The theme is a **Razor class library** (`Microsoft.NET.Sdk.Razor`) targeting `net10.0`. The skin,
container, partial and menu templates that used to be WebForms `.ascx` controls and DDRMenu
`RazorMenu.cshtml` templates are now Blazor components:

| Component | Replaces | Role |
| --- | --- | --- |
| `ApertureLayout.razor` | `default.ascx` | Main skin layout. `ContentPane` is `@Body`; `BannerPane`, `FluidPane` and `FooterPane` are `RenderFragment` parameters. |
| `PopUpSkinLayout.razor` | `popUpSkin.ascx` | Minimal layout for modal/popup pages. |
| `Containers/NoneContainer.razor` | `containers/none.ascx` | Chromeless module container. |
| `Containers/TitleContainer.razor` | `containers/title.ascx` | Module container with a title heading. |
| `Partials/ApertureHeader.razor` | `partials/_header.ascx` | Eyebrow bar, logo and the desktop/mobile menus. |
| `Partials/ApertureFooter.razor` | `partials/_footer.ascx` | Footer menu, terms/privacy, copyright and `FooterPane`. |
| `Partials/ApertureIncludes.razor` | `partials/_includes.ascx` | Viewport meta, theme stylesheet and the 16 Ubuntu font preloads. |
| `Partials/PopUpIncludes.razor` | `partials/_includes-popUpSkin.ascx` | Viewport meta for the popup skin. |
| `Menus/DesktopMenu.razor` + `DesktopMenuItems.razor` | `menus/desktop/RazorMenu.cshtml` | Desktop navigation. |
| `Menus/MobileMenu.razor` + `MobileMenuItems.razor` | `menus/mobile/RazorMenu.cshtml` | Hamburger drill-down navigation. |
| `Menus/FooterMenu.razor` + `FooterMenuItems.razor` | `menus/footer/RazorMenu.cshtml` | Footer navigation. |
| `SkinObjects/*.razor` | `partials/_registers.ascx` declarations | The individual DNN skin objects the theme renders. |

`partials/_registers.ascx` and `partials/_registers-popUpSkin.ascx` held only `<%@ Register %>`
directives; they are replaced by the `@using` list in `_Imports.razor`. Every one of their 31
registrations is accounted for in [`SkinObjects/README.md`](SkinObjects/README.md).

`skin.doctype.xml` declared `<!DOCTYPE html>` for the WebForms skin engine. In Blazor the document
shell — and therefore the doctype — belongs to the host's `App.razor`, so the file is gone.

## Hosting the theme

The theme is a class library: it has no `Program.cs` and defines no routes. A Blazor Server host
wires it up as follows.

1. Reference the project from the host (the reference always points **host → theme**):

   ```xml
   <ProjectReference Include="..\..\Skins\Aperture\Dnn.Skins.Aperture.csproj" />
   ```

2. Register the theme services and the services its components depend on, in `Program.cs`:

   ```csharp
   builder.Services.AddRazorComponents().AddInteractiveServerComponents();
   builder.Services.AddCascadingAuthenticationState(); // required by ApertureLoginLink/ApertureUserLink
   builder.Services.AddApertureTheme();
   ```

   `AddApertureTheme` registers configuration-backed defaults with `TryAdd`, so a host that resolves
   portal values and its page tree itself simply registers its own `IApertureSkinContext` and
   `IApertureMenuProvider` beforehand and those win.

3. Serve the theme's static assets and reference its script from `App.razor`. The stylesheet and
   font preloads are emitted by `ApertureIncludes` through `HeadContent`, but Blazor does not
   execute `<script>` elements rendered from inside a component, so the script tag belongs in the
   host document — at the end of `<body>`, which is where the legacy
   `ForceProvider="DnnFormBottomProvider"` put it:

   ```razor
   <script src="@ApertureThemeAssets.ScriptHref" defer></script>
   ```

   The host must call `app.UseStaticFiles()` or the `_content/Dnn.Skins.Aperture/` paths 404.

4. Select a layout. The two skins are ordinary Blazor layouts, so they are used wherever the host
   resolves layouts — as the router default, per page, or from the host's theme resolution:

   ```razor
   <RouteView RouteData="routeData" DefaultLayout="typeof(Dnn.Skins.Aperture.ApertureLayout)" />
   ```

   Because the theme declares no `@page` routes, it needs no `Router AdditionalAssemblies` or
   `MapRazorComponents(...).AddAdditionalAssemblies(...)` registration; those are only required for
   routable components. The host `_Imports.razor` should include `@using Dnn.Skins.Aperture` so the
   components resolve by tag name.

> [!NOTE]
> The DNN host web application (`DotNetNuke.Website`) still targets .NET Framework 4.8 WebForms and
> cannot reference a `net10.0` library, so the wiring above applies once that host is migrated. The
> theme is complete and self-contained in the meantime: nothing in it depends on the host beyond the
> two service abstractions.

## Configuration

The default `IApertureSkinContext` and `IApertureMenuProvider` read the `Aperture` configuration
section:

```json
{
  "Aperture": {
    "PortalName": "My Portal",
    "HomeHref": "/",
    "LogoHref": "_content/Dnn.Skins.Aperture/images/logo.svg",
    "Copyright": "Copyright 2026 by My Organization",
    "TermsHref": "/Terms",
    "PrivacyHref": "/Privacy",
    "LoginHref": "/Login",
    "LogoutHref": "/Logout",
    "RegisterHref": "/Register",
    "ProfileHref": "/Profile",
    "Menu": [
      { "Text": "Home", "Href": "/" },
      { "Text": "About", "Href": "/About", "Children": [
        { "Text": "Team", "Href": "/About/Team" }
      ] }
    ]
  }
}
```

## Building the assets

`yarn build` (or `npm run build`) compiles `src/scss/style.scss` to `wwwroot/css/skin.min.css` and
bundles `src/scripts/main.ts` to `wwwroot/js/skin.min.js`, then produces the DNN install package.
Everything under `wwwroot/` is published as a static web asset and served from
`_content/Dnn.Skins.Aperture/`.
