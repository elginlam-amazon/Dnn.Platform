// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// The base class for the client resource registration providers.
    /// </summary>
    /// <remarks>
    /// This replaces <c>ClientDependency.Core.FileRegistration.Providers.WebFormsFileRegistrationProvider</c>.
    /// Instead of injecting <c>LiteralControl</c> instances into WebForms placeholders, the providers return the
    /// markup to render, which the Tag Helpers emit at the matching position in the page.
    /// </remarks>
    public abstract class ClientResourceRegistrationProviderBase
    {
        private readonly IHostSettings hostSettings;
        private readonly IHttpContextAccessor httpContextAccessor;
        private bool enableCompositeFiles = true;

        /// <summary>Initializes a new instance of the <see cref="ClientResourceRegistrationProviderBase"/> class.</summary>
        /// <param name="compositeFileProcessor">The composite file processor used to bundle and minify resources.</param>
        /// <param name="hostSettings">The host settings, used to detect debug mode.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// <param name="name">The default friendly name of the provider.</param>
        protected ClientResourceRegistrationProviderBase(
            ICompositeFileProcessor compositeFileProcessor,
            IHostSettings hostSettings,
            IHttpContextAccessor httpContextAccessor,
            string name)
        {
            this.Name = name;
            this.CompositeFileProcessor = compositeFileProcessor;
            this.hostSettings = hostSettings;
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <summary>Gets the friendly name of the provider.</summary>
        public string Name { get; private set; }

        /// <summary>Gets a value indicating whether composite files (bundling) are enabled through the provider configuration.</summary>
        public virtual bool EnableCompositeFiles => this.enableCompositeFiles;

        /// <summary>Gets the composite file processor.</summary>
        protected ICompositeFileProcessor CompositeFileProcessor { get; }

        /// <summary>Gets a value indicating whether the application is running in debug mode.</summary>
        /// <remarks>This replaces <c>HttpContextBase.IsDebuggingEnabled</c>.</remarks>
        protected bool IsDebuggingEnabled => this.hostSettings is not null && this.hostSettings.DebugMode;

        /// <summary>Gets the current HTTP context, may be <see langword="null"/> outside of a request.</summary>
        protected HttpContext HttpContext => this.httpContextAccessor?.HttpContext;

        /// <summary>Initializes the provider.</summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing the provider-specific attributes specified in the configuration for this provider.</param>
        /// <exception cref="ArgumentNullException">The name of the provider is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The name of the provider has a length of zero.</exception>
        public virtual void Initialize(string name, NameValueCollection config)
        {
            ArgumentNullException.ThrowIfNull(name);

            if (name.Length == 0)
            {
                throw new ArgumentException("The provider name cannot be empty.", nameof(name));
            }

            this.Name = name;

            if (config is not null && bool.TryParse(config["enableCompositeFiles"], out var compositeFilesEnabled))
            {
                this.enableCompositeFiles = compositeFilesEnabled;
            }

            this.CompositeFileProcessor?.Initialize(config);
        }

        /// <summary>Renders the dependencies assigned to this provider and registers them for async post-backs.</summary>
        /// <param name="jsDependencies">The JavaScript dependencies.</param>
        /// <param name="cssDependencies">The CSS dependencies.</param>
        /// <param name="htmlAttributes">The HTML attributes to apply to each rendered tag.</param>
        /// <returns>The markup to emit at this provider's position in the page.</returns>
        public abstract string RegisterDependencies(
            IEnumerable<IResource> jsDependencies,
            IEnumerable<IResource> cssDependencies,
            IDictionary<string, string> htmlAttributes);

        /// <summary>Gets the URL of a dependency, preferring the resolved path.</summary>
        /// <param name="dependency">The dependency.</param>
        /// <returns>The URL to render.</returns>
        protected static string GetFilePath(IResource dependency)
        {
            if (dependency is null)
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(dependency.ResolvedPath) ? dependency.FilePath : dependency.ResolvedPath;
        }

        /// <summary>
        /// Escapes the ampersands in the rendered markup.
        /// </summary>
        /// <param name="markup">The markup to escape.</param>
        /// <returns>The escaped markup.</returns>
        /// <remarks>
        /// For some reason ampersands that aren't html escaped are not compliant to HTML standards when they exist in 'link' or 'script' tags in URLs,
        /// we need to replace the ampersands with &amp;amp; . This is only required for this one w3c compliancy, the URL itself is a valid URL.
        /// </remarks>
        protected static string EscapeAmpersands(string markup)
        {
            return string.IsNullOrEmpty(markup) ? string.Empty : markup.Replace("&", "&amp;");
        }

        /// <summary>Renders the markup for the given JavaScript dependencies.</summary>
        /// <param name="jsDependencies">The JavaScript dependencies.</param>
        /// <param name="htmlAttributes">The HTML attributes to apply to each rendered tag.</param>
        /// <returns>The markup.</returns>
        protected abstract string RenderJsDependencies(IEnumerable<IResource> jsDependencies, IDictionary<string, string> htmlAttributes);

        /// <summary>Renders the markup for a single JavaScript file.</summary>
        /// <param name="js">The URL of the file.</param>
        /// <param name="htmlAttributes">The HTML attributes to apply.</param>
        /// <returns>The markup.</returns>
        protected abstract string RenderSingleJsFile(string js, IDictionary<string, string> htmlAttributes);

        /// <summary>Renders the markup for the given CSS dependencies.</summary>
        /// <param name="cssDependencies">The CSS dependencies.</param>
        /// <param name="htmlAttributes">The HTML attributes to apply to each rendered tag.</param>
        /// <returns>The markup.</returns>
        protected abstract string RenderCssDependencies(IEnumerable<IResource> cssDependencies, IDictionary<string, string> htmlAttributes);

        /// <summary>Renders the markup for a single CSS file.</summary>
        /// <param name="css">The URL of the file.</param>
        /// <param name="htmlAttributes">The HTML attributes to apply.</param>
        /// <returns>The markup.</returns>
        protected abstract string RenderSingleCssFile(string css, IDictionary<string, string> htmlAttributes);
    }
}
