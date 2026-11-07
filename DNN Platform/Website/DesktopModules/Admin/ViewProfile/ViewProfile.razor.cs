// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Admin.ViewProfile
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Threading;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Lists;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Profile;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Entities.Users.Social;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Social.Notifications;
    using DotNetNuke.Services.Tokens;
    using DotNetNuke.UI.Modules;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The ViewProfile ProfileModuleUserControlBase is used to view a Users Profile.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>ViewProfile.ascx</c> + <c>ViewProfile.ascx.cs</c> + <c>ViewProfile.ascx.designer.cs</c>;
    /// the markup now lives in <c>ViewProfile.razor</c>. The designer fields map as follows:
    /// <c>profileOutput.InnerHtml</c>/<c>.Visible</c> are <see cref="ProfileOutputHtml"/> and
    /// <see cref="ProfileOutputVisible"/>, <c>noPropertiesLabel.Visible</c> is
    /// <see cref="NoPropertiesLabelVisible"/>, <c>buttonPanel.Visible</c> and
    /// <c>editLink.NavigateUrl</c> are <see cref="ButtonPanelVisible"/> and
    /// <see cref="EditLinkNavigateUrl"/>, and <c>pnlScripts.Visible</c> is
    /// <see cref="ScriptsVisible"/>.
    /// </para>
    /// <para>
    /// <b>Template token replacement.</b> It is unchanged: the five tokens the control replaced
    /// itself (<c>[BUTTON:EDITPROFILE]</c>, <c>[HYPERLINK:EDITPROFILE]</c>,
    /// <c>[HYPERLINK:MYACCOUNT]</c> and <c>[PROFILE:PHOTO]</c>) are replaced under the same
    /// conditions and with the same markup, and the rest is still handed to
    /// <see cref="TokenReplace.ReplaceEnvironmentTokens(string)"/> with the same
    /// <c>User</c>/<c>AccessingUser</c> pair. The result is rendered as a
    /// <see cref="Microsoft.AspNetCore.Components.MarkupString"/> because the Web Forms control
    /// assigned it to <c>HtmlGenericControl.InnerHtml</c>, i.e. it was raw markup there too - the
    /// profile template is administrator authored HTML. Nothing that used to be encoded became raw:
    /// the substituted <i>values</i> are encoded by <c>TokenReplace</c>/<c>ProfilePropertyAccess</c>
    /// as before, and the three hyperlinks this control builds itself contain only a DNN generated
    /// URL and a localized caption, exactly as the <c>string.Format</c> calls did.
    /// </para>
    /// <para>
    /// <b>Per-property visibility.</b> Also unchanged, and it is an authorization control rather
    /// than cosmetics: the property values come from
    /// <see cref="ProfilePropertyAccess.GetProperty(string, string, CultureInfo, UserInfo, Scope, ref bool)"/>
    /// with the <i>accessing</i> user (<c>PortalSettings.UserInfo</c>) and
    /// <see cref="Scope.DefaultSettings"/>, which is what applies each property's
    /// <c>ProfileVisibility</c>; the "no properties" message still depends on
    /// <c>ProfilePropertyDefinition.Visible</c>; and the e-mail address is still only emitted to
    /// the profile's owner or to a member of the portal's administrator role.
    /// </para>
    /// <para>
    /// <b>The knockout view model.</b> <c>ProfileProperties</c> used to be a block of JavaScript
    /// assignments interpolated into an inline <c>&lt;script&gt;</c>, which a Razor component may
    /// not emit. It is now the JSON of the same view model, rendered into a data island and applied
    /// by <c>~/desktopmodules/Admin/ViewProfile/dnn.viewProfile.js</c>, which carries the rest of
    /// that script verbatim. The per-value sanitation chain is preserved character for character;
    /// only the <c>Localization.GetSafeJSString</c> wrapper is gone, because its single job was to
    /// escape the value for the JavaScript string literal it was being pasted into and the JSON
    /// serializer now performs exactly that escaping (keeping it would double-escape and show
    /// backslashes to the user).
    /// </para>
    /// </remarks>
    public partial class ViewProfile : ProfileModuleUserControlBase
    {
        private readonly INavigationManager navigationManager;
        private readonly IJavaScriptLibraryHelper javaScript;
        private readonly IHostSettings hostSettings;
        private readonly ListController listController;

        private bool loaded;

        /// <summary>Initializes a new instance of the <see cref="ViewProfile"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public ViewProfile()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ViewProfile"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="javaScript">The JavaScript library helper.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public ViewProfile(INavigationManager navigationManager, IJavaScriptLibraryHelper javaScript)
            : this(navigationManager, javaScript, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ViewProfile"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="javaScript">The JavaScript library helper.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="listController">The list controller.</param>
        public ViewProfile(INavigationManager navigationManager, IJavaScriptLibraryHelper javaScript, IHostSettings hostSettings, ListController listController)
        {
            this.navigationManager = navigationManager ?? Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
            this.javaScript = javaScript ?? Globals.GetCurrentServiceProvider().GetRequiredService<IJavaScriptLibraryHelper>();
            this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            this.listController = listController ?? Globals.GetCurrentServiceProvider().GetRequiredService<ListController>();
        }

        /// <inheritdoc />
        public override bool DisplayModule => true;

        public bool IncludeButton
        {
            get
            {
                var includeButton = true;
                if (this.ModuleContext.Settings.ContainsKey("IncludeButton"))
                {
                    includeButton = Convert.ToBoolean(this.ModuleContext.Settings["IncludeButton"]);
                }

                return includeButton;
            }
        }

        /// <summary>Gets or sets the knockout view model of this module instance.</summary>
        /// <remarks>
        /// Was the block of <c>self['Name'] = ko.observable("value");</c> statements the inline
        /// script interpolated; it is the JSON of the same model now (see the class remarks).
        /// </remarks>
        public string ProfileProperties { get; set; }

        /// <summary>Gets the id of the element the profile template is rendered into (was <c>profileOutput.ClientID</c>).</summary>
        protected string ProfileOutputElementId =>
            "profileOutput_" + this.ModuleContext.ModuleId.ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the id of the view-model data island (replaces the per-module script function name).</summary>
        protected string ViewModelElementId =>
            "viewProfileViewModel_" + this.ModuleContext.ModuleId.ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the rendered profile template (was <c>profileOutput.InnerHtml</c>).</summary>
        protected string ProfileOutputHtml { get; private set; } = string.Empty;

        /// <summary>Gets a value indicating whether the profile template renders (was <c>profileOutput.Visible</c>).</summary>
        protected bool ProfileOutputVisible { get; private set; } = true;

        /// <summary>Gets a value indicating whether the "no properties" message renders (was <c>noPropertiesLabel.Visible</c>).</summary>
        protected bool NoPropertiesLabelVisible { get; private set; }

        /// <summary>Gets a value indicating whether the edit button renders (was <c>buttonPanel.Visible</c>).</summary>
        protected bool ButtonPanelVisible { get; private set; } = true;

        /// <summary>Gets the url of the edit button (was <c>editLink.NavigateUrl</c>).</summary>
        protected string EditLinkNavigateUrl { get; private set; }

        /// <summary>Gets a value indicating whether the view-model data island renders (was <c>pnlScripts.Visible</c>).</summary>
        protected bool ScriptsVisible { get; private set; } = true;

        private IPortalAliasInfo CurrentPortalAlias => PortalSettings.Current.PortalAlias;

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            // throw 404 so that deleted profile is not reindexed
            if (this.ProfileUser == null || this.ProfileUser.IsDeleted)
            {
                UrlUtils.Handle404Exception(this.Response, PortalSettings.Current);
                return;
            }

            this.ProcessQuerystring();

            this.javaScript.RequestRegistration(CommonJs.jQuery);
            this.javaScript.RequestRegistration(CommonJs.jQueryMigrate);
            this.javaScript.RequestRegistration(CommonJs.Knockout);

            // Replaces the inline knockout bootstrap of pnlScripts, which cannot be emitted from a
            // Razor component; the script reads the view model out of the rendered data island.
            Globals.GetCurrentServiceProvider()
                .GetRequiredService<DotNetNuke.Abstractions.ClientResources.IClientResourceController>()
                .RegisterScript("~/desktopmodules/Admin/ViewProfile/dnn.viewProfile.js");
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // OnLoad is raised whenever the component's parameters are set; the Web Forms Load
            // phase ran once per request, so the work below is guarded to run once.
            if (this.loaded)
            {
                return;
            }

            this.loaded = true;

            try
            {
                if (Null.IsNull(this.ProfileUserId))
                {
                    this.Visible = false;
                    return;
                }

                var template = Convert.ToString(this.ModuleContext.Settings["ProfileTemplate"]);
                if (string.IsNullOrEmpty(template))
                {
                    template = Localization.GetString("DefaultTemplate", this.LocalResourceFile);
                }

                var editUrl = this.navigationManager.NavigateURL(this.ModuleContext.PortalSettings.ActiveTab.TabID, "Profile", "userId=" + this.ProfileUserId, "pageno=1");
                var profileUrl = this.navigationManager.NavigateURL(this.ModuleContext.PortalSettings.ActiveTab.TabID, "Profile", "userId=" + this.ProfileUserId, "pageno=2");

                if (template.Contains("[BUTTON:EDITPROFILE]"))
                {
                    if (this.IncludeButton && this.IsUser)
                    {
                        string editHyperLink = string.Format("<a href=\"{0}\" class=\"dnnPrimaryAction\">{1}</a>", profileUrl, this.LocalizeString("Edit"));
                        template = template.Replace("[BUTTON:EDITPROFILE]", editHyperLink);
                    }

                    this.ButtonPanelVisible = false;
                }
                else
                {
                    this.ButtonPanelVisible = this.IncludeButton;
                    this.EditLinkNavigateUrl = editUrl;
                }

                if (template.Contains("[HYPERLINK:EDITPROFILE]"))
                {
                    if (this.IsUser)
                    {
                        string editHyperLink = string.Format("<a href=\"{0}\" class=\"dnnSecondaryAction\">{1}</a>", profileUrl, this.LocalizeString("Edit"));
                        template = template.Replace("[HYPERLINK:EDITPROFILE]", editHyperLink);
                    }
                }

                if (template.Contains("[HYPERLINK:MYACCOUNT]"))
                {
                    if (this.IsUser)
                    {
                        string editHyperLink = string.Format("<a href=\"{0}\" class=\"dnnSecondaryAction\">{1}</a>", editUrl, this.LocalizeString("MyAccount"));
                        template = template.Replace("[HYPERLINK:MYACCOUNT]", editHyperLink);
                    }

                    this.ButtonPanelVisible = false;
                }

                if (!this.IsUser && this.ButtonPanelVisible)
                {
                    this.ButtonPanelVisible = false;
                }

                if (this.ProfileUser.Profile.ProfileProperties.Cast<ProfilePropertyDefinition>().Count(profProperty => profProperty.Visible) == 0)
                {
                    this.NoPropertiesLabelVisible = true;
                    this.ProfileOutputVisible = false;
                    this.ScriptsVisible = false;
                }
                else
                {
                    if (template.IndexOf("[PROFILE:PHOTO]") > -1)
                    {
                        var profileImageHandlerBasedURL =
                            UserController.Instance?.GetUserProfilePictureUrl(this.ProfileUserId, 120, 120);
                        template = template.Replace("[PROFILE:PHOTO]", profileImageHandlerBasedURL);
                    }

                    var token = new TokenReplace { User = this.ProfileUser, AccessingUser = this.ModuleContext.PortalSettings.UserInfo };
                    this.ProfileOutputHtml = token.ReplaceEnvironmentTokens(template);
                    this.NoPropertiesLabelVisible = false;
                    this.ProfileOutputVisible = true;
                }

                var propertyAccess = new ProfilePropertyAccess(this.ProfileUser);
                var observables = new Dictionary<string, string>(StringComparer.Ordinal);
                var labels = new Dictionary<string, string>(StringComparer.Ordinal);
                bool propertyNotFound = false;

                foreach (ProfilePropertyDefinition property in this.ProfileUser.Profile.ProfileProperties)
                {
                    var displayDataType = ProfilePropertyAccess.DisplayDataType(this.listController, property).ToLowerInvariant();

                    // The per-property visibility rule lives here: GetProperty is given the
                    // ACCESSING user and the default scope, so a property the accessing user may
                    // not see comes back empty - never rendered. Do not widen this call.
                    string value = propertyAccess.GetProperty(
                        property.PropertyName,
                        string.Empty,
                        Thread.CurrentThread.CurrentUICulture,
                        this.ModuleContext.PortalSettings.UserInfo,
                        Scope.DefaultSettings,
                        ref propertyNotFound);

                    var clientName = property.PropertyName;
                    if (!string.IsNullOrEmpty(value))
                    {
                        value = (displayDataType == "richtext" ? value : WebUtility.HtmlDecode(value))
                            .Replace("\r", string.Empty)
                            .Replace("\n", " ")
                            .Replace(";", string.Empty)
                            .Replace("://", ":||") // protect http protocols won't be replaced in next step
                            .Replace("//", string.Empty)
                            .Replace(":||", "://"); // restore http protocols
                    }

                    observables[clientName] = value;
                    labels[clientName + "Text"] = clientName;
                }

                string email = (this.ProfileUserId == this.ModuleContext.PortalSettings.UserId
                                || this.ModuleContext.PortalSettings.UserInfo.IsInRole(this.ModuleContext.PortalSettings.AdministratorRoleName))
                                   ? this.ProfileUser.Email
                                   : string.Empty;

                email = WebUtility.HtmlDecode(email ?? string.Empty);
                email = email.Replace(";", string.Empty).Replace("//", string.Empty);
                observables["Email"] = email;
                labels["EmailText"] = this.LocalizeString("Email");

                this.ProfileProperties = JsonSerializer.Serialize(new
                {
                    // the six localized captions the inline script assigned before the properties.
                    texts = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["AboutMeText"] = this.LocalizeString("AboutMe"),
                        ["LocationText"] = this.LocalizeString("Location"),
                        ["GetInTouchText"] = this.LocalizeString("GetInTouch"),
                        ["EmptyAboutMeText"] = this.LocalizeString("EmptyAboutMe"),
                        ["EmptyLocationText"] = this.LocalizeString("EmptyLocation"),
                        ["EmptyGetInTouchText"] = this.LocalizeString("EmptyGetInTouch"),
                    },
                    observables,
                    labels,
                });
            }
            catch (Exception exc)
            {
                // Module failed to load
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        private string GetRedirectUrl()
        {
            // redirect user to default page if not specific the home tab, do this action to prevent loop redirect.
            var homeTabId = this.ModuleContext.PortalSettings.HomeTabId;
            string redirectUrl;

            if (homeTabId > Null.NullInteger)
            {
                redirectUrl = this.navigationManager.NavigateURL(homeTabId);
            }
            else
            {
                redirectUrl = Globals.GetPortalDomainName(this.CurrentPortalAlias.HttpAlias, this.Request, true) +
                              "/" + Globals.glbDefaultPage;
            }

            return redirectUrl;
        }

        private void ProcessQuerystring()
        {
            // in case someone is being redirected to here from an e-mail link action we need to process that here
            var action = this.Request.Query["action"].ToString();

            if (!this.Request.IsAuthenticated() && !string.IsNullOrEmpty(action))
            {
                // action requested but not logged in.
                string loginUrl = Common.Globals.LoginURL(this.Request.GetRawUrl(), false);
                this.Response.Redirect(loginUrl);
                return;
            }

            // only process this for authenticated requests
            if (this.Request.IsAuthenticated() && !string.IsNullOrEmpty(action))
            {
                // current user, i.e. the one that the request was for
                var currentUser = UserController.Instance.GetCurrentUserInfo();

                // the initiating user,i.e. the one who wanted to be friend
                // note that in this case here currentUser is visiting the profile of initiatingUser, most likely from a link in the notification e-mail
                var initiatingUser = UserController.Instance.GetUserById(PortalSettings.Current.PortalId, Convert.ToInt32(this.Request.Query["UserID"].ToString()));

                if (initiatingUser.UserID == currentUser.UserID)
                {
                    return; // do not further process for users who are on their own profile page
                }

                var friendRelationship = RelationshipController.Instance.GetFriendRelationship(currentUser, initiatingUser);

                if (friendRelationship != null)
                {
                    if (action.Equals("acceptfriend", StringComparison.OrdinalIgnoreCase))
                    {
                        var friend = UserController.GetUserById(this.hostSettings, PortalSettings.Current.PortalId, friendRelationship.UserId);
                        FriendsController.Instance.AcceptFriend(friend);
                    }

                    if (action.Equals("followback", StringComparison.OrdinalIgnoreCase))
                    {
                        var follower = UserController.GetUserById(this.hostSettings, PortalSettings.Current.PortalId, friendRelationship.UserId);
                        try
                        {
                            FollowersController.Instance.FollowUser(follower);
                            var notifications = NotificationsController.Instance.GetNotificationByContext(3, initiatingUser.UserID.ToString());
                            if (notifications.Count > 0)
                            {
                                NotificationsController.Instance.DeleteNotificationRecipient(notifications[0].NotificationID, currentUser.UserID);
                            }
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }

                this.Response.Redirect(Common.Globals.UserProfileURL(initiatingUser.UserID));
            }
        }
    }
}
