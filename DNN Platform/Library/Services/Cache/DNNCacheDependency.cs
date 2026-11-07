// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Cache
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Extensions.Primitives;

    /// <summary>DNNCacheDependency provides dependency policies of cache system.</summary>
    /// <remarks>
    /// <para>
    /// A cache dependency monitors files, directories and/or other cache keys so that when any of
    /// them changes, the cached item is automatically removed.
    /// </para>
    /// <para>
    /// On ASP.NET Core there is no <c>System.Web.Caching.CacheDependency</c>. This type is now
    /// implemented with <see cref="IChangeToken"/>s: file and directory dependencies become
    /// <see cref="Microsoft.Extensions.FileProviders.PhysicalFileProvider"/> watch tokens, and
    /// cache-key dependencies become <see cref="CancellationChangeToken"/>s that the
    /// <see cref="DnnCacheStore"/> cancels whenever the key they watch changes. All public
    /// constructors and members are preserved; only <c>SystemCacheDependency</c> changed type
    /// (from the removed <c>CacheDependency</c> to <see cref="IChangeToken"/>).
    /// </para>
    /// </remarks>
    public class DNNCacheDependency : IDisposable
    {
        private readonly DateTime utcStart = DateTime.MaxValue;
        private DNNCacheDependency cacheDependency;
        private string[] cacheKeys;
        private string[] fileNames;
        private IChangeToken changeToken;
        private bool disposed;

        /// <summary>Initializes a new instance of the <see cref="DNNCacheDependency"/> class from an existing change token.</summary>
        /// <param name="changeToken">The change token which signals that the cached item is stale.</param>
        public DNNCacheDependency(IChangeToken changeToken)
        {
            this.changeToken = changeToken;
        }

        /// <summary>Initializes a new instance of the <see cref="DNNCacheDependency"/> class that monitors a file or directory for changes.</summary>
        /// <param name="filename">The path to a file which the cache depends on.</param>
        public DNNCacheDependency(string filename)
        {
            this.fileNames = new[] { filename };
        }

        /// <summary>Initializes a new instance of the <see cref="DNNCacheDependency"/> class that monitors an array of paths (to files or directories) for changes.</summary>
        /// <param name="filenames">set the cache depend on multiple files.</param>
        public DNNCacheDependency(string[] filenames)
        {
            this.fileNames = filenames;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DNNCacheDependency"/> class that monitors an array of paths
        ///  (to files or directories) for changes and specifies a time when change monitoring begins.
        /// </summary>
        /// <param name="filenames">The filenames.</param>
        /// <param name="start">The start.</param>
        public DNNCacheDependency(string[] filenames, DateTime start)
        {
            this.utcStart = start.ToUniversalTime();
            this.fileNames = filenames;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DNNCacheDependency"/> class that monitors an array of paths (to files or directories),
        /// an array of cache keys, or both for changes.
        /// </summary>
        /// <param name="filenames">The filenames.</param>
        /// <param name="cachekeys">The cachekeys.</param>
        public DNNCacheDependency(string[] filenames, string[] cachekeys)
        {
            this.fileNames = filenames;
            this.cacheKeys = cachekeys;
        }

        /// <summary>Initializes a new instance of the <see cref="DNNCacheDependency"/> class that monitors a file or directory for changes.</summary>
        /// <param name="filename">The filename.</param>
        /// <param name="start">The start.</param>
        public DNNCacheDependency(string filename, DateTime start)
        {
            this.utcStart = start.ToUniversalTime();
            if (filename != null)
            {
                this.fileNames = new[] { filename };
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DNNCacheDependency"/> class that monitors an array of paths (to files or directories),
        /// an array of cache keys, or both for changes.
        /// </summary>
        /// <param name="filenames">The filenames.</param>
        /// <param name="cachekeys">The cachekeys.</param>
        /// <param name="start">The start.</param>
        public DNNCacheDependency(string[] filenames, string[] cachekeys, DateTime start)
        {
            this.utcStart = start.ToUniversalTime();
            this.fileNames = filenames;
            this.cacheKeys = cachekeys;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DNNCacheDependency"/> classthat monitors an array of paths (to files or directories),
        /// an array of cache keys, or both for changes. It also makes itself dependent upon a separate instance of the <see cref="DNNCacheDependency"/> class.
        /// </summary>
        /// <param name="filenames">The filenames.</param>
        /// <param name="cachekeys">The cachekeys.</param>
        /// <param name="dependency">The dependency.</param>
        public DNNCacheDependency(string[] filenames, string[] cachekeys, DNNCacheDependency dependency)
        {
            this.fileNames = filenames;
            this.cacheKeys = cachekeys;
            this.cacheDependency = dependency;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DNNCacheDependency"/> class that monitors an array of paths (to files or directories),
        /// an array of cache keys, or both for changes.
        /// It also makes itself dependent upon another instance of the <see cref="DNNCacheDependency"/> class and a time when the change monitoring begins.
        /// </summary>
        /// <param name="filenames">The filenames.</param>
        /// <param name="cachekeys">The cachekeys.</param>
        /// <param name="dependency">The dependency.</param>
        /// <param name="start">The start.</param>
        public DNNCacheDependency(string[] filenames, string[] cachekeys, DNNCacheDependency dependency, DateTime start)
        {
            this.utcStart = start.ToUniversalTime();
            this.fileNames = filenames;
            this.cacheKeys = cachekeys;
            this.cacheDependency = dependency;
        }

        /// <summary>Gets the cache keys.</summary>
        public string[] CacheKeys
        {
            get
            {
                return this.cacheKeys;
            }
        }

        /// <summary>Gets the file names.</summary>
        public string[] FileNames
        {
            get
            {
                return this.fileNames;
            }
        }

        /// <summary>Gets a value indicating whether this instance has changed.</summary>
        /// <value>
        ///     <see langword="true"/> if this instance has changed; otherwise, <see langword="false"/>.
        /// </value>
        public bool HasChanged
        {
            get
            {
                return this.ChangeToken.HasChanged;
            }
        }

        /// <summary>Gets the cache dependency.</summary>
        public DNNCacheDependency CacheDependency
        {
            get
            {
                return this.cacheDependency;
            }
        }

        /// <summary>Gets the start time.</summary>
        public DateTime StartTime
        {
            get
            {
                return this.utcStart;
            }
        }

        /// <summary>Gets the change token which signals that the cached item is stale.</summary>
        public IChangeToken ChangeToken
        {
            get
            {
                if (this.changeToken == null)
                {
                    var tokens = this.BuildChangeTokens();
                    this.changeToken = tokens.Count == 1
                        ? tokens[0]
                        : new CompositeChangeToken(tokens);
                }

                return this.changeToken;
            }
        }

        /// <summary>Gets the underlying change token.</summary>
        /// <remarks>
        /// Preserved for source compatibility with the legacy <c>SystemCacheDependency</c> member.
        /// The type changed from the removed <c>System.Web.Caching.CacheDependency</c> to
        /// <see cref="IChangeToken"/>; prefer <see cref="ChangeToken"/> in new code.
        /// </remarks>
        public IChangeToken SystemCacheDependency
        {
            get
            {
                return this.ChangeToken;
            }
        }

        /// <summary>Gets the UTC last modified.</summary>
        public DateTime UtcLastModified
        {
            get
            {
                var lastModified = CacheFileWatcher.GetUtcLastModified(this.fileNames);
                if (this.cacheDependency != null)
                {
                    var inner = this.cacheDependency.UtcLastModified;
                    if (inner > lastModified)
                    {
                        lastModified = inner;
                    }
                }

                return lastModified;
            }
        }

        /// <summary>Gets the change tokens making up this dependency.</summary>
        /// <returns>A non-<see langword="null"/> list of change tokens.</returns>
        internal IList<IChangeToken> GetChangeTokens()
        {
            var token = this.ChangeToken;
            if (token is CompositeChangeToken composite)
            {
                return new List<IChangeToken>(composite.ChangeTokens);
            }

            return new List<IChangeToken> { token };
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Method that does the actual disposal of resources
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                this.disposed = true;

                this.cacheDependency?.Dispose(disposing);

                this.fileNames = null;
                this.cacheKeys = null;
                this.cacheDependency = null;
                this.changeToken = null;
            }
        }

        private List<IChangeToken> BuildChangeTokens()
        {
            var tokens = new List<IChangeToken>();

            if (this.fileNames != null)
            {
                foreach (var fileName in this.fileNames)
                {
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        tokens.Add(CacheFileWatcher.Watch(fileName));
                    }
                }
            }

            if (this.cacheKeys != null)
            {
                foreach (var cacheKey in this.cacheKeys)
                {
                    if (!string.IsNullOrWhiteSpace(cacheKey))
                    {
                        tokens.Add(DnnCacheStore.Instance.GetKeyChangeToken(cacheKey));
                    }
                }
            }

            if (this.cacheDependency != null)
            {
                tokens.Add(this.cacheDependency.ChangeToken);
            }

            if (tokens.Count == 0)
            {
                // No files, keys or nested dependency: a dependency which never fires, exactly as
                // an empty legacy CacheDependency behaved.
                tokens.Add(Microsoft.Extensions.FileProviders.NullChangeToken.Singleton);
            }

            return tokens;
        }
    }
}
