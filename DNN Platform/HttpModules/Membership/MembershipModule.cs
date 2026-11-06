// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.HttpModules.Membership
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Abstractions.Pages;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.HttpModules.Services;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Authentication;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.UserRequest;
    using DotNetNuke.UI.Skins;
    using DotNetNuke.UI.Skins.Controls;
    using DotNetNuke.UI.Skins.EventListeners;

    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    /// <summary>Information about membership.</summary>
    /// <remarks>
    /// The <c>IHttpModule</c> implementation that used to hook <c>AuthenticateRequest</c>,
    /// <c>PreRequestHandlerExecute</c> and <c>PreSendRequestHeaders</c> is now
    /// <see cref="DnnMembershipMiddleware"/>; the authentication logic itself still lives in the
    /// <see cref="AuthenticateRequest(IHostSettingsService,IPortalController,IUserRequestIPAddressController,IRoleController,IEventLogger,IHostSettings,HttpContext,bool)"/>
    /// overloads so that external callers keep working.
    /// </remarks>
    public partial class MembershipModule
    {
        /// <summary>
        /// The <see cref="HttpContext.Items"/> key set when <c>AuthenticateRequest</c> had to
        /// force a logout and already wrote a redirect to the response. The middleware uses it
        /// instead of the legacy <c>Response.Redirect(url, endResponse: true)</c>
        /// thread-abort to stop the request from reaching an endpoint.
        /// </summary>
        internal const string ForcedLogoutRedirectKey = "DNN_MembershipForcedLogout";

        /// <summary>
        /// The <see cref="HttpContext.Items"/> slot holding the authentication ticket's issue date
        /// (local time), as resolved by <see cref="ResolveAuthTicketIssueDateAsync"/>.
        /// </summary>
        internal const string AuthTicketIssueDateKey = "DNN_AuthTicketIssueDate";

        private static readonly ILogger Logger = DnnLoggingController.GetLogger<MembershipModule>();

        /// <summary>Initializes a new instance of the <see cref="MembershipModule"/> class.</summary>
        public MembershipModule()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MembershipModule"/> class.</summary>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="ipAddressController">The IP address controller.</param>
        /// <param name="roleController">The role controller.</param>
        /// <param name="userController">The user controller.</param>
        /// <param name="eventLogger">The event logger.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload without parameters. Scheduled removal in v12.0.0.")]
        public MembershipModule(IHostSettingsService hostSettingsService, IPortalController portalController, IUserRequestIPAddressController ipAddressController, IRoleController roleController, IUserController userController, IEventLogger eventLogger)
            : this()
        {
        }

        /// <summary>Gets the name of the module.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string ModuleName => "DNNMembershipModule";

        /// <summary>Called when unverified user skin initialize.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SkinEventArgs"/> instance containing the event data.</param>
        [Obsolete("This method has been deprecated. No equivalent. Scheduled removal in v11.0.0.")]
        public static void OnUnverifiedUserSkinInit(object sender, SkinEventArgs e)
        {
            var pageService = Globals.GetCurrentServiceProvider().GetRequiredService<IPageService>();
            var strMessage = Localization.GetString("UnverifiedUser", Localization.SharedResourceFile, Thread.CurrentThread.CurrentCulture.Name);
            pageService.AddMessage(new PageMessage(string.Empty, strMessage, PageMessageType.Warning, string.Empty, PagePriority.Site));
        }

        /// <summary>Authenticates the request.</summary>
        /// <param name="context">The context.</param>
        /// <param name="allowUnknownExtensions">if set to <see langword="true"/> to allow unknown extensions.</param>
        [DnnDeprecated(10, 0, 2, "Use overload taking IHostSettingsService")]
        public static partial void AuthenticateRequest(HttpContext context, bool allowUnknownExtensions)
            => AuthenticateRequest(
                Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettingsService>(),
                Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>(),
                Globals.GetCurrentServiceProvider().GetRequiredService<IUserRequestIPAddressController>(),
                Globals.GetCurrentServiceProvider().GetRequiredService<IRoleController>(),
                context,
                allowUnknownExtensions);

        /// <summary>Authenticates the request.</summary>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="ipAddressController">The user request IP address controller.</param>
        /// <param name="roleController">The role controller.</param>
        /// <param name="context">The context.</param>
        /// <param name="allowUnknownExtensions">if set to <c>true</c> to allow unknown extensions.</param>
        [DnnDeprecated(10, 2, 2, "Use overload taking IEventLogger")]
        public static partial void AuthenticateRequest(IHostSettingsService hostSettingsService, IPortalController portalController, IUserRequestIPAddressController ipAddressController, IRoleController roleController, HttpContext context, bool allowUnknownExtensions)
            => AuthenticateRequest(hostSettingsService, portalController, ipAddressController, roleController, Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>(), context, allowUnknownExtensions);

        /// <summary>Authenticates the request.</summary>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="ipAddressController">The user request IP address controller.</param>
        /// <param name="roleController">The role controller.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="context">The context.</param>
        /// <param name="allowUnknownExtensions">if set to <c>true</c> to allow unknown extensions.</param>
        [DnnDeprecated(10, 2, 4, "Use overload taking IHostSettings")]
        public static partial void AuthenticateRequest(IHostSettingsService hostSettingsService, IPortalController portalController, IUserRequestIPAddressController ipAddressController, IRoleController roleController, IEventLogger eventLogger, HttpContext context, bool allowUnknownExtensions)
            => AuthenticateRequest(hostSettingsService, portalController, ipAddressController, roleController, eventLogger, Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>(), context, allowUnknownExtensions);

        /// <summary>Authenticates the request.</summary>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="ipAddressController">The user request IP address controller.</param>
        /// <param name="roleController">The role controller.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="context">The context.</param>
        /// <param name="allowUnknownExtensions">if set to <c>true</c> to allow unknown extensions.</param>
        public static void AuthenticateRequest(IHostSettingsService hostSettingsService, IPortalController portalController, IUserRequestIPAddressController ipAddressController, IRoleController roleController, IEventLogger eventLogger, IHostSettings hostSettings, HttpContext context, bool allowUnknownExtensions)
        {
            ArgumentNullException.ThrowIfNull(context);

            var request = context.Request;
            var response = context.Response;

            // First check if we are upgrading/installing
            if (!Initialize.ProcessHttpModule(request, allowUnknownExtensions, false))
            {
                return;
            }

            // Obtain PortalSettings from Current Context
            var portalSettings = portalController.GetCurrentSettings();

            var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;
            if (isAuthenticated && !IsActiveDirectoryAuthHeaderPresent(context) && portalSettings != null)
            {
                var user = UserController.GetCachedUser(hostSettings, portalSettings.PortalId, context.User.Identity.Name);

                // if current login is from windows authentication, the ignore the process
                if (user == null && IsWindowsPrincipal(context))
                {
                    return;
                }

                // authenticate user and set last login ( this is necessary for users who have a permanent Auth cookie set )
                if (RequireLogout(hostSettingsService, context, user))
                {
                    var portalSecurity = PortalSecurity.Instance;
                    portalSecurity.SignOut();

                    // Remove user from cache
                    if (user != null)
                    {
                        DataCache.ClearUserCache(portalSettings.PortalId, context.User.Identity.Name);
                    }

                    // Redirect browser back to home page
                    response.Redirect(GetRawUrl(request));
                    context.Items[ForcedLogoutRedirectKey] = true;
                    return;
                }

                // if users LastActivityDate is outside the UsersOnlineTimeWindow then record user activity
#pragma warning disable CS0618 // Type or member is obsolete
                var usersOnlineTimeWindow = Host.UsersOnlineTimeWindow;
#pragma warning restore CS0618 // Type or member is obsolete
                if (user != null && DateTime.Compare(user.Membership.LastActivityDate.AddMinutes(usersOnlineTimeWindow), DateTime.Now) < 0)
                {
                    // update LastActivityDate and IP Address for user
                    user.Membership.LastActivityDate = DateTime.Now;
                    user.LastIPAddress = ipAddressController.GetUserRequestIPAddress(request);
                    UserController.UpdateUser(eventLogger, portalSettings.PortalId, user, false, false);
                }

                // check for RSVP code
                var rsvp = request.Query["rsvp"].ToString();
                if (user != null && !string.IsNullOrEmpty(rsvp))
                {
                    foreach (var role in roleController.GetRoles(portalSettings.PortalId, r => (r.SecurityMode != SecurityMode.SocialGroup || r.IsPublic) && r.Status == RoleStatus.Approved))
                    {
                        if (role.RSVPCode == rsvp)
                        {
                            roleController.UpdateUserRole(portalSettings.PortalId, user.UserID, role.RoleID, RoleStatus.Approved, false, false);
                        }
                    }
                }

                // save userinfo object in context
                context.Items["UserInfo"] = user;

                // Add user id to log context for better logging
                DotNetNuke.Common.Globals.GetCurrentServiceProvider().GetRequiredService<LogRequestContext>()?.AddToLogContext("UserId", user?.UserID ?? Null.NullInteger);

                // Localization.SetLanguage also updates the user profile, so this needs to go after the profile is loaded
                var rawUrl = GetRawUrl(request);
                if (user != null && rawUrl != null && !DnnServicesFrameworkMiddleware.ServiceApi.IsMatch(rawUrl))
                {
                    Localization.SetLanguage(user.Profile.PreferredLocale);
                }
            }

            if (!context.Items.TryGetValue("UserInfo", out var userInfoItem) || userInfoItem is null)
            {
                context.Items["UserInfo"] = new UserInfo();
                DotNetNuke.Common.Globals.GetCurrentServiceProvider().GetRequiredService<LogRequestContext>()?.AddToLogContext("UserId", Null.NullInteger);
            }
        }

        /// <summary>
        /// The <c>PreRequestHandlerExecute</c> work: shows the unverified-user warning and the
        /// e-mail-verification success message by putting them in <see cref="HttpContext.Items"/>
        /// where <see cref="Skin"/> picks them up while rendering the page.
        /// </summary>
        /// <param name="context">The current request.</param>
        internal static void AddSkinInitMessages(HttpContext context)
        {
            var request = context.Request;
            var serviceProvider = context.GetScope().ServiceProvider;
            var portalController = serviceProvider.GetRequiredService<IPortalController>();
            var userController = serviceProvider.GetRequiredService<IUserController>();

            var portalSettings = portalController.GetCurrentSettings();
            var user = userController.GetCurrentUserInfo();
            var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;
            if (!isAuthenticated || IsActiveDirectoryAuthHeaderPresent(context) || portalSettings == null)
            {
                return;
            }

            var contextItems = context.Items;
            if (user is not null && !user.IsSuperUser && user.IsInRole("Unverified Users") && !contextItems.ContainsKey(Skin.OnInitMessage))
            {
                contextItems.Add(Skin.OnInitMessage, Localization.GetString("UnverifiedUser", Localization.SharedResourceFile, Thread.CurrentThread.CurrentCulture.Name));
            }

            if (user is not null && !user.IsSuperUser && request.Query.ContainsKey("VerificationSuccess") && !contextItems.ContainsKey(Skin.OnInitMessage))
            {
                contextItems.Add(Skin.OnInitMessage, Localization.GetString("VerificationSuccess", Localization.SharedResourceFile, Thread.CurrentThread.CurrentCulture.Name));
                contextItems.Add(Skin.OnInitMessageType, ModuleMessage.ModuleMessageType.GreenSuccess);
            }
        }

        /// <summary>
        /// DNN-6973: if the authentication cookie was set by the cookie slide in membership and
        /// the current portal is in a portal group, re-issue it through
        /// <see cref="PortalSecurity.SignInAsync"/> so that it gets the portal-group cookie
        /// domain. Replaces the <c>PreSendRequestHeaders</c> handler.
        /// </summary>
        /// <param name="context">The current request.</param>
        /// <returns>A task that completes once the cookie has been re-issued (if it had to be).</returns>
        internal static async System.Threading.Tasks.Task FixUpAuthCookieDomainAsync(HttpContext context)
        {
            var serviceProvider = GetOrCreateServiceProvider(context);
            var portalController = serviceProvider.GetRequiredService<IPortalController>();
            var cookieService = serviceProvider.GetRequiredService<IAuthenticationCookieService>();
            var cookieName = cookieService.CookieName;

            var portalSettings = portalController.GetCurrentSettings();
            var setCookieHeaders = context.Response.Headers.SetCookie;
            var hasAuthCookie = setCookieHeaders.Any(value => value != null && value.Contains(cookieName, StringComparison.Ordinal));
            if (portalSettings == null || !hasAuthCookie || context.Items.ContainsKey(DnnAuthenticationDefaults.UserSignInContextKey))
            {
                return;
            }

            var isInPortalGroup = PortalController.IsMemberOfPortalGroup(portalController, portalSettings.PortalId);
            if (!isInPortalGroup)
            {
                return;
            }

            // The legacy code read the cookie back out of Response.Cookies; on ASP.NET Core the
            // response cookies are only visible as Set-Cookie headers, so the "has a value but no
            // domain" test is performed against the header itself.
            var kept = new List<string>();
            var needsReissue = false;
            foreach (var header in setCookieHeaders)
            {
                if (header is null)
                {
                    continue;
                }

                if (IsValuedAuthCookieWithoutDomain(header, cookieName))
                {
                    needsReissue = true;
                    continue;
                }

                kept.Add(header);
            }

            if (!needsReissue)
            {
                return;
            }

            // Equivalent of Response.Cookies.Remove(FormsAuthentication.FormsCookieName): drop the
            // domain-less cookie instead of expiring it, then sign in again so a correct one is written.
            context.Response.Headers.SetCookie = new StringValues(kept.ToArray());
            await PortalSecurity.Instance.SignInAsync(UserController.Instance.GetCurrentUserInfo(), false).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves the current authentication ticket's issue date and publishes it on
        /// <see cref="HttpContext.Items"/> for <c>RequireLogout</c> to read.
        /// </summary>
        /// <param name="context">The current request.</param>
        /// <returns>A task that completes once the issue date has been resolved.</returns>
        /// <remarks>
        /// This replaces the legacy <c>FormsIdentity.Ticket.IssueDate</c>. It is async because
        /// <see cref="AuthenticationHttpContextExtensions.AuthenticateAsync(HttpContext, string)"/>
        /// is the fallback when <see cref="IAuthenticateResultFeature"/> has not been populated;
        /// <c>RequireLogout</c> itself runs inside the synchronous <c>AuthenticateRequest</c> and
        /// so cannot await, and blocking on the call there would risk a thread-pool deadlock.
        /// </remarks>
        internal static async Task ResolveAuthTicketIssueDateAsync(HttpContext context)
        {
            if (context is null || context.User?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            try
            {
                var issuedUtc = context.Features.Get<IAuthenticateResultFeature>()?.AuthenticateResult?.Properties?.IssuedUtc;

                if (issuedUtc is null)
                {
                    var result = await context.AuthenticateAsync(DnnAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
                    issuedUtc = result?.Properties?.IssuedUtc;
                }

                if (issuedUtc is not null)
                {
                    context.Items[AuthTicketIssueDateKey] = issuedUtc.Value.LocalDateTime;
                }
            }
            catch (Exception ex)
            {
                // Leaving the item unset makes RequireLogout fail closed, which is the safe
                // outcome; the failure itself is still logged.
                Logger.MembershipModuleRequireLogoutException(ex);
            }
        }

        private static bool IsValuedAuthCookieWithoutDomain(string setCookieHeader, string cookieName)
        {
            var separatorIndex = setCookieHeader.IndexOf('=');
            if (separatorIndex <= 0)
            {
                return false;
            }

            var name = setCookieHeader.Substring(0, separatorIndex).Trim();
            if (!string.Equals(name, cookieName, StringComparison.Ordinal))
            {
                return false;
            }

            var attributes = setCookieHeader.Substring(separatorIndex + 1).Split(';');
            var value = attributes[0];
            var hasDomain = attributes.Skip(1).Any(attribute => attribute.TrimStart().StartsWith("domain=", StringComparison.OrdinalIgnoreCase));
            return !string.IsNullOrEmpty(value) && !hasDomain;
        }

        private static bool IsWindowsPrincipal(HttpContext context)
        {
            // Windows (Negotiate/NTLM) authentication is handled by the server, not by DNN
            // membership. Checked through the authentication type rather than a
            // WindowsPrincipal type test so this does not become a Windows-only code path.
            var authenticationType = context.User?.Identity?.AuthenticationType;
            return string.Equals(authenticationType, "Negotiate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(authenticationType, "NTLM", StringComparison.OrdinalIgnoreCase)
                || string.Equals(authenticationType, "Windows", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRawUrl(HttpRequest request)
        {
            // Replacement for HttpRequest.RawUrl, which does not exist on ASP.NET Core.
            return $"{request.PathBase}{request.Path}{request.QueryString}";
        }

        private static bool IsActiveDirectoryAuthHeaderPresent(HttpContext context)
        {
            var auth = context?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(auth))
            {
                if (auth.StartsWith("Negotiate", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RequireLogout(IHostSettingsService hostSettingsService, HttpContext context, UserInfo user)
        {
            try
            {
                if (user == null || user.IsDeleted || user.Membership.LockedOut
                    || (!user.Membership.Approved && !user.IsInRole("Unverified Users"))
                    || !user.Username.Equals(context.User.Identity.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var forceLogout = hostSettingsService.GetBoolean("ForceLogoutAfterPasswordChanged");
                if (!forceLogout)
                {
                    return false;
                }

                // if user's password changed after the user cookie created, then force user to login again.
                // The forms-authentication ticket issue date is now the cookie authentication
                // ticket's IssuedUtc, resolved by ResolveAuthTicketIssueDateAsync (which can
                // await) and published on HttpContext.Items.
                var issueDate = context.Items.TryGetValue(AuthTicketIssueDateKey, out var resolved)
                    ? resolved as DateTime?
                    : null;

                if (issueDate is null)
                {
                    // The ticket issue date could not be determined for an authenticated user.
                    // Fail CLOSED: returning false here would silently keep alive exactly the
                    // sessions this host setting exists to terminate.
                    Logger.MembershipModuleRequireLogoutException(new InvalidOperationException(
                        "ForceLogoutAfterPasswordChanged is enabled but the authentication ticket issue date could not be determined; forcing a logout."));
                    return true;
                }

                return issueDate < user.Membership.LastPasswordChangeDate;
            }
            catch (Exception ex)
            {
                Logger.MembershipModuleRequireLogoutException(ex);
                return true;
            }
        }

        private static IServiceProvider GetOrCreateServiceProvider(HttpContext context)
        {
            // NOTE: Using HttpContextDependencyInjectionExtensions.GetScope can cause an infinite loop/stack overflow, presumably due to timing issues
            var scope = context.Items[typeof(IServiceScope)] as IServiceScope ?? Globals.DependencyProvider.CreateScope();
            return scope.ServiceProvider;
        }
    }
}
