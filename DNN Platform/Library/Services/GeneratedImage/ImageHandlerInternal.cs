// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using DotNetNuke.Abstractions.Logging;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;
using DotNetNuke.Services.Log.EventLog;
using DotNetNuke.Services.UserRequest;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>A helper for <see cref="ImageHandler"/>.</summary>
/// <remarks>
/// Ported from <c>System.Web</c> to ASP.NET Core and from <c>System.Drawing</c> to ImageSharp.
/// Every security check, every cache decision and the response status codes are unchanged; the
/// request handling is now asynchronous because writing the response body and transmitting a
/// cached file are asynchronous on ASP.NET Core.
/// </remarks>
internal class ImageHandlerInternal
{
    private static readonly TimeSpan DefaultClientCacheExpiration = new TimeSpan(0, 10, 0);

    private readonly IEventLogger eventLogger;
    private readonly IImageStore imageStore;
    private readonly DateTime? now;
    private TimeSpan clientCacheExpiration = DefaultClientCacheExpiration;

    /// <summary>Initializes a new instance of the <see cref="ImageHandlerInternal"/> class.</summary>
    /// <param name="eventLogger">The event logger.</param>
    public ImageHandlerInternal(IEventLogger eventLogger)
    {
        this.eventLogger = eventLogger;

        this.ContentType = JpegFormat.Instance;
        this.ImageCompression = 95;
        this.ImageTransforms = [];
        this.AllowStandalone = false;
    }

    /// <summary>Initializes a new instance of the <see cref="ImageHandlerInternal"/> class.</summary>
    /// <param name="eventLogger">The event logger.</param>
    /// <param name="imageStore">The image store.</param>
    /// <param name="now">The current <see cref="DateTime"/>.</param>
    internal ImageHandlerInternal(IEventLogger eventLogger, IImageStore imageStore, DateTime now)
        : this(eventLogger)
    {
        this.imageStore = imageStore;
        this.now = now;
    }

    public TimeSpan ClientCacheExpiration
    {
        get
        {
            return this.clientCacheExpiration;
        }

        set
        {
            if (value.Ticks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "ClientCacheExpiration must be positive");
            }

            this.clientCacheExpiration = value;
            this.EnableClientCache = true;
        }
    }

    public IImageFormat ContentType { get; set; }

    public long ImageCompression { get; set; }

    public int IPCountMax
    {
        get => IPCount.MaxCount;
        set => IPCount.MaxCount = value;
    }

    public TimeSpan IpCountPurgeInterval
    {
        get => IPCount.PurgeInterval;
        set => IPCount.PurgeInterval = value;
    }

    public bool EnableClientCache { get; set; }

    public bool EnableServerCache { get; set; }

    public bool EnableIPCount { get; set; }

    public bool AllowStandalone { get; set; }

    public string[] AllowedDomains { get; set; }

    public bool LogSecurity { get; set; }

    public List<ImageTransform> ImageTransforms { get; private set; }

    private DateTime DateTime_Now => this.now ?? DateTime.Now;

    private IImageStore ImageStore => this.imageStore ?? DiskImageStore.Instance;

    public async Task HandleImageRequestAsync(HttpContext context, Func<NameValueCollection, ImageInfo> imageGenCallback, string uniqueIdStringSeed)
    {
        // Replaces Response.Clear(): on ASP.NET Core nothing has been written yet at this point, so
        // only the headers an earlier middleware may have added need resetting.
        context.Response.Headers.Clear();

        var request = context.Request;
        var ipAddress = UserRequestIPAddressController.Instance.GetUserRequestIPAddress(request);
        var referrer = GetReferrer(request);

        // Check if allowed standalone
        if (!this.AllowStandalone && referrer == null && !IsLocal(context))
        {
            const string Message = "Not allowed to use standalone";
            if (this.LogSecurity)
            {
                this.LogSecurityEvent(Message, ipAddress);
            }

            SetStatus(context, (int)HttpStatusCode.Forbidden, Message);
            return;
        }

        // Check if domain is allowed to embed image
        if (!string.IsNullOrEmpty(this.AllowedDomains[0]) &&
            referrer != null &&
            !referrer.Host.Equals(request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            var allowed = false;
            var allowedDomains = string.Empty;
            foreach (var allowedDomain in this.AllowedDomains)
            {
                if (string.IsNullOrEmpty(allowedDomain))
                {
                    continue;
                }

                allowedDomains += allowedDomain + ",";
                if (referrer.Host.Contains(allowedDomain, StringComparison.OrdinalIgnoreCase))
                {
                    allowed = true;
                }
            }

            if (!allowed)
            {
                var message = $"Not allowed to use from referrer '{referrer.Host}'";
                if (this.LogSecurity)
                {
                    ILogInfo logInfo = new LogInfo
                    {
                        LogTypeKey = nameof(EventLogType.ADMIN_ALERT),
                    };
                    logInfo.LogUserId = PortalSettings.Current.UserId;
                    logInfo.LogPortalId = PortalSettings.Current.PortalId;
                    logInfo.AddProperty("DnnImageHandler", message);
                    logInfo.AddProperty("IP", ipAddress);

#pragma warning disable CA1507 // Use nameof in place of string
                    logInfo.AddProperty("AllowedDomains", allowedDomains);
#pragma warning restore CA1507

                    this.eventLogger.AddLog(logInfo);
                }

                SetStatus(context, (int)HttpStatusCode.Forbidden, "Forbidden");
                return;
            }
        }

        var queryString = ToNameValueCollection(request.Query);

        // Generate Image
        var imageMethodData = imageGenCallback(queryString);

        context.Response.ContentType = GetImageMimeType(this.ContentType);
        if (imageMethodData == null)
        {
            throw new InvalidOperationException("The DnnImageHandler cannot return null.");
        }

        if (imageMethodData.IsEmptyImage)
        {
            using var imageOutputBuffer = new MemoryStream();
            this.RenderImage(imageMethodData.Image, imageOutputBuffer);
            imageOutputBuffer.Position = 0;
            await imageOutputBuffer.CopyToAsync(context.Response.Body).ConfigureAwait(false);
            return;
        }

        string cacheId = this.GetUniqueIdString(queryString, uniqueIdStringSeed);

        var userId = -1;
        var cacheCleared = false;
        var isProfilePic = "profilepic".Equals(queryString["mode"], StringComparison.OrdinalIgnoreCase);
        if (isProfilePic)
        {
            if (int.TryParse(queryString["userId"], out userId))
            {
                cacheCleared = this.ClearDiskImageCacheIfNecessary(userId, PortalSettings.Current.PortalId, cacheId);
            }
        }

        var isAuthorizedProfilePicRequest = !isProfilePic || userId <= 0 || IsPicVisibleToCurrentUser(userId);
        if (!isAuthorizedProfilePicRequest)
        {
            const string message = "Not allowed to see profile picture";

            if (this.LogSecurity)
            {
                this.LogSecurityEvent(message, ipAddress);
            }

            await ReturnDefaultProfilePictureAsync(context).ConfigureAwait(false);
            return;
        }

        // Handle client cache
        var headers = context.Response.Headers;
        if (this.EnableClientCache)
        {
            if (!string.IsNullOrEmpty(request.Headers[HeaderNames.IfModifiedSince]) &&
                !string.IsNullOrEmpty(request.Headers[HeaderNames.IfNoneMatch]) &&
                !cacheCleared)
            {
                var provider = CultureInfo.InvariantCulture;
                var lastMod = DateTime.ParseExact(request.Headers[HeaderNames.IfModifiedSince], "r", provider).ToLocalTime();
                var etag = request.Headers[HeaderNames.IfNoneMatch].ToString();
                if (lastMod + this.ClientCacheExpiration > this.DateTime_Now && etag == cacheId)
                {
                    SetStatus(context, (int)HttpStatusCode.NotModified, "Not Modified");
                    return;
                }
            }

            // Replaces HttpCachePolicy: SetCacheability(Public) + SetValidUntilExpires(true) +
            // SetLastModified + SetExpires + SetETag.
            headers[HeaderNames.CacheControl] = "public, max-age=" + ((int)this.ClientCacheExpiration.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            headers[HeaderNames.LastModified] = this.DateTime_Now.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
            headers[HeaderNames.Expires] = (this.DateTime_Now + this.ClientCacheExpiration).ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
            headers[HeaderNames.ETag] = cacheId;
        }

        // Handle Server cache
        if (this.EnableServerCache)
        {
            if (await this.ImageStore.TryTransmitIfContainsAsync(cacheId, context.Response).ConfigureAwait(false))
            {
                await context.Response.Body.FlushAsync().ConfigureAwait(false);
                return;
            }
        }

        // Check IP Count boundaries
        if (this.EnableIPCount)
        {
            if (!IPCount.CheckIp(ipAddress))
            {
                const string Message = "Too many requests";

                if (this.LogSecurity)
                {
                    this.LogSecurityEvent(Message, ipAddress);
                }

                SetStatus(context, StatusCodes.Status429TooManyRequests, Message);
                return;
            }
        }

        if (imageMethodData.HttpStatusCode != null)
        {
            context.Response.StatusCode = (int)imageMethodData.HttpStatusCode;
            return;
        }

        using (var imageOutputBuffer = new MemoryStream())
        {
            Debug.Assert(!(imageMethodData.Image == null && imageMethodData.ImageByteBuffer == null), "Image or ImageByteBuffer must have a value");
            if (imageMethodData.Image != null)
            {
                this.RenderImage(this.GetImageThroughTransforms(imageMethodData.Image), imageOutputBuffer);
            }
            else if (imageMethodData.ImageByteBuffer != null)
            {
                this.RenderImage(this.GetImageThroughTransforms(imageMethodData.ImageByteBuffer), imageOutputBuffer);
            }

            var buffer = imageOutputBuffer.ToArray();

            await context.Response.Body.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

            if (this.EnableServerCache)
            {
                this.ImageStore.Add(cacheId, buffer);
            }
        }
    }

    /// <summary>Gets the MIME type the given image format is served with.</summary>
    /// <param name="format">The image format.</param>
    /// <returns>The MIME type.</returns>
    /// <remarks>
    /// The GDI+ <c>if/else</c> chain over <c>ImageFormat</c> singletons becomes a single read of
    /// ImageSharp's <see cref="IImageFormat.DefaultMimeType"/>, which produces the same strings.
    /// </remarks>
    internal static string GetImageMimeType(IImageFormat format)
    {
        return format?.DefaultMimeType ?? "image/x-unknown";
    }

    /// <summary>Returns the ImageSharp encoder for an image format, honoring <see cref="ImageCompression"/>.</summary>
    /// <remarks>
    /// Replaces <c>ImageCodecInfo.GetImageEncoders()</c> plus the <c>Encoder.Quality</c>
    /// <c>EncoderParameter</c>. Only the lossy formats have a quality knob; the lossless ones
    /// ignore it, exactly as GDI+ did.
    /// </remarks>
    private IImageEncoder GetEncoder(IImageFormat format)
    {
        var quality = (int)Math.Clamp(this.ImageCompression, 1, 100);
        return format switch
        {
            PngFormat => new PngEncoder(),
            GifFormat => new GifEncoder { Quantizer = new OctreeQuantizer(new QuantizerOptions { MaxColors = 255 }) },
            BmpFormat => new BmpEncoder(),
            TiffFormat => new TiffEncoder(),
            WebpFormat => new WebpEncoder { Quality = quality },
            _ => new JpegEncoder { Quality = quality },
        };
    }

    private static NameValueCollection ToNameValueCollection(IQueryCollection query)
    {
        var collection = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query)
        {
            foreach (var value in pair.Value)
            {
                collection.Add(pair.Key, value);
            }
        }

        return collection;
    }

    /// <summary>Replaces <c>HttpRequest.UrlReferrer</c>.</summary>
    private static Uri GetReferrer(HttpRequest request)
    {
        var referrer = request.Headers[HeaderNames.Referer].ToString();
        return !string.IsNullOrEmpty(referrer) && Uri.TryCreate(referrer, UriKind.Absolute, out var uri) ? uri : null;
    }

    /// <summary>Replaces <c>HttpRequest.IsLocal</c>.</summary>
    private static bool IsLocal(HttpContext context)
    {
        var connection = context.Connection;
        if (connection.RemoteIpAddress == null)
        {
            // No remote address means an in-process request, which the old IsLocal also treated as local.
            return true;
        }

        if (System.Net.IPAddress.IsLoopback(connection.RemoteIpAddress))
        {
            return true;
        }

        return connection.LocalIpAddress != null && connection.RemoteIpAddress.Equals(connection.LocalIpAddress);
    }

    /// <summary>
    /// Replaces the <c>Response.StatusDescription</c> setter plus <c>Response.End()</c>: the reason
    /// phrase lives on <see cref="IHttpResponseFeature"/>, and returning from the handler is how a
    /// Core request is ended.
    /// </summary>
    private static void SetStatus(HttpContext context, int statusCode, string reasonPhrase)
    {
        context.Response.StatusCode = statusCode;
        var responseFeature = context.Features.Get<IHttpResponseFeature>();
        if (responseFeature != null)
        {
            responseFeature.ReasonPhrase = reasonPhrase;
        }
    }

    private static string GetIdFromBytes(byte[] buffer)
    {
        using var hasher = CryptographyUtils.CreateSHA256();
        var sb = new StringBuilder();
        foreach (var b in hasher.ComputeHash(buffer))
        {
            sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static bool IsPicVisibleToCurrentUser(int profileUserId)
    {
        var settings = PortalController.Instance.GetCurrentSettings();
        var profileUser = UserController.Instance.GetUser(settings.PortalId, profileUserId);

        var photoProperty = profileUser?.Profile.GetProperty("Photo");
        if (photoProperty == null)
        {
            return false;
        }

        var currentUser = UserController.Instance.GetCurrentUserInfo();
        return ProfilePropertyAccess.CheckAccessLevel(settings, photoProperty, currentUser, profileUser);
    }

    private static async Task ReturnDefaultProfilePictureAsync(HttpContext context)
    {
        context.Response.Headers[HeaderNames.CacheControl] = "private, no-store, no-cache";
        context.Response.ContentType = "image/gif";

        // Replaces Response.WriteFile(Server.MapPath(...)).
        var path = HttpContextSource.MapPath("~/images/no_avatar.gif");
        if (!File.Exists(path))
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return;
        }

        await context.Response.SendFileAsync(path).ConfigureAwait(false);
    }

    private void LogSecurityEvent(string message, string ipAddress)
    {
        ILogInfo logInfo = new LogInfo
        {
            LogTypeKey = nameof(EventLogType.ADMIN_ALERT),
        };
        logInfo.LogUserId = PortalSettings.Current.UserId;
        logInfo.LogPortalId = PortalSettings.Current.PortalId;
        logInfo.AddProperty("DnnImageHandler", message);
        logInfo.AddProperty("IP", ipAddress);
        this.eventLogger.AddLog(logInfo);
    }

    private string GetUniqueIdString(NameValueCollection queryString, string uniqueIdStringSeed)
    {
        var builder = new StringBuilder();
        builder.Append(uniqueIdStringSeed);
        foreach (var key in queryString.AllKeys.OrderBy(k => k, StringComparer.Ordinal))
        {
            builder.Append(key);
            builder.Append(queryString.Get(key));
        }

        foreach (var tran in this.ImageTransforms)
        {
            builder.Append(tran.UniqueString);
        }

        return GetIdFromBytes(Encoding.ASCII.GetBytes(builder.ToString()));
    }

    private Image GetImageThroughTransforms(Image image)
    {
        try
        {
            return this.ImageTransforms.Aggregate(image, (current, tran) => tran.ProcessImage(current));
        }
        finally
        {
            image?.Dispose();
        }
    }

    // Clear the user image disk cache if userid is found in clear list and is within ClientCacheExpiration time.
    private bool ClearDiskImageCacheIfNecessary(int userId, int portalId, string cacheId)
    {
        var cacheKey = string.Format(CultureInfo.InvariantCulture, DataCache.UserIdListToClearDiskImageCacheKey, portalId);
        Dictionary<int, DateTime> userIds;
        if ((userIds = DataCache.GetCache<Dictionary<int, DateTime>>(cacheKey)) == null || !userIds.ContainsKey(userId))
        {
            return false;
        }

        this.ImageStore.ForcePurgeFromServerCache(cacheId);

        // The clear mechanism is performed for ClientCacheExpiration timespan so that all active clients clears the cache and don't see old data.
        if (!userIds.TryGetValue(userId, out var expiry) || DateTime.UtcNow <= expiry.Add(this.ClientCacheExpiration))
        {
            return true;
        }

        // Remove the userId from the clear list when timespan is > ClientCacheExpiration.
        userIds.Remove(userId);
        DataCache.SetCache(cacheKey, userIds);
        return true;
    }

    private Image GetImageThroughTransforms(byte[] buffer)
    {
        return this.GetImageThroughTransforms(Image.Load(buffer));
    }

    private void RenderImage(Image image, Stream outStream)
    {
        try
        {
            if (image == null)
            {
                return;
            }

            // The requested output format is honored rather than hard-coding one encoder, so a PNG
            // keeps its transparency and a GIF keeps its palette.
            image.Save(outStream, this.GetEncoder(this.ContentType));
        }
        finally
        {
            image?.Dispose();
        }
    }
}
