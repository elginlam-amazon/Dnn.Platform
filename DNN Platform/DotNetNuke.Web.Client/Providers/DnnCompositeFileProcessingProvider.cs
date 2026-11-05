// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.Providers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Web.Client.Cdf;

    using NUglify;

    /// <summary>A provider for combining, minifying, compressing and saving composite scripts/css files.</summary>
    [DnnDeprecated(10, 2, 0, "Bundling and minification is no longer supported, there is no replacement within DNN for this functionality.")]
    public partial class DnnCompositeFileProcessingProvider : ICompositeFileProcessor
    {
        /// <summary>The default virtual path in which the composite files are persisted.</summary>
        public const string DefaultCompositeFilePath = "~/App_Data/ClientDependency";

        private static readonly char[] InvalidFileNameCharacters = ['/', '\\', ':'];

        private static readonly ConcurrentDictionary<string, string> TransientBundles = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly ClientResourceSettings clientResourceSettings = new ClientResourceSettings();
        private readonly IApplicationStatusInfo applicationStatusInfo;

        /// <summary>Initializes a new instance of the <see cref="DnnCompositeFileProcessingProvider"/> class.</summary>
        /// <param name="applicationStatusInfo">The application status info, used to resolve physical paths.</param>
        public DnnCompositeFileProcessingProvider(IApplicationStatusInfo applicationStatusInfo)
        {
            this.applicationStatusInfo = applicationStatusInfo;
        }

        /// <inheritdoc />
        public bool EnableCssMinify { get; private set; } = true;

        /// <inheritdoc />
        public bool EnableJsMinify { get; private set; } = true;

        /// <inheritdoc />
        public bool PersistFiles { get; private set; } = true;

        /// <inheritdoc />
        public string CompositeFilePath { get; private set; } = DefaultCompositeFilePath;

        private bool MinifyCss
        {
            get
            {
                var enableCssMinification = this.clientResourceSettings.EnableCssMinification();
                return enableCssMinification.HasValue ? enableCssMinification.Value : this.EnableCssMinify;
            }
        }

        private bool MinifyJs
        {
            get
            {
                var enableJsMinification = this.clientResourceSettings.EnableJsMinification();
                return enableJsMinification.HasValue ? enableJsMinification.Value : this.EnableJsMinify;
            }
        }

        /// <inheritdoc />
        public void Initialize(NameValueCollection config)
        {
            if (config is null)
            {
                return;
            }

            if (bool.TryParse(config["enableCssMinify"], out var enableCssMinify))
            {
                this.EnableCssMinify = enableCssMinify;
            }

            if (bool.TryParse(config["enableJsMinify"], out var enableJsMinify))
            {
                this.EnableJsMinify = enableJsMinify;
            }

            if (bool.TryParse(config["persistFiles"], out var persistFiles))
            {
                this.PersistFiles = persistFiles;
            }

            var compositeFilePath = config["compositeFilePath"];
            if (!string.IsNullOrEmpty(compositeFilePath))
            {
                this.CompositeFilePath = compositeFilePath;
            }
        }

        /// <inheritdoc />
        public string MinifyFile(Stream fileStream, ClientDependencyType type)
        {
            Func<Stream, string> streamToString = stream =>
            {
                if (!stream.CanRead)
                {
                    throw new InvalidOperationException("Cannot read input stream");
                }

                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            };

            switch (type)
            {
                case ClientDependencyType.Css:
                    return this.MinifyCss ? MinifyCssContent(streamToString(fileStream)) : streamToString(fileStream);
                case ClientDependencyType.Javascript:
                    return this.MinifyJs ? MinifyJsContent(streamToString(fileStream)) : streamToString(fileStream);
                default:
                    return streamToString(fileStream);
            }
        }

        /// <inheritdoc />
        public string MinifyFile(string fileContents, ClientDependencyType type)
        {
            switch (type)
            {
                case ClientDependencyType.Css:
                    return this.MinifyCss ? MinifyCssContent(fileContents) : fileContents;
                case ClientDependencyType.Javascript:
                    {
                        if (!this.MinifyJs)
                        {
                            return fileContents;
                        }

                        return MinifyJsContent(fileContents);
                    }

                default:
                    return fileContents;
            }
        }

        /// <inheritdoc />
        public IEnumerable<string> ProcessCompositeList(IEnumerable<IResource> dependencies, ClientDependencyType type)
        {
            var urls = new List<string>();
            if (dependencies is null)
            {
                return urls;
            }

            var contents = new StringBuilder();
            var bundleKey = new StringBuilder();

            foreach (var dependency in dependencies)
            {
                if (dependency is null)
                {
                    continue;
                }

                var path = string.IsNullOrEmpty(dependency.ResolvedPath) ? dependency.FilePath : dependency.ResolvedPath;
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (IsExternalPath(path))
                {
                    // An external resource cannot be bundled, flush what we have so far to keep the render order
                    // and let the external resource be requested on its own.
                    this.FlushBundle(contents, bundleKey, type, urls);
                    urls.Add(path);
                    continue;
                }

                var physicalPath = this.MapPath(path);
                if (physicalPath is null || !File.Exists(physicalPath))
                {
                    // The file cannot be bundled (it may be dynamically generated), request it on its own.
                    this.FlushBundle(contents, bundleKey, type, urls);
                    urls.Add(path);
                    continue;
                }

                var fileContents = File.ReadAllText(physicalPath);
                if (type == ClientDependencyType.Css)
                {
                    fileContents = RebaseCssUrls(fileContents, path);
                }

                contents.Append(this.MinifyFile(fileContents, type)).Append('\n');
                bundleKey
                    .Append(physicalPath)
                    .Append('|')
                    .Append(File.GetLastWriteTimeUtc(physicalPath).Ticks.ToString(CultureInfo.InvariantCulture))
                    .Append(';');
            }

            this.FlushBundle(contents, bundleKey, type, urls);

            return urls;
        }

        /// <inheritdoc />
        public bool TryGetBundle(string fileName, out string content, out string contentType)
        {
            content = null;
            contentType = null;

            if (string.IsNullOrEmpty(fileName) || fileName.IndexOfAny(InvalidFileNameCharacters) >= 0 || fileName.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            contentType = fileName.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                ? "text/css"
                : "application/javascript";

            if (TransientBundles.TryGetValue(fileName, out content))
            {
                return true;
            }

            var directory = this.MapPath(this.CompositeFilePath);
            if (directory is null)
            {
                return false;
            }

            var physicalPath = Path.Combine(directory, fileName);
            if (!File.Exists(physicalPath))
            {
                return false;
            }

            content = File.ReadAllText(physicalPath);
            return true;
        }

        private static bool IsExternalPath(string path)
        {
            return path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("//", StringComparison.Ordinal);
        }

        private static string MinifyCssContent(string fileContents)
        {
            var result = Uglify.Css(fileContents);
            return result.HasErrors ? fileContents : result.Code;
        }

        private static string MinifyJsContent(string fileContents)
        {
            var result = Uglify.Js(fileContents);
            return result.HasErrors ? fileContents : result.Code;
        }

        /// <summary>Rewrites relative URLs inside a CSS file so that they still resolve from the bundle location.</summary>
        /// <param name="fileContents">The CSS content.</param>
        /// <param name="virtualPath">The virtual path of the original CSS file.</param>
        /// <returns>The CSS content with rebased URLs.</returns>
        private static string RebaseCssUrls(string fileContents, string virtualPath)
        {
            var basePath = virtualPath.TrimStart('~');
            var lastSlash = basePath.LastIndexOf('/');
            if (lastSlash < 0)
            {
                return fileContents;
            }

            basePath = basePath.Substring(0, lastSlash + 1);

            return System.Text.RegularExpressions.Regex.Replace(
                fileContents,
                @"url\(\s*(?<quote>['""]?)(?<url>[^'""\)]+)\k<quote>\s*\)",
                match =>
                {
                    var url = match.Groups["url"].Value.Trim();
                    if (url.Length == 0
                        || url.StartsWith('/')
                        || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                        || url.StartsWith('#')
                        || IsExternalPath(url))
                    {
                        return match.Value;
                    }

                    var quote = match.Groups["quote"].Value;
                    return $"url({quote}{basePath}{url}{quote})";
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(5));
        }

        private void FlushBundle(StringBuilder contents, StringBuilder bundleKey, ClientDependencyType type, List<string> urls)
        {
            if (contents.Length == 0)
            {
                return;
            }

            var content = contents.ToString();
            var fileName = GetBundleFileName(bundleKey.ToString(), type);

            if (this.PersistFiles)
            {
                var directory = this.MapPath(this.CompositeFilePath);
                if (directory is not null)
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                        var physicalPath = Path.Combine(directory, fileName);
                        if (!File.Exists(physicalPath))
                        {
                            File.WriteAllText(physicalPath, content);
                        }
                    }
                    catch (IOException)
                    {
                        // The bundle could not be persisted, keep it in memory for this application's lifetime.
                        TransientBundles[fileName] = content;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        TransientBundles[fileName] = content;
                    }
                }
                else
                {
                    TransientBundles[fileName] = content;
                }
            }
            else
            {
                TransientBundles[fileName] = content;
            }

            urls.Add($"{CompositeFileBundleEndpoint.BundleRoutePath}/{fileName}");

            contents.Clear();
            bundleKey.Clear();

            static string GetBundleFileName(string key, ClientDependencyType bundleType)
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
                var builder = new StringBuilder(32);
                for (var i = 0; i < 16; i++)
                {
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.Append(bundleType == ClientDependencyType.Css ? ".css" : ".js").ToString();
            }
        }

        /// <summary>Resolves a virtual path into a physical path.</summary>
        /// <param name="virtualPath">The virtual path.</param>
        /// <returns>The physical path or <see langword="null"/> when it cannot be resolved.</returns>
        private string MapPath(string virtualPath)
        {
            var applicationMapPath = this.applicationStatusInfo?.ApplicationMapPath;
            if (string.IsNullOrEmpty(applicationMapPath) || string.IsNullOrEmpty(virtualPath))
            {
                return null;
            }

            var relativePath = virtualPath;
            var queryStringIndex = relativePath.IndexOf('?');
            if (queryStringIndex >= 0)
            {
                relativePath = relativePath.Substring(0, queryStringIndex);
            }

            relativePath = relativePath
                .TrimStart('~')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            return Path.Combine(applicationMapPath, relativePath);
        }
    }
}
