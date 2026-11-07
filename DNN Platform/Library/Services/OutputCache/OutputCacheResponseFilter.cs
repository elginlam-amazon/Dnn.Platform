// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.OutputCache
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;

    // helper class to capture the response into a file
    [SuppressMessage("Microsoft.Design", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "Breaking change")]
    public abstract class OutputCacheResponseFilter : Stream
    {
        private Stream captureStream;
        private HttpContext attachedContext;
        private IHttpResponseBodyFeature originalBodyFeature;

        /// <summary>Initializes a new instance of the <see cref="OutputCacheResponseFilter"/> class.</summary>
        /// <param name="filterChain">The stream to write into <see cref="CaptureStream"/>.</param>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="cacheDuration">The duration for which the response should be cached.</param>
        /// <param name="maxVaryByCount">The maximum number of values by which the cached response can vary.</param>
        public OutputCacheResponseFilter(Stream filterChain, string cacheKey, TimeSpan cacheDuration, int maxVaryByCount)
        {
            this.ChainedStream = filterChain;
            this.CacheKey = cacheKey;
            this.CacheDuration = cacheDuration;
            this.MaxVaryByCount = maxVaryByCount;
            this.captureStream = this.CaptureStream;
        }

        /// <inheritdoc />
        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        /// <inheritdoc />
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        /// <inheritdoc />
        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        /// <inheritdoc />
        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public TimeSpan CacheDuration { get; set; }

        public virtual string CacheKey { get; set; }

        public Stream CaptureStream
        {
            get
            {
                return this.captureStream;
            }

            set
            {
                this.captureStream = value;
            }
        }

        public Stream ChainedStream { get; set; }

        public bool HasErrored { get; set; }

        public int MaxVaryByCount { get; set; }

        /// <inheritdoc />
        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        /// <inheritdoc />
        public override void Flush()
        {
            this.ChainedStream.Flush();
            if (this.HasErrored)
            {
                return;
            }

            if (this.captureStream != null)
            {
                this.captureStream.Flush();
            }
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.ChainedStream.Write(buffer, offset, count);
            if (this.HasErrored)
            {
                return;
            }

            if (this.captureStream != null)
            {
                this.captureStream.Write(buffer, offset, count);
            }
        }

        /// <inheritdoc />
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await this.ChainedStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            if (this.HasErrored)
            {
                return;
            }

            var capture = this.captureStream;
            if (capture != null)
            {
                await capture.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await this.ChainedStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (this.HasErrored)
            {
                return;
            }

            var capture = this.captureStream;
            if (capture != null)
            {
                await capture.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await this.ChainedStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (this.HasErrored)
            {
                return;
            }

            var capture = this.captureStream;
            if (capture != null)
            {
                await capture.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Installs this filter as the response body of the given request, so that everything
        /// written by the rest of the pipeline is both sent to the client and captured for the
        /// output cache.
        /// </summary>
        /// <param name="context">The current request.</param>
        /// <remarks>
        /// This is the ASP.NET Core replacement for the legacy <c>HttpResponse.Filter</c> property:
        /// the <see cref="IHttpResponseBodyFeature"/> is swapped for one wrapping this stream, and
        /// restored again by <see cref="Detach"/>.
        /// </remarks>
        public void AttachTo(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (this.attachedContext != null)
            {
                return;
            }

            this.attachedContext = context;
            this.originalBodyFeature = context.Features.Get<IHttpResponseBodyFeature>();
            context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(this));
        }

        /// <summary>Restores the original response body feature replaced by <see cref="AttachTo"/>.</summary>
        public void Detach()
        {
            var context = this.attachedContext;
            if (context == null)
            {
                return;
            }

            this.attachedContext = null;
            context.Features.Set(this.originalBodyFeature);
            this.originalBodyFeature = null;
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public virtual byte[] StopFiltering(int itemId, bool deleteData)
        {
            this.Detach();

            if (this.HasErrored)
            {
                return null;
            }

            if (this.CaptureStream != null)
            {
                this.CaptureStream.Position = 0;
                using (var reader = new StreamReader(this.CaptureStream, Encoding.UTF8))
                {
                    string output = reader.ReadToEnd();
                    this.AddItemToCache(itemId, output);
                }

                this.CaptureStream.Close();
                this.CaptureStream = null;
            }

            if (deleteData)
            {
                this.RemoveItemFromCache(itemId);
            }

            return null;
        }

        protected virtual void AddItemToCache(int itemId, string output)
        {
        }

        protected virtual void RemoveItemFromCache(int itemId)
        {
        }
    }
}
