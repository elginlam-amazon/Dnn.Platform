// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Skins.Aperture.SkinObjects
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Supplies the portal-level values that the Aperture skin objects render.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the legacy theme each value came from a WebForms skin object user control under
    /// <c>~/Admin/Skins/</c> (<c>Logo.ascx</c>, <c>Copyright.ascx</c>, <c>Terms.ascx</c>,
    /// <c>Privacy.ascx</c>, <c>Login.ascx</c>, <c>user.ascx</c>) and from the container
    /// <c>~/Admin/Containers/Title.ascx</c>. Those controls read the current <c>PortalSettings</c>
    /// through the page lifecycle, which does not exist in ASP.NET Core.
    /// </para>
    /// <para>
    /// The host application implements this abstraction once and registers it with
    /// <see cref="ApertureThemeServiceCollectionExtensions.AddApertureTheme"/>, which keeps the theme
    /// free of any <c>System.Web</c> dependency while preserving every value the original skin
    /// objects displayed.
    /// </para>
    /// </remarks>
    public interface IApertureSkinContext
    {
        /// <summary>
        /// Gets the web-root-relative base path of the theme's own assets, including a trailing slash.
        /// </summary>
        /// <remarks>
        /// Replaces the WebForms <c>SkinPath</c> property and the <c>PathNameAlias="SkinPath"</c>
        /// client-resource attribute. For a theme consumed as a Razor class library this is
        /// <c>_content/Dnn.Skins.Aperture/</c>.
        /// </remarks>
        string SkinPath { get; }

        /// <summary>Gets the portal logo, replacing the <c>dnn:LOGO</c> skin object.</summary>
        /// <param name="injectSvg">
        /// When <see langword="true"/> and the logo is an SVG file, the implementation populates
        /// <see cref="ApertureLogo.SvgMarkup"/> so the markup can be inlined. This mirrors the legacy
        /// <c>InjectSvg="true"</c> attribute.
        /// </param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The logo to render.</returns>
        ValueTask<ApertureLogo> GetLogoAsync(bool injectSvg, CancellationToken cancellationToken = default);

        /// <summary>Gets the portal copyright notice, replacing the <c>dnn:COPYRIGHT</c> skin object.</summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The copyright text, or an empty string when the portal has none.</returns>
        ValueTask<string> GetCopyrightAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets the terms-of-use link, replacing the <c>dnn:TERMS</c> skin object.</summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The terms link; a link with an empty <see cref="ApertureLink.Href"/> renders nothing.</returns>
        ValueTask<ApertureLink> GetTermsLinkAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets the privacy-statement link, replacing the <c>dnn:PRIVACY</c> skin object.</summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The privacy link; a link with an empty <see cref="ApertureLink.Href"/> renders nothing.</returns>
        ValueTask<ApertureLink> GetPrivacyLinkAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the login, logout, registration and profile links, replacing the <c>dnn:Login</c> and
        /// <c>dnn:User</c> skin objects.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The authentication-dependent links for the current request.</returns>
        ValueTask<ApertureUserLinks> GetUserLinksAsync(CancellationToken cancellationToken = default);
    }
}
