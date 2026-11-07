// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.GeneratedImage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Http;

    /// <summary>A disk-backed server cache for the images produced by <see cref="ImageHandler"/>.</summary>
    /// <remarks>
    /// <para>
    /// The public API is unchanged. What changed is how stale files are purged: the old code queued
    /// <see cref="System.Threading.ThreadPool.QueueUserWorkItem(WaitCallback)"/> work from inside a
    /// request and called <see cref="Thread.Sleep(int)"/>, which is exactly the fire-and-forget
    /// pattern that loses work when the host recycles. Purging is now driven by
    /// <see cref="DiskImageStorePurgeService"/>, a <c>BackgroundService</c> on a
    /// <see cref="PeriodicTimer"/>, and <see cref="PurgeAsync"/> is the cooperative,
    /// cancellation-aware body it calls.
    /// </para>
    /// <para>
    /// The cache directory is resolved through <see cref="HttpContextSource.MapPath"/> instead of
    /// <c>HostingEnvironment.MapPath</c>, and lazily, because on ASP.NET Core the hosting
    /// environment is not available while static constructors run.
    /// </para>
    /// </remarks>
    public class DiskImageStore : IImageStore
    {
        private const string TempFileExtension = ".tmp";
        private const string CacheAppRelativePath = @"~\App_Data\_imagecache\";
        private static readonly object InstanceLock = new object();
        private static DiskImageStore diskImageStore;
        private static string cachePath;
        private static TimeSpan purgeInterval;
        private readonly object fileLock = new object();

        static DiskImageStore()
        {
            EnableAutoPurge = true;
            PurgeInterval = new TimeSpan(0, 5, 0);
        }

        /// <summary>Initializes a new instance of the <see cref="DiskImageStore"/> class.</summary>
        internal DiskImageStore()
        {
            EnsureCacheDirectory();
        }

        /// <summary>Gets or sets the physical directory the cached images are written to.</summary>
        public static string CachePath
        {
            get
            {
                if (cachePath == null)
                {
                    lock (InstanceLock)
                    {
                        cachePath ??= HttpContextSource.MapPath(CacheAppRelativePath) + Path.DirectorySeparatorChar;
                    }
                }

                return cachePath;
            }

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentNullException(nameof(value));
                }

                cachePath = value;
            }
        }

        /// <summary>Gets or sets a value indicating whether the purge feature is turned on.</summary>
        public static bool EnableAutoPurge { get; set; } // turn on/off purge feature

        /// <summary>Gets or sets how long a cached image is kept, and therefore how often the purge runs.</summary>
        public static TimeSpan PurgeInterval
        {
            get
            {
                return purgeInterval;
            }

            set
            {
                if (value.Ticks < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                purgeInterval = value;
            }
        }

        internal static IImageStore Instance
        {
            get
            {
                if (diskImageStore == null)
                {
                    lock (InstanceLock)
                    {
                        if (diskImageStore == null)
                        {
                            diskImageStore = new DiskImageStore();
                        }
                    }
                }

                return diskImageStore;
            }
        }

        /// <inheritdoc />
        public void ForcePurgeFromServerCache(string cacheId)
        {
            EnsureCacheDirectory();
            var files = new DirectoryInfo(CachePath).GetFiles();
            var fileInfo = files.FirstOrDefault(file => file.Name.Contains(cacheId, StringComparison.Ordinal));
            try
            {
                fileInfo?.Delete();
            }
            catch (Exception)
            {
                // do nothing at this point.
            }
        }

        /// <summary>Deletes every cached image that is older than <see cref="PurgeInterval"/>.</summary>
        /// <param name="cancellationToken">A token that aborts the purge when the host is shutting down.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This is the old <c>PurgeCallback</c>. The two-phase "delete, yield, retry the ones that
        /// were locked" behavior is kept; <see cref="Thread.Sleep(int)"/> became
        /// <see cref="Task.Delay(int, CancellationToken)"/>, and
        /// <see cref="OperationCanceledException"/> is allowed to propagate so the hosted service
        /// stops promptly.
        /// </remarks>
        internal static async Task PurgeAsync(CancellationToken cancellationToken)
        {
            EnsureCacheDirectory();
            var files = new DirectoryInfo(CachePath).GetFiles();
            var threshold = DateTime.Now.Subtract(PurgeInterval);
            var toTryDeleteAgain = new List<FileInfo>();
            foreach (var fileinfo in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (fileinfo.CreationTime < threshold)
                {
                    try
                    {
                        fileinfo.Delete();
                    }
                    catch (Exception)
                    {
                        toTryDeleteAgain.Add(fileinfo);
                    }
                }
            }

            await Task.Delay(TimeSpan.Zero, cancellationToken).ConfigureAwait(false);

            foreach (var fileinfo in toTryDeleteAgain)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    fileinfo.Delete();
                }
                catch (Exception)
                {
                    // do nothing at this point, try to delete file during next purge
                }
            }
        }

        /// <inheritdoc />
        void IImageStore.Add(string id, byte[] data)
        {
            this.Add(id, data);
        }

        /// <inheritdoc />
        Task<bool> IImageStore.TryTransmitIfContainsAsync(string id, HttpResponse response)
        {
            return this.TryTransmitIfContainsAsync(id, response);
        }

        private static void EnsureCacheDirectory()
        {
            var path = CachePath;
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static string BuildFilePath(string id)
        {
            return CachePath + id + TempFileExtension;
        }

        private void Add(string id, byte[] data)
        {
            EnsureCacheDirectory();
            var path = BuildFilePath(id);
            lock (this.GetFileLockObject(id))
            {
                try
                {
                    File.WriteAllBytes(path, data);
                }
                catch (Exception)
                {
                    // REVIEW for now ignore any write problems
                }
            }
        }

        private async Task<bool> TryTransmitIfContainsAsync(string id, HttpResponse response)
        {
            string path = BuildFilePath(id);
            bool exists;
            lock (this.GetFileLockObject(id))
            {
                exists = File.Exists(path);
            }

            if (!exists)
            {
                return false;
            }

            // Replaces HttpResponse.TransmitFile, which has no synchronous equivalent on
            // ASP.NET Core; SendFileAsync uses the same kernel-level sendfile path.
            await response.SendFileAsync(path).ConfigureAwait(false);
            return true;
        }

        private object GetFileLockObject(string id)
        {
            return this.fileLock;
        }
    }
}
