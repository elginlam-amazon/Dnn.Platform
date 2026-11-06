// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.HttpModules.UsersOnline
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Internal.SourceGenerators;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Middleware which keeps track of the users actively on the site. Replaces the
    /// <c>AuthorizeRequest</c> handler of the legacy <c>UsersOnlineModule</c> HTTP module.
    /// </summary>
    [DnnDeprecated(8, 0, 0, "Other solutions exist outside of the DNN Platform", RemovalVersion = 11)]
    public partial class DnnUsersOnlineMiddleware
    {
        private readonly RequestDelegate next;

        /// <summary>Initializes a new instance of the <see cref="DnnUsersOnlineMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public DnnUsersOnlineMiddleware(RequestDelegate next)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>Tracks the current user, then runs the rest of the pipeline.</summary>
        /// <param name="context">The current request.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            TrackUsers(context.Request);

            return this.next(context);
        }

        private static void TrackUsers(HttpRequest request)
        {
            // check if we are upgrading/installing or if this is a captcha request
            if (!Initialize.ProcessHttpModule(request, false, false))
            {
                return;
            }

            // Create a Users Online Controller
            var objUserOnlineController = new UserOnlineController();

            // Is Users Online Enabled?
            if (objUserOnlineController.IsEnabled())
            {
                objUserOnlineController.TrackUsers();
            }
        }
    }
}
