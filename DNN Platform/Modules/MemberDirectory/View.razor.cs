// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.MemberDirectory
{
    using System;
    using System.Collections;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    using FileOrder = DotNetNuke.Abstractions.ClientResources.FileOrder;

    /// <summary>Display member directory.</summary>
    /// <remarks>
    /// Was <c>View.ascx</c> + <c>View.ascx.cs</c> + <c>View.ascx.designer.cs</c>; the markup now
    /// lives in <c>View.razor</c>. The designer file's server-control fields have no Blazor
    /// equivalent and are replaced as follows:
    /// <list type="bullet">
    /// <item><description>the eight <c>DnnJsInclude</c> / <c>DnnCssInclude</c> controls became the
    /// <see cref="IClientResourceController"/> registrations of <see cref="RegisterClientResources"/>,
    /// keeping every <c>FilePath</c> and every <c>Priority</c> — Tag Helpers are an MVC-view
    /// concept and cannot run in a Blazor component;</description></item>
    /// <item><description><c>memberDirectory</c> (read through <c>memberDirectory.ClientID</c> by
    /// the inline script) became <see cref="ClientId"/>, the stable per-module id
    /// <c>MemberDirectory.js</c> targets with <c>md.init('#…')</c>;</description></item>
    /// <item><description><c>searchBar</c>, <c>advancedSearchBar</c>, <c>popUpPanel</c> and
    /// <c>loadMore</c>, whose only use was the <c>Visible</c> assignment in <c>OnInit</c>, became
    /// <see cref="ShowSearchBar"/>, <see cref="ShowAdvancedSearchBar"/>, <see cref="EnablePopUp"/>
    /// and <see cref="DisablePaging"/>, which the markup renders conditionally on.</description></item>
    /// </list>
    /// </remarks>
    public partial class View : ProfileModuleUserControlBase
    {
        private readonly IClientResourceController clientResourceController;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;
        private readonly IServicesFramework servicesFramework;

        /// <summary>
        /// Backs <see cref="GroupId"/>. The <c>GroupId</c> request parameter is read ONCE, during
        /// the initial (static) render, because <c>HttpContext</c> is <see langword="null"/> for
        /// the rest of a Blazor Server circuit.
        /// </summary>
        private int groupId = Null.NullInteger;

        /// <summary>
        /// The profile user id captured during the initial (static) render. Nullable so that a
        /// read before <c>OnInit</c> (the module skin asks for <see cref="DisplayModule"/> during
        /// composition) still falls back to the live request rather than silently seeing
        /// <see cref="Null.NullInteger"/>, which is also a legitimate <c>ProfileUserId</c> value
        /// and so cannot be used as a "not captured yet" sentinel.
        /// </summary>
        private int? profileUserIdValue;

        private bool renderIe7StyleSheet;

        /// <summary>Initializes a new instance of the <see cref="View"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public View()
            : this(null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="View"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        public View(IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IServicesFramework servicesFramework)
        {
            var services = Globals.GetCurrentServiceProvider();
            this.clientResourceController = clientResourceController ?? services.GetRequiredService<IClientResourceController>();
            this.appStatus = appStatus ?? services.GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? services.GetRequiredService<IEventLogger>();
            this.servicesFramework = servicesFramework ?? services.GetRequiredService<IServicesFramework>();
        }

        /// <inheritdoc />
        public override bool DisplayModule
            => !(this.CapturedProfileUserId == this.ModuleContext.PortalSettings.UserId && this.FilterBy == "User") && this.ModuleContext.PortalSettings.UserId > -1;

        /// <summary>Gets the resource file the profile property labels are localized from.</summary>
        public string ProfileResourceFile
            => "~/DesktopModules/Admin/Security/App_LocalResources/Profile.ascx";

        /// <summary>
        /// Gets the client side id of this module control's root element, which replaces
        /// <c>memberDirectory.ClientID</c>.
        /// </summary>
        /// <remarks>
        /// A module control is a Blazor component and has no WebForms naming container, so the id
        /// is built from the module instance — which is what made the WebForms id unique on the
        /// page as well. <c>MemberDirectory.js</c> consumes this id verbatim through
        /// <c>md.init('#…')</c>, so it must stay stable.
        /// </remarks>
        public override string ClientId => $"dnn_ctr{this.ModuleContext.ModuleId}_{this.GetType().Name}";

        /// <summary>
        /// Gets the profile user id, preferring the value captured during the initial static
        /// render. <c>HttpContext</c> is <see langword="null"/> for the rest of a Blazor Server
        /// circuit, so the live <c>ProfileUserId</c> must only be consulted before the capture
        /// has happened.
        /// </summary>
        protected int CapturedProfileUserId => this.profileUserIdValue ?? this.ProfileUserId;

        /// <summary>Gets or sets the JavaScript runtime the view model is bootstrapped through.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets the alternate item template.</summary>
        protected string AlternateItemTemplate
#pragma warning disable CS0618 // Type or member is obsolete
            => GetSetting(this.ModuleContext.Configuration.TabModuleSettings, "AlternateItemTemplate", Settings.DefaultAlternateItemTemplate);
#pragma warning restore CS0618 // Type or member is obsolete

        /// <summary>Gets a value indicating whether paging is disabled. Replaces <c>loadMore.Visible</c>.</summary>
        protected bool DisablePaging
            => bool.Parse(GetSetting(this.ModuleContext.Configuration.TabModuleSettings, "DisablePaging", "false"));

        /// <summary>Gets the display mode of the search bars.</summary>
        protected string DisplaySearch
            => GetSetting(this.ModuleContext.Configuration.TabModuleSettings, "DisplaySearch", "Both");

        /// <summary>Gets a value indicating whether the hover pop-up is enabled. Replaces <c>popUpPanel.Visible</c>.</summary>
        protected bool EnablePopUp
            => bool.Parse(GetSetting(this.ModuleContext.Configuration.TabModuleSettings, "EnablePopUp", "false"));

        /// <summary>Gets a value indicating whether the search bar is rendered. Replaces <c>searchBar.Visible</c>.</summary>
        protected bool ShowSearchBar => this.DisplaySearch != "None";

        /// <summary>Gets a value indicating whether the advanced search bar is rendered. Replaces <c>advancedSearchBar.Visible</c>.</summary>
        protected bool ShowAdvancedSearchBar => this.DisplaySearch == "Both";

        /// <summary>Gets the filter mode of the directory.</summary>
        protected string FilterBy
            => GetSetting(this.ModuleContext.Configuration.ModuleSettings, "FilterBy", "None");

        /// <summary>Gets the group the directory is filtered to, from the request.</summary>
        protected int GroupId => this.groupId;

        /// <summary>Gets the item template.</summary>
        protected string ItemTemplate
#pragma warning disable CS0618 // Type or member is obsolete
            => GetSetting(this.ModuleContext.Configuration.TabModuleSettings, "ItemTemplate", Settings.DefaultItemTemplate);
#pragma warning restore CS0618 // Type or member is obsolete

        /// <summary>Gets the page size.</summary>
        protected int PageSize
#pragma warning disable CS0618 // Type or member is obsolete
            => GetSettingAsInt32(this.ModuleContext.Configuration.TabModuleSettings, "PageSize", Settings.DefaultPageSize);
#pragma warning restore CS0618 // Type or member is obsolete

        /// <summary>Gets the pop-up template.</summary>
        protected string PopUpTemplate
#pragma warning disable CS0618 // Type or member is obsolete
            => GetSetting(this.ModuleContext.Configuration.TabModuleSettings, "PopUpTemplate", Settings.DefaultPopUpTemplate);
#pragma warning restore CS0618 // Type or member is obsolete

        /// <summary>Gets the item template as markup. Replaces <c>&lt;%=ItemTemplate %&gt;</c>, which emitted it raw.</summary>
        protected MarkupString ItemTemplateMarkup => (MarkupString)this.ItemTemplate;

        /// <summary>Gets the alternate item template as markup. Replaces <c>&lt;%=AlternateItemTemplate %&gt;</c>.</summary>
        protected MarkupString AlternateItemTemplateMarkup => (MarkupString)this.AlternateItemTemplate;

        /// <summary>Gets the pop-up template as markup. Replaces <c>&lt;%=PopUpTemplate %&gt;</c>.</summary>
        protected MarkupString PopUpTemplateMarkup => (MarkupString)this.PopUpTemplate;

        /// <summary>Gets the token the view profile URL carries the user id in.</summary>
        protected string ProfileUrlUserToken
            => "PROFILEUSER";

        /// <summary>Gets the first advanced search field.</summary>
        protected string SearchField1
            => GetSetting(this.ModuleContext.Configuration.TabModuleSettings, "SearchField1", "DisplayName");

        /// <summary>Gets the second advanced search field.</summary>
        protected string SearchField2
            => GetSetting(this.ModuleContext.Configuration.TabModuleSettings, "SearchField2", "Email");

        /// <summary>Gets the third advanced search field.</summary>
        protected string SearchField3
            => GetSetting(this.ModuleContext.Configuration.TabModuleSettings, "SearchField3", "City");

        /// <summary>Gets the fourth advanced search field.</summary>
        protected string SearchField4
            => GetSetting(this.ModuleContext.Configuration.TabModuleSettings, "SearchField4", "Country");

        /// <summary>Gets the URL template of a member's profile page.</summary>
        protected string ViewProfileUrl
            => this.NavigationManager.NavigateURL(this.ModuleContext.PortalSettings.UserTabId, string.Empty, "userId=PROFILEUSER");

        /// <summary>Gets a value indicating whether private messaging is disabled for the viewer.</summary>
        protected bool DisablePrivateMessage
            => this.PortalSettings.DisablePrivateMessage && !this.UserInfo.IsSuperUser && !this.UserInfo.IsInRole(this.PortalSettings.AdministratorRoleName);

        /// <summary>Gets the current portal settings.</summary>
        protected PortalSettings PortalSettings => PortalSettings.Current;

        /// <summary>Gets the current user.</summary>
        protected UserInfo UserInfo => UserController.Instance.GetCurrentUserInfo();

        /// <summary>Gets the URL of the "loading" animation.</summary>
        /// <remarks>
        /// Replaces <c>ResolveUrl("images/ajax-loader.gif")</c>, which was resolved against the
        /// control's own directory. The folder on disk is <c>Images</c>; the original lowercase
        /// spelling only worked on a case insensitive file system.
        /// </remarks>
        protected string AjaxLoaderUrl => Globals.ResolveUrl("~/DesktopModules/MemberDirectory/Images/ajax-loader.gif");

        /// <summary>
        /// Gets a value indicating whether the legacy IE stylesheet link is rendered into the page head.
        /// </summary>
        /// <remarks>Replaces the <c>Page.Header.Controls.Add(new LiteralControl(cssLink))</c> call of <c>AddIe7StyleSheet</c>.</remarks>
        protected bool RenderIe7StyleSheet => this.renderIe7StyleSheet;

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            // Request derived values are read once, here, during the initial (static) render.
            var groupIdParameter = this.Request?.GetValue("GroupId");
            this.groupId = string.IsNullOrEmpty(groupIdParameter)
                ? Null.NullInteger
                : int.Parse(groupIdParameter, CultureInfo.InvariantCulture);
            this.profileUserIdValue = this.ProfileUserId;

            this.servicesFramework.RequestAjaxAntiForgerySupport();
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, CommonJs.DnnPlugins);
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, CommonJs.jQueryFileUpload);
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, CommonJs.Knockout);

            this.RegisterClientResources();
            this.clientResourceController.RegisterScript("~/DesktopModules/MemberDirectory/Scripts/MemberDirectory.js");
            this.AddIe7StyleSheet();

            base.OnInit(e);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Runs the client side seam of this control: <c>View.ascx</c> emitted the knockout
        /// bootstrap inline, which a Blazor render tree never executes, so it lives in
        /// <c>Scripts/memberDirectoryInterop.js</c> and is invoked here with every value the
        /// inline script interpolated server side.
        /// </remarks>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (!firstRender)
            {
                return;
            }

            try
            {
                // Globals.ResolveUrl applies the application path, so the import still resolves
                // when DNN is hosted in a virtual directory / sub-application. A hardcoded
                // absolute "/DesktopModules/..." path 404s there and takes the whole knockout
                // bootstrap with it.
                var module = await this.JsRuntime.InvokeAsync<IJSObjectReference>(
                    "import",
                    Globals.ResolveUrl("~/DesktopModules/MemberDirectory/Scripts/memberDirectoryInterop.js"));
                await module.InvokeVoidAsync("initMemberDirectory", this.BuildInteropOptions());
            }
            catch (JSException ex)
            {
                // the extension's assets are not reachable (or there is no browser, as during a
                // plain HTTP render): the markup degrades to its static form. This must be logged
                // rather than discarded -- it is the only initialization of the member list, so
                // swallowing it silently leaves an empty directory with no diagnostic anywhere.
                this.eventLogger.AddLog(
                    "MemberDirectory",
                    $"Failed to initialize the member directory client script: {ex}",
                    EventLogType.HOST_ALERT);
            }
        }

        private static string GetSetting(Hashtable settings, string key, string defaultValue)
        {
            string setting = defaultValue;
            if (settings[key] != null)
            {
                setting = Convert.ToString(settings[key]);
            }

            return setting;
        }

        private static int GetSettingAsInt32(Hashtable settings, string key, int defaultValue)
        {
            int setting = defaultValue;
            if (settings[key] != null)
            {
                setting = Convert.ToInt32(settings[key]);
            }

            return setting;
        }

        /// <summary>
        /// Gets the browser major version advertised by the <c>User-Agent</c> header.
        /// </summary>
        /// <param name="userAgent">The <c>User-Agent</c> header value.</param>
        /// <returns>The highest version number found in the header, or <c>0</c> when there is none.</returns>
        /// <remarks>
        /// <c>HttpRequest.Browser</c> (the System.Web browser capabilities database) has no
        /// ASP.NET Core counterpart, so the header is parsed directly, exactly as the migrated
        /// CoreMessaging view does. <c>0</c> is what System.Web produced for an unknown browser,
        /// so the original "major version below 8" branch keeps the same outcome.
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

            // the .ascx declared this stylesheet twice (DnnCssInclude2 and DnnCssInclude3 had the
            // same FilePath); registering the same file twice is idempotent, as it was there.
            this.clientResourceController.RegisterStylesheet("~/Resources/Shared/Components/UserFileManager/UserFileManager.css");
            this.clientResourceController.RegisterStylesheet("~/Resources/Shared/Components/UserFileManager/UserFileManager.css");
            this.clientResourceController.RegisterScript("~/Resources/Shared/Components/Tokeninput/jquery.tokeninput.js", (FileOrder.Js)103);
            this.clientResourceController.RegisterStylesheet("~/Resources/Shared/Components/Tokeninput/Themes/token-input-facebook.css");
            this.clientResourceController.RegisterScript("~/Resources/Shared/Components/UserFileManager/jquery.dnnUserFileUpload.js", (FileOrder.Js)104);
        }

        private void AddIe7StyleSheet()
        {
            // The original condition was `browser.Type == "IE" || browser.MajorVersion < 8`.
            // System.Web's HttpBrowserCapabilities.Type is the browser name CONCATENATED with the
            // major version ("IE8", "IE11"), so it never equalled the bare string "IE" and the
            // condition reduced to `MajorVersion < 8`. Only that effective behavior is reproduced.
            var userAgent = this.Request?.Headers.UserAgent.ToString() ?? string.Empty;

            if (GetBrowserMajorVersion(userAgent) < 8)
            {
                // The link element is rendered into the page head by View.razor's HeadContent,
                // which is the ASP.NET Core equivalent of adding a LiteralControl to Page.Header.
                this.renderIe7StyleSheet = true;
            }
        }

        /// <summary>Builds the options the inline knockout bootstrap interpolated server side.</summary>
        /// <returns>The argument of <c>initMemberDirectory</c>.</returns>
        /// <remarks>
        /// The localized values use <see cref="DotNetNuke.UI.Modules.ModuleUserControlBase.LocalizeString"/>
        /// rather than the <c>LocalizeSafeJsString</c> the inline script needed: the values are now
        /// JSON serialized by the JS interop layer, which escapes them itself, so pre-escaping them
        /// would put literal backslashes in the UI.
        /// </remarks>
        private object BuildInteropOptions()
        {
            string Text(string key) => this.LocalizeString(key);

            // Config.GetMaxUploadSize() itself is deprecated in favour of the overload taking an
            // IApplicationStatusInfo; the injected service is used so the behavior is identical.
            var maxFileSize = Config.GetMaxUploadSize(this.appStatus);

#pragma warning disable CS0618 // Globals.UserProfilePicRelativeUrl returns the URL *template* that MemberDirectory.js formats per user; UserController.Instance.GetUserProfilePictureUrl, which replaces it, resolves a single concrete user's picture and cannot express that template. The .ascx called this very member.
            var profilePicHandler = Globals.UserProfilePicRelativeUrl();
#pragma warning restore CS0618

            return new
            {
                clientId = this.ClientId,
                moduleId = this.ModuleContext.ModuleId,
                settings = new
                {
                    userId = this.CapturedProfileUserId == -1 ? this.ModuleContext.PortalSettings.UserId : this.CapturedProfileUserId,
                    viewerId = this.ModuleContext.PortalSettings.UserId,
                    groupId = this.GroupId,
                    pageSize = this.PageSize,
                    profileUrl = this.ViewProfileUrl,
                    profileUrlUserToken = this.ProfileUrlUserToken,
                    profilePicHandler,
                    addFriendText = Text("AddFriend"),
                    acceptFriendText = Text("AcceptFriend"),
                    friendPendingText = Text("FriendPending"),
                    removeFriendText = Text("RemoveFriend"),
                    followText = Text("Follow"),
                    unFollowText = Text("UnFollow"),
                    sendMessageText = Text("SendMessage"),
                    userNameText = Text("UserName"),
                    emailText = Text("Email"),
                    cityText = Text("City"),
                    searchErrorText = Text("SearchError"),
                    serverErrorText = Text("ServerError"),
                    serverErrorWithDescriptionText = Text("ServerErrorWithDescription"),
                    disablePrivateMessage = this.DisablePrivateMessage,
                },
                composeMessageSettings = new
                {
                    title = Text("Title"),
                    toText = Text("To"),
                    subjectText = Text("Subject"),
                    messageText = Text("Message"),
                    sendText = Text("Send"),
                    cancelText = Text("Cancel"),
                    attachmentsText = Text("Attachments"),
                    browseText = Text("Browse"),
                    uploadText = Text("Upload"),
                    maxFileSize,
                    removeText = Text("Remove"),
                    messageSentTitle = Text("MessageSentTitle"),
                    messageSentText = Text("MessageSent"),
                    dismissThisText = Text("DismissThis"),
                    throttlingText = Text("Throttling"),
                    noResultsText = Text("NoResults"),
                    searchingText = Text("Searching"),
                    createMessageErrorText = Text("CreateMessageError"),
                    createMessageErrorWithDescriptionText = Text("CreateMessageErrorWithDescription"),
                    autoSuggestErrorText = Text("AutoSuggestError"),
                },
            };
        }
    }
}
