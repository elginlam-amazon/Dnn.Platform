// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal.Auth
{
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.HttpModules.Membership;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.UserRequest;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A web API message handler for web forms auth.</summary>
    /// <remarks>
    /// <para>
    /// This is the bridge that lets a browser session authenticated through the normal DNN sign-in be
    /// recognised on a services framework request. It is NOT a second cookie scheme: it calls the very
    /// same <see cref="MembershipModule.AuthenticateRequest(IHostSettingsService,IPortalController,IUserRequestIPAddressController,IRoleController,IEventLogger,IHostSettings,HttpContext,bool)"/>
    /// the host's own membership middleware calls, which reads the existing <c>.DOTNETNUKE</c>
    /// authentication cookie registered by <c>AddDnnPlatform</c> and establishes the DNN principal and
    /// roles from it. No <c>AddAuthentication</c>/<c>AddCookie</c> registration is introduced here.
    /// </para>
    /// <para>
    /// It was a <c>MessageProcessingHandler</c> and is still appended LAST in the handler chain by
    /// <c>ServicesRoutingManager.RegisterAuthenticationHandlers</c>, so the explicitly configured schemes
    /// (Basic/Digest/API token) still get the first chance to authenticate the request.
    /// </para>
    /// </remarks>
    public class WebFormsAuthMessageHandler : IDnnAuthMessageHandler
    {
        /// <summary>Gets the auth scheme.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string AuthScheme => "Forms";

        /// <inheritdoc />
        public bool OnAuthenticateRequest(HttpContext context)
        {
            using var scope = Globals.GetOrCreateServiceScope();
            MembershipModule.AuthenticateRequest(
                scope.ServiceProvider.GetRequiredService<IHostSettingsService>(),
                scope.ServiceProvider.GetRequiredService<IPortalController>(),
                scope.ServiceProvider.GetRequiredService<IUserRequestIPAddressController>(),
                scope.ServiceProvider.GetRequiredService<IRoleController>(),
                scope.ServiceProvider.GetRequiredService<IEventLogger>(),
                scope.ServiceProvider.GetRequiredService<IHostSettings>(),
                context,
                allowUnknownExtensions: true);

            // Never terminates inbound processing; the legacy ProcessRequest always returned the request.
            return false;
        }

        /// <inheritdoc />
        public void OnSendResponse(HttpContext context)
        {
            // left empty by design; the legacy ProcessResponse returned the response unchanged.
        }
    }
}
