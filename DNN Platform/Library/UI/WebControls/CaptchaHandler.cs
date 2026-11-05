// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Formats;
    using SixLabors.ImageSharp.Formats.Png;

    /// <summary>The CaptchaHandler endpoint renders the image of a CAPTCHA Challenge.</summary>
    /// <remarks>
    /// Was an <c>IHttpHandler</c> mapped to <c>ImageChallenge.captcha.aspx</c> in <c>web.config</c>.
    /// It is now a minimal-API endpoint on the very same path, so every existing captcha image URL
    /// keeps working. The image is produced by ImageSharp rather than <c>System.Drawing</c>, and it
    /// is written with the encoder of the format it was decoded from — a supplied background image
    /// keeps its own format instead of being re-encoded as JPEG.
    /// </remarks>
    public static class CaptchaHandler
    {
        /// <summary>The path the captcha image is served from, unchanged from the WebForms handler.</summary>
        public const string RouteTemplate = "/ImageChallenge.captcha.aspx";

        /// <summary>
        /// The catch-all route that matches the <c>*.captcha.aspx</c> path the WebForms handler was
        /// registered for in <c>web.config</c>.
        /// </summary>
        /// <remarks>
        /// <see cref="CaptchaControl.RenderUrl"/> defaults to the relative
        /// <c>ImageChallenge.captcha.aspx</c>, so the request arrives under whatever directory the
        /// page lives in. This route keeps every one of those URLs working.
        /// </remarks>
        public const string WildcardRouteTemplate = "/{**captchaPath:regex(^.*\\.captcha\\.aspx$)}";

        /// <summary>Maps the captcha image endpoint.</summary>
        /// <param name="endpoints">The endpoint route builder to map onto.</param>
        /// <returns><paramref name="endpoints"/>, for chaining.</returns>
        public static IEndpointRouteBuilder MapCaptchaHandler(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            endpoints.MapGet(RouteTemplate, HandleAsync);
            endpoints.MapGet(WildcardRouteTemplate, HandleAsync);
            return endpoints;
        }

        private static async Task HandleAsync(HttpContext context)
        {
            var text = context.Request.Query[CaptchaControl.KEY].ToString();
            using (var image = CaptchaControl.GenerateImage(text))
            {
                if (image == null)
                {
                    // the ticket was missing, tampered with or expired: there is no image to serve
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                // Preserve the format the background image was decoded from; a generated background
                // has none, so it is encoded losslessly as PNG.
                var format = image.Metadata.DecodedImageFormat;
                var encoder = format == null
                    ? new PngEncoder()
                    : (IImageEncoder)image.Configuration.ImageFormatsManager.GetEncoder(format);

                context.Response.ContentType = format?.DefaultMimeType ?? PngFormat.Instance.DefaultMimeType;
                context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                await image.SaveAsync(context.Response.Body, encoder);
            }
        }
    }
}
