// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Skins.Aperture
{
    /// <summary>
    /// The static web asset paths published by the Aperture theme.
    /// </summary>
    /// <remarks>
    /// These replace the <c>dnn:DnnCssInclude</c> and <c>dnn:DnnJsInclude</c> client-resource
    /// registrations from <c>partials/_includes.ascx</c>. The stylesheet is emitted by
    /// <c>ApertureIncludes</c> through <c>HeadContent</c>; the script cannot be, because Blazor does
    /// not execute <c>&lt;script&gt;</c> elements rendered from inside a component, so the host must
    /// reference <see cref="ScriptHref"/> once from its <c>App.razor</c> document — at the end of the
    /// document body, matching the legacy <c>ForceProvider="DnnFormBottomProvider"</c> placement.
    /// </remarks>
    public static class ApertureThemeAssets
    {
        /// <summary>The static web asset base path of this theme, including a trailing slash.</summary>
        public const string BasePath = "_content/Dnn.Skins.Aperture/";

        /// <summary>The theme stylesheet compiled from <c>src/scss/style.scss</c>.</summary>
        public const string StylesheetHref = BasePath + "css/skin.min.css";

        /// <summary>The theme script bundled from <c>src/scripts/main.ts</c>.</summary>
        public const string ScriptHref = BasePath + "js/skin.min.js";
    }
}
