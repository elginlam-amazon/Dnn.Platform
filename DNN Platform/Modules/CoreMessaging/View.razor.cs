// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.CoreMessaging
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Skins.Controls;
    using DotNetNuke.UI.Utilities;

    using Microsoft.Extensions.DependencyInjection;

    // DotNetNuke.UI.Utilities (imported for ClientAPI) declares its own Globals, so the alias
    // pins the DotNetNuke.Common.Globals the original control used. FileOrder is pinned to the
    // abstractions one, which is the enum the IClientResourceController extension methods take.
    using FileOrder = DotNetNuke.Abstractions.ClientResources.FileOrder;
    using Globals = DotNetNuke.Common.Globals;

    /// <summary>Implements the logic for the default view.</summary>
    /// <remarks>
    /// Was <c>View.ascx</c> + <c>View.ascx.cs</c> + <c>View.ascx.designer.cs</c>; the markup now
    /// lives in <c>View.razor</c>. The designer file's server-control fields have no Blazor
    /// equivalent and are replaced as follows:
    /// <list type="bullet">
    /// <item><description>
    /// <c>CoreMessagingContainer</c> (an <c>asp:Panel</c> whose only use was <c>Visible = false</c>)
    /// becomes <see cref="ShowContainer"/>, which the markup uses to render the wrapper
    /// conditionally.
    /// </description></item>
    /// <item><description>
    /// The six <c>DnnJsInclude</c> / three <c>DnnCssInclude</c> controls become
    /// <see cref="IClientResourceController"/> registrations in <see cref="OnInit"/>, keeping every
    /// <c>FilePath</c> and every <c>Priority</c>. Tag Helpers are an MVC-view concept and cannot be
    /// used from a Blazor component, so the controller is driven directly.
    /// </description></item>
    /// <item><description>
    /// <c>coreMessaging</c> (a <c>runat="server"</c> div, referenced through
    /// <c>coreMessaging.ClientID</c>) becomes <see cref="ClientId"/>, the stable per-module id that
    /// <c>CoreMessaging.js</c> targets with <c>sm.init</c>, <c>ko.contextFor</c> and
    /// <c>parentsUntil</c>.
    /// </description></item>
    /// </list>
    /// </remarks>
    public partial class View : PortalModuleBase
    {
        private readonly IJavaScriptLibraryHelper javaScript;
        private readonly IPortalController portalController;
        private readonly IClientResourceController clientResourceController;
        private readonly IServicesFramework servicesFramework;

        /// <summary>
        /// Backs <see cref="ProfileUserId"/>. The <c>UserId</c> request parameter is read ONCE,
        /// during the initial (static) render, because <c>HttpContext</c> is <see langword="null"/>
        /// for the rest of a Blazor Server circuit.
        /// </summary>
        private int profileUserId = Null.NullInteger;

        private bool showContainer = true;
        private bool renderIe7StyleSheet;
        private string bootstrapScript;

        /// <summary>Initializes a new instance of the <see cref="View"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.2. Please use overload with IPortalController. Scheduled removal in v12.0.0.")]
        public View()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="View"/> class.</summary>
        /// <param name="javaScript">The JavaScript library helper.</param>
        [Obsolete("Deprecated in DotNetNuke 10.0.2. Please use overload with IPortalController. Scheduled removal in v12.0.0.")]
        public View(IJavaScriptLibraryHelper javaScript)
            : this(javaScript, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="View"/> class.</summary>
        /// <param name="javaScript">The JavaScript library helper.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="clientResourceController">The client resources controller.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public View(IJavaScriptLibraryHelper javaScript, IPortalController portalController, IClientResourceController clientResourceController)
            : this(javaScript, portalController, clientResourceController, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="View"/> class.</summary>
        /// <param name="javaScript">The JavaScript library helper.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="clientResourceController">The client resources controller.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        public View(IJavaScriptLibraryHelper javaScript, IPortalController portalController, IClientResourceController clientResourceController, IServicesFramework servicesFramework)
        {
            this.javaScript = javaScript ?? this.DependencyProvider.GetRequiredService<IJavaScriptLibraryHelper>();
            this.portalController = portalController ?? this.DependencyProvider.GetRequiredService<IPortalController>();
            this.clientResourceController = clientResourceController ?? this.DependencyProvider.GetRequiredService<IClientResourceController>();
            this.servicesFramework = servicesFramework ?? this.DependencyProvider.GetRequiredService<IServicesFramework>();
        }

        /// <summary>Gets the user id from the request parameters.</summary>
        public int ProfileUserId => this.profileUserId;

        /// <summary>Gets a string indicating whether attachments are allowed "true" or not "false".</summary>
        public string ShowAttachments =>
            PortalController.GetPortalSetting(this.portalController, "MessagingAllowAttachments", this.PortalId, "NO") == "NO" ? "false" : "true";

        /// <summary>Gets a value indicating whether the subscriptions tab should be shown.</summary>
        public bool ShowSubscriptionTab =>
            !this.Settings.ContainsKey("ShowSubscriptionTab") ||
            this.Settings["ShowSubscriptionTab"].ToString().Equals("true", StringComparison.InvariantCultureIgnoreCase);

        /// <summary>Gets a value indicating whether the private messaging should be disabled.</summary>
        public bool DisablePrivateMessage =>
            this.PortalSettings.DisablePrivateMessage && !this.UserInfo.IsSuperUser
                                                      && !this.UserInfo.IsInRole(this.PortalSettings.AdministratorRoleName);

        /// <summary>
        /// Gets the client side id of this module control's root element, which replaces
        /// <c>coreMessaging.ClientID</c>.
        /// </summary>
        /// <remarks>
        /// A module control is a Blazor component and has no WebForms naming container, so the id is
        /// built from the module instance — which is what made the WebForms id unique on the page as
        /// well. This is the <c>dnn_ctr{ModuleId}_{TypeName}</c> pattern established by the migrated
        /// DDRMenu module. <c>CoreMessaging.js</c> consumes this id verbatim through
        /// <c>sm.init('#…')</c>, <c>ko.contextFor(document.getElementById(…))</c> and
        /// <c>$("#…").parentsUntil("div[id$=ContentPane]")</c>, so it must stay stable.
        /// </remarks>
        public string ClientId => $"dnn_ctr{this.ModuleId}_{this.GetType().Name}";

        /// <summary>
        /// Gets a value indicating whether the module content is rendered.
        /// </summary>
        /// <remarks>Replaces <c>CoreMessagingContainer.Visible</c>.</remarks>
        protected bool ShowContainer => this.showContainer;

        /// <summary>
        /// Gets a value indicating whether the legacy IE stylesheet link is rendered into the page head.
        /// </summary>
        /// <remarks>Replaces the <c>Page.Header.Controls.Add(new LiteralControl(cssLink))</c> call.</remarks>
        protected bool RenderIe7StyleSheet => this.renderIe7StyleSheet;

        /// <summary>Gets the Knockout bootstrap script emitted at the end of the module markup.</summary>
        /// <remarks>
        /// Every value in it is computed server side during the initial (static) render — the only
        /// time a <c>&lt;script&gt;</c> element in a component is written to the page and the only
        /// time the request is still in flight.
        /// </remarks>
        protected Microsoft.AspNetCore.Components.MarkupString BootstrapScript =>
            (Microsoft.AspNetCore.Components.MarkupString)(this.bootstrapScript ?? string.Empty);

        /// <summary>Gets the <c>data-bind</c> attribute of the messages tab link.</summary>
        /// <remarks>
        /// Built here rather than inline in the markup because the Knockout expression embeds a
        /// localized string in a double quoted attribute. The rendered value is identical to what
        /// the <c>.ascx</c> emitted.
        /// </remarks>
        protected string MessagesTabBinding =>
            "click: loadMessagesTab, attr: { title: TotalNewThreads() + '" + this.LocalizeText("NewUnreadMessages") + "    '}";

        /// <summary>Gets the <c>data-bind</c> attribute of the notifications tab link.</summary>
        protected string NotificationsTabBinding =>
            "click: loadNotificationsTab, attr: {title: TotalNotifications() + '" + this.LocalizeText("TotalNotifications") + "    '}";

        /// <summary>Gets the <c>data-bind</c> attribute of the per-message read/unread toggle.</summary>
        protected string ReadStatusToggleBinding =>
            "    click: $root.toggleState, visible: $root.showInbox(), attr: { title: (Read() === true ? '"
            + this.LocalizeText("MarkUnread") + "    ' : '" + this.LocalizeText("MarkRead") + "    ') }";

        /// <summary>Gets the <c>data-bind</c> attribute of the archive/unarchive conversation link.</summary>
        protected string ToggleArchiveConversationBinding =>
            "click: toggleArchiveConversation, text: (TotalArchivedThreads() === 0 ? '"
            + Localization.GetSafeJSString(this.LocalizeText("MarkArchive")) + "    ' : '"
            + Localization.GetSafeJSString(this.LocalizeText("MarkUnarchive")) + "    ')";

        /// <summary>Gets the <c>data-bind</c> attribute of the read/unread conversation link.</summary>
        protected string ToggleConversationStateBinding =>
            "click: toggleConversationState, text: (conversationRead() === true ? '"
            + Localization.GetSafeJSString(this.LocalizeText("MarkUnread")) + "    ' : '"
            + Localization.GetSafeJSString(this.LocalizeText("MarkRead")) + "    '), visible: TotalArchivedThreads() === 0";

        /// <summary>Gets the URL of the "loading" animation.</summary>
        /// <remarks>
        /// Replaces <c>ResolveUrl("images/ajax-loader.gif")</c>, which was resolved against the
        /// control's own directory. The folder on disk is <c>Images</c>; the original lowercase
        /// spelling only worked on a case insensitive file system.
        /// </remarks>
        protected string AjaxLoaderUrl => Globals.ResolveUrl("~/DesktopModules/CoreMessaging/Images/ajax-loader.gif");

        /// <summary>Gets the URL of the attachment indicator icon.</summary>
        /// <remarks>Replaces <c>ResolveUrl("images/paperClip.png")</c>.</remarks>
        protected string PaperClipUrl => Globals.ResolveUrl("~/DesktopModules/CoreMessaging/Images/paperClip.png");

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            // Request derived values are read once, here, during the initial (static) render.
            var request = this.Request;
            var userIdParameter = request?.GetValue("UserId");
            if (!string.IsNullOrEmpty(userIdParameter))
            {
                this.profileUserId = int.Parse(userIdParameter, CultureInfo.InvariantCulture);
            }
            else
            {
                this.profileUserId = Null.NullInteger;
            }

            if (request?.IsAuthenticated() != true)
            {
                // Do not redirect but hide the content of the module and display a message.
                this.showContainer = false;
                UI.Skins.Skin.AddModuleMessage(this, Localization.GetString("ContentNotAvailable", this.LocalResourceFile), ModuleMessage.ModuleMessageType.YellowWarning);
                return;
            }

            if (this.UserId != this.ProfileUserId && (this.PortalSettings.ActiveTab.ParentId == this.PortalSettings.UserTabId || this.TabId == this.PortalSettings.UserTabId))
            {
                // Do not redirect but hide the content of the module.
                this.showContainer = false;
                return;
            }

            if (this.IsEditable && this.PermissionsNotProperlySet())
            {
                UI.Skins.Skin.AddModuleMessage(this, Localization.GetString("PermissionsNotProperlySet", this.LocalResourceFile), ModuleMessage.ModuleMessageType.YellowWarning);
            }

            this.RegisterClientResources();

            this.servicesFramework.RequestAjaxScriptSupport();
            this.servicesFramework.RequestAjaxAntiForgerySupport();
            this.javaScript.RequestRegistration(CommonJs.DnnPlugins);
            this.javaScript.RequestRegistration(CommonJs.Knockout);
            this.clientResourceController.RegisterScript("~/DesktopModules/CoreMessaging/Scripts/CoreMessaging.js");
            this.javaScript.RequestRegistration(CommonJs.jQueryFileUpload);
            this.AddIe7StyleSheet();

            this.bootstrapScript = this.BuildBootstrapScript();

            base.OnInit(e);
        }

        private static bool PermissionPredicate(PermissionInfoBase p)
        {
            return p.PermissionKey == "VIEW" && p.AllowAccess && (p.RoleName == Globals.glbRoleAllUsersName || p.RoleName == Globals.glbRoleUnauthUserName);
        }

        /// <summary>
        /// Gets the browser major version advertised by the <c>User-Agent</c> header.
        /// </summary>
        /// <param name="userAgent">The <c>User-Agent</c> header value.</param>
        /// <returns>The highest version number found in the header, or <c>0</c> when there is none.</returns>
        /// <remarks>
        /// <c>HttpRequest.Browser</c> (the System.Web browser capabilities database) has no ASP.NET
        /// Core counterpart, so the header is parsed directly. The highest version token wins, so
        /// modern user agents yield a large number and correctly skip the IE7 stylesheet. <c>0</c>
        /// is returned for a header the parser does not recognise, which is also the value
        /// System.Web produced for an unknown browser, so the original "major version below 8"
        /// branch keeps the same outcome.
        /// </remarks>
        private static int GetBrowserMajorVersion(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
            {
                return 0;
            }

            var highest = 0;
            foreach (Match match in Regex.Matches(userAgent, @"(?:/|rv:|MSIE )(\d+)", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
            {
                if (int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) && version > highest)
                {
                    highest = version;
                }
            }

            return highest;
        }

        /// <summary>
        /// Registers the client resources the markup declared with <c>dnn:DnnJsInclude</c> and
        /// <c>dnn:DnnCssInclude</c>, preserving every file path and every priority.
        /// </summary>
        private void RegisterClientResources()
        {
            this.clientResourceController.RegisterScript("~/Resources/Shared/Components/ComposeMessage/ComposeMessage.js", (FileOrder.Js)101);
            this.clientResourceController.RegisterStylesheet("~/Resources/Shared/Components/ComposeMessage/ComposeMessage.css");
            this.clientResourceController.RegisterScript("~/Resources/Shared/Components/UserFileManager/UserFileManager.js", (FileOrder.Js)102);
            this.clientResourceController.RegisterStylesheet("~/Resources/Shared/Components/UserFileManager/UserFileManager.css");
            this.clientResourceController.RegisterScript("~/Resources/Shared/Components/Tokeninput/jquery.tokeninput.js", (FileOrder.Js)103);
            this.clientResourceController.RegisterStylesheet("~/Resources/Shared/Components/Tokeninput/Themes/token-input-facebook.css");
            this.clientResourceController.RegisterScript("~/Resources/Shared/Scripts/jquery.history.js", (FileOrder.Js)104);
            this.clientResourceController.RegisterScript("~/Resources/Shared/Components/UserFileManager/jquery.dnnUserFileUpload.js", (FileOrder.Js)106);
        }

        private void AddIe7StyleSheet()
        {
            // The original condition was `browser.Type == "IE" || browser.MajorVersion < 8`.
            // System.Web's HttpBrowserCapabilities.Type is the browser name CONCATENATED with the
            // major version ("IE8", "IE11"), so it never equalled the bare string "IE" and the
            // first clause was always false - the condition reduced to `MajorVersion < 8`. Only
            // that effective behavior is reproduced here; reinstating an IE test would newly
            // apply this stylesheet to IE 8-11, which the original excluded.
            var userAgent = this.Request?.Headers.UserAgent.ToString() ?? string.Empty;

            if (GetBrowserMajorVersion(userAgent) < 8)
            {
                // The link element is rendered into the page head by View.razor's HeadContent,
                // which is the ASP.NET Core equivalent of adding a LiteralControl to Page.Header.
                this.renderIe7StyleSheet = true;
            }
        }

        private bool PermissionsNotProperlySet()
        {
            List<PermissionInfoBase> permissions;

            if (this.ModuleConfiguration.InheritViewPermissions)
            {
                var tabPermissionCollection = TabPermissionController.GetTabPermissions(this.TabId, this.PortalId);
                permissions = tabPermissionCollection.ToList();
            }
            else
            {
                permissions = this.ModuleConfiguration.ModulePermissions.ToList();
            }

            return permissions.Find(PermissionPredicate) != null;
        }

        /// <summary>Builds the inline Knockout bootstrap script of the module.</summary>
        /// <returns>The script body the markup emits inside its <c>&lt;script&gt;</c> element.</returns>
        private string BuildBootstrapScript()
        {
            string Js(string key) => ClientAPI.GetSafeJSString(this.LocalizeText(key));
            string Shared(string key) => Localization.GetSafeJSString(key, Localization.SharedResourceFile);

            var clientId = this.ClientId;
            var moduleId = this.ModuleId.ToString(CultureInfo.InvariantCulture);

            // Config.GetMaxUploadSize() itself is deprecated in favour of the overload taking an
            // IApplicationStatusInfo; the service is resolved here so the four public constructor
            // overloads keep their original signatures.
            var maxUploadSize = Config.GetMaxUploadSize(this.DependencyProvider.GetRequiredService<IApplicationStatusInfo>());

#pragma warning disable CS0618 // Globals.UserProfilePicRelativeUrl returns the URL *template* that CoreMessaging.js formats per user; UserController.Instance.GetUserProfilePictureUrl, which replaces it, resolves a single concrete user's picture and cannot express that template. The .ascx called this very member.
            var profilePicHandler = Globals.UserProfilePicRelativeUrl();
#pragma warning restore CS0618

            var script = new StringBuilder();
            script.AppendLine("\tjQuery(document).ready(function ($) {");
            script.AppendLine("\t\tvar sm = new CoreMessaging($, ko, {");
            script.AppendLine("\t\t\tprofilePicHandler: '" + profilePicHandler + "',");
            script.AppendLine("\t\t\tconversationSetAsReadText: '" + Js("ConversationSetAsRead") + "',");
            script.AppendLine("\t\t\tconversationSetAsUnreadText: '" + Js("ConversationSetAsUnread") + "',");
            script.AppendLine("\t\t\tloadingText: '" + Js("Loading") + "',");
            script.AppendLine("\t\t\tloadMoreText: '" + Js("LoadMore") + "',");
            script.AppendLine("\t\t\tconversationArchivedText: '" + Js("ConversationArchived") + "',");
            script.AppendLine("\t\t\tconversationUnarchivedText: '" + Js("ConversationUnarchived") + "',");
            script.AppendLine("\t\t\tnotificationConfirmTitleText: '" + Js("NotificationConfirmTitle") + "',");
            script.AppendLine("\t\t\tnotificationConfirmYesText: '" + Js("NotificationConfirmYes") + "',");
            script.AppendLine("\t\t\tnotificationConfirmNoText: '" + Js("NotificationConfirmNo") + "',");
            script.AppendLine("\t\t\tactionPerformedText: '" + Js("ActionPerformed") + "',");
            script.AppendLine("\t\t\tactionNotPerformedText: '" + Js("ActionNotPerformed") + "',");
            script.AppendLine("\t\t\treplyHasNoRecipientsText: '" + Js("ReplyHasNoRecipients") + "',");
            script.AppendLine("\t\t\tmessageSentText: '" + Js("MessageSent") + "',");
            script.AppendLine("\t\t\tserverErrorText: '" + Js("ServerError") + "',");
            script.AppendLine("\t\t\tserverErrorWithDescriptionText: '" + Js("ServerErrorWithDescription") + "',");
            script.AppendLine("\t\t\tdismissAllConfirmText: '" + Js("DismissAllConfirm") + "',");
            script.AppendLine("\t\t\ttext: '" + Shared("DeleteItem.Text") + "',");
            script.AppendLine("\t\t\tyesText: '" + Shared("Yes.Text") + "',");
            script.AppendLine("\t\t\tnoText: '" + Shared("No.Text") + "',");
            script.AppendLine("\t\t\ttitle: '" + Shared("Confirm.Text") + "',");
            script.AppendLine("\t\t    disablePrivateMessage: " + this.DisablePrivateMessage.ToString().ToLowerInvariant() + ",");
            script.AppendLine("\t\t    messageTitleText: '" + Js("Messages") + "',");
            script.AppendLine("\t\t    notificationTitleText: '" + Js("Notifications") + "',");
            script.AppendLine("\t\t    archiveTitleText: '" + Js("MarkArchive") + "',");
            script.AppendLine("\t\t    sentTitleText: '" + Js("Sent") + "',");
            script.AppendLine("\t\t    currentUserText: '" + ClientAPI.GetSafeJSString(UserController.Instance.GetCurrentUserInfo().DisplayName) + "',");
            script.AppendLine("            portalText: '" + ClientAPI.GetSafeJSString(this.PortalSettings.PortalName) + "'");
            script.AppendLine("\t}, {");
            script.AppendLine("            openTriggerScope: '#" + clientId + "',");
            script.AppendLine("            openTriggerSelector: '.ComposeMessage',");
            script.AppendLine("            title: '" + Js("Title") + "',");
            script.AppendLine("            toText: '" + Js("To") + "',");
            script.AppendLine("            subjectText: '" + Js("Subject") + "',");
            script.AppendLine("            messageText: '" + Js("Message") + "',");
            script.AppendLine("            sendText: '" + Js("Send") + "',");
            script.AppendLine("            cancelText: '" + Js("Cancel") + "',");
            script.AppendLine("            attachmentsText: '" + Js("Attachments") + "',");
            script.AppendLine("            browseText: '" + Js("Browse") + "',");
            script.AppendLine("            uploadText: '" + Js("Upload") + "',");
            script.AppendLine("            maxFileSize: " + maxUploadSize.ToString(CultureInfo.InvariantCulture) + ",");
            script.AppendLine("            removeText: '" + Js("Remove") + "',");
            script.AppendLine("            messageSentTitle: '" + Js("MessageSentTitle") + "',");
            script.AppendLine("            messageSentText: '" + Js("MessageSent") + "',");
            script.AppendLine("            dismissThisText: '" + Js("DismissThis") + "',");
            script.AppendLine("            throttlingText: '" + Js("Throttling") + "',");
            script.AppendLine("            noResultsText: '" + Js("NoResults") + "',");
            script.AppendLine("            searchingText: '" + Js("Searching") + "',");
            script.AppendLine("            createMessageErrorText: '" + Js("CreateMessageError") + "',");
            script.AppendLine("            createMessageErrorWithDescriptionText: '" + Js("CreateMessageErrorWithDescription") + "',");
            script.AppendLine("            autoSuggestErrorText: '" + Js("AutoSuggestError") + "',");
            script.AppendLine("            showAttachments: " + this.ShowAttachments + ",");
            script.AppendLine("            onMessageSent: function () {");
            script.AppendLine("                var context = ko.contextFor(document.getElementById($(\"#" + clientId + "\").attr(\"id\")));");
            script.AppendLine("                if (!context.$root.showReplies()) context.$root.reloadBoxes();");
            script.AppendLine("            },");
            script.AppendLine("            userFileManagerOptions: {");
            script.AppendLine("                title: '" + Js("UserFileManagerTitle") + "',");
            script.AppendLine("                cancelText: '" + Js("Cancel") + "',");
            script.AppendLine("                attachText: '" + Js("Attach") + "',");
            script.AppendLine("                getItemsServiceUrl: $.ServicesFramework(" + moduleId + ").getServiceRoot('InternalServices') + 'UserFile/GetItems',");
            script.AppendLine("                templatePath: '" + Globals.ResolveUrl("~/Resources/Shared/Components/UserFileManager/Templates/") + "',");
            script.AppendLine("                templateName: 'Default',");
            script.AppendLine("                templateExtension: '.html',");
            script.AppendLine("                nameHeaderText: '" + Js("Name.Header") + "',");
            script.AppendLine("                typeHeaderText: '" + Js("Type.Header") + "',");
            script.AppendLine("                lastModifiedHeaderText: '" + Js("LastModified.Header") + "',");
            script.AppendLine("                fileSizeText: '" + Js("FileSize.Header") + "'");
            script.AppendLine("            },");
            script.AppendLine("            servicesFramework: $.ServicesFramework(" + moduleId + ")");
            script.AppendLine("        });");
            script.AppendLine();
            script.AppendLine("        sm.init('#" + clientId + "');");
            script.AppendLine("        $(\"#" + clientId + "\").parentsUntil(\"div[id$=ContentPane]\").parent().find(\".dnnActionMenu\").css({left: \"232px\", top: \"1px\"});");
            script.AppendLine("    });");

            return script.ToString();
        }
    }
}
