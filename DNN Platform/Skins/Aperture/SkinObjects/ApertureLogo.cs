// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Skins.Aperture.SkinObjects
{
    /// <summary>
    /// The portal logo rendered by the <c>dnn:LOGO</c> replacement component.
    /// </summary>
    /// <param name="Href">The link the logo navigates to, normally the portal home page.</param>
    /// <param name="ImageHref">The logo image path. When empty, <paramref name="Text"/> is rendered instead.</param>
    /// <param name="Text">The portal name, used as the image alt text and as the textual fallback.</param>
    /// <param name="SvgMarkup">
    /// The inline SVG markup for the logo when the portal logo is an SVG file. Populated only when
    /// the consuming component requests SVG injection, which replaces the legacy
    /// <c>InjectSvg="true"</c> attribute of the WebForms skin object.
    /// </param>
    public readonly record struct ApertureLogo(string Href, string ImageHref, string Text, string? SvgMarkup);
}
