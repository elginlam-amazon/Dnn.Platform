# Skin object mapping

`partials/_registers.ascx` and `partials/_registers-popUpSkin.ascx` declared the tag prefixes and
skin objects available to the Aperture markup. They contained no logic — only `<%@ Register %>`
directives — and their Blazor equivalent is the `@using` list in `_Imports.razor` plus the
components in this folder.

The table below maps every one of the 31 registrations so none is silently dropped.

## Rendered by this theme

These skin objects were actually instantiated in the theme markup and are fully ported:

| Legacy registration | Source control | Blazor replacement | Used by |
| --- | --- | --- | --- |
| `dnn:LOGIN` | `~/Admin/Skins/Login.ascx` | `ApertureLoginLink.razor` | `Partials/ApertureHeader.razor` |
| `dnn:USER` | `~/Admin/Skins/user.ascx` | `ApertureUserLink.razor` | `Partials/ApertureHeader.razor` |
| `dnn:LOGO` | `~/Admin/Skins/Logo.ascx` | `ApertureLogoLink.razor` | `Partials/ApertureHeader.razor` |
| `dnn:MENU` | `~/DesktopModules/DDRMenu/Menu.ascx` | `Menus/DesktopMenu.razor`, `Menus/MobileMenu.razor`, `Menus/FooterMenu.razor` | header, footer |
| `dnn:COPYRIGHT` | `~/Admin/Skins/Copyright.ascx` | `ApertureCopyright.razor` | `Partials/ApertureFooter.razor` |
| `dnn:TERMS` | `~/Admin/Skins/Terms.ascx` | `ApertureTermsLink.razor` | `Partials/ApertureFooter.razor` |
| `dnn:PRIVACY` | `~/Admin/Skins/Privacy.ascx` | `AperturePrivacyLink.razor` | `Partials/ApertureFooter.razor` |
| `dnn:META` | `~/Admin/Skins/Meta.ascx` | `<meta>` inside `<HeadContent>` | `Partials/ApertureIncludes.razor`, `Partials/PopUpIncludes.razor` |
| `dnn:TITLE` | `~/Admin/Containers/Title.ascx` | `TitleContainer.Title` parameter | `Containers/TitleContainer.razor` |
| `DotNetNuke.Web.DDRMenu.TemplateEngine` | assembly tag prefix | `Dnn.Skins.Aperture.Menus` (`IApertureMenuProvider`, `ApertureMenuNode`) | menu components |
| `DotNetNuke.Web.Client.ClientResourceManagement` | assembly tag prefix | `ApertureThemeAssets` + `<HeadContent>` | `Partials/ApertureIncludes.razor` |

## Declared but never instantiated

The remaining registrations made a tag name available to skin authors; the Aperture markup never
used any of them, so there is no behaviour to port. The column records the modern mechanism a skin
author should reach for instead of the WebForms control.

| Legacy registration | Source control | Modern mechanism |
| --- | --- | --- |
| `dnn:BREADCRUMB` | `~/Admin/Skins/Breadcrumb.ascx` | A breadcrumb component driven by `IApertureMenuProvider` node selection |
| `dnn:CONTROLPANEL` | `~/Admin/Skins/ControlPanel.ascx` | Host-supplied administration component, rendered by the host layout |
| `dnn:CURRENTDATE` | `~/Admin/Skins/CurrentDate.ascx` | `@DateTime.Now.ToString(format, CultureInfo.CurrentCulture)` in markup |
| `dnn:DnnLink` | `~/Admin/Skins/DnnLink.ascx` | A plain `<a>` element |
| `dnn:DOTNETNUKE` | `~/Admin/Skins/DotNetNuke.ascx` | A plain `<a>` element to the DNN site |
| `dnn:Help` | `~/Admin/Skins/Help.ascx` | A plain `<a>` element to the portal help URL |
| `dnn:HOSTNAME` | `~/Admin/Skins/HostName.ascx` | A plain `<a>` element built from host settings |
| `dnn:JavaScriptLibraryInclude` | `~/Admin/Skins/JavaScriptLibraryInclude.ascx` | `<script>` in the host `App.razor`, or `IJSRuntime` module import |
| `dnn:JQUERY` | `~/Admin/Skins/jQuery.ascx` | `<script>` in the host `App.razor` (this theme needs no jQuery) |
| `dnn:LANGUAGE` | `~/Admin/Skins/Language.ascx` | ASP.NET Core request localization plus a culture-picker component |
| `dnn:LINKS` | `~/Admin/Skins/Links.ascx` | `IApertureMenuProvider` rendered as a flat link list |
| `dnn:LINKTOFULLSITE` | `~/Admin/Skins/LinkToFullSite.ascx` | A plain `<a>` element |
| `dnn:LINKTOMOBILESITE` | `~/Admin/Skins/LinkToMobileSite.ascx` | A plain `<a>` element |
| `dnn:MODULEMESSAGE` | `~/Admin/Skins/ModuleMessage.ascx` | A notification component fed by host state |
| `dnn:SEARCH` | `~/Admin/Skins/Search.ascx` | A host-supplied search component (styles exist in `src/scss/components/_search.scss`) |
| `dnn:STYLES` | `~/Admin/Skins/Styles.ascx` | `<link>` inside `<HeadContent>` |
| `dnn:TAGS` | `~/Admin/Skins/Tags.ascx` | A tag-list component fed by host state |
| `dnn:TEXT` | `~/Admin/Skins/Text.ascx` | `IStringLocalizer` in markup |
| `dnn:TOAST` | `~/Admin/Skins/Toast.ascx` | A notification component fed by host state |
| `dnn:TreeViewMenu` | `~/Admin/Skins/TreeViewMenu.ascx` | A recursive component over `ApertureMenuNode`, as `MobileMenuItems` demonstrates |
| `dnn:USERANDLOGIN` | `~/Admin/Skins/UserAndLogin.ascx` | `ApertureLoginLink` and `ApertureUserLink` together, as the header renders them |
