// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.UserProfile
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.Localization.Internal;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    /// <summary>
    /// Serves a user's profile picture, resized to the requested height. This is the ASP.NET Core
    /// replacement for the <c>UserProfilePicHandler</c> <c>IHttpHandler</c> which was registered
    /// in <c>web.config</c> for <c>path="ProfilePic.ashx" verb="*"</c>.
    /// </summary>
    public static class UserProfilePicHandler
    {
        /// <summary>The preserved profile picture URL, so existing links keep working.</summary>
        public const string ProfilePicPath = "/ProfilePic.ashx";

        /// <summary>
        /// The preserved profile picture URL when it appears below a portal/tab folder. The IIS
        /// handler mapping matched the file name at any folder depth, so both shapes are routed.
        /// </summary>
        public const string NestedProfilePicPath = "/{**dnnProfilePicPrefix}/ProfilePic.ashx";

        private static readonly object ResizeLocker = new object();

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".JPG",
            ".JPE",
            ".BMP",
            ".GIF",
            ".PNG",
            ".JPEG",
            ".ICO",
        };

        /// <summary>Maps the profile picture endpoint onto its original URL(s).</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        public static IEndpointRouteBuilder MapDnnUserProfilePic(this IEndpointRouteBuilder endpoints)
        {
            // Map (rather than MapGet): the handler was registered with verb="*".
            endpoints.Map(ProfilePicPath, ProcessRequestAsync);
            endpoints.Map(NestedProfilePicPath, ProcessRequestAsync);
            return endpoints;
        }

        /// <summary>Handles a request for a user's profile picture.</summary>
        /// <param name="context">The HTTP context of the request.</param>
        /// <returns>A task which completes once the image has been written to the response.</returns>
        public static async Task ProcessRequestAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            SetupCulture();

            var userId = -1;
            var height = 55;
            try
            {
                var rawUserId = context.Request.Query["userid"].ToString();
                if (!string.IsNullOrEmpty(rawUserId))
                {
                    userId = Convert.ToInt32(rawUserId, CultureInfo.InvariantCulture);
                }

                var rawHeight = context.Request.Query["h"].ToString();
                if (!string.IsNullOrEmpty(rawHeight))
                {
                    height = Convert.ToInt32(rawHeight, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                // The userid or height are incorrectly formatted (potential DOS): raise the 404.
                Exceptions.Exceptions.ProcessHttpException(context.Request.GetRawUrl());
            }

            if (height > 128)
            {
                height = 128;
            }

            CalculateSize(ref height, out var width, out var size);

            var settings = PortalController.Instance.GetCurrentSettings();
            var user = UserController.Instance.GetUser(settings.PortalId, userId);

            var photoLoaded = false;
            if (user != null && TryGetPhotoFile(user, out var photoFile))
            {
                if (!IsImageExtension(photoFile.Extension))
                {
                    // The old handler called Response.End() here, ending the request with an
                    // empty body. There is no Response.End in ASP.NET Core; returning does the
                    // same thing.
                    SetCachePolicy(context.Response);
                    return;
                }

                var folder = FolderManager.Instance.GetFolder(photoFile.FolderId);
                var extension = $".{photoFile.Extension}";
                var sizedPhoto = photoFile.FileName.Replace(extension, $"_{size}{extension}");
                if (!FileManager.Instance.FileExists(folder, sizedPhoto))
                {
                    lock (ResizeLocker)
                    {
                        if (!FileManager.Instance.FileExists(folder, sizedPhoto))
                        {
                            using var fileContent = FileManager.Instance.GetFileContent(photoFile);
                            using var sizedContent = ImageUtils.CreateImage(fileContent, height, width, extension);

                            const bool operationDoesNotRequirePermissionsCheck = true;
                            FileManager.Instance.AddFile(folder, sizedPhoto, sizedContent, false, !operationDoesNotRequirePermissionsCheck, FileContentTypeManager.Instance.GetContentType(Path.GetExtension(sizedPhoto)));
                        }
                    }
                }

                var contentType = photoFile.Extension.ToLowerInvariant() switch
                {
                    "png" => "image/png",
                    "jpeg" or "jpg" => "image/jpeg",
                    "gif" => "image/gif",
                    _ => context.Response.ContentType,
                };
                context.Response.ContentType = contentType;

                SetCachePolicy(context.Response);

                using (var content = FileManager.Instance.GetFileContent(FileManager.Instance.GetFile(folder, sizedPhoto)))
                {
                    await content.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
                }

                photoLoaded = true;
            }

            if (!photoLoaded)
            {
                context.Response.ContentType = "image/gif";
                SetCachePolicy(context.Response);

                var noAvatarPath = HttpContextSource.MapPath("~/images/no_avatar.gif");
                await context.Response.SendFileAsync(noAvatarPath, context.RequestAborted).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Writes the cache headers the old handler set via <c>Response.Cache</c>
        /// (public cacheability, a one minute expiry/max-age, and Last-Modified).
        /// </summary>
        /// <param name="response">The response to write the headers to.</param>
        private static void SetCachePolicy(HttpResponse response)
        {
            if (response.HasStarted)
            {
                return;
            }

            response.Headers.CacheControl = "public, max-age=60";
            response.Headers.Expires = DateTime.Now.AddMinutes(1).ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
            response.Headers.LastModified = DateTime.Now.ToString("r", CultureInfo.InvariantCulture);
        }

        // whether current user has permission to view target user's photo.
        private static bool TryGetPhotoFile(UserInfo targetUser, out IFileInfo photoFile)
        {
            photoFile = null;

            UserInfo user = UserController.Instance.GetCurrentUserInfo();
            var settings = PortalController.Instance.GetCurrentSettings();
            var photoProperty = targetUser.Profile.GetProperty("Photo");
            if (photoProperty == null)
            {
                return false;
            }

            var isVisible = ProfilePropertyAccess.CheckAccessLevel(settings, photoProperty, user, targetUser);
            if (!string.IsNullOrEmpty(photoProperty.PropertyValue) && isVisible)
            {
                photoFile = FileManager.Instance.GetFile(int.Parse(photoProperty.PropertyValue, CultureInfo.InvariantCulture));
                if (photoFile == null)
                {
                    isVisible = false;
                }
            }
            else
            {
                isVisible = false;
            }

            return isVisible;
        }

        private static void CalculateSize(ref int height, out int width, out string size)
        {
            switch (height)
            {
                case > 0 and <= 32:
                    height = 32;
                    width = 32;
                    size = "xs";
                    break;
                case > 32 and <= 50:
                    height = 50;
                    width = 50;
                    size = "s";
                    break;
                case > 50 and <= 64:
                    height = 64;
                    width = 64;
                    size = "l";
                    break;
                case > 64 and <= 128:
                    height = 128;
                    width = 128;
                    size = "xl";
                    break;
                default:
                    height = 32;
                    width = 32;
                    size = "xs";
                    break;
            }
        }

        private static bool IsImageExtension(string extension)
        {
            if (!extension.StartsWith(".", StringComparison.Ordinal))
            {
                extension = $".{extension}";
            }

            return ImageExtensions.Contains(extension);
        }

        private static void SetupCulture()
        {
            PortalSettings settings = PortalSettings.Current;
            if (settings is null)
            {
                return;
            }

            CultureInfo pageLocale = TestableLocalization.Instance.GetPageLocale(settings);
            if (pageLocale is not null)
            {
                TestableLocalization.Instance.SetThreadCultures(pageLocale, settings);
            }
        }
    }
}
