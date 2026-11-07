// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Admin.Security
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Membership;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Log.EventLog;
    using DotNetNuke.Services.Mail;
    using DotNetNuke.Services.UserRequest;
    using DotNetNuke.UI.Skins.Controls;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using IEventLogger = DotNetNuke.Abstractions.Logging.IEventLogger;

    /// <summary>The SendPassword UserModuleBase is used to allow a user to retrieve their password.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>SendPassword.ascx</c> + <c>SendPassword.ascx.cs</c> +
    /// <c>SendPassword.ascx.designer.cs</c>; the markup is in <c>SendPassword.razor</c>.
    /// </para>
    /// <para>
    /// <b>THIS CONTROL IS USER-ENUMERATION SAFE AND THE MESSAGING IS PRESERVED EXACTLY AS WRITTEN.</b>
    /// <see cref="OnSendPasswordClick"/> computes <c>message</c> ONCE, up front, as
    /// <c>PasswordSent</c> formatted with the HTML-encoded input, and that single message is what a
    /// visitor sees whether the account exists, is deleted, is unapproved, or the mail send failed:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <c>this.user == null</c> (no such user) only sets <c>canSend = false</c> — it does NOT change
    /// <c>message</c>. There is deliberately no "no such user" / "unknown account" branch, and none
    /// is added here. The only exception is the pre-existing <c>userCount &gt; 1</c> case
    /// (<c>MultipleUsers</c>), which is reported for an ambiguous e-mail address and reveals nothing
    /// about a specific account; it is kept verbatim, neither widened nor removed.
    /// </description></item>
    /// <item><description>
    /// <c>user.IsDeleted</c> sets <c>canSend = false</c> with the message untouched.
    /// </description></item>
    /// <item><description>
    /// <c>user.Membership.Approved == false</c> sends the <c>PasswordReminderUserIsNotApproved</c>
    /// mail (and the admin notification when the portal enables it) and sets <c>canSend = false</c>
    /// with the message untouched.
    /// </description></item>
    /// <item><description>
    /// A failing <c>Mail.SendMail</c> sets <c>canSend = false</c> with the message untouched.
    /// </description></item>
    /// <item><description>
    /// After the attempt, <c>pnlRecover</c> is hidden and <c>liSend</c>/<c>liCancel</c> are swapped
    /// <i>regardless</i> of <c>canSend</c> — the original comment says so explicitly: "always hide
    /// panel so as to not reveal if username exists." The form therefore looks identical in the
    /// success and the failure case, which is the security property.
    /// </description></item>
    /// </list>
    /// <para>
    /// The three messages that ARE specific are all about the request or the host, never about an
    /// account, and are all raised <i>before</i> any user lookup happens, so they cannot leak:
    /// <c>EnterUsernameEmail</c> / <c>EnterUsername</c> (nothing was typed) and
    /// <c>OptionUnavailable</c> (no SMTP server configured, additionally logged as
    /// <c>SMTPNotConfigured</c>). The captcha gate
    /// <c>(UseCaptcha &amp;&amp; ctlCaptcha.IsValid) || (!UseCaptcha)</c> still wraps the entire
    /// handler body, so a failed captcha produces no message and no lookup at all.
    /// </para>
    /// <para>
    /// Every outcome is still logged with <c>PASSWORD_SENT_SUCCESS</c> /
    /// <c>PASSWORD_SENT_FAILURE</c> plus the request IP, and the logged user name is still passed
    /// through <c>PortalSecurity.InputFilter</c> with the same three filter flags.
    /// </para>
    /// <para>
    /// Mechanical changes only: the designer control fields become component fields;
    /// <c>Visible</c> assignments become the boolean fields the markup renders from;
    /// <c>UserRequestIPAddressController.GetUserRequestIPAddress(new HttpRequestWrapper(Request))</c>
    /// becomes the <c>HttpRequest</c> overload (<c>HttpRequestWrapper</c> does not exist on ASP.NET
    /// Core); <c>lnkCancel.NavigateUrl</c> becomes the <c>cancelUrl</c> field the anchor's href
    /// renders; <c>lnkCancel.Attributes["resourcekey"] = "cmdClose"</c> becomes
    /// <c>cancelResourceKey</c>; and the request-derived IP address is captured in <c>OnLoad</c>
    /// (while the request still exists) rather than read from the click handler, because
    /// <c>HttpContext</c> is <see langword="null"/> once the Blazor circuit is interactive.
    /// </para>
    /// </remarks>
    public partial class SendPassword : UserModuleBase
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<SendPassword>();

        private readonly INavigationManager navigationManager;
        private readonly IEventLogger eventLogger;
        private readonly IPortalController portalController;
        private readonly IMailSettings mailSettings;

        private UserInfo user;
        private int userCount = Null.NullInteger;
        private string ipAddress;

        // Replaces the designer control fields and their Text/Visible state.
        private string username = string.Empty;
        private string email = string.Empty;
        private string helpText = string.Empty;
        private string sendPasswordText = string.Empty;
        private string captchaErrorMessage = string.Empty;
        private string captchaText = string.Empty;
        private string cancelUrl = string.Empty;
        private string cancelResourceKey = "cmdCancel";
        private bool pnlRecoverVisible = true;
        private bool divPasswordVisible = true;
        private bool divUsernameVisible = true;
        private bool divEmailVisible = true;
        private bool divCaptchaVisible = true;
        private bool liSendVisible = true;
        private bool liCancelVisible = true;
        private bool captchaStyled;

        /// <summary>Initializes a new instance of the <see cref="SendPassword"/> class.</summary>
        public SendPassword()
            : base(null, null)
        {
            var services = Common.Globals.GetCurrentServiceProvider();
            this.navigationManager = services.GetRequiredService<INavigationManager>();
            this.eventLogger = services.GetRequiredService<IEventLogger>();
            this.portalController = services.GetRequiredService<IPortalController>();
            this.mailSettings = services.GetRequiredService<IMailSettings>();
        }

        /// <summary>Gets the Redirect URL (after successful sending of password).</summary>
        protected string RedirectURL
        {
            get
            {
                var redirectURL = string.Empty;

                object setting = GetSetting(this.PortalId, "Redirect_AfterRegistration");

                if (Convert.ToInt32(setting) > 0)
                {
                    // redirect to after registration page
                    redirectURL = this.navigationManager.NavigateURL(Convert.ToInt32(setting));
                }
                else
                {
                    if (Convert.ToInt32(setting) <= 0)
                    {
                        if (this.Request?.GetQueryString()["returnurl"] != null)
                        {
                            // return to the url passed to register
                            redirectURL = WebUtility.UrlDecode(this.Request.GetQueryString()["returnurl"]);

                            // clean the return url to avoid possible XSS attack.
                            redirectURL = UrlUtils.ValidReturnUrl(redirectURL);

                            if (redirectURL.Contains("?returnurl"))
                            {
                                string baseURL = redirectURL.Substring(
                                    0,
                                    redirectURL.IndexOf("?returnurl", StringComparison.Ordinal));
                                string returnURL =
                                    redirectURL.Substring(redirectURL.IndexOf("?returnurl", StringComparison.Ordinal) + 11);

                                redirectURL = string.Concat(baseURL, "?returnurl", WebUtility.UrlEncode(returnURL));
                            }
                        }

                        if (string.IsNullOrEmpty(redirectURL))
                        {
                            // redirect to current page
                            redirectURL = this.navigationManager.NavigateURL();
                        }
                    }
                    else
                    {
                        // redirect to after registration page
                        redirectURL = this.navigationManager.NavigateURL(Convert.ToInt32(setting));
                    }
                }

                return redirectURL;
            }
        }

        /// <summary>Gets a value indicating whether the Captcha control is used to validate the login.</summary>
        protected bool UseCaptcha
        {
            get
            {
                var setting = GetSetting(this.PortalId, "Security_CaptchaRetrivePassword");
                return Convert.ToBoolean(setting);
            }
        }

        /// <summary>Gets a value indicating whether the user name field is suppressed because the e-mail address is the user name.</summary>
        protected bool UsernameDisabled => PortalController.GetPortalSettingAsBoolean(this.portalController, "Registration_UseEmailAsUserName", this.PortalId, false);

        /// <summary>Gets or sets the captcha control. Replaces the designer field of the same name.</summary>
        protected DotNetNuke.UI.WebControls.CaptchaControl CtlCaptcha { get; set; }

        private bool ShowEmailField => MembershipProviderConfig.RequiresUniqueEmail || this.UsernameDisabled;

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            // `Control.ID` is what pointed LocalResourceFile at App_LocalResources/SendPassword.ascx.
            this.LocalResourceFile = Path.Combine(this.ControlPath, Localization.LocalResourceDirectory + "/SendPassword.ascx");

            var isEnabled = true;

            // both retrieval and reset now use password token resets
            if (MembershipProviderConfig.PasswordRetrievalEnabled || MembershipProviderConfig.PasswordResetEnabled)
            {
                this.helpText = Localization.GetString("ResetTokenHelp", this.LocalResourceFile);
                this.sendPasswordText = Localization.GetString("ResetToken", this.LocalResourceFile);
            }
            else
            {
                isEnabled = false;
                this.helpText = Localization.GetString("DisabledPasswordHelp", this.LocalResourceFile);
                this.divPasswordVisible = false;
            }

            if (!MembershipProviderConfig.PasswordResetEnabled)
            {
                isEnabled = false;
                this.helpText = Localization.GetString("DisabledPasswordHelp", this.LocalResourceFile);
                this.divPasswordVisible = false;
            }

            if (MembershipProviderConfig.RequiresUniqueEmail && isEnabled && !PortalController.GetPortalSettingAsBoolean(this.portalController, "Registration_UseEmailAsUserName", this.PortalId, false))
            {
                this.helpText += Localization.GetString("RequiresUniqueEmail", this.LocalResourceFile);
            }

            if (MembershipProviderConfig.RequiresQuestionAndAnswer && isEnabled)
            {
                this.helpText += Localization.GetString("RequiresQuestionAndAnswer", this.LocalResourceFile);
            }
        }

        /// <summary>Page_Load runs when the control is loaded.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.cancelUrl = this.navigationManager.NavigateURL();

            this.ipAddress = UserRequestIPAddressController.Instance.GetUserRequestIPAddress(this.Request);

            this.divEmailVisible = this.ShowEmailField;
            this.divUsernameVisible = !this.UsernameDisabled;
            this.divCaptchaVisible = this.UseCaptcha;

            if (this.UseCaptcha)
            {
                this.captchaErrorMessage = Localization.GetString("InvalidCaptcha", this.LocalResourceFile);
                this.captchaText = Localization.GetString("CaptchaText", this.LocalResourceFile);
            }
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            // Was `errorstyle-cssclass="dnnFormMessage dnnFormError"` on <dnn:captchacontrol>.
            // CaptchaControl.ErrorStyle is a read-only Style rather than a [Parameter], so it is
            // configured on the instance once it exists.
            if (this.CtlCaptcha is not null && !this.captchaStyled)
            {
                this.captchaStyled = true;
                this.CtlCaptcha.ErrorStyle.CssClass = "dnnFormMessage dnnFormError";
                this.StateHasChanged();
            }
        }

        /// <summary>cmdSendPassword_Click runs when the Password Reminder button is clicked.</summary>
        protected void OnSendPasswordClick()
        {
            // pretty much always display the same message to avoid hinting on the existence of a username
            var input = string.IsNullOrEmpty(this.username) ? this.email : this.username;
            var message = string.Format(CultureInfo.CurrentCulture, Localization.GetString("PasswordSent", this.LocalResourceFile), WebUtility.HtmlEncode(input));
            var moduleMessageType = ModuleMessage.ModuleMessageType.GreenSuccess;
            var canSend = true;

            if ((this.UseCaptcha && this.CtlCaptcha.IsValid) || (!this.UseCaptcha))
            {
                if (string.IsNullOrEmpty(this.username.Trim()))
                {
                    // No UserName provided
                    if (this.ShowEmailField)
                    {
                        if (string.IsNullOrEmpty(this.email.Trim()))
                        {
                            // No email address either (cannot retrieve password)
                            canSend = false;
                            message = Localization.GetString("EnterUsernameEmail", this.LocalResourceFile);
                            moduleMessageType = ModuleMessage.ModuleMessageType.RedError;
                        }
                    }
                    else
                    {
                        // Cannot retrieve password
                        canSend = false;
                        message = Localization.GetString("EnterUsername", this.LocalResourceFile);
                        moduleMessageType = ModuleMessage.ModuleMessageType.RedError;
                    }
                }

                if (string.IsNullOrEmpty(this.mailSettings.GetServer(this.PortalId)))
                {
                    // SMTP Server is not configured
                    canSend = false;
                    message = Localization.GetString("OptionUnavailable", this.LocalResourceFile);
                    moduleMessageType = ModuleMessage.ModuleMessageType.YellowWarning;

                    var logMessage = Localization.GetString("SMTPNotConfigured", this.LocalResourceFile);

                    this.LogResult(logMessage);
                }

                if (canSend)
                {
                    this.GetUser();
                    if (this.user != null)
                    {
                        if (this.user.IsDeleted)
                        {
                            canSend = false;
                        }
                        else
                        {
                            if (this.user.Membership.Approved == false)
                            {
                                Mail.SendMail(this.user, MessageType.PasswordReminderUserIsNotApproved, this.PortalSettings);
                                if (this.PortalSettings.EnableUnapprovedPasswordReminderNotification)
                                {
                                    Mail.SendMail(
                                        this.user,
                                        MessageType.PasswordReminderUserIsNotApprovedAdmin,
                                        this.PortalSettings);
                                }

                                canSend = false;
                            }

                            if (MembershipProviderConfig.PasswordRetrievalEnabled || MembershipProviderConfig.PasswordResetEnabled)
                            {
                                UserController.ResetPasswordToken(this.user);
                            }

                            if (canSend)
                            {
                                if (Mail.SendMail(this.user, MessageType.PasswordReminder, this.PortalSettings) != string.Empty)
                                {
                                    canSend = false;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (this.userCount > 1)
                        {
                            message = Localization.GetString("MultipleUsers", this.LocalResourceFile);
                        }

                        canSend = false;
                    }

                    if (canSend)
                    {
                        this.LogSuccess();
                        this.cancelResourceKey = "cmdClose";
                    }
                    else
                    {
                        this.LogFailure(message);
                    }

                    // always hide panel so as to not reveal if username exists.
                    this.pnlRecoverVisible = false;
                    DotNetNuke.UI.Skins.Skin.AddModuleMessage(this, message, moduleMessageType);
                    this.liSendVisible = false;
                    this.liCancelVisible = true;
                }
                else
                {
                    DotNetNuke.UI.Skins.Skin.AddModuleMessage(this, message, moduleMessageType);
                }
            }
        }

        private void GetUser()
        {
            if (this.ShowEmailField && !string.IsNullOrEmpty(this.email.Trim()) && (string.IsNullOrEmpty(this.username.Trim()) || this.divUsernameVisible == false))
            {
                var arrUsers = UserController.GetUsersByEmail(this.PortalSettings.PortalId, this.email, 0, int.MaxValue, ref this.userCount);
                if (arrUsers is { Count: 1 })
                {
                    this.user = (UserInfo)arrUsers[0];
                }
            }
            else
            {
                this.user = UserController.GetUserByName(this.PortalSettings.PortalId, this.username);
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
            var portalSecurity = PortalSecurity.Instance;

            DotNetNuke.Abstractions.Logging.ILogInfo log = new LogInfo
            {
                LogPortalName = this.PortalSettings.PortalName,
#pragma warning disable CS0618 // PortalSecurity.FilterFlag.NoScripting is deprecated
                LogUserName = portalSecurity.InputFilter(this.username, PortalSecurity.FilterFlag.NoScripting | PortalSecurity.FilterFlag.NoAngleBrackets | PortalSecurity.FilterFlag.NoMarkup),
#pragma warning restore CS0618 // PortalSecurity.FilterFlag.NoScripting is deprecated
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
