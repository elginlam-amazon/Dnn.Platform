// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Skins.Aperture.SkinObjects
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Configuration-backed implementation of <see cref="IApertureSkinContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the out-of-the-box implementation registered by
    /// <see cref="ApertureThemeServiceCollectionExtensions.AddApertureTheme"/> so that the theme
    /// renders complete markup without any additional host code. It reads the portal values the
    /// legacy skin objects displayed from the <c>Aperture</c> configuration section
    /// (<c>PortalName</c>, <c>HomeHref</c>, <c>LogoHref</c>, <c>Copyright</c>, <c>TermsHref</c>,
    /// <c>PrivacyHref</c>, <c>LoginHref</c>, <c>LogoutHref</c>, <c>RegisterHref</c> and
    /// <c>ProfileHref</c>).
    /// </para>
    /// <para>
    /// A host that resolves these values from its own portal store replaces this service by
    /// registering its own <see cref="IApertureSkinContext"/> before calling
    /// <c>AddApertureTheme</c>.
    /// </para>
    /// </remarks>
    public sealed partial class DefaultApertureSkinContext : IApertureSkinContext
    {
        private const string SectionName = "Aperture";

        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment environment;
        private readonly ILogger<DefaultApertureSkinContext> logger;

        /// <summary>Initializes a new instance of the <see cref="DefaultApertureSkinContext"/> class.</summary>
        /// <param name="configuration">The application configuration holding the <c>Aperture</c> section.</param>
        /// <param name="environment">The hosting environment, used to read an SVG logo for inlining.</param>
        /// <param name="logger">The logger used to report missing portal values instead of failing silently.</param>
        public DefaultApertureSkinContext(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<DefaultApertureSkinContext> logger)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(environment);
            ArgumentNullException.ThrowIfNull(logger);

            this.configuration = configuration;
            this.environment = environment;
            this.logger = logger;
        }

        /// <inheritdoc />
        public string SkinPath => ApertureThemeAssets.BasePath;

        /// <inheritdoc />
        public async ValueTask<ApertureLogo> GetLogoAsync(bool injectSvg, CancellationToken cancellationToken = default)
        {
            var portalName = this.Value("PortalName", "DNN");
            var homeHref = this.Value("HomeHref", "/");
            var logoHref = this.Value("LogoHref", string.Empty);

            string? svgMarkup = null;
            if (injectSvg && logoHref.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                svgMarkup = await this.ReadSvgAsync(logoHref, cancellationToken).ConfigureAwait(false);
            }

            return new ApertureLogo(homeHref, logoHref, portalName, svgMarkup);
        }

        /// <inheritdoc />
        public ValueTask<string> GetCopyrightAsync(CancellationToken cancellationToken = default)
        {
            var copyright = this.Value("Copyright", string.Empty);
            if (copyright.Length == 0)
            {
                // Preserve the legacy Copyright.ascx behaviour of always emitting a notice.
                copyright = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    "Copyright {0} by {1}",
                    DateTime.UtcNow.Year,
                    this.Value("PortalName", "DNN"));
            }

            return ValueTask.FromResult(copyright);
        }

        /// <inheritdoc />
        public ValueTask<ApertureLink> GetTermsLinkAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new ApertureLink("Terms", this.Value("TermsHref", "/Terms")));
        }

        /// <inheritdoc />
        public ValueTask<ApertureLink> GetPrivacyLinkAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new ApertureLink("Privacy", this.Value("PrivacyHref", "/Privacy")));
        }

        /// <inheritdoc />
        public ValueTask<ApertureUserLinks> GetUserLinksAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new ApertureUserLinks(
                string.Empty,
                this.Value("LoginHref", "/Login"),
                this.Value("LogoutHref", "/Logout"),
                this.Value("RegisterHref", "/Register"),
                this.Value("ProfileHref", "/Profile")));
        }

        /// <summary>Reads a value from the <c>Aperture</c> configuration section.</summary>
        /// <param name="key">The key within the section.</param>
        /// <param name="fallback">The value to use when the key is absent or blank.</param>
        /// <returns>The configured value, or <paramref name="fallback"/>.</returns>
        private string Value(string key, string fallback)
        {
            var value = this.configuration[$"{SectionName}:{key}"];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        /// <summary>Reads the markup of an SVG logo so it can be inlined into the page.</summary>
        /// <param name="logoHref">The configured logo path.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The SVG markup, or <see langword="null"/> when the file cannot be found.</returns>
        private async ValueTask<string?> ReadSvgAsync(string logoHref, CancellationToken cancellationToken)
        {
            var relative = logoHref.TrimStart('~', '/');

            // Static web assets of a Razor class library are exposed through the host's web root
            // file provider under their "_content/<assembly>/" path, so the href resolves directly.
            var file = this.environment.WebRootFileProvider.GetFileInfo(relative);
            if (!file.Exists)
            {
                this.LogMissingLogoSvg(logoHref);
                return null;
            }

            await using var stream = file.CreateReadStream();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "Aperture logo SVG '{LogoHref}' was requested for inlining but could not be found; falling back to an <img> element.")]
        private partial void LogMissingLogoSvg(string logoHref);
    }
}
