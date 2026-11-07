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
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Social.Messaging.Internal;
    using DotNetNuke.Services.Social.Notifications;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A skin/theme object which displays a link to the user's profile.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>user.ascx</c> + <c>User.ascx.cs</c> + <c>User.ascx.designer.cs</c>; the markup now
    /// lives in <c>User.razor</c>. The designer fields map as follows:
    /// <c>registerLink</c> -&gt; <see cref="RegisterLinkVisible"/>/<see cref="RegisterText"/>/
    /// <see cref="RegisterNavigateUrl"/>/<see cref="RegisterToolTip"/>/<see cref="RegisterOnClick"/>,
    /// <c>enhancedRegisterLink</c> -&gt; <see cref="EnhancedRegisterLinkVisible"/>/
    /// <see cref="EnhancedRegisterText"/> (it always shared the register link's URL, tooltip and
    /// onclick), <c>messageLink</c>/<c>notificationLink</c> -&gt; the <c>Message*</c>/
    /// <c>Notification*</c> members, <c>registerGroup</c>/<c>messageGroup</c>/
    /// <c>notificationGroup</c>/<c>avatarGroup</c> -&gt; the matching <c>*Visible</c> flags and
    /// <c>avatar</c> -&gt; <see cref="AvatarImageUrl"/>/<see cref="AvatarToolTip"/>.
    /// </para>
    /// <para>
    /// <c>CssClass</c> is inherited from <see cref="DotNetNuke.UI.DnnComponentBase"/> rather than
    /// redeclared; as before it is only applied to the register anchors.
    /// </para>
    /// <para>
    /// Localization still goes through <see cref="Localization.GetString(string, string)"/> with the
    /// control's <c>App_LocalResources</c> file, because skin object text is routinely customised
    /// per portal through the Language Editor and an <c>IStringLocalizer</c> would only read the
    /// shipped satellite assemblies.
    /// </para>
    /// </remarks>
    public partial class User
    {
        private const string MyFileName = "User.ascx";
        private readonly INavigationManager navigationManager;
        private readonly IPortalController portalController;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IPortalGroupController portalGroupController;

        /// <summary>Initializes a new instance of the <see cref="User"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.2. Please use overload with IPortalController. Scheduled removal in v12.0.0.")]
        public User()
            : this(null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="User"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="portalGroupController">The portal group controller.</param>
        public User(INavigationManager navigationManager, IPortalController portalController, IApplicationStatusInfo appStatus, IPortalGroupController portalGroupController)
        {
            this.navigationManager = navigationManager ?? Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
            this.portalController = portalController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>();
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.portalGroupController = portalGroupController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalGroupController>();
            this.ShowUnreadMessages = true;
            this.ShowAvatar = true;
            this.LegacyMode = true;
        }

        /// <summary>Gets or sets a value indicating whether the unread message/notification counters are shown.</summary>
        [Parameter]
        public bool ShowUnreadMessages { get; set; }

        /// <summary>Gets or sets a value indicating whether the profile avatar is shown.</summary>
        [Parameter]
        public bool ShowAvatar { get; set; }

        /// <summary>Gets or sets a value indicating whether set this to false in the skin to take advantage of the enhanced markup.</summary>
        [Parameter]
        public bool LegacyMode { get; set; }

        /// <summary>Gets or sets the markup of the register link, overriding the localized text.</summary>
        [Parameter]
        public string Text { get; set; }

        /// <summary>Gets or sets the URL the register link points at, overriding the default registration URL.</summary>
        [Parameter]
        public string URL { get; set; }

        /// <summary>Gets or sets a value indicating whether set this to true to show in custom 404/500 page.</summary>
        [Parameter]
        public bool ShowInErrorPage { get; set; }

        /// <summary>Gets a value indicating whether the legacy register anchor renders (<c>registerLink.Visible</c>).</summary>
        protected bool RegisterLinkVisible { get; private set; } = true;

        /// <summary>Gets a value indicating whether the enhanced markup renders (<c>registerGroup.Visible</c>).</summary>
        protected bool RegisterGroupVisible { get; private set; } = true;

        /// <summary>Gets a value indicating whether the enhanced register anchor renders (<c>enhancedRegisterLink.Visible</c>).</summary>
        protected bool EnhancedRegisterLinkVisible { get; private set; } = true;

        /// <summary>Gets a value indicating whether the unread messages item renders (<c>messageGroup.Visible</c>).</summary>
        protected bool MessageGroupVisible { get; private set; } = true;

        /// <summary>Gets a value indicating whether the notifications item renders (<c>notificationGroup.Visible</c>).</summary>
        protected bool NotificationGroupVisible { get; private set; } = true;

        /// <summary>Gets a value indicating whether the avatar item renders (<c>avatarGroup.Visible</c>).</summary>
        protected bool AvatarGroupVisible { get; private set; } = true;

        /// <summary>Gets the text of the legacy register anchor (<c>registerLink.Text</c>).</summary>
        protected string RegisterText { get; private set; }

        /// <summary>Gets the text of the enhanced register anchor (<c>enhancedRegisterLink.Text</c>).</summary>
        protected string EnhancedRegisterText { get; private set; }

        /// <summary>Gets the URL of the register/profile anchors (<c>registerLink.NavigateUrl</c>).</summary>
        protected string RegisterNavigateUrl { get; private set; }

        /// <summary>Gets the tooltip of the register/profile anchors (<c>registerLink.ToolTip</c>).</summary>
        protected string RegisterToolTip { get; private set; }

        /// <summary>Gets the popup script of the register anchors (was <c>registerLink.Attributes["onclick"]</c>).</summary>
        protected string RegisterOnClick { get; private set; }

        /// <summary>Gets the text of the messages anchor (<c>messageLink.Text</c>).</summary>
        protected string MessageText { get; private set; }

        /// <summary>Gets the URL of the messages anchor (<c>messageLink.NavigateUrl</c>).</summary>
        protected string MessageNavigateUrl { get; private set; }

        /// <summary>Gets the tooltip of the messages anchor (<c>messageLink.ToolTip</c>).</summary>
        protected string MessageToolTip { get; private set; }

        /// <summary>Gets the text of the notifications anchor (<c>notificationLink.Text</c>).</summary>
        protected string NotificationText { get; private set; }

        /// <summary>Gets the URL of the notifications anchor (<c>notificationLink.NavigateUrl</c>).</summary>
        protected string NotificationNavigateUrl { get; private set; }

        /// <summary>Gets the tooltip of the notifications anchor (<c>notificationLink.ToolTip</c>).</summary>
        protected string NotificationToolTip { get; private set; }

        /// <summary>Gets the avatar image URL (<c>avatar.ImageUrl</c>).</summary>
        protected string AvatarImageUrl { get; private set; }

        /// <summary>Gets the avatar tooltip and alternate text (<c>avatar.ToolTip</c>/<c>avatar.Text</c>).</summary>
        protected string AvatarToolTip { get; private set; }

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

            try
            {
                if (this.LegacyMode)
                {
                    this.RegisterGroupVisible = false;
                }
                else
                {
                    this.RegisterLinkVisible = false;
                }

                if (this.Request.IsAuthenticated() == false)
                {
                    this.MessageGroupVisible = false;
                    this.NotificationGroupVisible = false;
                    this.AvatarGroupVisible = false;

                    if (this.PortalSettings.UserRegistration != (int)Globals.PortalRegistrationType.NoRegistration)
                    {
                        if (!string.IsNullOrEmpty(this.Text))
                        {
                            var text = this.Text;
                            if (text.IndexOf("src=", StringComparison.Ordinal) != -1)
                            {
                                text = text.Replace("src=\"", "src=\"" + this.PortalSettings.ActiveTab.SkinPath);
                            }

                            this.RegisterText = text;
                            this.EnhancedRegisterText = text;
                        }
                        else
                        {
                            this.RegisterText = Localization.GetString("Register", Localization.GetResourceFile(this, MyFileName));
                            this.EnhancedRegisterText = this.RegisterText;
                            this.RegisterToolTip = this.RegisterText;
                        }

                        if (this.PortalSettings.Users < this.PortalSettings.UserQuota || this.PortalSettings.UserQuota == 0)
                        {
                            if (this.LegacyMode)
                            {
                                this.RegisterLinkVisible = true;
                            }
                            else
                            {
                                this.EnhancedRegisterLinkVisible = true;
                            }
                        }
                        else
                        {
                            this.RegisterGroupVisible = false;
                            this.RegisterLinkVisible = false;
                        }

                        this.RegisterNavigateUrl = !string.IsNullOrEmpty(this.URL)
                                            ? this.URL
                                            : Globals.RegisterURL(WebUtility.UrlEncode(this.navigationManager.NavigateURL()), Null.NullString);

                        // AUTHORIZATION/behaviour guard preserved: the popup is only used when the
                        // portal enables popups, there is no dedicated registration page
                        // (SENTINEL: RegisterTabId == Null.NullInteger, NOT a <= 0 check) and no
                        // social authentication provider is enabled.
                        if (this.PortalSettings.EnablePopUps && this.PortalSettings.RegisterTabId == Null.NullInteger
                            && !AuthenticationController.HasSocialAuthenticationEnabled(this))
                        {
                            this.RegisterOnClick = "return " + UrlUtils.PopUpUrl(this.RegisterNavigateUrl, this.PortalSettings, true, false, 600, 950);
                        }
                    }
                    else
                    {
                        this.RegisterGroupVisible = false;
                        this.RegisterLinkVisible = false;
                    }
                }
                else
                {
                    var userInfo = UserController.Instance.GetCurrentUserInfo();

                    // SENTINEL: -1 (Null.NullInteger) means "no user"; kept as an equality check.
                    if (userInfo.UserID != -1)
                    {
                        this.RegisterText = userInfo.DisplayName;
                        this.RegisterNavigateUrl = Globals.UserProfileURL(userInfo.UserID);
                        this.RegisterToolTip = Localization.GetString("VisitMyProfile", Localization.GetResourceFile(this, MyFileName));

                        this.EnhancedRegisterText = this.RegisterText;

                        if (this.ShowUnreadMessages)
                        {
                            var unreadMessages = InternalMessagingController.Instance.CountUnreadMessages(userInfo.UserID, PortalController.GetEffectivePortalId(this.portalController, this.appStatus, this.portalGroupController, userInfo.PortalID));
                            var unreadAlerts = NotificationsController.Instance.CountNotifications(userInfo.UserID, PortalController.GetEffectivePortalId(this.portalController, this.appStatus, this.portalGroupController, userInfo.PortalID));

                            this.MessageText = unreadMessages > 0 ? string.Format(CultureInfo.CurrentCulture, Localization.GetString("Messages", Localization.GetResourceFile(this, MyFileName)), unreadMessages) : Localization.GetString("NoMessages", Localization.GetResourceFile(this, MyFileName));
                            this.NotificationText = unreadAlerts > 0 ? string.Format(CultureInfo.CurrentCulture, Localization.GetString("Notifications", Localization.GetResourceFile(this, MyFileName)), unreadAlerts) : Localization.GetString("NoNotifications", Localization.GetResourceFile(this, MyFileName));

                            this.MessageNavigateUrl = this.navigationManager.NavigateURL(this.GetMessageTab(), string.Empty, $"userId={userInfo.UserID}");
                            this.NotificationNavigateUrl = this.navigationManager.NavigateURL(this.GetMessageTab(), string.Empty, $"userId={userInfo.UserID}", "view=notifications", "action=notifications");
                            this.NotificationToolTip = Localization.GetString("CheckNotifications", Localization.GetResourceFile(this, MyFileName));
                            this.MessageToolTip = Localization.GetString("CheckMessages", Localization.GetResourceFile(this, MyFileName));
                            this.MessageGroupVisible = true;
                            this.NotificationGroupVisible = true;

                            if (this.LegacyMode && unreadMessages > 0)
                            {
                                this.RegisterText = this.RegisterText + string.Format(CultureInfo.CurrentCulture, Localization.GetString("NewMessages", Localization.GetResourceFile(this, MyFileName)), unreadMessages);
                            }
                        }
                        else
                        {
                            this.MessageGroupVisible = false;
                            this.NotificationGroupVisible = false;
                        }

                        if (this.ShowAvatar)
                        {
                            this.AvatarImageUrl = GetAvatarUrl(userInfo);
                            this.AvatarToolTip = Localization.GetString("ProfileAvatar", Localization.GetResourceFile(this, MyFileName));
                            this.AvatarGroupVisible = true;
                        }
                        else
                        {
                            this.AvatarGroupVisible = false;
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        private static string GetAvatarUrl(UserInfo userInfo)
        {
            return UserController.Instance.GetUserProfilePictureUrl(userInfo.UserID, 32, 32);
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
    }
}
