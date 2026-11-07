// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Skins.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Services.Authentication;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Social.Messaging.Internal;
    using DotNetNuke.Services.Social.Notifications;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A skin/theme object which displays a login link to unauthenticated users and a profile link to authenticated users.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>UserAndLogin.ascx</c> + <c>UserAndLogin.ascx.cs</c> +
    /// <c>UserAndLogin.ascx.designer.cs</c>; the markup now lives in <c>UserAndLogin.razor</c>. The
    /// designer fields map onto the URLs and counter state below: <c>registerLink</c>/
    /// <c>loginLink</c> -&gt; <see cref="RegisterUrl"/>/<see cref="LoginUrl"/> plus
    /// <see cref="RegisterOnClick"/>/<see cref="LoginOnClick"/>, <c>viewProfileLink</c> and
    /// <c>viewProfileImageLink</c> -&gt; <see cref="ViewProfileUrl"/>, <c>messagesLink</c>/
    /// <c>notificationsLink</c>/<c>accountLink</c>/<c>editProfileLink</c>/<c>logoffLink</c> -&gt;
    /// the matching <c>*Url</c> members, <c>messageCount</c>/<c>notificationCount</c>/
    /// <c>messages</c> -&gt; the <c>*CountText</c>/<c>*Visible</c>/<see cref="MessagesToolTip"/>
    /// members and <c>profilePicture</c> -&gt; <see cref="ProfilePictureUrl"/>/
    /// <see cref="ProfilePictureAlternateText"/>.
    /// </para>
    /// <para>
    /// <c>HttpContext.Current.Request.RawUrl</c> is gone: the return URL is read from the request
    /// captured while the component initialized, through
    /// <see cref="HttpRequestExtensions.GetRawUrl"/>.
    /// </para>
    /// </remarks>
    public partial class UserAndLogin
    {
        private const string MyFileName = "UserAndLogin.ascx";
        private readonly INavigationManager navigationManager;
        private readonly IHostSettingsService hostSettingsService;
        private readonly IPortalController portalController;

        /// <summary>Initializes a new instance of the <see cref="UserAndLogin"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.2. Please use overload with IPortalController. Scheduled removal in v12.0.0.")]
        public UserAndLogin()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="UserAndLogin"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="portalController">The portal controller.</param>
        public UserAndLogin(INavigationManager navigationManager, IHostSettingsService hostSettingsService, IPortalController portalController)
        {
            this.navigationManager = navigationManager ?? Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
            this.hostSettingsService = hostSettingsService ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettingsService>();
            this.portalController = portalController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>();
        }

        /// <summary>Gets or sets a value indicating whether set this to true to show in custom 404/500 page.</summary>
        [Parameter]
        public bool ShowInErrorPage { get; set; }

        protected string AvatarImageUrl => UserController.Instance.GetUserProfilePictureUrl(this.PortalSettings.UserId, 32, 32);

        protected bool CanRegister
        {
            get
            {
                return (this.PortalSettings.UserRegistration != (int)Globals.PortalRegistrationType.NoRegistration)
                    && (this.PortalSettings.Users < this.PortalSettings.UserQuota || this.PortalSettings.UserQuota == 0);
            }
        }

        protected string DisplayName
        {
            get
            {
                return this.PortalSettings.UserInfo.DisplayName;
            }
        }

        protected bool IsAuthenticated
        {
            get
            {
                return this.Request.IsAuthenticated();
            }
        }

        protected string LoginUrl
        {
            get
            {
                string returnUrl = this.Request.GetRawUrl();
                if (returnUrl.IndexOf("?returnurl=", StringComparison.Ordinal) != -1)
                {
                    returnUrl = returnUrl.Substring(0, returnUrl.IndexOf("?returnurl=", StringComparison.Ordinal));
                }

                returnUrl = WebUtility.UrlEncode(returnUrl);

                return Globals.LoginURL(returnUrl, this.Request.Query["override"].Count > 0);
            }
        }

        protected string LoginUrlForClickEvent
        {
            get
            {
                if (this.UsePopUp)
                {
                    return "return " + UrlUtils.PopUpUrl(WebUtility.UrlDecode(this.LoginUrl), this.PortalSettings, true, false, 300, 650);
                }

                return string.Empty;
            }
        }

        protected bool UsePopUp
        {
            get
            {
                // SENTINEL: LoginTabId == Null.NullInteger (-1) means "no dedicated login page";
                // a real TabId of 0 must not be treated as "not set".
                return this.PortalSettings.EnablePopUps
                    && this.PortalSettings.LoginTabId == Null.NullInteger
                    && !AuthenticationController.HasSocialAuthenticationEnabled(this);
            }
        }

        protected string RegisterUrl
        {
            get
            {
                return Globals.RegisterURL(WebUtility.UrlEncode(this.navigationManager.NavigateURL()), Null.NullString);
            }
        }

        protected string RegisterUrlForClickEvent
        {
            get
            {
                if (this.UsePopUp)
                {
                    return "return " + UrlUtils.PopUpUrl(WebUtility.UrlDecode(this.RegisterUrl), this.PortalSettings, true, false, 600, 950);
                }

                return string.Empty;
            }
        }

        protected string UserProfileUrl
        {
            get
            {
                return Globals.UserProfileURL(this.PortalSettings.UserInfo.UserID);
            }
        }

        /// <summary>Gets the popup script of the register anchor, or <see langword="null"/> when there is none.</summary>
        /// <remarks>Replaces <c>registerLink.Attributes["onclick"]</c>, which was only added when <see cref="UsePopUp"/>.</remarks>
        protected string RegisterOnClick => this.UsePopUp ? this.RegisterUrlForClickEvent : null;

        /// <summary>Gets the popup script of the login anchor, or <see langword="null"/> when there is none.</summary>
        /// <remarks>Replaces <c>loginLink.Attributes["onclick"]</c>, which was only added when <see cref="UsePopUp"/>.</remarks>
        protected string LoginOnClick => this.UsePopUp ? this.LoginUrlForClickEvent : null;

        /// <summary>Gets the URL of the profile links (<c>viewProfileLink</c>/<c>viewProfileImageLink</c>).</summary>
        protected string ViewProfileUrl { get; private set; }

        /// <summary>Gets the URL of the logoff link (<c>logoffLink</c>).</summary>
        protected string LogoffUrl { get; private set; }

        /// <summary>Gets the URL of the edit profile link (<c>editProfileLink</c>).</summary>
        protected string EditProfileUrl { get; private set; }

        /// <summary>Gets the URL of the account link (<c>accountLink</c>).</summary>
        protected string AccountUrl { get; private set; }

        /// <summary>Gets the URL of the messages link (<c>messagesLink</c>).</summary>
        protected string MessagesUrl { get; private set; }

        /// <summary>Gets the URL of the notifications link (<c>notificationsLink</c>).</summary>
        protected string NotificationsUrl { get; private set; }

        /// <summary>Gets the unread message count rendered in the user menu (<c>messageCount.Text</c>).</summary>
        protected string MessageCountText { get; private set; }

        /// <summary>Gets a value indicating whether the unread message count renders (<c>messageCount.Visible</c>).</summary>
        protected bool MessageCountVisible { get; private set; }

        /// <summary>Gets the unread notification count rendered in the user menu (<c>notificationCount.Text</c>).</summary>
        protected string NotificationCountText { get; private set; }

        /// <summary>Gets a value indicating whether the unread notification count renders (<c>notificationCount.Visible</c>).</summary>
        protected bool NotificationCountVisible { get; private set; }

        /// <summary>Gets the unread message count rendered next to the avatar (<c>messages.Text</c>).</summary>
        protected string MessagesText { get; private set; }

        /// <summary>Gets the tooltip of the avatar message count (<c>messages.ToolTip</c>).</summary>
        protected string MessagesToolTip { get; private set; }

        /// <summary>Gets a value indicating whether the avatar message count renders (<c>messages.Visible</c>).</summary>
        protected bool MessagesVisible { get; private set; }

        /// <summary>Gets the avatar URL (<c>profilePicture.ImageUrl</c>).</summary>
        protected string ProfilePictureUrl { get; private set; }

        /// <summary>Gets the avatar alternate text (<c>profilePicture.AlternateText</c>).</summary>
        protected string ProfilePictureAlternateText { get; private set; }

        protected string LocalizeString(string key)
        {
            return Localization.GetString(key, Localization.GetResourceFile(this, MyFileName));
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.Visible = !this.PortalSettings.InErrorPageRequest() || this.ShowInErrorPage;
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // SENTINEL: UserId > 0 is the original "there is a signed in user" test; kept verbatim.
            if (this.PortalSettings.UserId > 0)
            {
                this.ViewProfileUrl = Globals.UserProfileURL(this.PortalSettings.UserId);
                this.LogoffUrl = this.navigationManager.NavigateURL(this.PortalSettings.ActiveTab.TabID, "Logoff");
                this.EditProfileUrl = this.navigationManager.NavigateURL(this.PortalSettings.UserTabId, "Profile", "userId=" + this.PortalSettings.UserId, "pageno=2");
                this.AccountUrl = this.navigationManager.NavigateURL(this.PortalSettings.UserTabId, "Profile", "userId=" + this.PortalSettings.UserId, "pageno=1");
                this.MessagesUrl = this.navigationManager.NavigateURL(this.GetMessageTab(), string.Empty, string.Format("userId={0}", this.PortalSettings.UserId));
                this.NotificationsUrl = this.navigationManager.NavigateURL(this.GetMessageTab(), string.Empty, string.Format("userId={0}", this.PortalSettings.UserId), "view=notifications", "action=notifications");

                var unreadMessages = InternalMessagingController.Instance.CountUnreadMessages(this.PortalSettings.UserId, this.PortalSettings.PortalId);
                var unreadAlerts = NotificationsController.Instance.CountNotifications(this.PortalSettings.UserId, this.PortalSettings.PortalId);

                if (unreadMessages > 0)
                {
                    this.MessageCountText = unreadMessages.ToString(CultureInfo.InvariantCulture);
                    this.MessageCountVisible = true;

                    this.MessagesText = unreadMessages.ToString(CultureInfo.InvariantCulture);
                    this.MessagesToolTip = unreadMessages == 1
                                        ? this.LocalizeString("OneMessage")
                                        : string.Format(this.LocalizeString("MessageCount"), unreadMessages);
                    this.MessagesVisible = true;
                }

                if (unreadAlerts > 0)
                {
                    this.NotificationCountText = unreadAlerts.ToString(CultureInfo.InvariantCulture);
                    this.NotificationCountVisible = true;
                }

                this.ProfilePictureUrl = this.AvatarImageUrl;
                this.ProfilePictureAlternateText = Localization.GetString("ProfilePicture", Localization.GetResourceFile(this, MyFileName));

                if (this.AlwaysShowCount())
                {
                    this.MessageCountVisible = this.NotificationCountVisible = true;
                }
            }
        }

        private int GetMessageTab()
        {
            var cacheKey = string.Format("MessageCenterTab:{0}:{1}", this.PortalSettings.PortalId, this.PortalSettings.CultureCode);
            var messageTabId = DataCache.GetCache<int>(cacheKey);
            if (messageTabId > 0)
            {
                return messageTabId;
            }

            // Find the Message Tab
            messageTabId = this.FindMessageTab();

            // save in cache
            // NOTE - This cache is not being cleared. There is no easy way to clear this, except Tools->Clear Cache
            DataCache.SetCache(cacheKey, messageTabId, TimeSpan.FromMinutes(20));

            return messageTabId;
        }

        private int FindMessageTab()
        {
            // On brand new install the new Message Center Module is on the child page of User Profile Page
            // On Upgrade to 6.2.0, the Message Center module is on the User Profile Page
            var profileTab = TabController.Instance.GetTab(this.PortalSettings.UserTabId, this.PortalSettings.PortalId, false);
            if (profileTab != null)
            {
                var childTabs = TabController.Instance.GetTabsByPortal(profileTab.PortalID).DescendentsOf(profileTab.TabID);
                foreach (TabInfo tab in childTabs)
                {
                    foreach (KeyValuePair<int, ModuleInfo> kvp in ModuleController.Instance.GetTabModules(tab.TabID))
                    {
                        var module = kvp.Value;
                        if (module.DesktopModule.FriendlyName == "Message Center" && !module.IsDeleted)
                        {
                            return tab.TabID;
                        }
                    }
                }
            }

            // default to User Profile Page
            return this.PortalSettings.UserTabId;
        }

        private bool AlwaysShowCount()
        {
            const string SettingKey = "UserAndLogin_AlwaysShowCount";

            var portalSetting = PortalController.GetPortalSetting(this.portalController, SettingKey, this.PortalSettings.PortalId, string.Empty);
            if (!string.IsNullOrEmpty(portalSetting) && bool.TryParse(portalSetting, out var alwaysShowCount))
            {
                return alwaysShowCount;
            }

            var hostSetting = this.hostSettingsService.GetString(SettingKey, string.Empty);
            if (!string.IsNullOrEmpty(hostSetting) && bool.TryParse(hostSetting, out alwaysShowCount))
            {
                return alwaysShowCount;
            }

            return false;
        }
    }
}
