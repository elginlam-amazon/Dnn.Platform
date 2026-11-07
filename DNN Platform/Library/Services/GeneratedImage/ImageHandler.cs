// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    using SixLabors.ImageSharp.Formats;
    using SixLabors.ImageSharp.Formats.Jpeg;

    /// <summary>Image Handler abstract class.</summary>
    /// <remarks>
    /// <para>
    /// Was an <c>IHttpHandler</c> registered in <c>web.config</c>; it is now invoked from an
    /// endpoint through <see cref="ProcessRequestAsync"/> (see
    /// <c>DnnImageHandlerEndpoint.MapDnnImageHandler</c>). <c>ProcessRequest(HttpContext)</c> became
    /// <see cref="ProcessRequestAsync"/> because writing the image to the response is asynchronous
    /// on ASP.NET Core.
    /// </para>
    /// <para>
    /// <see cref="ContentType"/> is an ImageSharp <see cref="IImageFormat"/> rather than a
    /// <c>System.Drawing.Imaging.ImageFormat</c>; the format singletons it is assigned
    /// (<c>JpegFormat.Instance</c>, <c>PngFormat.Instance</c>, …) are the direct equivalents.
    /// </para>
    /// </remarks>
    public abstract class ImageHandler
    {
        /// <summary>Initializes a new instance of the <see cref="ImageHandler"/> class.</summary>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="imageStore">The image store.</param>
        /// <param name="now">The current <see cref="DateTime"/>.</param>
        internal ImageHandler(IEventLogger eventLogger, IImageStore imageStore, DateTime now)
            : this(new ImageHandlerInternal(eventLogger, imageStore, now))
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ImageHandler"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IEventLogger. Scheduled removal in v12.0.0.")]
        protected ImageHandler()
            : this(Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>())
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ImageHandler"/> class.</summary>
        /// <param name="eventLogger">The event logger.</param>
        protected ImageHandler(IEventLogger eventLogger)
            : this(new ImageHandlerInternal(eventLogger))
        {
        }

        private ImageHandler(ImageHandlerInternal implementation)
        {
            this.Implementation = implementation;
        }

        /// <summary>Gets a value indicating whether this handler instance can be reused for more than one request.</summary>
        /// <remarks>On ASP.NET Core a new handler instance is created per endpoint invocation, so this is informational only.</remarks>
        public virtual bool IsReusable => false;

        /// <summary>Gets or sets a value indicating whether enables server-side caching of the result.</summary>
        public bool EnableServerCache
        {
            get => this.Implementation.EnableServerCache;
            set { this.Implementation.EnableServerCache = value; }
        }

        /// <summary>Gets or sets a value indicating whether enables client-side caching of the result.</summary>
        public bool EnableClientCache
        {
            get => this.Implementation.EnableClientCache;
            set => this.Implementation.EnableClientCache = value;
        }

        /// <summary>Gets or sets the client-side cache expiration time.</summary>
        public TimeSpan ClientCacheExpiration
        {
            get => this.Implementation.ClientCacheExpiration;
            set => this.Implementation.ClientCacheExpiration = value;
        }

        /// <summary>Gets or sets list of Domains who are allowed to use the imagehandler when security is enabled.</summary>
        public string[] AllowedDomains
        {
            get => this.Implementation.AllowedDomains;
            set => this.Implementation.AllowedDomains = value;
        }

        public bool AllowStandalone
        {
            get => this.Implementation.AllowStandalone;
            set { this.Implementation.AllowStandalone = value; }
        }

        public bool LogSecurity
        {
            get => this.Implementation.LogSecurity;
            set => this.Implementation.LogSecurity = value;
        }

        /// <summary>Gets or sets the type of the result image. The handler will return ouput with MIME type matching this content.</summary>
        public IImageFormat ContentType
        {
            get => this.Implementation.ContentType ?? JpegFormat.Instance;
            set => this.Implementation.ContentType = value;
        }

        /// <summary>Gets or sets the image compression encoding for the result image. Default is 50L.</summary>
        public long ImageCompression
        {
            get => this.Implementation.ImageCompression;
            set => this.Implementation.ImageCompression = value;
        }

        /// <summary>Gets or sets a value indicating whether enables block mechanism for DDOS by referring IP.</summary>
        public bool EnableIPCount
        {
            get => this.Implementation.EnableIPCount;
            set => this.Implementation.EnableIPCount = value;
        }

        /// <summary>
        /// Gets or sets the maximum amount of images an IP address is allowed to generate
        /// in the defined purge interval.
        /// </summary>
        public int IPCountMaxCount
        {
            get => this.Implementation.IPCountMax;
            set { this.Implementation.IPCountMax = value; }
        }

        /// <summary>Gets or sets timespan for resetting the blocking.</summary>
        public TimeSpan IPCountPurgeInterval
        {
            get => this.Implementation.IpCountPurgeInterval;
            set => this.Implementation.IpCountPurgeInterval = value;
        }

        /// <summary>Gets a list of image transforms that will be applied successively to the image.</summary>
        protected List<ImageTransform> ImageTransforms => this.Implementation.ImageTransforms;

        private ImageHandlerInternal Implementation { get; set; }

        public abstract ImageInfo GenerateImage(NameValueCollection parameters);

        /// <summary>Processes the incoming HTTP request, writing the generated image to the response.</summary>
        /// <param name="context">The context of the current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task ProcessRequestAsync(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return this.Implementation.HandleImageRequestAsync(context, this.GenerateImage, this.ToString());
        }
    }
}
