// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Admin.Security
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text.Json;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Data;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Entities.Users.Membership;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Security.Membership;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Log.EventLog;
    using DotNetNuke.Services.UserRequest;
    using DotNetNuke.UI.Skins.Controls;
    using DotNetNuke.Web.UI.WebControls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    using IEventLogger = DotNetNuke.Abstractions.Logging.IEventLogger;

    /// <summary>A control which allows a user to request a password reset.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>PasswordReset.ascx</c> + <c>PasswordReset.ascx.cs</c> +
    /// <c>PasswordReset.ascx.designer.cs</c>; the markup is in <c>PasswordReset.razor</c>.
    /// </para>
    /// <para>
    /// <b>THE RESET-TOKEN VALIDATION AND EXPIRY ARE SECURITY CONTROLS AND EVERY BRANCH IS
    /// PRESERVED.</b> <see cref="LoadUserInfo"/> is the token gate and reads, unchanged:
    /// </para>
    /// <code>
    /// var user = UserController.GetUserByPasswordResetToken(this.PortalId, this.ResetToken);
    /// if (user == null || user.PasswordResetExpiration &lt; DateTime.Now)
    /// {
    ///     this.divPassword.Visible = false;          // -&gt; this.divPasswordVisible = false;
    ///     this.resetMessages.Visible = true;         // -&gt; this.resetMessagesVisible = true;
    ///     this.lblHelp.Text = Localization.GetString("ResetLinkExpired", this.LocalResourceFile);
    ///     return;
    /// }
    /// </code>
    /// <para>
    /// Both halves of the condition are kept, in the same order, with the same
    /// <c>&lt;</c> (strictly-less-than) comparison against <c>DateTime.Now</c> — NOT
    /// <c>&lt;=</c> and NOT <c>UtcNow</c>: an unknown/forged token and an expired token both hide
    /// the whole form and show <c>ResetLinkExpired</c>, and nothing downstream can run because the
    /// method returns. The <c>ResetToken</c> accessor keeps its original quirk too (see below), so a
    /// request that arrives without a <c>resetToken</c> yields <see cref="string.Empty"/>, which
    /// matches no user and therefore takes the same "expired" path. The token is validated a SECOND
    /// time, server side, by <c>UserController.ChangePasswordByToken(..., this.ResetToken, out
    /// errorMessage)</c> when the password is actually changed, and that failure path is also
    /// preserved — so hiding the form is not the only thing standing between a forged token and a
    /// password change.
    /// </para>
    /// <para>
    /// <b>READING <c>resetToken</c> AND <c>forced</c> WITHOUT AN <c>HttpContext</c>.</b> The
    /// parameter NAMES are unchanged (<c>resetToken</c> and <c>forced</c>, matching the
    /// <c>{alias}/default.aspx?ctl=PasswordReset&amp;resetToken=…&amp;forced=true</c> URL the Login
    /// flow issues on a must-change-password), and both are read ONCE in <see cref="OnLoad"/>, which
    /// <c>UserControlBase</c> raises from <c>OnParametersSet</c> while the initial, server-rendered
    /// request is still in flight. The values are then held in fields
    /// (<c>resetTokenQueryValue</c>, <c>forcedQueryValue</c>) for the life of the circuit. They are
    /// never read from a click handler: <c>IHttpContextAccessor.HttpContext</c> is
    /// <see langword="null"/> once the component is interactive over SignalR, so reading the query
    /// string from <see cref="CmdChangePasswordClick"/> would silently produce an empty token and
    /// turn the reset into a no-op. <c>returnurl</c> (query string AND cookie) is captured in the
    /// same place and for the same reason.
    /// </para>
    /// <para>
    /// <b>The <c>ResetToken</c> accessor quirk is kept deliberately.</b> The original was
    /// <c>get =&gt; this.ViewState["ResetToken"] != null ? this.Request.QueryString["resetToken"] :
    /// string.Empty;</c> — i.e. the token is only honoured once <c>OnLoad</c> has explicitly
    /// recorded that a <c>resetToken</c> was present on the URL. That is reproduced by the
    /// <c>resetTokenAssigned</c> flag plus the captured query value (a Blazor component field has
    /// exactly the lifetime <c>ViewState</c> had — the circuit). Making the getter return the
    /// captured value unconditionally would be a widening: a request that never carried a token
    /// would start resolving one.
    /// </para>
    /// <para>
    /// All of the <see cref="CmdChangePasswordClick"/> rules are ported in the original order, each
    /// returning immediately so no later rule can mask an earlier failure:
    /// </para>
    /// <list type="number">
    /// <item><description>Provider requires Q&amp;A and the answer is empty → return (no message, as before).</description></item>
    /// <item><description>new != confirm → <c>PasswordMismatch</c> (from the GLOBAL resource file, as before), logged as a failure, return.</description></item>
    /// <item><description><c>!UserController.ValidatePassword(newPassword)</c> → <c>PasswordResetFailed</c>, logged, return. This is the minimum-length / complexity / regular-expression seam.</description></item>
    /// <item><description>When <c>MembershipPasswordSettings(this.User.PortalID).EnableBannedList</c> is on: <c>FoundBannedPassword(new) || username == new</c> → <c>PasswordResetFailed</c>, logged, return. Both halves, and both inside the <c>EnableBannedList</c> gate, exactly as before.</description></item>
    /// <item><description>When the portal uses the e-mail address as the user name, the typed value is resolved to the real user name with an extra <c>GetUserByEmail</c> lookup — unchanged, including the fact that a miss leaves the typed value in place rather than reporting that no such account exists.</description></item>
    /// <item><description><c>UserController.ChangePasswordByToken(PortalId, username, newPassword, answer, ResetToken, out errorMessage)</c>; on failure the provider's own <c>errorMessage</c> is shown and logged, exactly as before.</description></item>
    /// <item><description>On success, <c>UserController.ValidateUser</c> is re-run and <c>UPDATEPROFILE</c> still redirects to the Login control instead of completing, so an incomplete profile cannot bypass the profile step.</description></item>
    /// </list>
    /// <para>
    /// The four ASP.NET validators are evaluated server side before any of that
    /// (<see cref="Validate"/>), with the same rules the controls carried: the user name is
    /// required, the password must match <c>[\w\W]{7,}</c>, the confirmation is required, and the
    /// answer is required when the Q&amp;A row is shown. In WebForms these ran in the browser and
    /// suppressed the post-back; evaluating them on the server as well only ever rejects more, never
    /// less.
    /// </para>
    /// <para>
    /// Mechanical changes: <c>Response.Redirect</c> → <c>NavigationManager.NavigateTo(…,
    /// forceLoad: true)</c>; <c>Request.Url.Query</c> → <c>Request.QueryString.Value</c>;
    /// <c>HttpUtility</c> → <c>System.Net.WebUtility</c>;
    /// <c>UserRequestIPAddressController.GetUserRequestIPAddress(new HttpRequestWrapper(Request))</c>
    /// → the <c>HttpRequest</c> overload; the <c>ScriptManager</c> /
    /// <c>Page.ClientScript.RegisterStartupScript</c> pairs that bootstrapped the strength meter and
    /// the password comparer → <see cref="IJSRuntime"/> calls from
    /// <see cref="OnAfterRenderAsync(bool)"/> (no <c>&lt;script&gt;</c> is legal in a .razor file),
    /// still serializing the option objects with <c>Json.Serialize</c> so their
    /// <c>[DataContract]</c> names and the <c>[OnSerializing]</c> hook that publishes the portal's
    /// password rules are preserved. The success redirect kept its 3000&#160;ms delay
    /// (<c>RedirectTimeout</c>), now as a <c>Task.Delay</c> on the circuit followed by
    /// <c>NavigateTo</c> instead of a <c>setTimeout</c> start-up script. <c>ViewState.Add("PageNo",
    /// 3)</c> is gone: it wrote into this control's own view state and nothing ever read it back
    /// (the value belongs to the Login control's wizard), and there is no view state to write to.
    /// </para>
    /// </remarks>
    public partial class PasswordReset : UserModuleBase
    {
        private const int RedirectTimeout = 3000;

        private readonly INavigationManager navigationManager;
        private readonly IEventLogger eventLogger;
        private readonly IJavaScriptLibraryHelper javaScript;
        private readonly IPortalController portalController;
        private readonly IClientResourceController clientResourceController;
        private readonly DataProvider dataProvider;

        private string ipAddress;

        // Captured ONCE from the initial, server-rendered request (see the remarks).
        private string resetTokenQueryValue;
        private string forcedQueryValue;
        private string returnUrlQueryValue;
        private string returnUrlCookieValue;
        private string requestQuery = string.Empty;

        // Replaces ViewState["ResetToken"] != null.
        private bool resetTokenAssigned;

        private bool userInfoLoaded;

        // Replaces the designer control fields and their Text/Visible/Enabled/CssClass state.
        private string username = string.Empty;
        private string password = string.Empty;
        private string confirmPassword = string.Empty;
        private string answer = string.Empty;
        private string infoText = string.Empty;
        private string helpText = string.Empty;
        private string questionText = string.Empty;
        private string valUsernameText = string.Empty;
        private string usernameDefaultText = string.Empty;
        private string passwordDefaultText = string.Empty;
        private string confirmDefaultText = string.Empty;
        private string answerDefaultText = string.Empty;
        private string cancelUrl = string.Empty;
        private string passwordContainerCssClass = string.Empty;
        private string passwordCssClass = string.Empty;
        private bool resetMessagesVisible;
        private bool divPasswordVisible = true;
        private bool divQAVisible;
        private bool usernameEnabled = true;
        private bool valUsernameVisible;
        private bool valPasswordVisible;
        private bool valConfirmPasswordVisible;
        private bool valAnswerVisible;
        private bool strengthMeterEnabled;

        /// <summary>Initializes a new instance of the <see cref="PasswordReset"/> class.</summary>
        public PasswordReset()
            : base(null, null)
        {
            var services = Common.Globals.GetCurrentServiceProvider();
            this.navigationManager = services.GetRequiredService<INavigationManager>();
            this.eventLogger = services.GetRequiredService<IEventLogger>();
            this.javaScript = services.GetRequiredService<IJavaScriptLibraryHelper>();
            this.portalController = services.GetRequiredService<IPortalController>();
            this.clientResourceController = services.GetRequiredService<IClientResourceController>();
            this.dataProvider = services.GetRequiredService<DataProvider>();
        }

        /// <summary>Gets or sets the Blazor navigation manager used by the redirects.</summary>
        [Inject]
        protected Microsoft.AspNetCore.Components.NavigationManager Navigation { get; set; }

        /// <summary>Gets or sets the JavaScript runtime the password scripts are initialized with.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>
        /// Gets or sets the reset token. The getter only honours the captured <c>resetToken</c>
        /// query value once <see cref="OnLoad"/> has assigned it, which is what
        /// <c>ViewState["ResetToken"] != null</c> expressed.
        /// </summary>
        private string ResetToken
        {
            get => this.resetTokenAssigned ? this.resetTokenQueryValue : string.Empty;
            set
            {
                this.resetTokenQueryValue = value;
                this.resetTokenAssigned = true;
            }
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // `Control.ID` is what pointed LocalResourceFile at App_LocalResources/PasswordReset.ascx.
            this.LocalResourceFile = Path.Combine(this.ControlPath, Localization.LocalResourceDirectory + "/PasswordReset.ascx");

            this.ipAddress = UserRequestIPAddressController.Instance.GetUserRequestIPAddress(this.Request);

            // Capture everything that comes off the request while the request still exists.
            var queryString = this.Request?.GetQueryString();
            var requestedToken = queryString?["resetToken"];
            this.forcedQueryValue = queryString?["forced"];
            this.returnUrlQueryValue = queryString?["returnurl"];
            this.returnUrlCookieValue = this.Request is not null && this.Request.Cookies.TryGetValue("returnurl", out var returnUrlCookie)
                ? returnUrlCookie
                : null;
            this.requestQuery = this.Request?.QueryString.Value ?? string.Empty;

            this.javaScript.RequestRegistration(CommonJs.DnnPlugins);
            this.clientResourceController.RegisterScript("~/resources/Shared/scripts/dnn.jquery.extensions.js");
            this.clientResourceController.RegisterScript("~/resources/Shared/scripts/dnn.jquery.tooltip.js");
            this.clientResourceController.RegisterScript("~/resources/Shared/scripts/dnn.PasswordStrength.js");
            this.clientResourceController.RegisterScript("~/desktopmodules/Admin/Security/Scripts/dnn.PasswordComparer.js");

            this.clientResourceController.RegisterStylesheet("~/resources/Shared/stylesheets/dnn.PasswordStrength.css", DotNetNuke.Abstractions.ClientResources.FileOrder.Css.ResourceCss);

            // SENTINEL PRESERVED: LoginTabId == -1 means "no login page configured"; it must stay a
            // `!= -1` test, because tab 0 is a real TabId.
            if (this.PortalSettings.LoginTabId != -1 && this.PortalSettings.ActiveTab.TabID != this.PortalSettings.LoginTabId)
            {
                this.Navigation.NavigateTo(this.navigationManager.NavigateURL(this.PortalSettings.LoginTabId) + this.requestQuery, forceLoad: true);
                return;
            }

            this.cancelUrl = this.navigationManager.NavigateURL();

            if (requestedToken != null)
            {
                this.ResetToken = requestedToken;
                this.usernameEnabled = false;
            }

            var useEmailAsUserName = PortalController.GetPortalSettingAsBoolean(this.portalController, "Registration_UseEmailAsUserName", this.PortalId, false);
            if (useEmailAsUserName)
            {
                this.valUsernameText = Localization.GetString("Email.Required", this.LocalResourceFile);
            }
            else
            {
                this.valUsernameText = Localization.GetString("Username.Required", this.LocalResourceFile);
            }

            if (this.forcedQueryValue == "true")
            {
                this.infoText = Localization.GetString("ForcedResetInfo", this.LocalResourceFile);
            }

            // Was txt*.Attributes.Add("data-default", …); the attributes are rendered from these.
            this.usernameDefaultText = useEmailAsUserName ? this.LocalizeText("Email") : this.LocalizeText("Username");
            this.passwordDefaultText = this.LocalizeText("Password");
            this.confirmDefaultText = this.LocalizeText("Confirm");
            this.answerDefaultText = this.LocalizeText("Answer");

            // Was `if (!this.Page.IsPostBack)`: the user info is loaded exactly once per circuit.
            if (!this.userInfoLoaded)
            {
                this.userInfoLoaded = true;
                this.LoadUserInfo();
            }
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            if (!string.IsNullOrEmpty(this.helpText) || !string.IsNullOrEmpty(this.infoText))
            {
                this.resetMessagesVisible = true;
            }

            if (this.HostSettings.EnableStrengthMeter)
            {
                this.strengthMeterEnabled = true;
                this.passwordContainerCssClass = "password-strength-container";
                this.passwordCssClass = "password-strength";
            }
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (!firstRender)
            {
                return;
            }

            if (this.strengthMeterEnabled)
            {
                var options = new DnnPaswordStrengthOptions();
                var optionsAsJsonString = Json.Serialize(options);
                using var optionsJson = JsonDocument.Parse(optionsAsJsonString);
                await this.JsRuntime.InvokeVoidAsync("dnn.initializePasswordStrength", ".password-strength", optionsJson.RootElement);
            }

            var confirmPasswordOptions = new DnnConfirmPasswordOptions()
            {
                FirstElementSelector = ".password-strength",
                SecondElementSelector = ".password-confirm",
                ContainerSelector = ".dnnPasswordReset",
                UnmatchedCssClass = "unmatched",
                MatchedCssClass = "matched",
            };

            var confirmOptionsAsJsonString = Json.Serialize(confirmPasswordOptions);
            using var confirmJson = JsonDocument.Parse(confirmOptionsAsJsonString);
            await this.JsRuntime.InvokeVoidAsync("dnn.initializePasswordComparer", confirmJson.RootElement);
        }

        /// <summary>After a successful password change will redirect the user to requested ReturnUrl OR the login page.</summary>
        protected void RedirectAfterPasswordChange()
        {
            var redirectUrl = string.Empty;

            if (this.returnUrlQueryValue != null)
            {
                // return to the url passed to signin
                redirectUrl = WebUtility.UrlDecode(this.returnUrlQueryValue);

                // clean the return url to avoid possible XSS attack.
                redirectUrl = UrlUtils.ValidReturnUrl(redirectUrl);
            }

            if (this.returnUrlCookieValue != null)
            {
                // return to the url passed to signin
                redirectUrl = WebUtility.UrlDecode(this.returnUrlCookieValue);

                // clean the return url to avoid possible XSS attack.
                redirectUrl = UrlUtils.ValidReturnUrl(redirectUrl);
            }

            if (string.IsNullOrEmpty(redirectUrl))
            {
                // return to the login page by default to allow users to login
                redirectUrl = this.navigationManager.NavigateURL(this.PortalSettings.ActiveTab.TabID, "Login");
            }

            this.AddModuleMessage("ChangeSuccessful", ModuleMessage.ModuleMessageType.GreenSuccess, true);
            this.resetMessagesVisible = this.divPasswordVisible = false;
            this.helpText = this.infoText = string.Empty;

            // redirect page after the same delay the setTimeout start-up script used.
            _ = this.RedirectAfterDelayAsync(redirectUrl);
        }

        private async Task RedirectAfterDelayAsync(string redirectUrl)
        {
            await Task.Delay(RedirectTimeout);
            await this.InvokeAsync(() => this.Navigation.NavigateTo(redirectUrl, forceLoad: true));
        }

        /// <summary>
        /// Evaluates the four ASP.NET validators of the original markup server side.
        /// </summary>
        /// <returns><see langword="true"/> when every validator is satisfied.</returns>
        private bool Validate()
        {
            // <asp:RequiredFieldValidator ID="valUsername" ControlToValidate="txtUsername" />
            this.valUsernameVisible = string.IsNullOrEmpty(this.username?.Trim());

            // <asp:RegularExpressionValidator ID="valPassword" ValidationExpression="[\w\W]{7,}" />
            this.valPasswordVisible = !string.IsNullOrEmpty(this.password)
                && !System.Text.RegularExpressions.Regex.IsMatch(this.password, @"^[\w\W]{7,}$");

            // <asp:RequiredFieldValidator ID="valConfirmPassword" ControlToValidate="txtConfirmPassword" />
            this.valConfirmPasswordVisible = string.IsNullOrEmpty(this.confirmPassword?.Trim());

            // <asp:RequiredFieldValidator ID="valAnswer" ControlToValidate="txtAnswer" /> — only
            // rendered, and therefore only validated, when the Q&A row is shown.
            this.valAnswerVisible = this.divQAVisible && string.IsNullOrEmpty(this.answer?.Trim());

            return !this.valUsernameVisible
                && !this.valPasswordVisible
                && !this.valConfirmPasswordVisible
                && !this.valAnswerVisible;
        }

        private void LoadUserInfo()
        {
            var user = UserController.GetUserByPasswordResetToken(this.PortalId, this.ResetToken);

            if (user == null || user.PasswordResetExpiration < DateTime.Now)
            {
                this.divPasswordVisible = false;
                this.resetMessagesVisible = true;
                this.helpText = Localization.GetString("ResetLinkExpired", this.LocalResourceFile);
                return;
            }

            this.username = user.Username;
            if (MembershipProviderConfig.RequiresQuestionAndAnswer)
            {
                this.questionText = user.Membership.PasswordQuestion;
                this.divQAVisible = true;
            }
        }

        private void CmdChangePasswordClick()
        {
            // The validators of the original markup; see Validate().
            if (!this.Validate())
            {
                return;
            }

            var username = this.username;

            if (MembershipProviderConfig.RequiresQuestionAndAnswer && string.IsNullOrEmpty(this.answer))
            {
                return;
            }

            // 1. Check New Password and Confirm are the same
            if (this.password != this.confirmPassword)
            {
                this.resetMessagesVisible = true;
                var failed = Localization.GetString("PasswordMismatch");
                this.LogFailure(failed);
                this.helpText = failed;
                return;
            }

            var newPassword = this.password.Trim();
            if (UserController.ValidatePassword(newPassword) == false)
            {
                this.resetMessagesVisible = true;
                var failed = Localization.GetString("PasswordResetFailed");
                this.LogFailure(failed);
                this.helpText = failed;
                return;
            }

            // Check New Password is not same as username or banned
            var settings = new MembershipPasswordSettings(this.User.PortalID);

            if (settings.EnableBannedList)
            {
                var m = new MembershipPasswordController(this.ListController, this.dataProvider);
                if (m.FoundBannedPassword(newPassword) || this.username == newPassword)
                {
                    this.resetMessagesVisible = true;
                    var failed = Localization.GetString("PasswordResetFailed");
                    this.LogFailure(failed);
                    this.helpText = failed;
                    return;
                }
            }

            if (PortalController.GetPortalSettingAsBoolean(this.portalController, "Registration_UseEmailAsUserName", this.PortalId, false))
            {
                var testUser = UserController.GetUserByEmail(this.PortalId, username); // one additional call to db to see if an account with that email actually exists
                if (testUser != null)
                {
                    username = testUser.Username; // we need the username of the account in order to change the password in the next step
                }
            }

            var answer = string.Empty;
            if (MembershipProviderConfig.RequiresQuestionAndAnswer)
            {
                answer = this.answer;
            }

            if (UserController.ChangePasswordByToken(this.PortalSettings.PortalId, username, newPassword, answer, this.ResetToken, out var errorMessage) == false)
            {
                this.resetMessagesVisible = true;
                this.LogFailure(errorMessage);
                this.helpText = errorMessage;
            }
            else
            {
                // check user has a valid profile
                var user = UserController.GetUserByName(this.PortalSettings.PortalId, username);
                var validStatus = UserController.ValidateUser(user, this.PortalSettings.PortalId, false);
                if (validStatus == UserValidStatus.UPDATEPROFILE)
                {
                    this.LogSuccess();

                    // ViewState.Add("PageNo", 3) is dropped: nothing read it back, and there is no
                    // view state on ASP.NET Core. The redirect it accompanied is preserved.
                    this.Navigation.NavigateTo(this.navigationManager.NavigateURL(this.PortalSettings.ActiveTab.TabID, "Login"), forceLoad: true);
                }
                else
                {
                    // Log user in to site
                    this.LogSuccess();
                    this.RedirectAfterPasswordChange();
                }
            }
        }

        private void LogSuccess()
        {
            this.LogResult(string.Empty);
        }

        private void LogFailure(string reason)
        {
            this.LogResult(reason);
        }

        private void LogResult(string message)
        {
            DotNetNuke.Abstractions.Logging.ILogInfo log = new LogInfo
            {
                LogPortalName = this.PortalSettings.PortalName,
            };
            log.LogUserId = this.UserId;
            log.LogPortalId = this.PortalSettings.PortalId;

            if (string.IsNullOrEmpty(message))
            {
                log.LogTypeKey = "PASSWORD_SENT_SUCCESS";
            }
            else
            {
                log.LogTypeKey = "PASSWORD_SENT_FAILURE";
                log.LogProperties.Add(new LogDetailInfo("Cause", message));
            }

            log.AddProperty("IP", this.ipAddress);

            this.eventLogger.AddLog(log);
        }
    }
}
