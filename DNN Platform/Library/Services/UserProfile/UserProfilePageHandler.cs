// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.UserProfile
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Instrumentation;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Redirects to the user's profile page. This is the ASP.NET Core replacement for the
    /// <c>UserProfilePageHandler</c> <c>IHttpHandler</c> which was registered in
    /// <c>web.config</c> for <c>path="User.aspx" verb="*"</c>.
    /// </summary>
    public static class UserProfilePageHandler
    {
        /// <summary>The preserved user profile URL, so existing links keep working.</summary>
        public const string UserProfilePath = "/User.aspx";

        /// <summary>
        /// The preserved user profile URL when it appears below a portal/tab folder, e.g.
        /// <c>/Home/User.aspx?UserId=1</c>. The IIS handler mapping matched the file name at any
        /// folder depth, so both shapes are routed.
        /// </summary>
        public const string NestedUserProfilePath = "/{**dnnUserProfilePrefix}/User.aspx";

        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(UserProfilePageHandler));

        /// <summary>Maps the user profile redirect endpoint onto its original URL(s).</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        public static IEndpointRouteBuilder MapDnnUserProfilePage(this IEndpointRouteBuilder endpoints)
        {
            // Map (rather than MapGet): the handler was registered with verb="*".
            endpoints.Map(UserProfilePath, HandleAsync);
            endpoints.Map(NestedUserProfilePath, HandleAsync);
            return endpoints;
        }

        /// <summary>Adapts the synchronous handler to a <see cref="RequestDelegate"/>.</summary>
        /// <param name="context">The HTTP context of the request.</param>
        /// <returns>A completed task; the work is synchronous (a database lookup and a redirect).</returns>
        private static Task HandleAsync(HttpContext context)
        {
            ProcessRequest(context);
            return Task.CompletedTask;
        }

        /// <summary>Resolves the requested user and redirects to that user's profile page.</summary>
        /// <param name="context">The HTTP context of the request.</param>
        public static void ProcessRequest(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var portalSettings = PortalController.Instance.GetCurrentSettings();
            int userId = Null.NullInteger;
            int portalId = portalSettings.PortalId;
            var rawUrl = context.Request.GetRawUrl();

            try
            {
                // try UserId
                var rawUserId = context.Request.Query["UserId"].ToString();
                if (!string.IsNullOrEmpty(rawUserId))
                {
                    userId = int.Parse(rawUserId, CultureInfo.InvariantCulture);
                    if (UserController.GetUserById(portalId, userId) == null)
                    {
                        // The user cannot be found (potential DOS)
                        Exceptions.Exceptions.ProcessHttpException(rawUrl);
                    }
                }

                if (userId == Null.NullInteger)
                {
                    // try userName
                    var userName = context.Request.Query["UserName"].ToString();
                    if (!string.IsNullOrEmpty(userName))
                    {
                        userId = GetUserId(userName, portalId);
                    }
                }

                if (userId == Null.NullInteger)
                {
                    // try user
                    string user = context.Request.Query["User"].ToString();
                    if (!string.IsNullOrEmpty(user))
                    {
                        if (!int.TryParse(user, out userId))
                        {
                            // User is not an integer, so try it as a name
                            userId = GetUserId(user, portalId);
                        }
                        else
                        {
                            if (UserController.GetUserById(portalId, userId) == null)
                            {
                                // The user cannot be found (potential DOS)
                                Exceptions.Exceptions.ProcessHttpException(rawUrl);
                            }
                        }
                    }
                }

                if (userId == Null.NullInteger)
                {
                    // The user cannot be found (potential DOS)
                    Exceptions.Exceptions.ProcessHttpException(rawUrl);
                }
            }
            catch (Exception exc)
            {
                Logger.UserProfilePageHandlerException(exc);

                // The user cannot be found (potential DOS)
                Exceptions.Exceptions.ProcessHttpException(rawUrl);
            }

            // Redirect to Userprofile Page
            context.Response.Redirect(Globals.UserProfileURL(userId));
        }

        private static int GetUserId(string username, int portalId)
        {
            int userId = Null.NullInteger;
            UserInfo userInfo = UserController.GetUserByName(portalId, username);
            if (userInfo != null)
            {
                userId = userInfo.UserID;
            }
            else
            {
                // The user cannot be found (potential DOS)
                Exceptions.Exceptions.ProcessHttpException();
            }

            return userId;
        }
    }
}
