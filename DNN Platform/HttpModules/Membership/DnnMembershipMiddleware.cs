// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.HttpModules.Membership
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.UserRequest;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Runs DNN's membership authentication for the current request. Replaces the
    /// <c>AuthenticateRequest</c>, <c>PreRequestHandlerExecute</c> and
    /// <c>PreSendRequestHeaders</c> handlers of the legacy <c>MembershipModule</c> HTTP module.
    /// </summary>
    /// <remarks>
    /// Must be installed immediately after <c>UseAuthentication</c>/<c>UseAuthorization</c>: it
    /// needs <see cref="HttpContext.User"/>, and everything downstream (the URL rewriter, the
    /// output cache and the page itself) needs the DNN <c>UserInfo</c> it publishes in
    /// <see cref="HttpContext.Items"/>.
    /// </remarks>
    public class DnnMembershipMiddleware
    {
        private readonly RequestDelegate next;

        /// <summary>Initializes a new instance of the <see cref="DnnMembershipMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public DnnMembershipMiddleware(RequestDelegate next)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>Authenticates the DNN user, then runs the rest of the pipeline.</summary>
        /// <param name="context">The current request.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var serviceProvider = context.GetScope()?.ServiceProvider ?? context.RequestServices;

            // DNN-6973: the auth-cookie domain fix-up used to run on PreSendRequestHeaders.
            context.Response.OnStarting(static state => MembershipModule.FixUpAuthCookieDomainAsync((HttpContext)state), context);

            // RequireLogout compares the auth ticket's issue date against the user's last
            // password change, and it runs inside the synchronous AuthenticateRequest, so the
            // date is resolved here — where awaiting is legal — and published for it. The
            // request feature is normally already populated by UseAuthentication; the
            // AuthenticateAsync fallback covers the case where it is not, so that an
            // indeterminate issue date never silently degrades into "no logout required".
            await MembershipModule.ResolveAuthTicketIssueDateAsync(context).ConfigureAwait(false);

            MembershipModule.AuthenticateRequest(
                serviceProvider.GetRequiredService<IHostSettingsService>(),
                serviceProvider.GetRequiredService<IPortalController>(),
                serviceProvider.GetRequiredService<IUserRequestIPAddressController>(),
                serviceProvider.GetRequiredService<IRoleController>(),
                serviceProvider.GetRequiredService<IEventLogger>(),
                serviceProvider.GetRequiredService<IHostSettings>(),
                context,
                false);

            // A forced logout already wrote a redirect: the legacy code ended the response with
            // Response.Redirect(url, endResponse: true), which here means not running the
            // remaining middleware.
            if (context.Items.ContainsKey(MembershipModule.ForcedLogoutRedirectKey))
            {
                return;
            }

            // PreRequestHandlerExecute ran after AuthenticateRequest and before the handler, so
            // the skin messages are added here, before the endpoint executes.
            MembershipModule.AddSkinInitMessages(context);

            await this.next(context).ConfigureAwait(false);
        }
    }
}
