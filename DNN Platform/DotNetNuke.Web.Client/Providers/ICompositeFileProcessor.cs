// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.Providers
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;

    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Web.Client.Cdf;

    /// <summary>
    /// Combines, minifies and persists composite script/CSS files.
    /// </summary>
    /// <remarks>
    /// This is the DI-resolved replacement for
    /// <c>ClientDependency.Core.Config.ClientDependencySettings.Instance.DefaultCompositeFileProcessingProvider</c>.
    /// </remarks>
    public interface ICompositeFileProcessor
    {
        /// <summary>Gets a value indicating whether CSS minification is enabled through the provider configuration (<c>enableCssMinify</c>).</summary>
        bool EnableCssMinify { get; }

        /// <summary>Gets a value indicating whether JavaScript minification is enabled through the provider configuration (<c>enableJsMinify</c>).</summary>
        bool EnableJsMinify { get; }

        /// <summary>Gets a value indicating whether composite files are written to disk (<c>persistFiles</c>).</summary>
        bool PersistFiles { get; }

        /// <summary>Gets the virtual path in which composite files are persisted (<c>compositeFilePath</c>), defaults to <c>~/App_Data/ClientDependency</c>.</summary>
        string CompositeFilePath { get; }

        /// <summary>Applies the provider configuration options.</summary>
        /// <param name="config">The name/value pairs declared for the provider, may be <see langword="null"/>.</param>
        void Initialize(NameValueCollection config);

        /// <summary>Minifies the content of a stream.</summary>
        /// <param name="fileStream">The stream containing the file contents.</param>
        /// <param name="type">The type of the client dependency.</param>
        /// <returns>The (possibly minified) file contents.</returns>
        string MinifyFile(Stream fileStream, ClientDependencyType type);

        /// <summary>Minifies file contents.</summary>
        /// <param name="fileContents">The file contents.</param>
        /// <param name="type">The type of the client dependency.</param>
        /// <returns>The (possibly minified) file contents.</returns>
        string MinifyFile(string fileContents, ClientDependencyType type);

        /// <summary>Combines, minifies and persists the given dependencies into one or more composite bundles.</summary>
        /// <param name="dependencies">The dependencies to bundle, in render order.</param>
        /// <param name="type">The type of the client dependencies.</param>
        /// <returns>The URLs to render, in order. External (absolute) resources are returned as-is.</returns>
        IEnumerable<string> ProcessCompositeList(IEnumerable<IResource> dependencies, ClientDependencyType type);

        /// <summary>Gets a previously generated composite bundle by its file name.</summary>
        /// <param name="fileName">The bundle file name, as produced by <see cref="ProcessCompositeList"/>.</param>
        /// <param name="content">The bundle contents when found, otherwise <see langword="null"/>.</param>
        /// <param name="contentType">The content type of the bundle when found, otherwise <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when the bundle was found, otherwise <see langword="false"/>.</returns>
        bool TryGetBundle(string fileName, out string content, out string contentType);
    }
}
